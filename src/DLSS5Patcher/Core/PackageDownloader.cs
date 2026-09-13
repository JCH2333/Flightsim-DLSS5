using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace DLSS5Patcher.Core;

/// <summary>
/// 包体下载器：缓存（%LOCALAPPDATA%\DLSS5Patcher\cache）→ EXE 旁 packages\（离线分发通道）→
/// 多源下载（服务器 → GitHub Release → gh-proxy 镜像；HTTP Range 断点续传；限速看门狗；SHA256 硬校验）。
/// </summary>
public static class PackageDownloader
{
    public static string CacheDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DLSS5Patcher", "cache");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string CachedPath(PackageCatalog.PackageSpec spec) => Path.Combine(CacheDir, spec.FileName);

    private static string OfflinePath(PackageCatalog.PackageSpec spec) =>
        Path.Combine(AppContext.BaseDirectory, "packages", spec.FileName);

    /// <summary>本地缓存命中且大小+SHA256 全部通过。</summary>
    public static bool IsCachedValid(PackageCatalog.PackageSpec spec)
    {
        try
        {
            var p = CachedPath(spec);
            return File.Exists(p) && new FileInfo(p).Length == spec.Size
                && PackageStore.Sha256File(p).Equals(spec.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>确保包体可用（缓存 → 离线目录 → 下载），返回本地路径。进度为 (已接收, 总字节)。</summary>
    public static async Task<string> EnsureAsync(PackageCatalog.PackageSpec spec, Action<string> log,
        IProgress<(long received, long total)>? progress, CancellationToken ct = default)
    {
        if (IsCachedValid(spec))
        {
            log(L.S("组件包已缓存且校验通过，直接使用。", "Package already cached and verified — reusing it."));
            return CachedPath(spec);
        }

        // EXE 旁的 packages 目录（网盘整包分发 / 离线场景）
        var offline = OfflinePath(spec);
        try
        {
            if (File.Exists(offline) && new FileInfo(offline).Length == spec.Size
                && PackageStore.Sha256File(offline).Equals(spec.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                log(L.S("发现 EXE 旁的离线组件包，复制到缓存。", "Found an offline package next to the EXE — copying to cache."));
                Directory.CreateDirectory(CacheDir);
                await Task.Run(() => File.Copy(offline, CachedPath(spec), overwrite: true), ct);
                log(L.S("离线组件包校验通过。", "Offline package verified."));
                return CachedPath(spec);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* 离线包不可用则走下载 */ }

        var tmp = CachedPath(spec) + ".tmp";
        Directory.CreateDirectory(CacheDir);

        Exception? last = null;
        foreach (var url in PackageCatalog.CandidateUrls(spec))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                log(L.S($"开始下载 {spec.FileName}（{spec.Size / 1048576.0:F0} MB）…",
                        $"Downloading {spec.FileName} ({spec.Size / 1048576.0:F0} MB)..."));
                await HttpDownloadAsync(url, tmp, spec, progress, ct);
                if (!PackageStore.Sha256File(tmp).Equals(spec.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tmp);
                    throw new InvalidOperationException(L.S("下载内容校验失败（SHA256 不匹配）。", "Download failed verification (SHA256 mismatch)."));
                }
                File.Move(tmp, CachedPath(spec), overwrite: true);
                log(L.S("下载完成，校验通过。", "Download complete and verified."));
                return CachedPath(spec);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                last = ex;
                log(L.S($"该下载源失败（{ex.Message}），换下一个源…", $"Source failed ({ex.Message}), trying the next one..."));
            }
        }
        throw new InvalidOperationException(L.S("所有下载源均失败，请检查网络后重试。", "All download sources failed — check your network and retry."), last);
    }

    private static async Task HttpDownloadAsync(string url, string tmp, PackageCatalog.PackageSpec spec,
        IProgress<(long received, long total)>? progress, CancellationToken ct)
    {
        long existing = File.Exists(tmp) ? new FileInfo(tmp).Length : 0;
        if (existing > spec.Size)
        {
            File.Delete(tmp);   // 异常残留（比目标还大），无法续传
            existing = 0;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"DLSS5Patcher/{Updater.CurrentVersion}");
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0) req.Headers.Range = new RangeHeaderValue(existing, null);

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (existing > 0 && resp.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            if (existing == spec.Size) return;   // 断点恰好等于总长，交给外层校验
            File.Delete(tmp);
            throw new InvalidOperationException("invalid partial file");
        }
        resp.EnsureSuccessStatusCode();

        var resumed = existing > 0 && resp.StatusCode == HttpStatusCode.PartialContent;
        if (existing > 0 && !resumed) existing = 0;   // 服务器不支持 Range，返回整包 → 从头写

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = new FileStream(tmp, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write);
        var buffer = new byte[1 << 19];
        long written = 0;               // 本次连接新写入的字节
        int read;
        var lastReport = Environment.TickCount64;
        var wdTick = lastReport;        // 看门狗：20 秒窗口内 < 8MB 视为限速死链，切源
        var wdBytes = 0L;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            written += read;
            var now = Environment.TickCount64;
            if (now - lastReport > 300)
            {
                lastReport = now;
                progress?.Report((existing + written, spec.Size));
            }
            if (now - wdTick > 20_000)
            {
                if (written - wdBytes < 8_000_000)
                    throw new InvalidOperationException(L.S("下载速度过慢（< 400KB/s）", "Download too slow (< 400KB/s)"));
                wdTick = now;
                wdBytes = written;
            }
        }
        progress?.Report((spec.Size, spec.Size));
    }
}
