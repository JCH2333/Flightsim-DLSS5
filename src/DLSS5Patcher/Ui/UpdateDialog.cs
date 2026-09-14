using System.Text.RegularExpressions;
using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 强制更新对话框：发现新版本时阻塞主界面，直到下载校验完成并重启，或用户主动退出程序。
/// 没有关闭按钮，Alt+F4 也被拦截；下载失败时仅提供 重试 / 打开发布页 / 退出 三个出口。
/// </summary>
public sealed class UpdateDialog : Theme.DpiScaledForm
{
    private readonly Updater.UpdateInfo _info;
    private readonly TextBox _notes;
    private readonly Panel _track;
    private readonly Panel _fill;
    private readonly Label _status;
    private readonly Theme.GlassButton _btnUpdate = new();
    private readonly Theme.GlassButton _btnPage = new();
    private readonly Theme.GlassButton _btnExit = new();
    private bool _allowClose;

    public UpdateDialog(Updater.UpdateInfo info) : base(560, 400)
    {
        _info = info;

        Text = L.S("发现新版本", "Update Available");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;   // 无关闭按钮
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 400);
        BackColor = Theme.FromHex("#1a1c18");
        Font = new Font(Theme.FontUi, 9F);
        FormClosing += (_, e) => e.Cancel = !_allowClose;
        Shown += (_, _) => Theme.ApplyWindowChrome(this);

        var title = Theme.MakeLabel(
            L.S($"发现新版本 {info.Tag}（当前 v{Updater.CurrentVersion}）",
                $"Update {info.Tag} available (current v{Updater.CurrentVersion})"),
            Theme.Text, 12f, bold: true);
        title.Location = new Point(28, 20);
        Controls.Add(title);

        var sub = Theme.MakeLabel(
            L.S("必须更新后才能继续使用，程序将自动完成下载与替换（不可跳过）。",
                "You must update before continuing — download and replacement run automatically (cannot be skipped)."),
            Theme.TextSecondary, 9f);
        sub.Location = new Point(28, 56);
        Controls.Add(sub);

        var notesText = NotesPreview(info.Body);
        _notes = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Size = new Size(504, 168),
            Location = new Point(28, 84),
            BackColor = Theme.SurfaceInset,
            ForeColor = Theme.TextSecondary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font(Theme.FontUi, 8.25f),
            Text = notesText,
        };
        _notes.Visible = notesText.Length > 0;
        Controls.Add(_notes);

        _track = new Panel { Size = new Size(504, 6), Location = new Point(28, 268), BackColor = Theme.FromHex("#353630"), Visible = false };
        Theme.EnableBorder(_track, Theme.Border);
        _fill = new Panel { Size = new Size(0, 4), Location = new Point(1, 1), BackColor = Theme.SignalStrong };
        _track.Controls.Add(_fill);
        Controls.Add(_track);

        _status = new Label
        {
            Text = L.S("准备更新…", "Preparing update..."),
            AutoSize = false,
            Size = new Size(504, 34),
            Location = new Point(28, 286),
            ForeColor = Theme.TextMuted,
            Font = new Font(Theme.FontUi, 8.5f),
        };
        Controls.Add(_status);

        _btnExit.Text = L.S("退出程序", "Exit");
        _btnExit.Size = new Size(100, 40);
        _btnExit.Location = new Point(142, 332);
        _btnExit.Visible = false;
        _btnExit.Click += (_, _) => { _allowClose = true; Application.Exit(); };
        Controls.Add(_btnExit);

        _btnPage.Text = L.S("打开发布页", "Release Page");
        _btnPage.Size = new Size(130, 40);
        _btnPage.Location = new Point(252, 332);
        _btnPage.Visible = false;
        _btnPage.Click += (_, _) => OpenUrl(Updater.ReleasesUrl);
        Controls.Add(_btnPage);

        _btnUpdate.Text = L.S("立即更新", "Update Now");
        _btnUpdate.Primary = true;
        _btnUpdate.Size = new Size(140, 40);
        _btnUpdate.Location = new Point(392, 332);
        _btnUpdate.Click += (_, _) => _ = RunAsync();
        Controls.Add(_btnUpdate);

        SealLayout();   // 布局缩放由 DpiScaledForm 在 OnLoad 按真实窗口 DPI 进行
    }

    private async Task RunAsync()
    {
        _btnUpdate.Enabled = false;
        _btnPage.Visible = _btnExit.Visible = false;
        _track.Visible = true;
        _fill.Width = 0;
        _status.ForeColor = Theme.TextMuted;
        _status.Text = L.S("正在连接服务器…", "Connecting...");

        var progress = new Progress<(long received, long total)>(t =>
        {
            var pct = t.total > 0 ? (double)t.received / t.total : 0;
            _fill.Width = (int)((_track.Width - 2) * Math.Clamp(pct, 0, 1));
            _status.Text = L.S($"正在下载 {t.received / 1048576.0:F1} / {t.total / 1048576.0:F1} MB…",
                               $"Downloading {t.received / 1048576.0:F1} / {t.total / 1048576.0:F1} MB…");
        });

        try
        {
            var path = await Updater.DownloadAsync(_info, progress, s => SafeStatus(s));
            SafeStatus(L.S("校验通过，正在应用更新并重启…", "Verified — applying update and restarting..."));
            await Task.Run(() => Updater.ApplyAndRestart(path));
            _allowClose = true;
            Application.Exit();
        }
        catch (Exception ex)
        {
            _status.ForeColor = Theme.Danger;
            SafeStatus(L.S($"更新失败：{ex.Message}", $"Update failed: {ex.Message}"));
            _btnUpdate.Enabled = true;
            _btnUpdate.Text = L.S("重试", "Retry");
            _btnPage.Visible = _btnExit.Visible = true;
        }
    }

    private void SafeStatus(string text)
    {
        try { if (IsHandleCreated) BeginInvoke(() => _status.Text = text); }
        catch { _status.Text = text; }
    }

    private static string NotesPreview(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        var s = Regex.Replace(body, @"!\[[^\]]*\]\([^)]*\)", "");      // 图片 → 去掉
        s = Regex.Replace(s, @"\[([^\]]*)\]\([^)]*\)", "$1");           // 链接 → 纯文本
        s = Regex.Replace(s, @"^#{1,6}\s*", "", RegexOptions.Multiline); // 标题井号
        s = s.Replace("**", "").Replace("`", "");                       // 加粗 / 行内代码标记
        s = s.Replace("\r\n", "\n").Replace("\r", "\n");
        return s.Length > 3000 ? s[..3000] + "\n…" : s;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { /* 无法打开浏览器时忽略 */ }
    }
}
