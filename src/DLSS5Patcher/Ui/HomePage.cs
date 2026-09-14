using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 首页（GSX 2.0 patch-card 风格）：页头 + GPU 验证条 + 三张玻璃游戏卡片 + 运行日志。
/// 卡片底部为独立操作条（安装 primary / 卸载 secondary + 4px 细进度轨）。
/// 业务逻辑在 MainForm，本控件只负责展示与转发点击事件。
/// </summary>
public sealed class HomePage : Theme.AmbientPage
{
    public event Action? RefreshRequested;
    public event Action? MsfsInstallRequested;
    public event Action? MsfsUninstallRequested;
    public event Action? Msfs2020InstallRequested;
    public event Action? Msfs2020UninstallRequested;
    public event Action? XpInstallRequested;
    public event Action? XpUninstallRequested;
    /// <summary>用户拨动 MSFS 卡片上的神经渲染开关（参数：卡片序号，true=开启）。</summary>
    public event Action<int, bool>? NrToggleRequested;

    public const int CardMsfs2024 = 0;
    public const int CardMsfs2020 = 1;
    public const int CardXp12 = 2;
    public const int CardXp11 = 3;

    private const int PadX = 36;
    private const int ContentW = 790;   // 862 - 36×2
    private const int CardH = 132;
    private const int CardGap = 10;

    private readonly Label _lblGpu = new();
    private readonly Label _lblChecks = new();
    private readonly Label[] _cardState = new Label[4];
    private readonly Label[] _cardPath = new Label[4];
    private readonly Theme.GlassButton[] _btnInstall = new Theme.GlassButton[4];
    private readonly Theme.GlassButton[] _btnUninstall = new Theme.GlassButton[4];
    private readonly List<Theme.GlassButton> _allButtons = new();
    private readonly Panel[] _progressTrack = new Panel[4];
    private readonly Label[] _progressFill = new Label[4];
    private readonly Label[] _progressLabels = new Label[4];
    private readonly Theme.GlassSwitch[] _nrSwitch = new Theme.GlassSwitch[4];
    private readonly bool[] _nrDesiredVisible = new bool[4];
    private bool _busy;
    private Theme.GlassButton _btnRefresh = new();

    public HomePage()
    {
        Size = new Size(862, 800);

        Controls.Add(Theme.MakePageHeader(L.S("ONE-CLICK INSTALL", "ONE-CLICK INSTALL"), L.S("一键安装", "One-Click Install")));

        _btnRefresh = Theme.MakeButton(L.S("重新检测", "Re-detect"), height: 36);
        _btnRefresh.Size = new Size(110, 36);
        _btnRefresh.Location = new Point(PadX + ContentW - 110, 34);
        _btnRefresh.Click += (_, _) => RefreshRequested?.Invoke();
        _allButtons.Add(_btnRefresh);
        Controls.Add(_btnRefresh);

        // GPU 验证条（verification-bar）
        var gpuBar = new Theme.GlassCard { Size = new Size(ContentW, 46), Location = new Point(PadX, 104) };
        var gpuGlyph = Theme.MakeLabel("✔", Theme.Signal, 11f, bold: true);
        gpuGlyph.Location = new Point(16, 13);
        _lblGpu.AutoSize = false;
        _lblGpu.Location = new Point(40, 13);
        _lblGpu.Size = new Size(ContentW - 56, 20);
        _lblGpu.Font = new Font(Theme.FontUi, 9.5f);
        _lblGpu.ForeColor = Theme.TextSecondary;
        _lblGpu.AutoEllipsis = true;
        gpuBar.Controls.Add(gpuGlyph);
        gpuBar.Controls.Add(_lblGpu);
        Controls.Add(gpuBar);
        _gpuBar = gpuBar;

        _lblChecks.AutoSize = false;
        _lblChecks.Location = new Point(PadX + 4, 160);
        _lblChecks.Size = new Size(ContentW - 8, 56);
        _lblChecks.ForeColor = Theme.TextMuted;
        _lblChecks.Font = new Font(Theme.FontUi, 8.5f);
        Controls.Add(_lblChecks);

        int cardY = 226;
        BuildCard(CardMsfs2024, L.S("微软模拟飞行 2024", "Microsoft Flight Simulator 2024"),
            L.S("OptiScaler · DLSS Unlocked", "OptiScaler · DLSS Unlocked"), "muted", cardY);
        BuildCard(CardMsfs2020, L.S("微软模拟飞行 2020", "Microsoft Flight Simulator 2020"),
            L.S("Beta · 实验性", "BETA · Experimental"), "warning", cardY + (CardH + CardGap));
        BuildCard(CardXp12, "X-Plane 12",
            L.S("DLSS5-Feeder · Vulkan", "DLSS5-Feeder · Vulkan"), "muted", cardY + (CardH + CardGap) * 2);
        BuildCard(CardXp11, "X-Plane 11",
            L.S("开发中", "In development"), "muted", cardY + (CardH + CardGap) * 3, dev: true);
    }

    private readonly Theme.GlassCard _gpuBar;

    private void BuildCard(int idx, string title, string tag, string tone, int y, bool dev = false)
    {
        var card = new Theme.GlassCard { Size = new Size(ContentW, CardH), Location = new Point(PadX, y) };

        var lblTitle = Theme.MakeLabel(title, Theme.Text, 11.5f, bold: true);
        lblTitle.Location = new Point(20, 14);
        card.Controls.Add(lblTitle);

        var badge = Theme.MakeBadge(tag, tone);
        card.Controls.Add(badge);
        badge.Location = new Point(ContentW - badge.Width - 20, 15);   // 加入后测量宽度再右对齐

        var lblState = new Label
        {
            AutoSize = false,
            Size = new Size(ContentW - 40, 20),
            Location = new Point(20, 44),
            ForeColor = Theme.TextMuted,
            Font = new Font(Theme.FontUi, 9f, FontStyle.Bold),
            AutoEllipsis = true,
            BackColor = Color.Transparent,
        };
        _cardState[idx] = lblState;
        if (dev)
        {
            lblState.Text = L.S("开发中 —— 敬请期待", "In development — stay tuned");
            lblState.ForeColor = Theme.TextMuted;
            lblState.Font = new Font(Theme.FontUi, 9f);
        }
        card.Controls.Add(lblState);

        var lblPath = new Label
        {
            AutoSize = false,
            Size = new Size(ContentW - 40, 18),
            Location = new Point(20, 68),
            ForeColor = Theme.TextMuted,
            Font = new Font("Consolas", 8.25f),
            AutoEllipsis = true,
            BackColor = Color.Transparent,
        };
        _cardPath[idx] = lblPath;
        card.Controls.Add(lblPath);

        // 底部操作条：细分隔线 + 微亮底 + 按钮 + 进度轨
        var strip = new Panel
        {
            Size = new Size(ContentW - 2, 42),
            Location = new Point(1, CardH - 42 - 1),
            BackColor = Color.Transparent,
        };
        strip.Paint += (_, e) =>
        {
            using var bg = new SolidBrush(Color.FromArgb(6, 255, 255, 255));
            e.Graphics.FillRectangle(bg, 0, 0, strip.Width, strip.Height);
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, strip.Width, 0);
        };
        card.Controls.Add(strip);

        // 4px 细进度轨（GSX operation-progress）+ 进度文字
        var track = new Panel
        {
            Size = new Size(260, 4),
            Location = new Point(20, 19),
            BackColor = Theme.FromHex("#353630"),
            Visible = false,
        };
        var fill = new Label
        {
            Size = new Size(0, 4),
            Location = new Point(0, 0),
            BackColor = Theme.Signal,
        };
        track.Controls.Add(fill);
        _progressTrack[idx] = track;
        _progressFill[idx] = fill;
        strip.Controls.Add(track);

        var lblTrack = new Label
        {
            Size = new Size(220, 18),
            Location = new Point(20, 1),
            ForeColor = Theme.TextMuted,
            Font = new Font(Theme.FontUi, 8f),
            BackColor = Color.Transparent,
            Visible = false,
        };
        _progressLabels[idx] = lblTrack;
        strip.Controls.Add(lblTrack);

        if (dev)
        {
            var btnDev = Theme.MakeButton(L.S("开发中", "In development"), height: 30);
            btnDev.Size = new Size(124, 30);
            btnDev.Location = new Point(ContentW - 124 - 16, 6);
            btnDev.Enabled = false;
            _allButtons.Add(btnDev);
            strip.Controls.Add(btnDev);
            Controls.Add(card);
            return;
        }

        var btnInstall = Theme.MakeButton(L.S("一键安装", "Install"), primary: true, height: 30);
        btnInstall.Size = new Size(124, 30);
        btnInstall.Location = new Point(ContentW - 124 - 110 - 12 - 16, 6);
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
        _allButtons.Add(btnInstall);
        strip.Controls.Add(btnInstall);

        var btnUninstall = Theme.MakeButton(L.S("一键卸载", "Uninstall"), height: 30);
        btnUninstall.Size = new Size(110, 30);
        btnUninstall.Location = new Point(ContentW - 110 - 16, 6);
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
        _allButtons.Add(btnUninstall);
        strip.Controls.Add(btnUninstall);

        // OptiScaler 路线（MSFS 2024 / 2020）：条内左侧放「神经渲染」开关，
        // 直接读写游戏目录 OptiScaler.ini 的 [DlssNr] Enabled（爆显存死机时的自救开关）
        if (idx == CardMsfs2024 || idx == CardMsfs2020)
        {
            var sw = new Theme.GlassSwitch
            {
                Text = L.S("神经渲染", "Neural Render"),
                Size = new Size(132, 22),
                Location = new Point(20, 10),
                ForeColor = Theme.TextSecondary,
            };
            sw.CheckedChanged += (_, _) => NrToggleRequested?.Invoke(idx, sw.Checked);
            _nrSwitch[idx] = sw;
            strip.Controls.Add(sw);
        }

        Controls.Add(card);
    }

    /// <summary>卡片状态行的建议配色：已安装=信号绿，未安装=灰，其余=警示黄（中英文均判断）。</summary>
    public static Color StateColor(string state) =>
        state.StartsWith("已安装") || state.StartsWith("Installed") ? Theme.Signal
        : state.StartsWith("未安装") || state.StartsWith("Not installed")
            || state.StartsWith("未检测到") || state.StartsWith("Not detected") ? Theme.TextMuted
        : Theme.Warning;

    public void SetGpu(string text, bool ok)
    {
        _lblGpu.Text = text;
        _lblGpu.ForeColor = ok ? Theme.TextSecondary : Theme.Warning;
    }

    public void SetChecks(string text, bool ok)
    {
        _lblChecks.Text = text;
        _lblChecks.ForeColor = ok ? Theme.TextMuted : Theme.Warning;
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

    /// <summary>更新神经渲染开关（visible=false 表示未安装/无法读取，开关隐藏）；不触发用户切换事件。</summary>
    public void SetNrSwitch(int idx, bool visible, bool on)
    {
        var sw = _nrSwitch[idx];
        if (sw == null) return;
        _nrDesiredVisible[idx] = visible;
        sw.SetCheckedSilent(on);
        sw.Visible = visible && !_busy;
    }

    /// <summary>忙碌时禁用全部按钮并显示状态；结束时恢复「重新检测」。</summary>
    public void SetBusy(bool busy, string? status = null)
    {
        if (InvokeRequired) { BeginInvoke(() => SetBusy(busy, status)); return; }
        _busy = busy;
        for (int i = 0; i < 4; i++)
        {
            if (_nrSwitch[i] != null)
                _nrSwitch[i].Visible = !busy && _nrDesiredVisible[i];   // 忙时隐藏：进度轨会覆盖此处
        }
        if (busy)
        {
            foreach (var b in _allButtons)
            {
                b.Tag = b.Enabled;
                b.Enabled = false;
            }
        }
        else
        {
            _btnRefresh.Enabled = true;
            foreach (var track in _progressTrack) track.Visible = false;
            foreach (var lbl in _progressLabels) lbl.Visible = false;
        }
    }

    public void SetProgress(long received, long total)
    {
        if (InvokeRequired) { BeginInvoke(() => SetProgress(received, total)); return; }
        var pct = (int)Math.Clamp(received * 100 / Math.Max(total, 1), 0, 100);
        var text = L.S($"安装进度: {received / 1048576} / {total / 1048576} MB",
                       $"Progress: {received / 1048576} / {total / 1048576} MB");
        for (int i = 0; i < 4; i++)
        {
            _progressTrack[i].Visible = true;
            _progressLabels[i].Visible = true;
            _progressFill[i].Width = _progressTrack[i].Width * pct / 100;
            _progressLabels[i].Text = text;
        }
    }
}
