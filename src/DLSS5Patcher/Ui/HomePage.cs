using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 首页：GPU 状态头 + 三张游戏卡片（MSFS 2024 / MSFS 2020 Beta / XP12）+ 进度与日志。
/// 卡片只保留「一键安装」「卸载」两个按钮；手动指定目录 / 选择组件包在设置页「手动配置」。
/// 业务逻辑在 MainForm，本控件只负责展示与转发点击事件。
/// </summary>
public sealed class HomePage : UserControl
{
    public event Action? RefreshRequested;
    public event Action? MsfsInstallRequested;
    public event Action? MsfsUninstallRequested;
    public event Action? Msfs2020InstallRequested;
    public event Action? Msfs2020UninstallRequested;
    public event Action? XpInstallRequested;
    public event Action? XpUninstallRequested;

    public const int CardMsfs2024 = 0;
    public const int CardMsfs2020 = 1;
    public const int CardXp12 = 2;

    private const int ContentW = 832;   // 主区宽 880 - 左右留白 24×2
    private const int CardH = 132;

    private readonly Label _lblGpu = new();
    private readonly Label _lblChecks = new();
    private readonly Label[] _cardState = new Label[3];
    private readonly Label[] _cardPath = new Label[3];
    private readonly Button[] _btnInstall = new Button[3];
    private readonly Button[] _btnUninstall = new Button[3];
    private Button _btnRefresh = new();
    private readonly List<Button> _allButtons = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _lblProgress = new();
    private readonly RichTextBox _log = new();

    public HomePage()
    {
        BackColor = Theme.Bg;
        Size = new Size(880, 800);

        Controls.Add(Theme.MakePageTitle(L.S("一键安装", "One-Click Install"), 24, 16));

        _lblGpu.AutoSize = false;
        _lblGpu.Location = new Point(24, 52);
        _lblGpu.Size = new Size(ContentW, 22);
        _lblGpu.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        _lblGpu.ForeColor = Theme.TextSecondary;
        Controls.Add(_lblGpu);

        _lblChecks.AutoSize = false;
        _lblChecks.Location = new Point(24, 78);
        _lblChecks.Size = new Size(ContentW, 56);   // 3 行摘要，高 DPI 缩放后也放得下
        _lblChecks.ForeColor = Theme.TextMuted;
        Controls.Add(_lblChecks);

        BuildCard(CardMsfs2024, L.S("微软模拟飞行 2024", "Microsoft Flight Simulator 2024"), L.S("OptiScaler · DLSS Unlocked 路线", "OptiScaler · DLSS Unlocked route"), 140);
        BuildCard(CardMsfs2020, L.S("微软模拟飞行 2020", "Microsoft Flight Simulator 2020"), L.S("Beta · 实验性支持（流程同 2024，未实测）", "Beta · experimental (same flow as 2024, untested)"), 282);
        BuildCard(CardXp12, "X-Plane 12", L.S("DLSS5-Feeder · Vulkan 路线", "DLSS5-Feeder · Vulkan route"), 424);

        _progress.Location = new Point(24, 568);
        _progress.Size = new Size(ContentW, 14);
        _progress.Visible = false;
        Controls.Add(_progress);

        _lblProgress.AutoSize = true;
        _lblProgress.Location = new Point(24, 586);
        _lblProgress.ForeColor = Theme.TextSecondary;
        _lblProgress.Visible = false;
        Controls.Add(_lblProgress);

        var logPanel = new Panel { Location = new Point(24, 612), Size = new Size(ContentW, 178), BackColor = Theme.Surface, Padding = new Padding(6) };
        Theme.EnableBorder(logPanel, Theme.Border);
        _log.Dock = DockStyle.Fill;
        _log.BackColor = Theme.Surface;
        _log.ForeColor = Theme.TextSecondary;
        _log.BorderStyle = BorderStyle.None;
        _log.ReadOnly = true;
        _log.Font = new Font("Consolas", 9f);
        _log.ScrollBars = RichTextBoxScrollBars.Vertical;
        logPanel.Controls.Add(_log);
        Controls.Add(logPanel);

        var btnRefresh = Theme.MakeButton(L.S("重新检测", "Re-detect"));
        btnRefresh.Size = new Size(104, 32);
        btnRefresh.Location = new Point(880 - 24 - 104, 16); // 页头右上角
        btnRefresh.Click += (_, _) => RefreshRequested?.Invoke();
        _btnRefresh = btnRefresh;
        _allButtons.Add(btnRefresh);
        Controls.Add(btnRefresh);
    }

    private void BuildCard(int idx, string title, string tag, int y)
    {
        var card = Theme.MakeCard(ContentW, CardH);
        card.Location = new Point(24, y);

        var lblTitle = Theme.MakeLabel(title, Theme.Text, 10f, bold: true);
        lblTitle.Location = new Point(16, 12);
        card.Controls.Add(lblTitle);

        var lblTag = new Label
        {
            Text = tag,
            AutoSize = false,
            Size = new Size(380, 20),
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = idx == CardMsfs2020 ? Theme.Warning : Theme.TextMuted,
            Font = new Font("Microsoft YaHei UI", 8f),
        };
        lblTag.Location = new Point(ContentW - 380 - 16, 14);
        card.Controls.Add(lblTag);

        var lblState = new Label
        {
            AutoSize = false,
            Size = new Size(ContentW - 32, 20),
            Location = new Point(16, 40),
            ForeColor = Theme.TextMuted,
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
            AutoEllipsis = true,
        };
        _cardState[idx] = lblState;
        card.Controls.Add(lblState);

        var lblPath = new Label
        {
            AutoSize = false,
            Size = new Size(ContentW - 32, 18),
            Location = new Point(16, 64),
            ForeColor = Theme.TextMuted,
            Font = new Font("Microsoft YaHei UI", 8.25f),
            AutoEllipsis = true,
        };
        _cardPath[idx] = lblPath;
        card.Controls.Add(lblPath);

        var btnInstall = Theme.MakeButton(L.S("一键安装", "Install"), primary: true);
        btnInstall.Size = new Size(110, 32);
        btnInstall.Location = new Point(16, 90);
        btnInstall.Click += (_, _) =>
        {
            switch (idx)
            {
                case CardMsfs2024: MsfsInstallRequested?.Invoke(); break;
                case CardMsfs2020: Msfs2020InstallRequested?.Invoke(); break;
                case CardXp12: XpInstallRequested?.Invoke(); break;
            }
        };
        _btnInstall[idx] = btnInstall;
        card.Controls.Add(btnInstall);

        var btnUninstall = Theme.MakeButton(L.S("一键卸载", "Uninstall"));
        btnUninstall.Size = new Size(110, 32);
        btnUninstall.Location = new Point(134, 90);
        btnUninstall.Click += (_, _) =>
        {
            switch (idx)
            {
                case CardMsfs2024: MsfsUninstallRequested?.Invoke(); break;
                case CardMsfs2020: Msfs2020UninstallRequested?.Invoke(); break;
                case CardXp12: XpUninstallRequested?.Invoke(); break;
            }
        };
        _btnUninstall[idx] = btnUninstall;
        card.Controls.Add(btnUninstall);

        _allButtons.Add(btnInstall);
        _allButtons.Add(btnUninstall);

        Controls.Add(card);
    }

    /// <summary>卡片状态行的建议配色：已安装=主题绿，未安装=灰，其余=警示黄（中英文均判断）。</summary>
    public static Color StateColor(string state) =>
        state.StartsWith("已安装") || state.StartsWith("Installed") ? Theme.Accent
        : state.StartsWith("未安装") || state.StartsWith("Not installed")
            || state.StartsWith("未检测到") || state.StartsWith("Not detected") ? Theme.TextMuted
        : Theme.Warning;

    public void SetGpu(string text, bool ok)
    {
        _lblGpu.Text = text;
        _lblGpu.ForeColor = ok ? Theme.Accent : Theme.Danger;
    }

    public void SetChecks(string text, bool ok)
    {
        _lblChecks.Text = text;
        _lblChecks.ForeColor = ok ? Theme.TextSecondary : Theme.Warning;
    }

    /// <summary>更新卡片内容与按钮真实可用性（检测完成后调用）。</summary>
    public void SetCard(int idx, string state, string path, bool canInstall, bool canUninstall)
    {
        _cardState[idx].Text = L.S("状态：", "Status: ") + state;
        _cardState[idx].ForeColor = StateColor(state);
        _cardPath[idx].Text = string.IsNullOrEmpty(path) ? L.S("未检测到安装。", "No installation detected.") : path;
        _btnInstall[idx].Enabled = canInstall;
        _btnUninstall[idx].Enabled = canUninstall;
    }

    /// <summary>忙碌时禁用全部按钮并显示进度；结束时隐藏进度条并恢复「重新检测」。</summary>
    public void SetBusy(bool busy, string? status = null)
    {
        if (busy)
        {
            foreach (var b in _allButtons)
            {
                b.Tag = b.Enabled;
                b.Enabled = false;
            }
            _progress.Visible = true;
            _lblProgress.Visible = true;
            if (status != null) _lblProgress.Text = status;
        }
        else
        {
            _progress.Visible = false;
            _lblProgress.Visible = false;
            _btnRefresh.Enabled = true;
        }
    }

    public void SetProgress(long received, long total)
    {
        _progress.Maximum = 100;
        _progress.Value = (int)Math.Clamp(received * 100 / Math.Max(total, 1), 0, 100);
        _lblProgress.Text = L.S($"安装进度: {received / 1024 / 1024} / {total / 1024 / 1024} MB",
                                $"Progress: {received / 1024 / 1024} / {total / 1024 / 1024} MB");
    }

    public void Log(string s)
    {
        if (InvokeRequired) { BeginInvoke(() => Log(s)); return; }
        _log.SelectionStart = _log.TextLength;
        _log.SelectionColor = Theme.TextSecondary;
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {s}\r\n");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }
}
