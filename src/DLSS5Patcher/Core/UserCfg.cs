using System.Text.RegularExpressions;

namespace DLSS5Patcher.Core;

/// <summary>读写 MSFS 的 UserCfg.opt（图形配置），安装 NR 需要把抗锯齿切到 DLSS。</summary>
public static class UserCfg
{
    public static string? PathFor(string exeName)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return exeName.Contains("2024")
            ? Path.Combine(profile, @"AppData\Roaming\Microsoft Flight Simulator 2024", "UserCfg.opt")
            : Path.Combine(profile, @"AppData\Roaming\Microsoft Flight Simulator", "UserCfg.opt");
    }

    public static string? ReadAntiAliasing(string path)
    {
        if (!File.Exists(path)) return null;
        foreach (var line in File.ReadLines(path))
        {
            var m = Regex.Match(line, @"^\s*AntiAliasing\s+(\S+)");
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    /// <summary>把抗锯齿切到 DLSS；先备份原文件（仅在首次改动时），返回是否发生了修改。</summary>
    public static (bool Changed, string? BackupPath) SetAntiAliasing(string path, string value = "DLSS")
    {
        if (!File.Exists(path)) return (false, null);
        var text = File.ReadAllText(path);
        if (Regex.IsMatch(text, @"^\s*AntiAliasing\s+DLSS\s*$", RegexOptions.Multiline))
            return (false, null);

        var backup = path + ".bak_dlss5_tool";
        if (!File.Exists(backup)) File.Copy(path, backup);

        var newText = Regex.Replace(
            text,
            @"^(\s*AntiAliasing\s+)\S+$",
            "$1" + value,
            RegexOptions.Multiline);
        File.WriteAllText(path, newText);
        return (true, backup);
    }

    /// <summary>从备份恢复。</summary>
    public static void Restore(string path, string backupPath)
    {
        if (File.Exists(backupPath)) File.Copy(backupPath, path, overwrite: true);
    }
}
