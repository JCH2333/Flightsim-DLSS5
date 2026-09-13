using System.Diagnostics;
using System.Text.Json.Serialization;

namespace DLSS5Patcher.Core;

public sealed class GameInstall
{
    public required string GameDir { get; init; }
    public required string ExePath { get; init; }
    public required string Source { get; init; }   // Steam / 微软商店 / 手动
    public required string ExeName { get; init; }  // FlightSimulator2024.exe / FlightSimulator.exe
}

public enum GpuGeneration { Unknown, Other, Turing, Ampere, Ada, Blackwell }

public sealed record GpuInfo(string Name, string Driver, GpuGeneration Generation, int VramMb = 0)
{
    public bool IsNvidia => Generation is not (GpuGeneration.Unknown or GpuGeneration.Other);

    public string GenerationCn => Generation switch
    {
        GpuGeneration.Turing => L.S("RTX 20 系 (Turing / SM75)", "RTX 20 series (Turing / SM75)"),
        GpuGeneration.Ampere => L.S("RTX 30 系 (Ampere / SM86)", "RTX 30 series (Ampere / SM86)"),
        GpuGeneration.Ada => L.S("RTX 40 系 (Ada / SM89)", "RTX 40 series (Ada / SM89)"),
        GpuGeneration.Blackwell => L.S("RTX 50 系 (Blackwell / SM120)", "RTX 50 series (Blackwell / SM120)"),
        GpuGeneration.Other => L.S("非 RTX 显卡", "Non-RTX GPU"),
        _ => L.S("未知", "Unknown")
    };

    /// <summary>本版本工具支持的 GPU 世代（20-50 系均走 OptiScaler 路线，按世代换 runtime 与默认参数）。</summary>
    public bool SupportedThisVersion => Generation is GpuGeneration.Turing or GpuGeneration.Ampere
                                                      or GpuGeneration.Ada or GpuGeneration.Blackwell;

    /// <summary>
    /// 推荐 WorkingScale（模型工作分辨率占比）：按世代给基础值，再按显存封顶。
    /// 神经渲染的显存开销随 WorkingScale 增长，8GB 级笔记本卡跑 1.0 会 OOM 崩溃（群友实测）。
    /// VramMb == 0 表示未探测到显存，仅按世代推荐。
    /// </summary>
    public string RecommendedWorkingScale
    {
        get
        {
            var byGen = Generation switch
            {
                GpuGeneration.Turing or GpuGeneration.Ampere => "0.5",
                GpuGeneration.Ada => "0.75",
                GpuGeneration.Blackwell => "1.0",
                _ => "0.5",
            };
            if (VramMb <= 0) return byGen;

            // MSFS 2024 主菜单显存占用极高（世界预载），16GB 以下一律压到 0.5 及以下，
            // 避免在菜单开启神经渲染时 OOM 闪退（用户实测反馈）。
            var cap = VramMb >= 16_000 ? "1.0" : VramMb >= 8_000 ? "0.5" : "0.35";
            return string.CompareOrdinal(byGen, cap) > 0 ? cap : byGen;
        }
    }

    /// <summary>显存描述（用于状态行与支持排查）。</summary>
    public string VramText => VramMb > 0
        ? (VramMb >= 1024 ? $"{VramMb / 1024} GB" : $"{VramMb} MB")
        : L.S("未知", "unknown");

    /// <summary>神经渲染 runtime 需要 616.56 及以上驱动。</summary>
    public bool DriverOk => DriverVersionAtLeast(616, 56);

    public bool DriverVersionAtLeast(int major, int minor)
    {
        var m = System.Text.RegularExpressions.Regex.Match(Driver, @"(\d+)\.(\d+)");
        if (!m.Success) return false;
        int mj = int.Parse(m.Groups[1].Value), mi = int.Parse(m.Groups[2].Value);
        return mj > major || (mj == major && mi >= minor);
    }

    public static GpuInfo Detect()
    {
        string name = "", driver = "";
        int vramMb = 0;

        foreach (var exe in new[] { "nvidia-smi.exe", Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe") })
        {
            try
            {
                var psi = new ProcessStartInfo(exe, "--query-gpu=name,driver_version,memory.total --format=csv,noheader")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                var line = p!.StandardOutput.ReadLine();
                p.WaitForExit(5000);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split(',');
                    name = parts[0].Trim();
                    driver = parts.Length > 1 ? parts[1].Trim() : "";
                    if (parts.Length > 2)
                    {
                        // 形如 "8192 MiB"
                        var m = System.Text.RegularExpressions.Regex.Match(parts[2], @"(\d+)");
                        if (m.Success) vramMb = int.Parse(m.Groups[1].Value);
                    }
                    break;
                }
            }
            catch { /* 尝试下一个来源 */ }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Name FROM Win32_VideoController WHERE Name LIKE '%NVIDIA%'");
                foreach (var o in searcher.Get())
                {
                    name = o["Name"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(name)) break;
                }
            }
            catch { }
        }

        return new GpuInfo(name, driver, Classify(name), vramMb);
    }

    private static GpuGeneration Classify(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return GpuGeneration.Unknown;
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"RTX|GTX|GeForce", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return GpuGeneration.Other;

        var m = System.Text.RegularExpressions.Regex.Match(name.Replace(" ", ""), @"RTX(\d{3,4})");
        if (!m.Success) return GpuGeneration.Other;
        return m.Groups[1].Value[0] switch
        {
            '2' => GpuGeneration.Turing,
            '3' => GpuGeneration.Ampere,
            '4' => GpuGeneration.Ada,
            '5' => GpuGeneration.Blackwell,
            _ => GpuGeneration.Other,
        };
    }
}

/// <summary>安装清单：记录安装了哪些文件、改了什么配置，用于精确回滚。</summary>
public sealed class InstallManifest
{
    [JsonPropertyName("tag")] public string Tag { get; set; } = "";
    [JsonPropertyName("installedAt")] public string InstalledAt { get; set; } = "";
    [JsonPropertyName("packageSha256")] public string PackageSha256 { get; set; } = "";
    [JsonPropertyName("gameDir")] public string GameDir { get; set; } = "";
    [JsonPropertyName("files")] public List<string> Files { get; set; } = new();
    [JsonPropertyName("dirs")] public List<string> Dirs { get; set; } = new();
    [JsonPropertyName("userCfgPath")] public string? UserCfgPath { get; set; }
    [JsonPropertyName("userCfgBackupPath")] public string? UserCfgBackupPath { get; set; }
    [JsonPropertyName("workingScale")] public string WorkingScale { get; set; } = "";
}
