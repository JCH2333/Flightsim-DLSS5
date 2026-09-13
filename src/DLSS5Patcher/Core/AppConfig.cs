namespace DLSS5Patcher.Core;

/// <summary>
/// 用户配置（%LOCALAPPDATA%\DLSS5Patcher\config.txt）。
/// 新格式为 key=value（lang / kit）；兼容旧版单行文件——无 '=' 时整行视为组件包目录。
/// </summary>
public static class AppConfig
{
    /// <summary>"" = 尚未选择（触发首启语言对话框）；"zh" / "en"。</summary>
    public static string Lang { get; set; } = "";
    public static string KitDir { get; set; } = "";

    /// <summary>手动指定的游戏目录（自动检测不到时使用；空 = 自动检测）。</summary>
    public static string ManualMsfs2024Dir { get; set; } = "";
    public static string ManualMsfs2020Dir { get; set; } = "";

    /// <summary>手动指定的游戏主程序 exe 路径（优先于目录与自动检测；空 = 未指定）。</summary>
    public static string ManualExe24 { get; set; } = "";
    public static string ManualExe20 { get; set; } = "";
    public static string ManualXp12Exe { get; set; } = "";

    /// <summary>分发服务器基地址覆盖（repo= 键；空 = 内置源。测试/应急用）。</summary>
    public static string RepoOverride { get; set; } = "";

    /// <summary>公告已读水位（已看过的最大公告 id）与已展示过的弹窗公告 id 列表（逗号分隔）。</summary>
    public static long AnnReadId { get; set; } = 0;
    public static string AnnShownPopupIds { get; set; } = "";

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DLSS5Patcher", "config.txt");

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var text = File.ReadAllText(FilePath).Trim();
            if (string.IsNullOrEmpty(text)) return;

            if (!text.Contains('='))
            {
                KitDir = text;   // 旧版单行：组件包目录
                return;
            }

            foreach (var line in text.Split('\n'))
            {
                var i = line.IndexOf('=');
                if (i <= 0) continue;
                var key = line[..i].Trim();
                var val = line[(i + 1)..].Trim();
                if (key == "lang") Lang = val;
                else if (key == "kit") KitDir = val;
                else if (key == "dir24") ManualMsfs2024Dir = val;
                else if (key == "dir20") ManualMsfs2020Dir = val;
                else if (key == "exe24") ManualExe24 = val;
                else if (key == "exe20") ManualExe20 = val;
                else if (key == "exe12") ManualXp12Exe = val;
                else if (key == "repo") RepoOverride = val;
                else if (key == "annread") { if (long.TryParse(val, out var v)) AnnReadId = v; }
                else if (key == "annshown") AnnShownPopupIds = val;
            }
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, new[]
            {
                $"lang={Lang}", $"kit={KitDir}", $"dir24={ManualMsfs2024Dir}", $"dir20={ManualMsfs2020Dir}",
                $"exe24={ManualExe24}", $"exe20={ManualExe20}", $"exe12={ManualXp12Exe}", $"repo={RepoOverride}",
                $"annread={AnnReadId}", $"annshown={AnnShownPopupIds}",
            });
        }
        catch { }
    }
}
