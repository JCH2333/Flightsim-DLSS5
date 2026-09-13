using System.Reflection;

namespace DLSS5Patcher.Core;

/// <summary>
/// 运行日志：每次启动一个会话文件（%LOCALAPPDATA%\DLSS5Patcher\logs\log-*.txt）。
/// 单文件超过 1MB 即停止追加（防膨胀），启动时只保留最近 8 个会话文件。
/// 提交反馈时自动附带本次会话日志（尾部 256KB）。
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static string? _path;
    private const long MaxBytes = 1_000_000;
    private const int KeepFiles = 8;

    /// <summary>本次会话日志路径；初始化失败时为 null（日志功能静默降级）。</summary>
    public static string? SessionPath => _path;

    public static void Init()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DLSS5Patcher", "logs");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "log-*.txt")
                         .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                         .Skip(KeepFiles))
            {
                try { File.Delete(f); } catch { /* 被占用则下次再清 */ }
            }
            _path = Path.Combine(dir, $"log-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            File.WriteAllText(_path, $"# DLSS5Patcher v{ver?.Major}.{ver?.Minor}.{ver?.Build} — session started {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");
        }
        catch
        {
            _path = null;
        }
    }

    public static void Info(string msg) => Write("INFO", msg, null);
    public static void Warn(string msg) => Write("WARN", msg, null);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", msg, ex);

    private static void Write(string level, string msg, Exception? ex)
    {
        var path = _path;
        if (path == null) return;
        try
        {
            lock (Gate)
            {
                if (new FileInfo(path).Length > MaxBytes) return;   // 超限停写，防膨胀
                File.AppendAllText(path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{(ex != null ? "\r\n" + ex : "")}\r\n");
            }
        }
        catch
        {
            // 日志绝不影响主流程
        }
    }
}
