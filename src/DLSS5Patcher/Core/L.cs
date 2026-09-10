namespace DLSS5Patcher.Core;

/// <summary>极简双语助手：界面、日志与 CLI 共用。语言在首次启动时选择，存于 AppConfig，切换后重启生效。</summary>
public static class L
{
    public static bool English { get; set; }

    public static string S(string zh, string en) => English ? en : zh;
}
