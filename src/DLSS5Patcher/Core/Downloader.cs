using System.Net;
using System.Security.Cryptography;

namespace DLSS5Patcher.Core;

/// <summary>DLSS Unlocked 组件包信息与下载（直连优先，可选代理）。</summary>
public static class Downloader
{
    public const string PackageTag = "DLSSNR-v0.7.6";
    public const string PackageName = "dlss-unlocked-standalone-DLSSNR-v0.7.6.zip";
    public const string PackageUrl =
        "https://github.com/ShyVortex/dlss-unlocked/releases/download/" + PackageTag + "/" + PackageName;
    public const string PackageSha256 = "ca824acb693e39975b152c7a6aa21af64799dc1d05ec0462451491ccda8e6ec2";
    public const long PackageSize = 461_963_289;

    public static string CacheDir
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DLSS5Patcher", "cache");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string CachedZipPath => Path.Combine(CacheDir, PackageName);

    public static string Sha256File(string path)
    {
        using var h = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(h.ComputeHash(fs)).ToLowerInvariant();
    }

    /// <summary>获取组件包：本地缓存/本地目录已存在且校验通过则复用，否则下载。</summary>
    public static async Task<string> AcquirePackageAsync(
        string? proxy, IProgress<(long received, long total)>? progress, Action<string> log, CancellationToken ct = default)
    {
        var cached = CachedZipPath;

        // 本地缓存命中
        if (File.Exists(cached) && new FileInfo(cached).Length == PackageSize
            && Sha256File(cached) == PackageSha256)
        {
            log("组件包缓存校验通过，直接复用。");
            return cached;
        }

        // exe 旁边的 packages 目录（离线分发场景）
        var exeDir = AppContext.BaseDirectory;
        var local = Path.Combine(exeDir, "packages", PackageName);
        if (File.Exists(local) && Sha256File(local) == PackageSha256)
        {
            log("发现本地组件包，复制到缓存。");
            Directory.CreateDirectory(CacheDir);
            File.Copy(local, cached, overwrite: true);
            return cached;
        }

        log($"开始下载组件包（{PackageSize / 1024 / 1024} MB）...");
        Directory.CreateDirectory(CacheDir);
        var tmp = cached + ".tmp";

        using var handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(proxy))
        {
            handler.Proxy = new WebProxy(proxy);
            handler.UseProxy = true;
        }
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
        using var resp = await http.GetAsync(PackageUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? PackageSize;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(tmp);
        var buffer = new byte[1 << 19];
        long received = 0;
        int read;
        var lastReport = Environment.TickCount64;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;
            if (Environment.TickCount64 - lastReport > 300)
            {
                lastReport = Environment.TickCount64;
                progress?.Report((received, total));
            }
        }
        progress?.Report((received, total));

        var hash = Sha256File(tmp);
        if (hash != PackageSha256)
        {
            File.Delete(tmp);
            throw new InvalidOperationException($"下载包校验失败（SHA256 不匹配），已删除。可尝试配置代理后重试。");
        }
        File.Move(tmp, cached, overwrite: true);
        log("下载完成，SHA256 校验通过。");
        return cached;
    }
}
