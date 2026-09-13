using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DLSS5Patcher.Core;

/// <summary>DLSS Unlocked（OptiScaler 路线）安装 / 卸载引擎。适用 RTX 20/30 系。</summary>
public static class UnlockedInstaller
{
    private static string DataDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DLSS5Patcher");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string ManifestPath => Path.Combine(DataDir, "manifest.json");

    public static InstallManifest? LoadManifest()
    {
        if (!File.Exists(ManifestPath)) return null;
        try
        {
            return JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(ManifestPath));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>快速判断游戏目录当前的安装状态。</summary>
    public static string DetectState(string gameDir)
    {
        var manifest = LoadManifest();
        if (manifest != null && manifest.GameDir.Equals(gameDir, StringComparison.OrdinalIgnoreCase))
            return L.S($"已安装（{manifest.Tag}，{manifest.InstalledAt}，WorkingScale={manifest.WorkingScale}）", $"Installed ({manifest.Tag}, {manifest.InstalledAt}, WorkingScale={manifest.WorkingScale})");

        var dxgi = Path.Combine(gameDir, "dxgi.dll");
        var ini = Path.Combine(gameDir, "OptiScaler.ini");
        var nr = Path.Combine(gameDir, "nvngx_dlssnr.dll");
        if (File.Exists(dxgi) && File.Exists(ini) && File.Exists(nr))
            return L.S("已安装（非本工具安装，或清单丢失）", "Installed (not by this tool, or manifest missing)");

        var reshade = File.Exists(dxgi) && new FileInfo(dxgi).Length < 20_000_000;
        if (reshade) return L.S("存在第三方 dxgi.dll（可能是 ReShade），安装前需处理", "Third-party dxgi.dll found (possibly ReShade) — resolve it before installing");
        return L.S("未安装", "Not installed");
    }

    public sealed class InstallOptions
    {
        public required string GameDir { get; init; }
        public required string ExeName { get; init; }
        public required string WorkingScale { get; init; }   // "0.5" 等
        public required GpuGeneration Generation { get; init; }
        public required Action<string> Log { get; init; }
        public required IProgress<(long, long)>? Progress { get; init; }
    }

    public static async Task<InstallManifest> InstallAsync(InstallOptions o, CancellationToken ct = default)
        => await Task.Run(async () => await InstallCore(o, ct), ct);

    private static async Task<InstallManifest> InstallCore(InstallOptions o, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        o.Log(L.S($"目标游戏目录：{o.GameDir}", $"Target game folder: {o.GameDir}"));

        // 0. 冲突检查：已有第三方 dxgi.dll 时拒绝
        var dxgiPath = Path.Combine(o.GameDir, "dxgi.dll");
        if (File.Exists(dxgiPath) && new FileInfo(dxgiPath).Length < 20_000_000 && LoadManifest() is null)
            throw new InvalidOperationException(
                "游戏目录已存在第三方 dxgi.dll（可能是 ReShade / 其他注入器），与 OptiScaler 冲突。请先备份并移除它，再重新安装。");

        // 1. 确保组件包就绪（缓存 → 离线目录 → 服务器/GitHub 下载，SHA256 校验）
        await PackageDownloader.EnsureAsync(PackageCatalog.MsfsPackage, o.Log, o.Progress, ct);

        // 2. 打开组件包（再次完整校验）
        using var zip = new ZipArchive(PackageStore.OpenMsfsPackage(msg => o.Log(msg)), ZipArchiveMode.Read);

        // 3. 解压到游戏目录
        o.Log(L.S("从组件包解压（约 440MB，视磁盘速度需一两分钟）...", "Extracting the package (~440MB, may take a minute or two)..."));
        var files = new List<string>();
        var dirs = new HashSet<string>();
        long totalBytes = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Sum(e => e.Length);
        long doneBytes = 0;
        var lastReport = Environment.TickCount64;
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // 目录条目跳过
            var rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var dst = Path.Combine(o.GameDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            try
            {
                entry.ExtractToFile(dst, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       && rel.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                o.Log(L.S($"跳过被占用的日志文件 {rel}（不影响功能）。", $"Skipping locked log file {rel} (harmless)."));
                doneBytes += entry.Length;
                continue;
            }
            files.Add(rel);
            doneBytes += entry.Length;
            if (Environment.TickCount64 - lastReport > 300)
            {
                lastReport = Environment.TickCount64;
                o.Progress?.Report((doneBytes, Math.Max(totalBytes, 1)));
            }
            // 记录中间目录（卸载时按深度倒序删空目录）
            var acc = "";
            foreach (var part in rel.Split(Path.DirectorySeparatorChar)[..^1])
            {
                acc = acc.Length == 0 ? part : acc + Path.DirectorySeparatorChar + part;
                dirs.Add(acc);
            }
        }
        o.Progress?.Report((totalBytes, Math.Max(totalBytes, 1)));
        o.Log(L.S($"已写入 {files.Count} 个文件。", $"Wrote {files.Count} files."));

        // 2.5 RTX 50 系：用包内 OptiScaler/streamline/nvngx_dlssnr.dll（NVIDIA 原版 runtime，
        //     与官方 310.8.0 哈希一致）覆盖根目录的跨代补丁版；20/30/40 系保留跨代版。
        if (o.Generation == GpuGeneration.Blackwell)
        {
            var original = Path.Combine(o.GameDir, "OptiScaler", "streamline", "nvngx_dlssnr.dll");
            if (File.Exists(original))
            {
                File.Copy(original, Path.Combine(o.GameDir, "nvngx_dlssnr.dll"), overwrite: true);
                o.Log(L.S("RTX 50 系：已切换为 NVIDIA 原版 NR runtime（跨代补丁版不适用于 Blackwell）。", "RTX 50 series: switched to NVIDIA's original NR runtime (the cross-gen patched build does not apply to Blackwell)."));
            }
            else
            {
                o.Log(L.S("警告：未找到包内原版 NR runtime，保留跨代版本。", "Warning: original NR runtime not found in the package; keeping the cross-gen build."));
            }
        }

        // 3. 修改 OptiScaler.ini：启用 NR + WorkingScale
        var iniPath = Path.Combine(o.GameDir, "OptiScaler.ini");
        var ini = File.ReadAllText(iniPath);
        ini = PatchDlssNrSection(ini, o.WorkingScale);
        File.WriteAllText(iniPath, ini);
        o.Log(L.S($"OptiScaler.ini：[DlssNr] Enabled=true，WorkingScale={o.WorkingScale}", $"OptiScaler.ini: [DlssNr] Enabled=true, WorkingScale={o.WorkingScale}"));

        // 4. 游戏抗锯齿切到 DLSS（备份原配置）
        string? userCfgPath = null, userCfgBackup = null;
        var cfgPath = UserCfg.PathFor(o.ExeName);
        if (cfgPath != null)
        {
            var aa = UserCfg.ReadAntiAliasing(cfgPath);
            if (aa is not ("DLSS" or "DLAA"))
            {
                var (changed, backup) = UserCfg.SetAntiAliasing(cfgPath);
                if (changed)
                {
                    userCfgPath = cfgPath;
                    userCfgBackup = backup;
                    o.Log(L.S("UserCfg.opt：抗锯齿 TAA → DLSS（原文件已备份）。", "UserCfg.opt: Anti-Aliasing TAA → DLSS (original file backed up)."));
                }
            }
            else
            {
                o.Log(L.S($"UserCfg.opt：抗锯齿已是 {aa}，无需修改。", $"UserCfg.opt: Anti-Aliasing already {aa}; no change needed."));
            }
        }

        // 5. 写安装清单
        var manifest = new InstallManifest
        {
            Tag = PackageStore.MsfsPackageTag,
            InstalledAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            PackageSha256 = PackageCatalog.MsfsPackage.Sha256,
            GameDir = o.GameDir,
            Files = files,
            Dirs = dirs.OrderByDescending(d => d.Count(c => c == Path.DirectorySeparatorChar)).ToList(),
            UserCfgPath = userCfgPath,
            UserCfgBackupPath = userCfgBackup,
            WorkingScale = o.WorkingScale,
        };
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        o.Log(L.S("安装清单已写入。", "Install manifest written."));
        return manifest;
    }

    /// <summary>真实文件锁探测：进程名判断会被崩溃残留的僵尸进程误导。</summary>
    public static bool CoreFileLocked(string gameDir)
    {
        foreach (var probe in new[] { "dxgi.dll", "OptiScaler.ini", "nvngx_dlssnr.dll" })
        {
            var p = Path.Combine(gameDir, probe);
            if (!File.Exists(p)) continue;
            try
            {
                using var fs = File.Open(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>卸载：删除清单内文件、恢复配置。清单丢失时按已知特征兜底清理。</summary>
    public static (int files, int dirs) Uninstall(string gameDir, Action<string> log)
    {
        var manifest = LoadManifest();
        List<string> files;
        List<string> dirs;

        if (manifest != null && manifest.GameDir.Equals(gameDir, StringComparison.OrdinalIgnoreCase))
        {
            files = manifest.Files;
            dirs = manifest.Dirs;
        }
        else
        {
            log(L.S("未找到本工具的安装清单，按已知特征清理...", "No install manifest found; cleaning up by known fingerprints..."));
            files = new List<string>
            {
                "dxgi.dll", "OptiScaler.ini", "nvngx_dlssnr.dll", "nvngx.dll_dlssnr.dll",
                "OptiScaler.log", "nvngx.log",
            };
            dirs = new List<string> { "OptiScaler", "Licenses", "dlssnr-capture" };
        }

        int n = 0;
        var lockedCritical = new List<string>();
        var lockedSkippable = new List<string>();
        foreach (var f in files)
        {
            var p = Path.Combine(gameDir, f);
            if (!File.Exists(p)) continue;
            try { File.Delete(p); n++; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // 日志类文件被占用不致命（如崩溃残留的僵尸进程握着 OptiScaler.log），下次重启后可清除
                if (f.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || f.Contains("capture", StringComparison.OrdinalIgnoreCase))
                    lockedSkippable.Add(f);
                else
                    lockedCritical.Add(f);
            }
        }
        foreach (var d in dirs.OrderByDescending(x => x.Count(c => c == Path.DirectorySeparatorChar)))
        {
            var p = Path.Combine(gameDir, d);
            try
            {
                if (Directory.Exists(p) && !Directory.EnumerateFileSystemEntries(p).Any())
                    Directory.Delete(p);
            }
            catch { /* 目录非空（含被锁文件），保留 */ }
        }
        log(L.S($"已删除 {n} 个文件。", $"Deleted {n} files."));
        if (lockedSkippable.Count > 0)
            log(L.S($"以下日志文件被占用暂时跳过（不影响功能，重启系统后可手动删除）：{string.Join(", ", lockedSkippable)}", $"These locked log files were skipped (harmless; delete manually after reboot): {string.Join(", ", lockedSkippable)}"));
        if (lockedCritical.Count > 0)
            throw new InvalidOperationException(
                "以下文件被占用（游戏未关闭？），请关闭游戏后重新执行卸载：" + string.Join(", ", lockedCritical));

        if (manifest?.UserCfgPath is not null && manifest.UserCfgBackupPath is not null)
        {
            UserCfg.Restore(manifest.UserCfgPath, manifest.UserCfgBackupPath);
            log(L.S("UserCfg.opt 已从备份恢复。", "UserCfg.opt restored from backup."));
        }

        // 仅当清单属于本目录时才删除（清单可能记录的是另一代 MSFS，如 2024/2020 共存时）
        if (manifest != null && manifest.GameDir.Equals(gameDir, StringComparison.OrdinalIgnoreCase) && File.Exists(ManifestPath))
            File.Delete(ManifestPath);
        return (n, dirs.Count);
    }

    private static string PatchDlssNrSection(string ini, string workingScale)
    {
        int s = ini.IndexOf("[DlssNr]", StringComparison.Ordinal);
        if (s < 0) throw new InvalidOperationException("组件包 OptiScaler.ini 缺少 [DlssNr] 段，包结构可能已变更。");
        int e = ini.IndexOf("\n[", s + 1, StringComparison.Ordinal);
        if (e < 0) e = ini.Length;

        var seg = ini[s..e];
        if (!seg.Contains("Enabled=", StringComparison.Ordinal))
            throw new InvalidOperationException("[DlssNr] 段结构异常（无 Enabled 键）。");
        seg = Regex.Replace(seg, @"(?m)^(Enabled=)auto\r?$", "${1}true");
        seg = Regex.Replace(seg, @"(?m)^(WorkingScale=)auto\r?$", "${1}" + workingScale);
        return ini[..s] + seg + ini[e..];
    }
}
