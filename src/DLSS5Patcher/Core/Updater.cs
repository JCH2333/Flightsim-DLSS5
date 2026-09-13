using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace DLSS5Patcher.Core;

/// <summary>
/// GitHub Release 自动更新：启动时检查 → 强制下载校验 → 自替换重启。
/// 运行中的 EXE 无法覆盖删除但可以重命名：先把当前 EXE 改名为 .old，再把新 EXE 移到原路径并启动，新进程启动时清理 .old。
/// </summary>
public static class Updater
{
    public const string Repo = "JCH2333/Flightsim-DLSS5";
    public static string ReleasesUrl => $"https://github.com/{Repo}/releases/latest";
    private const string ApiUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";

    /// <summary>检查接口：直连 API 优先，gh-proxy 可代理 API，作为大陆网络下的兜底。</summary>
    private static readonly string[] ApiUrls =
    {
        ApiUrl,
        "https://gh-proxy.com/" + ApiUrl,
    };

    /// <summary>直连下载失败时的镜像（前缀拼接）。ghfast.top 只支持资产下载，不支持 API。</summary>
    private static readonly string[] MirrorPrefixes = { "https://gh-proxy.com/", "https://ghfast.top/" };

    public static string UpdateDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DLSS5Patcher", "update");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version!;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public sealed record UpdateInfo(string Tag, string FileName, string ServerFile, long Size, string? Sha256, string Body);

    private static readonly Version CurrentVer = Version.Parse(CurrentVersion);

    private static bool IsNewer(string versionText) =>
        Version.TryParse(versionText.TrimStart('v', 'V').Split('-')[0], out var v) && v > CurrentVer;

    /// <summary>
    /// 查询最新版本：先取自有服务器的签名 manifest（快、无限流），无结果或不可达再回退 GitHub API（两轮重试）。
    /// 服务器已确认无更新而 GitHub 不可达时返回 null（视为已是最新）。
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        // 1. 自有服务器（ECDSA 验签，HTTP 明文链路同样可信）
        PackageCatalog.ServerAppManifest? server = null;
        try { server = await PackageCatalog.TryFetchAppManifestAsync(ct: ct); }
        catch { /* 服务器不可达，走 GitHub */ }
        if (server != null && IsNewer(server.Version))
            return new UpdateInfo("v" + server.Version.TrimStart('v', 'V'),
                Path.GetFileName(server.ExeFile), server.ExeFile,
                server.ExeSize,
                string.IsNullOrEmpty(server.ExeSha256) ? null : server.ExeSha256,
                server.Notes);

        // 2. GitHub API 兜底
        Exception? last = null;
        for (var round = 0; round < 2; round++)
        {
            foreach (var api in ApiUrls)
            {
                try
                {
                    return await CheckOnceAsync(api, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { last = ex; }
            }
            if (round == 0)
                await Task.Delay(3000, ct);
        }
        if (server != null) return null;   // 服务器在线且已是最新，GitHub 故障不影响结论
        throw last ?? new InvalidOperationException(L.S("更新检查失败。", "Update check failed."));
    }

    private static async Task<UpdateInfo?> CheckOnceAsync(string apiUrl, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"DLSS5Patcher/{CurrentVersion}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        using var doc = JsonDocument.Parse(await http.GetStringAsync(apiUrl, ct));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

        string? exeName = null;
        long size = 0;
        string? sha = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                exeName = name;
                size = a.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;
                // digest 形如 "sha256:abc..."（旧版 API 无此字段则退化为按大小校验）
                sha = a.TryGetProperty("digest", out var d) ? d.GetString() : null;
                if (sha is null || !sha.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) sha = null;
                else sha = sha["sha256:".Length..];
                break;
            }
        }
        if (string.IsNullOrEmpty(exeName)) return null;

        var verText = tag.TrimStart('v', 'V').Split('-')[0];
        if (!Version.TryParse(verText, out var latest)) return null;
        if (latest <= CurrentVer) return null;

        return new UpdateInfo(tag, exeName, "", size, sha, body);
    }

    /// <summary>下载新版本 EXE（服务器优先 → GitHub → 镜像；带限速看门狗），SHA256 / 大小校验通过后返回本地路径。</summary>
    public static async Task<string> DownloadAsync(
        UpdateInfo info, IProgress<(long received, long total)>? progress, Action<string> log, CancellationToken ct = default)
    {
        var final = Path.Combine(UpdateDir, $"DLSS5Patcher-{info.Tag}.exe");

        // 上次下载已通过校验（例如下载成功后重启失败），直接复用
        if (File.Exists(final) && VerifyFile(final, info))
        {
            log(L.S("已有通过校验的更新包，直接使用。", "A verified update package already exists — reusing it."));
            return final;
        }

        var tmp = final + ".tmp";
        var sources = PackageCatalog.AppCandidateUrls(info.FileName, info.ServerFile)
            .Select(u => (Label: u, Url: u))
            .ToList();

        // 上次成功过的源优先（省掉对慢源的看门狗等待）
        try
        {
            var marker = Path.Combine(UpdateDir, "last_source.txt");
            if (File.Exists(marker))
            {
                var last = File.ReadAllText(marker).Trim();
                var idx = sources.FindIndex(s => s.Label == last);
                if (idx > 0) { var s = sources[idx]; sources.RemoveAt(idx); sources.Insert(0, s); }
            }
        }
        catch { }

        Exception? lastError = null;
        foreach (var (label, url) in sources)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                log(L.S($"开始下载更新（{info.Size / 1048576.0:F0} MB）…", $"Downloading update ({info.Size / 1048576.0:F0} MB)..."));
                await HttpDownload(url, tmp, info.Size, progress, ct);
                if (!VerifyFile(tmp, info))
                {
                    File.Delete(tmp);
                    throw new InvalidOperationException(L.S("下载文件校验失败。", "Downloaded file failed verification."));
                }
                File.Move(tmp, final, overwrite: true);
                try { File.WriteAllText(Path.Combine(UpdateDir, "last_source.txt"), label); } catch { }
                log(L.S("更新包下载完成，校验通过。", "Update downloaded and verified."));
                return final;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                lastError = ex;
                log(L.S($"该下载源失败（{ex.Message}），换下一个源…", $"Source failed ({ex.Message}), trying the next one..."));
            }
        }
        throw new InvalidOperationException(L.S("所有下载源均失败。", "All download sources failed."), lastError);
    }

    private static async Task HttpDownload(string url, string dest, long fallbackSize,
        IProgress<(long received, long total)>? progress, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"DLSS5Patcher/{CurrentVersion}");
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? fallbackSize;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(dest);
        var buffer = new byte[1 << 19];
        long received = 0;
        int read;
        var lastReport = Environment.TickCount64;
        // 速度看门狗：大陆直连 GitHub 常被限速到几十 KB/s，539MB 会拖几个小时。
        // 每 20 秒检查一次，窗口内不足 8MB（< 400KB/s）就放弃当前源，让外层切镜像。
        var wdTick = Environment.TickCount64;
        var wdBytes = 0L;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;
            var now = Environment.TickCount64;
            if (now - lastReport > 300)
            {
                lastReport = now;
                progress?.Report((received, total));
            }
            if (now - wdTick > 20_000)
            {
                if (received - wdBytes < 8_000_000)
                    throw new InvalidOperationException(L.S("下载速度过慢（< 400KB/s）", "Download too slow (< 400KB/s)"));
                wdTick = now;
                wdBytes = received;
            }
        }
        progress?.Report((received, total));
    }

    private static bool VerifyFile(string path, UpdateInfo info)
    {
        try
        {
            if (info.Sha256 is { Length: 64 } want)
                return Sha256File(path).Equals(want, StringComparison.OrdinalIgnoreCase);
            return info.Size <= 0 || new FileInfo(path).Length == info.Size;
        }
        catch
        {
            return false;
        }
    }

    public static string Sha256File(string path)
    {
        using var h = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(h.ComputeHash(fs)).ToLowerInvariant();
    }

    /// <summary>把下载好的新 EXE 换到当前程序路径并启动新版本；成功后由调用方退出当前进程。</summary>
    public static void ApplyAndRestart(string newExePath, Action<string>? log = null)
    {
        var cur = Environment.ProcessPath
                  ?? throw new InvalidOperationException(L.S("无法确定当前程序路径。", "Cannot determine the current executable path."));
        var old = cur + ".old";

        // 清掉上次更新残留的 .old（被占用则等新进程启动后再清）
        for (var i = 0; i < 5 && File.Exists(old); i++)
        {
            try { File.Delete(old); break; }
            catch { Thread.Sleep(300); }
        }

        for (var i = 0; ; i++)
        {
            try
            {
                File.Move(cur, old);
                break;
            }
            catch (Exception ex) when (i < 4)
            {
                log?.Invoke(L.S($"重命名当前程序失败（{ex.Message}），重试中…（如有其他实例正在运行请先关闭）",
                    $"Renaming the running EXE failed ({ex.Message}), retrying... (close other instances if any)"));
                Thread.Sleep(400);
            }
        }

        try
        {
            File.Move(newExePath, cur);
        }
        catch
        {
            // 新文件没移成功，把运行中的旧文件名改回去
            File.Move(old, cur);
            throw;
        }

        using (Process.Start(new ProcessStartInfo { FileName = cur, UseShellExecute = true })) { }
    }

    /// <summary>启动时清理：上次更新留下的 .old 与过期的 .tmp。</summary>
    public static void CleanLeftovers()
    {
        try
        {
            var cur = Environment.ProcessPath;
            if (cur != null && File.Exists(cur + ".old"))
                File.Delete(cur + ".old");
        }
        catch { /* 被占用时下次启动再清 */ }
        try
        {
            foreach (var f in Directory.EnumerateFiles(UpdateDir, "*.tmp"))
                if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-1))
                    File.Delete(f);
        }
        catch { }
    }
}
