using System.Reflection;
using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>设置 / 关于页：语言、QQ 群、免费声明、安装选项（WorkingScale / 代理）与版本信息。</summary>
public sealed class AboutPage : UserControl
{
    public const string QqGroup = "615523002";
    public const string QqJoinUrl = "https://qun.qq.com/join.html?gc=" + QqGroup;

    private readonly ComboBox _cboScale = new();
    private readonly TextBox _txtProxy = new();
    private readonly ComboBox _cboLang = new();
    private bool _langReady;   // 防止初始 SelectedIndex 触发切换逻辑

    /// <summary>所选 WorkingScale（MSFS 安装时生效）。</summary>
    public string WorkingScale => _cboScale.SelectedItem?.ToString() ?? "0.5";

    /// <summary>所选代理（可为 null）。</summary>
    public string? Proxy => string.IsNullOrWhiteSpace(_txtProxy.Text) ? null : _txtProxy.Text.Trim();

    public AboutPage()
    {
        BackColor = Theme.Bg;
        Size = new Size(880, 800);

        Controls.Add(Theme.MakePageTitle(L.S("设置 / 关于", "Settings / About"), 24, 16));

        BuildLanguageCard(52);
        BuildQqCard(158);
        BuildDeclarationCard(290);
        BuildOptionsCard(406);
        BuildAboutCard(568);
    }

    private void BuildLanguageCard(int y)
    {
        var card = Theme.MakeCard(832, 94);
        card.Location = new Point(24, y);

        var head = Theme.MakeLabel(L.S("语言 / Language", "Language / 语言"), Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 10);
        card.Controls.Add(head);

        var lbl = Theme.MakeLabel(L.S("切换语言后需重启应用生效：", "Restart the app after switching to apply:"), Theme.TextSecondary, 9f);
        lbl.Location = new Point(16, 46);
        card.Controls.Add(lbl);

        _cboLang.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboLang.Items.AddRange(new object[] { "简体中文", "English" });
        _cboLang.SelectedIndex = L.English ? 1 : 0;
        _cboLang.Location = new Point(440, 42);
        _cboLang.Size = new Size(140, 26);
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

        Controls.Add(card);
    }

    private void BuildQqCard(int y)
    {
        var card = Theme.MakeCard(832, 120);
        card.Location = new Point(24, y);

        var avatar = new Label
        {
            Text = "QQ",
            Size = new Size(48, 48),
            Location = new Point(20, 36),
            BackColor = Theme.AccentStrong,
            ForeColor = Theme.AccentText,
            Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        card.Controls.Add(avatar);

        var title = Theme.MakeLabel(L.S("DLSS5 神经渲染交流群", "DLSS5 Neural Render Community"), Theme.Text, 10.5f, bold: true);
        title.Location = new Point(84, 28);
        card.Controls.Add(title);

        var sub = Theme.MakeLabel(
            L.S($"群号：{QqGroup}（获取更新、反馈问题、安装求助）", $"Group ID: {QqGroup} (updates, feedback and install help)"),
            Theme.TextSecondary, 9f);
        sub.Location = new Point(84, 54);
        card.Controls.Add(sub);

        var copy = Theme.MakeLabel(
            L.S("点击右侧按钮加入；也可复制群号后在 QQ 中搜索加群。", "Click the button on the right to join, or copy the group ID and search it in QQ."),
            Theme.TextMuted, 8.25f);
        copy.Location = new Point(84, 78);
        card.Controls.Add(copy);

        var btnJoin = Theme.MakeButton(L.S("加入 QQ 群", "Join QQ Group"), primary: true);
        btnJoin.Size = new Size(140, 32);
        btnJoin.Location = new Point(832 - 140 - 124 - 16, 44);
        btnJoin.Click += (_, _) => OpenUrl(QqJoinUrl);
        card.Controls.Add(btnJoin);

        var btnCopy = Theme.MakeButton(L.S("复制群号", "Copy ID"));
        btnCopy.Size = new Size(108, 32);
        btnCopy.Location = new Point(832 - 108 - 16, 44);
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

    private void BuildDeclarationCard(int y)
    {
        var card = Theme.MakeCard(832, 104);
        card.Location = new Point(24, y);

        var head = Theme.MakeLabel(L.S("声明", "Statement"), Theme.Warning, 10f, bold: true);
        head.Location = new Point(16, 12);
        card.Controls.Add(head);

        var body = Theme.MakeLabel(
            L.S("资源来源于网络，完全免费，禁止倒卖。",
                "All resources come from the internet. This tool is completely free — reselling it is strictly prohibited."),
            Theme.Text, 9.75f, bold: true);
        body.Location = new Point(16, 38);
        card.Controls.Add(body);

        var sub = Theme.MakeLabel(
            L.S("本工具及其引用的组件仅供个人学习交流使用，请支持正版游戏；若你为此工具付费，请立即退款并举报。",
                "This tool and the components it uses are for personal study and exchange only. Please support official releases; if you paid for this tool, refund immediately and report the seller."),
            Theme.TextMuted, 8.25f);
        sub.Location = new Point(16, 66);
        card.Controls.Add(sub);

        Controls.Add(card);
    }

    private void BuildOptionsCard(int y)
    {
        var card = Theme.MakeCard(832, 150);
        card.Location = new Point(24, y);

        var head = Theme.MakeLabel(
            L.S("安装选项（点击各游戏卡片「一键安装」时生效）", "Install options (applied when you click Install on a game card)"),
            Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 10);
        card.Controls.Add(head);

        var lblScale = Theme.MakeLabel(
            L.S("模型分辨率 WorkingScale（越低越省算力）：", "Model resolution WorkingScale (lower = less GPU cost):"),
            Theme.TextSecondary, 9f);
        lblScale.Location = new Point(16, 44);
        card.Controls.Add(lblScale);

        _cboScale.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboScale.Items.AddRange(new object[] { "0.35", "0.5", "0.75", "1.0" });
        _cboScale.SelectedIndex = 1;
        _cboScale.Location = new Point(440, 40);
        _cboScale.Size = new Size(90, 26);
        _cboScale.BackColor = Theme.SurfaceRaised;
        _cboScale.ForeColor = Theme.Text;
        card.Controls.Add(_cboScale);

        var lblProxy = Theme.MakeLabel(
            L.S("代理（可选，直连下载失败时使用）：", "Proxy (optional, for downloads when direct connection fails):"),
            Theme.TextSecondary, 9f);
        lblProxy.Location = new Point(16, 84);
        card.Controls.Add(lblProxy);

        _txtProxy.Location = new Point(440, 80);
        _txtProxy.Size = new Size(300, 26);
        _txtProxy.BackColor = Theme.SurfaceRaised;
        _txtProxy.ForeColor = Theme.Text;
        _txtProxy.BorderStyle = BorderStyle.FixedSingle;
        _txtProxy.PlaceholderText = L.S("示例：http://127.0.0.1:7890", "e.g. http://127.0.0.1:7890");
        card.Controls.Add(_txtProxy);

        Controls.Add(card);
    }

    private void BuildAboutCard(int y)
    {
        var card = Theme.MakeCard(832, 150);
        card.Location = new Point(24, y);

        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
        var head = Theme.MakeLabel($"DLSS5Patcher  v{ver.Major}.{ver.Minor}.{ver.Build}", Theme.Text, 10f, bold: true);
        head.Location = new Point(16, 12);
        card.Controls.Add(head);

        var lines = new[]
        {
            L.S("支持：微软模拟飞行 2024 / X-Plane 12（RTX 20-50 系显卡，驱动 ≥ 616.56）",
                "Supports: MSFS 2024 / X-Plane 12 (RTX 20-50 series GPU, driver ≥ 616.56)"),
            L.S("路线：MSFS = DLSS Unlocked / OptiScaler；XP12 = DLSS5-Feeder + Deep Fried Chicken（ReShade 路线规划中）",
                "Routes: MSFS = DLSS Unlocked / OptiScaler; XP12 = DLSS5-Feeder + Deep Fried Chicken (ReShade route planned)"),
            L.S("命令行：--detect | --install [scale] [--proxy url] | --uninstall | --install-xp12 [--kit 目录] | --uninstall-xp12",
                "CLI: --detect | --install [scale] [--proxy url] | --uninstall | --install-xp12 [--kit dir] | --uninstall-xp12"),
        };
        for (int i = 0; i < lines.Length; i++)
        {
            var l = Theme.MakeLabel(lines[i], Theme.TextMuted, 8.25f);
            l.Location = new Point(16, 44 + i * 24);
            card.Controls.Add(l);
        }

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
