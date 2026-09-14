using System.Reflection;

namespace DLSS5Patcher.Core;

/// <summary>
/// 运行日志：单一持久文件（%LOCALAPPDATA%\DLSS5Patcher\logs\log.txt），跨启动追加。
/// 每次启动写入会话分隔头；旧版按会话分文件的日志在启动时并入该文件后删除。
/// 提交反馈时附带整个文件（读取方按尾部 256KB 截断）。
/// 文件超过 8MB 时裁剪到末尾 1MB，防止无限膨胀。
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static string? _path;
    private const long TrimThreshold = 8_000_000;
    private const long TrimKeep = 1_000_000;

    /// <summary>日志文件路径；初始化失败时为 null（日志功能静默降级）。</summary>
    public static string? LogPath => _path;

    public static void Init()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DLSS5Patcher", "logs");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "log.txt");
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            var header = $"\r\n# ── DLSS5Patcher v{ver?.Major}.{ver?.Minor}.{ver?.Build} — session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ──\r\n";
            lock (Gate)
            {
                // 旧版按会话分文件（log-*.txt）：并入单文件后删除，保证"所有日志"完整可附
                foreach (var f in Directory.GetFiles(dir, "log-*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.AppendAllText(_path, $"\r\n# ── 旧版会话日志 {Path.GetFileName(f)} ──\r\n");
                        File.AppendAllText(_path, File.ReadAllText(f));
                        File.Delete(f);
                    }
                    catch { /* 被占用则保留，下次再试 */ }
                }
                TrimIfNeeded();
                File.AppendAllText(_path, header);
            }
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
                if (new FileInfo(path).Length > TrimThreshold) return;   // 超限停写，下次启动裁剪
                File.AppendAllText(path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{(ex != null ? "\r\n" + ex : "")}\r\n");
            }
        }
        catch
        {
            // 日志绝不影响主流程
        }
    }

    /// <summary>超过阈值时只保留末尾 1MB（读取方本就只上传尾部 256KB）。</summary>
    private static void TrimIfNeeded()
    {
        var path = _path;
        if (path == null) return;
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists || fi.Length <= TrimThreshold) return;
            string tail;
            using (var fs = fi.OpenRead())
            {
                fs.Seek(-TrimKeep, SeekOrigin.End);
                using var reader = new StreamReader(fs);
                tail = reader.ReadToEnd();
            }
            File.WriteAllText(path, tail);
        }
        catch { /* 裁剪失败不影响使用 */ }
    }
}
