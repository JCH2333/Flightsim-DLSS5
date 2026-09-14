using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 启动公告弹窗（对齐 GSX AnnouncementPopupDialog）：类别/置顶标签 + 标题 + 时间 + 内容 + "我知道了"。
/// 内容区与使用教程页同一套滚动方案：ScrollBars.None + WheelRouter 滚轮 + ScrollIndicator 拖动滑块。
/// 逐条展示：关闭后由主窗体弹出下一条待展示公告。
/// </summary>
public sealed class AnnouncementDialog : Theme.DpiScaledForm
{
    private readonly Theme.GlassButton _btnOk = new();
    private readonly Theme.GlassButton _btnClose = new();

    public AnnouncementDialog(AnnouncementsClient.Announcement announcement) : base(480, 460)
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

        // ✕ 关闭按钮（右上角）；与"我知道了"等效：关闭 = 已读
        _btnClose.Text = "✕";
        _btnClose.Size = new Size(30, 30);
        _btnClose.Location = new Point(480 - 30 - 14, 14);
        _btnClose.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        Controls.Add(_btnClose);

        var tags = new FlowLayoutPanel
        {
            Location = new Point(30, 72),
            Size = new Size(360, 30),
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
        title.MaximumSize = new Size(420, 60);   // 最多两行，超出省略；标题区固定 106..168
        Controls.Add(title);

        var time = Theme.MakeLabel(AnnouncementsClient.FormatTime(announcement.CreatedAt), Theme.TextMuted, 8.5f);
        time.Location = new Point(30, 168);
        Controls.Add(time);

        // 内容区：固定矩形 + 四向锚定，与标题区/按钮区互不重叠；
        // 与使用教程页同一套滚动方案（ScrollBars.None + 滚轮路由 + 拖动滑块）
        var content = new RichTextBox
        {
            Text = announcement.Content,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.SurfaceRaised,
            ScrollBars = RichTextBoxScrollBars.None,
            Font = new Font(Theme.FontUi, 9.5f),
            Location = new Point(30, 200),
            Size = new Size(420, 186),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        content.ForeColor = Theme.TextSecondary;
        Controls.Add(content);
        Theme.AttachScrollIndicator(content, this, rightInset: 30, topInset: 200, height: 186);

        _btnOk.Text = L.S("我知道了", "Got it");
        _btnOk.Primary = true;
        _btnOk.Size = new Size(420, 42);
        _btnOk.Location = new Point(30, 396);
        _btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _btnOk.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        Controls.Add(_btnOk);
        // GlassButton 未实现 IButtonControl，ESC 快捷键不可用；关闭走 ✕ / 我知道了 两个按钮

        Shown += (_, _) =>
        {
            Theme.ApplyWindowChrome(this);
        };
        Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.GlassBorder);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
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
