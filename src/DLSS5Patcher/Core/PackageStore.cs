using System.Security.Cryptography;

namespace DLSS5Patcher.Core;

/// <summary>
/// 组件包访问（v1.1.0 起包体不再内嵌 EXE，全部从分发服务器下载后缓存本地）。
/// 本类只负责：文件 SHA256 校验 + 打开已校验的包文件。
/// </summary>
public static class PackageStore
{
    // ── MSFS 组件包标识（写入安装清单，用于状态展示与升级判定） ──
    public const string MsfsPackageTag = "DLSSNR-v0.7.6";

    // ── ReShade 6.8.0 Vulkan 隐式层 DLL（随 XP12 组件包分发） ──
    public const string ReShadeLayerDllSha256 = "0cee63f9c9f13f3ac909c5b4903f4dbb4b719a7ab3b4f13b0deaf83c814b94f7";

    public static string Sha256File(string path)
    {
        using var h = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(h.ComputeHash(fs)).ToLowerInvariant();
    }

    /// <summary>打开已下载的 MSFS 组件包 zip（先做 SHA256 完整性校验；调用方先用 PackageDownloader.EnsureAsync 确保存在）。</summary>
    public static Stream OpenMsfsPackage(Action<string> log)
    {
        var path = PackageDownloader.CachedPath(PackageCatalog.MsfsPackage);
        return OpenVerified(path, PackageCatalog.MsfsPackage.Sha256,
            L.S("校验组件包（约 1-2 秒）...", "Verifying the package (takes a second)..."),
            L.S("组件包校验通过。", "Package verified."),
            L.S("组件包校验失败（SHA256 不匹配），缓存可能已损坏，请重试安装（会自动重新下载）。",
                "Package verification failed (SHA256 mismatch) — the cache may be corrupted, retry the install (it re-downloads automatically)."),
            log);
    }

    /// <summary>打开本地文件并校验 SHA256，通过后返回只读流。</summary>
    public static Stream OpenVerified(string path, string sha256, string verifyMsg, string okMsg, string failMsg, Action<string> log)
    {
        log(verifyMsg);
        var hash = Sha256File(path);
        if (!hash.Equals(sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(failMsg);
        log(okMsg);
        return File.OpenRead(path);
    }
}
