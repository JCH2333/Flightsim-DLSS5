using System.Security.Cryptography;
using System.Text.Json;

namespace DLSS5Patcher.Core;

/// <summary>
/// 包体与更新的分发目录：自有服务器优先（备案后追加域名置顶），GitHub Release 兜底，gh-proxy 镜像最后。
/// 包体 SHA256 硬编码在本类——服务器被攻破或下载链路被篡改都无法通过校验；
/// 自更新 manifest 由服务器 ECDSA P-256 私钥签名，客户端内嵌公钥验签，HTTP 明文链路同样可信。
/// </summary>
public static class PackageCatalog
{
    private const string ServerBaseBuiltin = "http://47.109.31.236:8420/repo/dlss5";
    private const string GithubLatest = "https://github.com/JCH2333/Flightsim-DLSS5/releases/latest/download";
    public const string MirrorPrefix = "https://gh-proxy.com/";

    /// <summary>服务器基地址：环境变量 DLSS5_REPO 或 config repo= 可覆盖（测试/应急），否则用内置。</summary>
    public static string ServerBase { get; } =
        NonEmpty(Environment.GetEnvironmentVariable("DLSS5_REPO"), AppConfig.RepoOverride, ServerBaseBuiltin)
        .TrimEnd('/');

    private static string NonEmpty(params string?[] candidates)
    {
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c)) return c;
        return "";
    }

    /// <summary>包体描述。sha256 硬编码：更换包体必须发布新版本客户端。</summary>
    public sealed record PackageSpec(string Id, string FileName, long Size, string Sha256);

    // dlss-unlocked-standalone-DLSSNR-v0.7.6.zip（与历史版本内嵌包字节一致）
    public static readonly PackageSpec MsfsPackage = new(
        "msfs", "dlss-unlocked-standalone-DLSSNR-v0.7.6.zip", 461_963_289,
        "ca824acb693e39975b152c7a6aa21af64799dc1d05ec0462451491ccda8e6ec2");

    // dlss5-feeder-kit = 教程组件包 + ReShade64.dll/json + 框架头文件（tools/make_kit_zip.py 产出）
    public static readonly PackageSpec Xp12Kit = new(
        "xp12kit", "dlss5-feeder-kit-v0.13.1-beta.1-dfc-1.4.8.zip", 153_537_610,
        "a1dbf683669c565ee1cd1f46882a4f4f1f80e3e92f6598b2df272a7f29974fc8");

    /// <summary>某包体的候选下载地址（服务器 → GitHub Release 同名资产 → gh-proxy 镜像）。</summary>
    public static List<string> CandidateUrls(PackageSpec spec)
    {
        var gh = $"{GithubLatest}/{spec.FileName}";
        return new List<string>
        {
            $"{ServerBase}/packages/{spec.FileName}",
            gh,
            MirrorPrefix + gh,
        };
    }

    /// <summary>新版 EXE 的候选下载地址（服务器（若有清单文件路径）→ GitHub Release 同名资产 → 镜像）。</summary>
    public static List<string> AppCandidateUrls(string fileName, string? serverFile)
    {
        var gh = $"{GithubLatest}/{fileName}";
        var list = new List<string>();
        if (!string.IsNullOrEmpty(serverFile)) list.Add($"{ServerBase}/{serverFile}");
        list.Add(gh);
        list.Add(MirrorPrefix + gh);
        return list;
    }

    // ───────────────────────── 自更新 manifest（服务器签名） ─────────────────────────

    /// <summary>服务器 manifest（manifest.json，由 manifest.sig ECDSA-P256 签名）。</summary>
    public sealed record ServerAppManifest(string Version, string ExeFile, long ExeSize, string ExeSha256, string Notes);

    /// <summary>清单验签公钥（server/keys/manifest.pub.der 的 SPKI DER，base64）。</summary>
    private static readonly byte[] ManifestPublicKey = Convert.FromBase64String(
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEfHYvX/RNBxgdUxDpmujUMTdcm71iKBg6+6Z9G7A8BneGt+b8niNFY4mG8K7AIYQls5/LRXpZngiWfCrX78nj9w==");

    public static bool VerifyManifestSignature(byte[] json, byte[] signature)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(ManifestPublicKey, out _);
            // 显式指定 DER 格式：部分机器上无参重载的默认格式判定会错误返回 false（实测踩坑）
            return ecdsa.VerifyData(json, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>从服务器取最新版清单（逐源尝试，验签失败视为无效）。全部不可达返回 null（调用方回退 GitHub API）。</summary>
    public static async Task<ServerAppManifest?> TryFetchAppManifestAsync(CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"DLSS5Patcher/{Updater.CurrentVersion}");
        foreach (var baseUrl in new[] { ServerBase })
        {
            try
            {
                var jsonBytes = await http.GetByteArrayAsync($"{baseUrl}/manifest.json", ct);
                var sig = await http.GetByteArrayAsync($"{baseUrl}/manifest.sig", ct);
                if (!VerifyManifestSignature(jsonBytes, sig)) continue;

                using var doc = JsonDocument.Parse(jsonBytes);
                var root = doc.RootElement;
                var version = root.GetProperty("version").GetString() ?? "";
                var exe = root.GetProperty("exe");
                var file = exe.GetProperty("file").GetString() ?? "";
                if (version.Length == 0 || file.Length == 0) continue;
                return new ServerAppManifest(
                    version, file,
                    exe.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0,
                    exe.TryGetProperty("sha256", out var h) ? h.GetString() ?? "" : "",
                    root.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { /* 该源不可达/无效，无更多服务器源时返回 null */ }
        }
        return null;
    }
}
