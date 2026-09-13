using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 用户协议确认弹窗。启动时强制展示：两份文件都滚动阅读完毕后「同意并继续使用」才可用；
/// 点击「不同意并退出」则直接关闭软件。设置页以 fromSettings=true 打开时可查看协议并修改同意状态。
/// </summary>
public sealed class AgreementDialog : Form
{
    private readonly RichTextBox _text = new();
    private readonly Theme.ScrollIndicator?[] _bars = new Theme.ScrollIndicator[2];
    private readonly Theme.GlassButton[] _tabButtons = new Theme.GlassButton[2];
    private readonly Label _lblRead = new();
    private readonly Theme.GlassButton _btnAgree = new();
    private readonly Theme.GlassButton _btnDecline = new();
    private readonly bool _fromSettings;
    private readonly bool[] _read = new bool[2];
    private int _active;
    private bool _allowClose;

    /// <summary>True = 用户点击了同意；False = 拒绝（required 模式下程序应退出）。</summary>
    public bool Accepted { get; private set; }

    public AgreementDialog(bool fromSettings)
    {
        _fromSettings = fromSettings;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        Size = new Size(620, 660);
        BackColor = Theme.FromHex("#1a1c18");
        TopMost = !fromSettings;

        var head = Theme.MakeEyebrow("FREE SOFTWARE NOTICE");
        head.Location = new Point(30, 22);
        Controls.Add(head);

        var title = Theme.MakeLabel(L.S("使用前请阅读并确认", "Please read and accept before use"), Theme.Text, 15f, bold: true);
        title.Location = new Point(30, 44);
        Controls.Add(title);

        var lead = Theme.MakeLabel(
            L.S($"本软件完全免费。作者：{AgreementContent.AuthorName}。协议修订号：{AgreementContent.Revision}。请依次完整阅读两份文件。",
                $"This software is free. Author: {AgreementContent.AuthorName}. Revision: {AgreementContent.Revision}. Read both documents fully."),
            Theme.TextMuted, 8.5f);
        lead.Location = new Point(30, 76);
        Controls.Add(lead);

        // 文档切换页签（✓ = 已读至末尾）
        for (int i = 0; i < AgreementContent.Documents.Length; i++)
        {
            int idx = i;
            var tab = new Theme.GlassButton
            {
                Text = AgreementContent.Documents[i].Title,
                Size = new Size(130, 34),
                Location = new Point(30 + i * 142, 106),
            };
            tab.Click += (_, _) => SwitchTab(idx);
            Controls.Add(tab);
            _tabButtons[i] = tab;
        }

        _text.ReadOnly = true;
        _text.BorderStyle = BorderStyle.None;
        _text.BackColor = Theme.SurfaceRaised;
        _text.ScrollBars = RichTextBoxScrollBars.None;
        _text.Font = new Font(Theme.FontUi, 9.25f);
        _text.Location = new Point(30, 152);
        _text.Size = new Size(530, 360);
        _text.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _text.ForeColor = Theme.TextSecondary;
        Controls.Add(_text);
        // 注意：指示条在 Shown 后、正文已填充时再挂载——构造期挂载会在空文本上
        // 触发“已到末尾”，导致两份文件被误判为已读（同意按钮提前解锁）
        _bars[0] = null;

        _lblRead.AutoSize = true;
        _lblRead.Location = new Point(30, 528);
        _lblRead.ForeColor = Theme.TextMuted;
        _lblRead.Font = new Font(Theme.FontUi, 8.5f);
        Controls.Add(_lblRead);

        _btnDecline.Text = L.S("不同意并退出", "Decline && Exit");
        _btnDecline.Size = new Size(170, 42);
        _btnDecline.Location = new Point(30, 570);
        _btnDecline.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _btnDecline.Click += (_, _) =>
        {
            Accepted = false;
            _allowClose = true;
            DialogResult = DialogResult.Cancel;
            Close();
        };
        Controls.Add(_btnDecline);

        _btnAgree.Primary = true;
        _btnAgree.Size = new Size(240, 42);
        _btnAgree.Location = new Point(620 - 240 - 30, 570);
        _btnAgree.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _btnAgree.Enabled = false;
        _btnAgree.Click += (_, _) =>
        {
            AppConfig.AgreedRevision = AgreementContent.Revision;
            AppConfig.AgreedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            AppConfig.Save();
            Accepted = true;
            _allowClose = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(_btnAgree);

        Shown += (_, _) =>
        {
            Theme.ApplyWindowChrome(this);
            SwitchTab(0);   // 先填充正文
            _bars[0] = Theme.AttachScrollIndicator(_text, this, rightInset: 30, topInset: 152, height: 360);
            _bars[0].ReachedBottom += () => MarkRead(_active);   // 当前页签滚动到底 → 已读
            UpdateButtons();
        };
        Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.GlassBorder);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        // 阻止 ALT+F4 / 系统关闭（必须点同意或不同意）
        FormClosing += (_, e) =>
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
        };

        // 高 DPI：与 UpdateDialog/LanguageDialog 相同的 96-DPI 设计 + 启动时整体缩放
        float dpi;
        using (var g = CreateGraphics()) dpi = g.DpiX / 96f;
        if (dpi > 1.01f)
        {
            Scale(new SizeF(dpi, dpi));
            ClientSize = new Size((int)(620 * dpi), (int)(660 * dpi));
        }
    }

    private void SwitchTab(int idx)
    {
        _active = idx;
        for (int i = 0; i < _tabButtons.Length; i++)
            _tabButtons[i].Text = (_read[i] ? "✓ " : "") + AgreementContent.Documents[i].Title;

        var bar = _bars[0];
        bar?.ResetReadState();
        _text.Text = AgreementContent.Documents[idx].Body;
        _text.SelectionStart = 0;
        _text.ScrollToCaret();
        UpdateButtons();
    }

    private void MarkRead(int idx)
    {
        if (idx != _active) return;   // 只认当前展示的文档
        if (_read[idx]) return;
        var bar = _bars[0];
        if (bar == null || !bar.HasUserScrolled) return;   // 必须有真实滚动行为（防切页瞬间误判）
        if (_text.TextLength < 400) return;   // 协议正文都很长：过短说明内容尚未就绪，忽略
        _read[idx] = true;
        _tabButtons[idx].Text = "✓ " + AgreementContent.Documents[idx].Title;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool allRead = _read[0] && _read[1];
        bool agreedNow = AppConfig.AgreedRevision == AgreementContent.Revision;

        if (_fromSettings)
        {
            Text = L.S("协议与声明", "Agreements");
            _btnDecline.Text = L.S("关闭", "Close");   // 设置页打开时仅查看/确认，不做“退出软件”
            _allowClose = true;
            if (agreedNow)
            {
                _btnAgree.Visible = false;
                _btnDecline.Location = new Point(ClientSize.Width - _btnDecline.Width - 30, ClientSize.Height - _btnDecline.Height - 30);
                _lblRead.Text = L.S($"当前状态：已同意（修订 {AgreementContent.Revision}，{AppConfig.AgreedAt}）",
                                    $"Current status: accepted (revision {AgreementContent.Revision}, {AppConfig.AgreedAt})");
            }
            else
            {
                _btnAgree.Text = L.S("同意并继续使用", "Agree && Continue");
                _btnAgree.Enabled = allRead;
                _lblRead.Text = allRead
                    ? L.S("已完整阅读两份文件。请点击「同意并继续使用」。", "Both documents fully read. Click \"Agree && Continue\".")
                    : L.S("请依次滚动阅读两份文件至末尾。", "Scroll through both documents to the end.");
            }
        }
        else
        {
            int done = _read.Count(r => r);
            _lblRead.Text = allRead
                ? L.S("已完整阅读两份文件。请点击「同意并继续使用」。", "Both documents fully read. Click \"Agree && Continue\".")
                : L.S($"请依次滚动阅读两份文件至末尾（已完成 {done}/2）。", $"Scroll through both documents to the end ({done}/2 done).");
            _btnAgree.Enabled = allRead;
        }
    }
}
