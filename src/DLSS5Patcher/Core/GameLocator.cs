using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace DLSS5Patcher.Core;

/// <summary>定位 MSFS 2024 / 2020 的安装目录（Steam / 微软商店 / 手动兜底）。</summary>
public static class GameLocator
{
    public static readonly (string AppId, string ExeName, string CnName, string EnName)[] Targets =
    {
        ("2537590", "FlightSimulator2024.exe", "微软模拟飞行 2024", "MSFS 2024"),
        ("1250410", "FlightSimulator.exe", "微软模拟飞行 2020", "MSFS 2020"),
        ("2014780", "X-Plane.exe", "X-Plane 12", "X-Plane 12"),
    };

    public static GameInstall? Find(string appId, string exeName)
    {
        // 1. Steam：注册表 + 各盘常见库目录（本机 Steam 就不在注册表里，靠盘符扫描兜底）
        foreach (var steamApps in SteamAppsCandidates())
        {
            var acf = Path.Combine(steamApps, $"appmanifest_{appId}.acf");
            if (EnvDebug) Console.WriteLine($"[dbg] acf: {acf} exists={File.Exists(acf)}");
            if (!File.Exists(acf)) continue;
            try
            {
                var m = Regex.Match(File.ReadAllText(acf), "\"installdir\"\\s+\"([^\"]+)\"");
                if (EnvDebug) Console.WriteLine($"[dbg] regex={m.Success} installdir={m.Groups[1].Value}");
                if (!m.Success) continue;
                var common = Path.Combine(steamApps, "common");
                var dir = Path.Combine(common, m.Groups[1].Value);
                if (EnvDebug) Console.WriteLine($"[dbg] dir={dir} exeExists={File.Exists(Path.Combine(dir, exeName))}");
                if (File.Exists(Path.Combine(dir, exeName)))
                    return new GameInstall { GameDir = dir, ExePath = Path.Combine(dir, exeName), Source = "Steam", ExeName = exeName };
            }
            catch (Exception ex)
            {
                if (EnvDebug) Console.WriteLine($"[dbg] ex: {ex.Message}");
            }
        }

        // 2. Steam 兜底：common 下直接按 exe 找（目录名非标准的情况）
        foreach (var steamApps in SteamAppsCandidates())
        {
            var common = Path.Combine(Path.GetDirectoryName(steamApps)!, "common");
            if (!Directory.Exists(common)) continue;
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(common))
                {
                    if (File.Exists(Path.Combine(sub, exeName)))
                        return new GameInstall { GameDir = sub, ExePath = Path.Combine(sub, exeName), Source = "Steam", ExeName = exeName };
                }
            }
            catch { }
        }

        // 3. 微软商店：XboxGames 目录扫描
        foreach (var drive in DriveRoots())
        {
            var xg = Path.Combine(drive, "XboxGames");
            if (!Directory.Exists(xg)) continue;
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(xg))
                {
                    var content = Path.Combine(sub, "Content");
                    if (File.Exists(Path.Combine(content, exeName)))
                        return new GameInstall { GameDir = content, ExePath = Path.Combine(content, exeName), Source = L.S("微软商店", "Microsoft Store"), ExeName = exeName };
                }
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// 校验手动选择的目录（须包含目标 exe）。
    /// 兼容 Xbox/微软商店结构：选到上层目录时自动尝试 Content 子目录。
    /// </summary>
    public static GameInstall? FromManualDir(string dir, string exeName)
    {
        foreach (var candidate in new[] { dir, Path.Combine(dir, "Content") })
        {
            if (File.Exists(Path.Combine(candidate, exeName)))
                return new GameInstall { GameDir = candidate, ExePath = Path.Combine(candidate, exeName), Source = L.S("手动指定", "Manual"), ExeName = exeName };
        }
        return null;
    }

    private static bool EnvDebug => Environment.GetEnvironmentVariable("DLSS5_DEBUG") == "1";

    private static IEnumerable<string> SteamAppsCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var steam = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) as string;
        foreach (var root in CandidateSteamRoots(steam))
        {
            var sa = Path.Combine(root, "steamapps");
            if (seen.Add(sa) && Directory.Exists(sa)) yield return sa;
        }
    }

    private static IEnumerable<string> CandidateSteamRoots(string? registered)
    {
        var roots = new List<string>();
        if (!string.IsNullOrEmpty(registered)) roots.Add(registered);
        foreach (var drive in DriveRoots())
        {
            roots.Add(Path.Combine(drive, "Steam"));
            roots.Add(Path.Combine(drive, "SteamLibrary"));
            roots.Add(Path.Combine(drive, "Program Files (x86)", "Steam"));
        }
        return roots;
    }

    private static IReadOnlyList<string> DriveRoots()
    {
        try
        {
            // 保留尾部反斜杠（"F:\\"），否则 Path.Combine 会生成 "F:SteamLibrary" 这类盘符相对路径
            return Directory.GetLogicalDrives();
        }
        catch
        {
            return new List<string> { "C:\\" };
        }
    }
}
