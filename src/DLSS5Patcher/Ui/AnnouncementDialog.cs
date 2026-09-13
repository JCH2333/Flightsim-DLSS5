using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 启动公告弹窗（对齐 GSX AnnouncementPopupDialog）：类别/置顶标签 + 标题 + 时间 + 内容 + "我知道了"。
/// 逐条展示：关闭后由主窗体弹出下一条待展示公告。
/// </summary>
public sealed class AnnouncementDialog : Form
{
    private readonly Theme.GlassButton _btnOk = new();

    public AnnouncementDialog(AnnouncementsClient.Announcement announcement)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Size = new Size(480, 460);
        BackColor = Theme.FromHex("#1a1c18");
        TopMost = true;

        var megaphone = new Label
        {
            Text = "📣",
            AutoSize = true,
            Font = new Font("Segoe UI Emoji", 15f),
            BackColor = Color.Transparent,
        };
        megaphone.Location = new Point(30, 26);
        Controls.Add(megaphone);

        var tags = new FlowLayoutPanel
        {
            Location = new Point(30, 72),
            Size = new Size(420, 30),
            BackColor = Color.Transparent,
            AutoSize = true,
            WrapContents = false,
        };
        tags.Controls.Add(Theme.MakeBadge(AnnouncementsClient.CategoryLabel(announcement.Category), "success"));
        if (announcement.Pinned)
            tags.Controls.Add(Theme.MakeBadge(L.S("置顶", "Pinned"), "warning"));
        Controls.Add(tags);

        var title = Theme.MakeLabel(announcement.Title, Theme.Text, 13.5f, bold: true);
        title.Location = new Point(30, 106);
        title.MaximumSize = new Size(420, 0);
        Controls.Add(title);

        var time = Theme.MakeLabel(AnnouncementsClient.FormatTime(announcement.CreatedAt), Theme.TextMuted, 8.5f);
        time.Location = new Point(30, title.Bottom + 10);
        Controls.Add(time);

        var content = new RichTextBox
        {
            Text = announcement.Content,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.SurfaceRaised,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Font = new Font(Theme.FontUi, 9.5f),
            Location = new Point(30, time.Bottom + 14),
            Size = new Size(420, 190),
        };
        content.ForeColor = Theme.TextSecondary;
        Controls.Add(content);

        _btnOk.Text = L.S("我知道了", "Got it");
        _btnOk.Primary = true;
        _btnOk.Size = new Size(420, 42);
        _btnOk.Location = new Point(30, 396);
        _btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _btnOk.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        Controls.Add(_btnOk);

        _btnOk.Location = new Point(30, ClientSize.Height - 42 - 22);

        Shown += (_, _) => Theme.ApplyWindowChrome(this);
        Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.GlassBorder);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        // 高 DPI：与 UpdateDialog/LanguageDialog 相同的 96-DPI 设计 + 启动时整体缩放
        float dpi;
        using (var g = CreateGraphics()) dpi = g.DpiX / 96f;
        if (dpi > 1.01f)
        {
            Scale(new SizeF(dpi, dpi));
            ClientSize = new Size((int)(480 * dpi), (int)(460 * dpi));
            _btnOk.Location = new Point((int)(30 * dpi), ClientSize.Height - (int)(42 * dpi) - (int)(22 * dpi));
        }
    }

    public static void ShowChain(Form owner, List<AnnouncementsClient.Announcement> popups)
    {
        var shown = new HashSet<string>(AppConfig.AnnShownPopupIds.Split(',', StringSplitOptions.RemoveEmptyEntries));
        foreach (var a in popups.OrderByDescending(p => p.Id))
        {
            if (shown.Contains(a.Id.ToString())) continue;
            using var dlg = new AnnouncementDialog(a);
            dlg.ShowDialog(owner);
            shown.Add(a.Id.ToString());
            // GSX 同款：只保留最近 50 条展示历史，防止无限增长
            AppConfig.AnnShownPopupIds = string.Join(",", shown.TakeLast(50));
            AppConfig.Save();
        }
    }
}
