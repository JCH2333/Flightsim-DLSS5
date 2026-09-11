using System.Security.Cryptography;

namespace DLSS5Patcher.Core;

/// <summary>
/// 内置组件包访问：所有安装素材作为嵌入资源打进 EXE（assets\*），安装全程无需联网。
/// </summary>
public static class PackageStore
{
    // ── MSFS 组件包（DLSS Unlocked / OptiScaler） ──
    public const string MsfsPackageTag = "DLSSNR-v0.7.6";
    public const string MsfsPackageName = "dlss-unlocked-standalone-DLSSNR-v0.7.6.zip";
    public const string MsfsPackageSha256 = "ca824acb693e39975b152c7a6aa21af64799dc1d05ec0462451491ccda8e6ec2";
    public const long MsfsPackageSize = 461_963_289;

    // ── ReShade 6.8.0 Vulkan 隐式层文件（从官方安装器提取后内嵌） ──
    public const string ReShadeVersion = "6.8.0.2155";
    public const string ReShadeLayerDllSha256 = "0cee63f9c9f13f3ac909c5b4903f4dbb4b719a7ab3b4f13b0deaf83c814b94f7";

    public static string Sha256File(string path)
    {
        using var h = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(h.ComputeHash(fs)).ToLowerInvariant();
    }

    private static System.IO.Stream OpenEmbedded(string name)
    {
        var stream = typeof(PackageStore).Assembly.GetManifestResourceStream("DLSS5Patcher.assets." + name);
        if (stream == null)
            throw new InvalidOperationException(
                L.S($"内置资源 {name} 缺失，请使用完整发行的 EXE。",
                    $"Embedded resource {name} is missing — please use the officially released EXE."));
        return stream;
    }

    /// <summary>打开内置 MSFS 组件包（先做 SHA256 完整性校验，再定位回起点）。</summary>
    public static System.IO.Stream OpenMsfsPackage(Action<string> log)
    {
        log(L.S("校验内置组件包（约 1-2 秒）...", "Verifying the embedded package (takes a second)..."));
        var stream = OpenEmbedded(MsfsPackageName);
        using var h = SHA256.Create();
        var hash = Convert.ToHexString(h.ComputeHash(stream)).ToLowerInvariant();
        if (hash != MsfsPackageSha256)
            throw new InvalidOperationException(
                L.S("内置组件包校验失败（SHA256 不匹配），EXE 可能已损坏，请重新下载。",
                    "Embedded package verification failed (SHA256 mismatch) — the EXE may be corrupted, please re-download."));
        stream.Seek(0, System.IO.SeekOrigin.Begin);
        log(L.S("内置组件包校验通过。", "Embedded package verified."));
        return stream;
    }

    /// <summary>从内置资源部署 ReShade Vulkan 层文件（ReShade64.dll / ReShade64.json）到目标目录。</summary>
    public static void WriteReShadeLayerFiles(string targetDir, Action<string> log)
    {
        DeployEmbedded("ReShade64.dll", System.IO.Path.Combine(targetDir, "ReShade64.dll"), ReShadeLayerDllSha256, log);
        DeployEmbedded("ReShade64.json", System.IO.Path.Combine(targetDir, "ReShade64.json"), null, log);
    }

    private static void DeployEmbedded(string name, string dest, string? sha256, Action<string> log)
    {
        using var stream = OpenEmbedded(name);
        using var h = SHA256.Create();
        var hash = Convert.ToHexString(h.ComputeHash(stream)).ToLowerInvariant();
        if (sha256 != null && hash != sha256)
            throw new InvalidOperationException(
                L.S($"内置资源 {name} 校验失败，EXE 可能已损坏，请重新下载。",
                    $"Embedded resource {name} failed verification — the EXE may be corrupted, please re-download."));
        stream.Seek(0, System.IO.SeekOrigin.Begin);
        using var outFs = System.IO.File.Create(dest);
        stream.CopyTo(outFs);
        log(L.S($"已部署 {System.IO.Path.GetFileName(dest)}（{stream.Length / 1024} KB）。", $"Deployed {System.IO.Path.GetFileName(dest)} ({stream.Length / 1024} KB)."));
    }

    /// <summary>读取内置的 ReShade 官方框架头文件（文本）。</summary>
    public static string ReadFxh(string name)
    {
        using var stream = OpenEmbedded(name);
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }
}
