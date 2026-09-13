using System.Reflection;
using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>设置 / 关于页（GSX 2.0 settings 风格）：语言+安装选项带、2×2 手动配置目标卡、声明、粉丝群、关于。</summary>
public sealed class AboutPage : Theme.AmbientPage
{
    public const string QqGroup = "615523002";
    public const string QqJoinUrl = "https://qun.qq.com/join.html?gc=" + QqGroup;

    private const int PadX = 36;
    private const int ContentW = 790;

    private readonly ComboBox _cboScale = new();
    private readonly ComboBox _cboLang = new();
    private readonly Label _lblMsfsDir = new();
    private readonly Label _lblMsfs2020Dir = new();
    private readonly Label _lblXp12Dir = new();
    private readonly Label _lblKitDir = new();
    private bool _langReady;   // 防止初始 SelectedIndex 触发切换逻辑

    public event Action? MsfsBrowseRequested;
    public event Action? Msfs2020BrowseRequested;
    public event Action? XpBrowseRequested;
    public event Action? XpPickKitRequested;
    public event Action? CheckUpdateRequested;

    /// <summary>所选 WorkingScale（MSFS 安装时生效）。</summary>
    public string WorkingScale => _cboScale.SelectedItem?.ToString() ?? "0.5";

    public AboutPage()
    {
        Size = new Size(862, 800);

        Controls.Add(Theme.MakePageHeader(L.S("SETTINGS · ABOUT", "SETTINGS · ABOUT"), L.S("设置 / 关于", "Settings / About")));

        BuildLanguageBand(100);
        BuildManualSetup(218);
        BuildDeclarationBand(492);
        BuildQqCard(572);
        BuildAboutBand(680);
    }

    // ───────────────────────────── 语言 + 安装选项 ─────────────────────────────

    private void BuildLanguageBand(int y)
    {
        var card = Theme.MakeCard(ContentW, 108);
        card.Location = new Point(PadX, y);

        var head = Theme.MakeLabel(L.S("语言 / Language", "Language / 语言"), Theme.Text, 9.75f, bold: true);
        head.Location = new Point(20, 12);
        card.Controls.Add(head);

        var lblLang = Theme.MakeLabel(L.S("切换语言后需重启应用生效：", "Restart the app after switching to apply:"), Theme.TextSecondary, 9f);
        lblLang.Location = new Point(20, 40);
        card.Controls.Add(lblLang);

        _cboLang.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboLang.Items.AddRange(new object[] { "简体中文", "English" });
        _cboLang.SelectedIndex = L.English ? 1 : 0;
        _cboLang.Location = new Point(258, 37);
        _cboLang.Size = new Size(132, 26);
        _cboLang.BackColor = Theme.SurfaceRaised;
        _cboLang.ForeColor = Theme.Text;
        _cboLang.SelectedIndexChanged += (_, _) =>
        {
            if (!_langReady) return;
            var newLang = _cboLang.SelectedIndex == 1 ? "en" : "zh";
            if (newLang == AppConfig.Lang) return;
            AppConfig.Lang = newLang;
            AppConfig.Save();
            if (MessageBox.Show(this,
                    L.S("语言已更改。立即重启应用以生效吗？", "Language changed. Restart the app now to apply it?"),
                    L.S("重启提示", "Restart required"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Application.Restart();
            }
        };
        card.Controls.Add(_cboLang);
        _langReady = true;

        var lblScale = Theme.MakeLabel(
            L.S("模型分辨率 WorkingScale：", "Model resolution WorkingScale:"),
            Theme.TextSecondary, 9f);
        lblScale.Location = new Point(430, 40);
        card.Controls.Add(lblScale);

        _cboScale.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboScale.Items.AddRange(new object[] { "0.35", "0.5", "0.75", "1.0" });
        _cboScale.SelectedIndex = 1;
        _cboScale.Location = new Point(ContentW - 16 - 80, 37);
        _cboScale.Size = new Size(80, 26);
        _cboScale.BackColor = Theme.SurfaceRaised;
        _cboScale.ForeColor = Theme.Text;
        card.Controls.Add(_cboScale);

        var hint = Theme.MakeLabel(
            L.S("安装选项在点击各游戏卡片「一键安装」时生效。", "Install options apply when you click Install on a game card."),
            Theme.TextMuted, 8.25f);
        hint.Location = new Point(20, 78);
        card.Controls.Add(hint);

        Controls.Add(card);
    }

    // ───────────────────────────── 手动配置（2×2 目标卡片） ─────────────────────────────

    private void BuildManualSetup(int y)
    {
        var heading = Theme.MakeLabel(
            L.S("手动配置（检测不到游戏时使用，正常情况无需更改）", "Manual setup (only needed when auto-detection fails)"),
            Theme.Text, 10.5f, bold: true);
        heading.Location = new Point(PadX, y);
        Controls.Add(heading);

        int gx = PadX, gy = y + 34;
        int cw = (ContentW - 10) / 2, ch = 116;

        BuildTargetCard(gx, gy, cw, ch,
            "MSFS 2024", L.S("游戏主程序 FlightSimulator2024.exe", "Executable FlightSimulator2024.exe"),
            _lblMsfsDir, L.S("指定程序...", "Pick exe..."), () => MsfsBrowseRequested?.Invoke());
        BuildTargetCard(gx + cw + 10, gy, cw, ch,
            "MSFS 2020 · BETA", L.S("游戏主程序 FlightSimulator.exe", "Executable FlightSimulator.exe"),
            _lblMsfs2020Dir, L.S("指定程序...", "Pick exe..."), () => Msfs2020BrowseRequested?.Invoke());
        BuildTargetCard(gx, gy + ch + 10, cw, ch,
            "X-Plane 12", L.S("游戏主程序 X-Plane.exe", "Executable X-Plane.exe"),
            _lblXp12Dir, L.S("指定程序...", "Pick exe..."), () => XpBrowseRequested?.Invoke());
        BuildTargetCard(gx + cw + 10, gy + ch + 10, cw, ch,
            L.S("XP12 组件包（可选）", "XP12 kit (optional)"), L.S("留空 = 自动下载官方组件包", "empty = auto-download the official kit"),
            _lblKitDir, L.S("指定目录...", "Browse..."), () => XpPickKitRequested?.Invoke());
    }

    private void BuildTargetCard(int x, int y, int w, int h, string title, string hint, Label valueLabel, string buttonText, Action browse)
    {
        var card = Theme.MakeCard(w, h);
        card.Location = new Point(x, y);

        var head = Theme.MakeLabel(title, Theme.Text, 9.75f, bold: true);
        head.Location = new Point(14, 10);
        card.Controls.Add(head);

        var sub = Theme.MakeLabel(hint, Theme.TextMuted, 8f);
        sub.Location = new Point(14, 30);
        card.Controls.Add(sub);

        valueLabel.AutoSize = false;
        valueLabel.Size = new Size(w - 28, 30);
        valueLabel.Location = new Point(14, 52);
        valueLabel.ForeColor = Theme.TextSecondary;
        valueLabel.Font = new Font("Consolas", 8.25f);
        valueLabel.AutoEllipsis = true;
        card.Controls.Add(valueLabel);

        var btn = Theme.MakeButton(buttonText, height: 30);
        btn.Size = new Size(108, 30);
        btn.Location = new Point(w - 108 - 12, h - 30 - 10);
        btn.Click += (_, _) => browse();
        card.Controls.Add(btn);

        Controls.Add(card);
    }

    /// <summary>刷新手动配置卡的路径显示（检测完成后由 MainForm 调用）。</summary>
    public void SetManualPaths(string msfsDirDisplay, string msfs2020DirDisplay, string xp12DirDisplay, string kitDirDisplay)
    {
        _lblMsfsDir.Text = msfsDirDisplay;
        _lblMsfs2020Dir.Text = msfs2020DirDisplay;
        _lblXp12Dir.Text = xp12DirDisplay;
        _lblKitDir.Text = kitDirDisplay;
    }

    // ───────────────────────────── 声明 ─────────────────────────────

    private void BuildDeclarationBand(int y)
    {
        var card = new Panel
        {
            Size = new Size(ContentW, 68),
            Location = new Point(PadX, y),
            BackColor = Color.Transparent,
        };
        card.Paint += (_, e) =>
        {
            using var bg = new SolidBrush(Theme.WarningBg);
            e.Graphics.FillRectangle(bg, 0, 0, card.Width, card.Height);
            using var bar = new Pen(Theme.Warning, 3f);
            e.Graphics.DrawLine(bar, 0, 0, 0, card.Height);
        };

        var head = Theme.MakeLabel(L.S("资源来源于网络，完全免费，禁止倒卖。", "All resources come from the internet. Completely free — reselling is prohibited."),
            Theme.Warning, 9.75f, bold: true);
        head.Location = new Point(16, 10);
        card.Controls.Add(head);

        var sub = Theme.MakeLabel(
            L.S("仅供个人学习交流使用，请支持正版游戏；若你为此工具付费，请立即退款并举报。",
                "For personal study only. Support official releases; if you paid for this tool, refund immediately and report the seller."),
            Theme.FromHex("#e9ce95"), 8.25f);
        sub.Location = new Point(16, 34);
        card.Controls.Add(sub);

        Controls.Add(card);
    }

    // ───────────────────────────── 粉丝群 ─────────────────────────────

    private void BuildQqCard(int y)
    {
        var card = Theme.MakeCard(ContentW, 96);
        card.Location = new Point(PadX, y);
        card.Fill = Theme.FromHex("#1a241f");
        card.BorderColor = Theme.SignalBorder;

        var avatar = new Label
        {
            Text = "QQ",
            Size = new Size(52, 52),
            Location = new Point(18, 22),
            BackColor = Theme.Signal,
            ForeColor = Theme.FromHex("#102019"),
            Font = new Font(Theme.FontBrand, 14f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        card.Controls.Add(avatar);

        var title = Theme.MakeLabel(
            L.S("B站 一只剑齿虎呀 粉丝群", "Bilibili '一只剑齿虎呀' Fan Group"),
            Theme.Text, 10.5f, bold: true);
        title.Location = new Point(86, 16);
        card.Controls.Add(title);

        var sub = Theme.MakeLabel(
            L.S($"QQ 群：{QqGroup}（获取更新、反馈问题、安装求助）", $"QQ group: {QqGroup} (updates, feedback and install help)"),
            Theme.TextSecondary, 9f);
        sub.Location = new Point(86, 40);
        card.Controls.Add(sub);

        var copy = Theme.MakeLabel(
            L.S("点击右侧按钮加入；也可复制群号后在 QQ 中搜索加群。", "Click the button on the right to join, or search the group ID in QQ."),
            Theme.TextMuted, 8.25f);
        copy.Location = new Point(86, 62);
        card.Controls.Add(copy);

        var btnJoin = Theme.MakeButton(L.S("加入 QQ 群", "Join QQ Group"), primary: true, height: 34);
        btnJoin.Size = new Size(132, 34);
        btnJoin.Location = new Point(ContentW - 132 - 116 - 14, 31);
        btnJoin.Click += (_, _) => OpenUrl(QqJoinUrl);
        card.Controls.Add(btnJoin);

        var btnCopy = Theme.MakeButton(L.S("复制群号", "Copy ID"), height: 34);
        btnCopy.Size = new Size(104, 34);
        btnCopy.Location = new Point(ContentW - 104 - 14, 31);
        btnCopy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(QqGroup);
                btnCopy.Text = L.S("已复制 ✓", "Copied ✓");
                var t = new System.Windows.Forms.Timer { Interval = 1500 };
                t.Tick += (_, _) => { btnCopy.Text = L.S("复制群号", "Copy ID"); t.Stop(); t.Dispose(); };
                t.Start();
            }
            catch { /* 剪贴板被占用时忽略 */ }
        };
        card.Controls.Add(btnCopy);

        Controls.Add(card);
    }

    // ───────────────────────────── 关于 ─────────────────────────────

    private void BuildAboutBand(int y)
    {
        var card = Theme.MakeCard(ContentW, 96);
        card.Location = new Point(PadX, y);

        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
        var head = Theme.MakeLabel($"DLSS5Patcher  v{ver.Major}.{ver.Minor}.{ver.Build}", Theme.Text, 11f, bold: true, fontFamily: Theme.FontBrand);
        head.Location = new Point(20, 12);
        card.Controls.Add(head);

        var lines = new[]
        {
            L.S("支持：MSFS 2024 / 2020（Beta）/ X-Plane 12（RTX 20-50 系显卡，驱动 ≥ 616.56）",
                "Supports: MSFS 2024 / 2020 (Beta) / X-Plane 12 (RTX 20-50 GPU, driver ≥ 616.56)"),
            L.S("路线：MSFS = DLSS Unlocked / OptiScaler；XP12 = DLSS5-Feeder + Deep Fried Chicken（ReShade 路线规划中）",
                "Routes: MSFS = DLSS Unlocked / OptiScaler; XP12 = DLSS5-Feeder + Deep Fried Chicken (ReShade route planned)"),
        };
        for (int i = 0; i < lines.Length; i++)
        {
            var l = Theme.MakeLabel(lines[i], Theme.TextMuted, 8.25f);
            l.Location = new Point(20, 40 + i * 20);
            card.Controls.Add(l);
        }

        var btnCheck = Theme.MakeButton(L.S("检查更新", "Check for Updates"), primary: true, height: 34);
        btnCheck.Size = new Size(130, 34);
        btnCheck.Location = new Point(ContentW - 130 - 16, 14);
        btnCheck.Click += (_, _) => CheckUpdateRequested?.Invoke();
        card.Controls.Add(btnCheck);

        Controls.Add(card);
    }

    /// <summary>检测到 GPU 后写入推荐的 WorkingScale。</summary>
    public void SetRecommendedScale(string scale)
    {
        var idx = _cboScale.Items.IndexOf(scale);
        if (idx >= 0) _cboScale.SelectedIndex = idx;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(L.S($"打开链接失败：{ex.Message}\n\n请手动加群：{QqGroup}",
                                $"Failed to open the link: {ex.Message}\n\nPlease join the group manually: {QqGroup}"),
                L.S("提示", "Notice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
