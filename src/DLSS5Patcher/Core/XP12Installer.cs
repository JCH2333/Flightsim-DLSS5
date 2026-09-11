using Microsoft.Win32;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DLSS5Patcher.Core;

/// <summary>
/// X-Plane 12（Vulkan，无原生 DLSS）的 DLSS5-Feeder 路线安装/卸载。
/// 组件来源：教程组件包（dlss5-feed.addon64、Deep Fried Chicken、NVIDIA runtime、着色器）
/// + ReShade 6.8 Addon 安装器（提取 Vulkan 隐式层）。
/// </summary>
public static class XP12Installer
{
    public const string ReShadeSetupSha256 = "afe4c8f13048306307983b8b3d41d5bf00a86820440b0e57dea10950e1176445";
    public const string ReShadeSetupName = "ReShade_Setup_6.8.0_Addon.exe";
    public const string ReShadeVersion = "6.8.0.2155";

    /// <summary>kit 目录中必须存在的文件（教程组件包）。</summary>
    public static readonly string[] RequiredKitFiles =
    {
        "dlss5-feed.addon64",
        "deep-fried-chicken.addon64",
        "deep-fried-chicken-nvngx.dll",
        "deep-fried-chicken.cfg",
        "nvngx_dlssnr.dll",
        "nvngx_dlss.dll",
        @"reshade-shaders\Shaders\DLSS5_Feed.fx",
        @"reshade-shaders\Shaders\MotionEstimation.fx",
        @"reshade-shaders\Shaders\MotionEstimation.fxh",
        @"reshade-shaders\Shaders\MotionEstimationUI.fxh",
        @"reshade-shaders\Shaders\MotionVectors.fxh",
    };

    /// <summary>ReShade 官方框架头文件（DLSS5_Feed.fx 编译必需，kit 不自带；内容已内嵌于 EXE）。</summary>
    private static readonly string[] FrameworkHeaders =
    {
        "ReShade.fxh",
        "ReShadeUI.fxh",
        "DrawText.fxh",
    };

    /// <summary>复制到游戏根目录的 kit 文件（不含 DX11 桥 —— Vulkan 不需要）。</summary>
    private static readonly string[] CopyToRoot =
    {
        "dlss5-feed.addon64",
        "deep-fried-chicken.addon64",
        "deep-fried-chicken-nvngx.dll",
        "deep-fried-chicken.cfg",
        "nvngx_dlssnr.dll",
        "nvngx_dlss.dll",
    };

    /// <summary>运行期生成的文件（卸载时一并清理）。</summary>
    private static readonly string[] RuntimeArtifacts =
    {
        "dlss5-feed.log", "deep-fried-chicken.log", "dlss5-feed.cfg",
        "ReShade.log", "ReShadePreset.ini",
    };

    private static string DataDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DLSS5Patcher");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string ManifestPath => Path.Combine(DataDir, "manifest_xp12.json");

    public static InstallManifest? LoadManifest()
    {
        if (!File.Exists(ManifestPath)) return null;
        try { return JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(ManifestPath)); }
        catch { return null; }
    }

    public static string DetectState(string gameDir)
    {
        var manifest = LoadManifest();
        if (manifest != null && manifest.GameDir.Equals(gameDir, StringComparison.OrdinalIgnoreCase))
            return L.S($"已安装（{manifest.Tag}，{manifest.InstalledAt}）", $"Installed ({manifest.Tag}, {manifest.InstalledAt})");

        if (File.Exists(Path.Combine(gameDir, "dlss5-feed.addon64")) &&
            File.Exists(Path.Combine(gameDir, "deep-fried-chicken.addon64")))
            return L.S("已安装（非本工具安装，或清单丢失）", "Installed (not by this tool, or manifest missing)");
        return L.S("未安装", "Not installed");
    }

    /// <summary>真实文件锁探测：进程名判断会被崩溃残留的僵尸进程误导。</summary>
    public static bool CoreFileLocked(string gameDir)
    {
        foreach (var probe in new[] { "dlss5-feed.addon64", "nvngx_dlssnr.dll", "deep-fried-chicken.addon64" })
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

    /// <summary>从内置资源部署 ReShade Vulkan 层文件（无需网络、无需解压安装器）。</summary>
    private static (string dll, string json) EnsureReShadeLayerFiles(Action<string> log)
    {
        var pd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ReShade");
        Directory.CreateDirectory(pd);
        var dll = Path.Combine(pd, "ReShade64.dll");
        var json = Path.Combine(pd, "ReShade64.json");

        // 已有同版本层文件则复用
        if (File.Exists(dll) && File.Exists(json))
        {
            log(L.S("ReShade Vulkan 层文件已存在，复用。", "ReShade Vulkan layer files already present; reusing them."));
            return (dll, json);
        }

        PackageStore.WriteReShadeLayerFiles(pd, log);
        log(L.S("ReShade Vulkan 层文件已部署。", "ReShade Vulkan layer files deployed."));
        return (dll, json);
    }

    /// <summary>把 api_version 降到 1.1.0 —— 高版本声明的隐式层会被加载器静默跳过（实测 XP12 上踩坑）。</summary>
    private static void PatchLayerJsonApiVersion(string jsonPath)
    {
        var text = File.ReadAllText(jsonPath);
        text = Regex.Replace(text, @"(""api_version""\s*:\s*)""[^""]+""", "$1\"1.1.0\"");
        File.WriteAllText(jsonPath, text);
    }

    private const string LayerRegistryName = @"C:\ProgramData\ReShade\ReShade64.json";

    private static void RegisterLayer()
    {
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var k = root.CreateSubKey(@"SOFTWARE\Khronos\Vulkan\ImplicitLayers");
            k?.SetValue(LayerRegistryName, 0, RegistryValueKind.DWord);
        }
    }

    private static void UnregisterLayer()
    {
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var k = root.OpenSubKey(@"SOFTWARE\Khronos\Vulkan\ImplicitLayers", writable: true);
            k?.DeleteValue(LayerRegistryName, throwOnMissingValue: false);
        }
    }

    private static void EnsureAppWhitelisted(string exePath, Action<string> log)
    {
        var appsIni = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ReShade", "ReShadeApps.ini");

        var existing = File.Exists(appsIni) ? File.ReadAllText(appsIni) : "[APPS]\r\n";
        var lines = existing.Split('\n').ToList();
        var idx = lines.FindIndex(l => Regex.IsMatch(l, @"(?i)^\s*Apps\s*="));
        var entries = new List<string>();
        if (idx >= 0)
        {
            entries = lines[idx].Split('=', 2).Last().Split(',')
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            lines.RemoveAt(idx);
        }
        // 去重后加入本游戏
        entries.RemoveAll(e => e.Equals(exePath, StringComparison.OrdinalIgnoreCase));
        entries.Insert(0, exePath);
        lines.Insert(0, "Apps=" + string.Join(",", entries));
        // 保持 [APPS] 段头在最上
        if (!lines.Any(l => l.Trim().Equals("[APPS]", StringComparison.OrdinalIgnoreCase)))
            lines.Insert(0, "[APPS]");
        Directory.CreateDirectory(Path.GetDirectoryName(appsIni)!);
        File.WriteAllText(appsIni, string.Join("\r\n", lines.Where(l => l.Trim().Length > 0)) + "\r\n");
        log(L.S($"ReShadeApps.ini：已将 {Path.GetFileName(exePath)} 加入白名单（共 {entries.Count} 项）。", $"ReShadeApps.ini: whitelisted {Path.GetFileName(exePath)} ({entries.Count} entries total)."));
    }

    private static void RemoveAppWhitelisted(string exePath, Action<string> log)
    {
        var appsIni = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ReShade", "ReShadeApps.ini");
        if (!File.Exists(appsIni)) return;
        var text = File.ReadAllText(appsIni);
        var lines = text.Split('\n').ToList();
        var idx = lines.FindIndex(l => Regex.IsMatch(l, @"(?i)^\s*Apps\s*="));
        if (idx < 0) return;
        var entries = lines[idx].Split('=', 2).Last().Split(',')
            .Select(s => s.Trim()).Where(s => s.Length > 0 && !s.Equals(exePath, StringComparison.OrdinalIgnoreCase)).ToList();
        if (entries.Count == 0)
        {
            // 白名单空了：连 ProgramData\ReShade 一起清理
            UnregisterLayer();
            var pd = Path.GetDirectoryName(appsIni)!;
            try { if (!Directory.EnumerateFileSystemEntries(pd).Any()) Directory.Delete(pd, true); } catch { }
            log(L.S("白名单已空，Vulkan 层注册与 ProgramData\\ReShade 一并移除。", "Whitelist is empty; removing the Vulkan layer registration and ProgramData\\ReShade."));
        }
        else
        {
            lines[idx] = "Apps=" + string.Join(",", entries);
            File.WriteAllText(appsIni, string.Join("\r\n", lines.Where(l => l.Trim().Length > 0)) + "\r\n");
            log(L.S($"已从白名单移除（剩 {entries.Count} 项）。", $"Removed from the whitelist ({entries.Count} entries left)."));
        }
    }

    public sealed class InstallOptions
    {
        public required string GameDir { get; init; }
        public required string ExePath { get; init; }
        public required string KitDir { get; init; }
        public required Action<string> Log { get; init; }
    }

    public static async Task<InstallManifest> InstallAsync(InstallOptions o, CancellationToken ct = default)
        => await Task.Run(() => InstallCore(o), ct);

    private static InstallManifest InstallCore(InstallOptions o)
    {
        o.Log(L.S($"目标游戏目录：{o.GameDir}", $"Target game folder: {o.GameDir}"));

        // 0. 校验 kit 完整性
        var missing = RequiredKitFiles
            .Where(f => !File.Exists(Path.Combine(o.KitDir, f))).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException("组件包缺少文件：" + string.Join(", ", missing));

        // 1. ReShade Vulkan 层（ProgramData + 注册表 + 白名单）
        var (dll, json) = EnsureReShadeLayerFiles(o.Log);
        PatchLayerJsonApiVersion(json);
        RegisterLayer();
        EnsureAppWhitelisted(o.ExePath, o.Log);

        // 2. 复制 kit 文件
        var files = new List<string>();
        foreach (var f in CopyToRoot)
        {
            File.Copy(Path.Combine(o.KitDir, f), Path.Combine(o.GameDir, f), overwrite: true);
            files.Add(f);
        }
        var shaderDirSrc = Path.Combine(o.KitDir, "reshade-shaders", "Shaders");
        var shaderDirDst = Path.Combine(o.GameDir, "reshade-shaders", "Shaders");
        Directory.CreateDirectory(shaderDirDst);
        foreach (var f in Directory.EnumerateFiles(shaderDirSrc))
        {
            var name = Path.GetFileName(f);
            File.Copy(f, Path.Combine(shaderDirDst, name), overwrite: true);
            files.Add(@"reshade-shaders\Shaders\" + name);
        }
        Directory.CreateDirectory(Path.Combine(o.GameDir, "reshade-shaders", "Textures"));
        o.Log(L.S($"已写入 {files.Count} 个文件（不含 DX11 桥 —— Vulkan 无需）。", $"Wrote {files.Count} files (no DX11 bridge — not needed on Vulkan)."));

        // 2.5 ReShade 官方框架头文件（kit 不自带；缺失会导致 DLSS5_Feed.fx 编译失败）
        foreach (var name in FrameworkHeaders)
        {
            var dst = Path.Combine(shaderDirDst, name);
            if (File.Exists(dst)) { files.Add(@"reshade-shaders\Shaders\" + name); continue; }
            var kitCopy = Path.Combine(o.KitDir, "reshade-shaders", "Shaders", name);
            if (File.Exists(kitCopy))
            {
                File.Copy(kitCopy, dst, overwrite: true);
            }
            else
            {
                File.WriteAllText(dst, PackageStore.ReadFxh(name));
            }
            files.Add(@"reshade-shaders\Shaders\" + name);
            o.Log(L.S($"已补齐框架头文件 {name}。", $"Added missing framework header {name}."));
        }

        // 3. ReShade.ini + 预设
        //    预设 Techniques 必须用纯 technique 名（MotionEstimation.fx 的 technique 叫 DRME）；
        //    写成 "名字@文件" 会被 ReShade 判为 unknown technique 而不勾选（群友实测踩坑）。
        //    DRME（着色器运动矢量估算）必须排在 DLSS5_Feed 之前，为后者提供运动矢量。
        File.WriteAllText(Path.Combine(o.GameDir, "ReShade.ini"),
            "[ADDONS]\nEnableAddons=1\n\n[GENERAL]\n" +
            "EffectSearchPaths=.\\reshade-shaders\\Shaders\\**\n" +
            "TextureSearchPaths=.\\reshade-shaders\\Textures\\**\n" +
            "PreprocessorDefinitions=DLSS5_MV_PROVIDER=0\n" +
            "PresetPath=.\\DLSS5-Feeder.ini\n");
        File.WriteAllText(Path.Combine(o.GameDir, "DLSS5-Feeder.ini"),
            "PreprocessorDefinitions=DLSS5_MV_PROVIDER=0\n" +
            "Techniques=DRME,DLSS5_Feed\n" +
            "TechniqueSorting=DRME,DLSS5_Feed\n");
        o.Log(L.S("ReShade.ini + DLSS5-Feeder 预设已写入（DRME + DLSS5_Feed 默认启用）。", "ReShade.ini + DLSS5-Feeder preset written (DRME + DLSS5_Feed enabled by default)."));

        // 4. 清单
        var dirs = files.Select(f => Path.GetDirectoryName(f)!)
            .Where(d => d.Length > 0).Select(d => d.TrimEnd('\\'))
            .Distinct().OrderByDescending(d => d.Count(c => c == '\\')).ToList();
        var manifest = new InstallManifest
        {
            Tag = "DLSS5-Feeder 0.13.1-beta.1 + DFC 1.4.8",
            InstalledAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            PackageSha256 = "kit:" + PackageStore.Sha256File(Path.Combine(o.KitDir, "dlss5-feed.addon64"))[..16],
            GameDir = o.GameDir,
            Files = files,
            Dirs = dirs,
            WorkingScale = "",
        };
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        o.Log(L.S("安装清单已写入。", "Install manifest written."));
        o.Log(L.S("完成。请完全重启 X-Plane：Home 键确认 DRME 与 DLSS 5 Feed 已勾选；若 DFC 显示 neural feature disabled，点 Refresh neural contract 或再次重启游戏。", "Done. Fully restart X-Plane: press Home to verify DRME and DLSS 5 Feed are ticked; if DFC shows neural feature disabled, click Refresh neural contract or restart the game again."));
        return manifest;
    }

    public static (int files, int dirs) Uninstall(string gameDir, string exePath, Action<string> log)
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
            log(L.S("未找到清单，按已知特征清理...", "No manifest found; cleaning up by known fingerprints..."));
            files = new List<string> { "dlss5-feed.addon64", "deep-fried-chicken.addon64", "deep-fried-chicken-nvngx.dll",
                "deep-fried-chicken.cfg", "nvngx_dlssnr.dll", "nvngx_dlss.dll", "ReShade.ini", "DLSS5-Feeder.ini" };
            files.AddRange(RuntimeArtifacts);
            dirs = new List<string> { "reshade-shaders", "dlssnr-capture" };
        }

        int n = 0;
        foreach (var f in files)
        {
            var p = Path.Combine(gameDir, f);
            if (!File.Exists(p)) continue;
            try { File.Delete(p); n++; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log(L.S($"跳过被占用文件 {f}", $"Skipping locked file {f}")); }
        }
        // DFC 运行期生成的 ReShade.ini 备份（通配清理）
        foreach (var p in Directory.EnumerateFiles(gameDir, "ReShade.ini.deep-fried-chicken-backup-*.bak"))
        {
            try { File.Delete(p); n++; } catch { }
        }
        foreach (var d in dirs.OrderByDescending(x => x.Count(c => c == Path.DirectorySeparatorChar)))
        {
            var p = Path.Combine(gameDir, d);
            try { if (Directory.Exists(p) && !Directory.EnumerateFileSystemEntries(p).Any()) Directory.Delete(p); } catch { }
        }
        log(L.S($"已删除 {n} 个文件。", $"Deleted {n} files."));

        RemoveAppWhitelisted(exePath, log);
        if (File.Exists(ManifestPath)) File.Delete(ManifestPath);
        return (n, dirs.Count);
    }
}
