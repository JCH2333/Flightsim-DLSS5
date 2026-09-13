using DLSS5Patcher.Core;
using Microsoft.Win32;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 问题反馈页（同 GSX 汉化）：自动识别环境（显卡/驱动/系统），勾选出问题的游戏后自动扫描可附加的日志文件，
/// 填写问题描述与可选用户名（留空匿名）并附截图提交；提交后取得反馈码，可随时在底部查询处理进度与管理员回复。
/// 截图 ≤4 张、每张 ≤8MB、每 IP 每天 10 条 / 每 10 分钟 1 条为本项目自有设置；查询限流每天 60 次由服务端执行。
/// </summary>
public sealed class FeedbackPage : UserControl
{
    private const int MaxShots = 4;
    private const long MaxShotBytes = 8 * 1024 * 1024;
    private const int MaxDescChars = 4000;
    private const int MaxUsernameChars = 50;

    private GpuInfo _gpu = new("", "", GpuGeneration.Unknown);
    private GameInstall? _g24, _g20, _gxp;
    private readonly List<string> _shots = new();
    private bool _submitting;
    private bool _submitted;
    private bool _querying;
    private string _lastCode = "";

    private readonly Label _lblApp = new();
    private readonly Label _lblOs = new();
    private readonly Label _lblGpu = new();
    private readonly Label _lblG24 = new();
    private readonly Label _lblG20 = new();
    private readonly Label _lblGxp = new();
    private readonly Theme.GlassCheck _ck24 = new();
    private readonly Theme.GlassCheck _ck20 = new();
    private readonly Theme.GlassCheck _ckxp = new();
    private readonly CheckedListBox _lstLogs = new();
    private readonly RichTextBox _txtDesc = new();
    private readonly TextBox _txtUsername = new();
    private readonly ListBox _lstShots = new();
    private Theme.GlassButton _btnSubmit = new();
    private readonly Theme.GlassButton _btnAdd = new();
    private readonly Theme.GlassButton _btnClear = new();
    private readonly Label _lblStatus = new();
    private readonly Label _lblCount = new();
    private readonly Theme.GlassButton _btnCopyCode = new();
    // 反馈码查询
    private readonly TextBox _txtQuery = new();
    private readonly Theme.GlassButton _btnQuery = new();
    private readonly Label _lblQueryState = new();
    private readonly Label _lblQueryReply = new();

    public FeedbackPage()
    {
        BackColor = Theme.Bg;
        Size = new Size(862, 800);

        Controls.Add(Theme.MakePageHeader(L.S("FEEDBACK", "FEEDBACK"), L.S("问题反馈", "Feedback")));

        // 紧凑单屏布局：总高 792 ≤ 页面 800，无需滚动（窗口固定尺寸且不可滚动）
        BuildEnvCard(100);
        BuildGamesCard(204);
        BuildLogsCard(264);
        BuildDescCard(396);
        BuildShotsCard(544);
        BuildSubmitRow(636);
        BuildQueryCard(680);
    }

    // ───────────────────────────── 环境信息 ─────────────────────────────

    private void BuildEnvCard(int y)
    {
        var card = Theme.MakeCard(790, 96);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(L.S("环境信息（自动识别）", "Environment (auto-detected)"), Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 6);
        card.Controls.Add(head);

        // 双列：左=版本/系统，右=操作系统列；GPU 独占整行，游戏状态一行三项
        AddEnvRow(card, L.S("程序版本：", "App version:"), _lblApp, 30, capX: 16, valX: 104, valW: 312);
        AddEnvRow(card, L.S("操作系统：", "OS:"), _lblOs, 30, capX: 440, valX: 508, valW: 266);
        AddEnvRow(card, L.S("显卡 / 驱动 / 显存：", "GPU / driver / VRAM:"), _lblGpu, 52, capX: 16, valX: 150, valW: 624);

        AddGameStateLabel(card, _lblG24, 16, 252);
        AddGameStateLabel(card, _lblG20, 290, 236);
        AddGameStateLabel(card, _lblGxp, 540, 234);

        Controls.Add(card);
    }

    private static void AddEnvRow(Panel card, string caption, Label value, int y, int capX, int valX, int valW)
    {
        var lbl = Theme.MakeLabel(caption, Theme.TextSecondary, 9f);
        lbl.Location = new Point(capX, y);
        card.Controls.Add(lbl);

        value.AutoSize = false;
        value.Size = new Size(valW, 18);
        value.Location = new Point(valX, y + 1);
        value.ForeColor = Theme.Text;
        value.Font = new Font("Microsoft YaHei UI", 8.5f);
        value.AutoEllipsis = true;
        card.Controls.Add(value);
    }

    private static void AddGameStateLabel(Panel card, Label lbl, int x, int w)
    {
        lbl.AutoSize = false;
        lbl.Size = new Size(w, 18);
        lbl.Location = new Point(x, 76);
        lbl.ForeColor = Theme.TextMuted;
        lbl.Font = new Font("Microsoft YaHei UI", 8.5f);
        lbl.AutoEllipsis = true;
        card.Controls.Add(lbl);
    }

    private static void SetGameState(Label lbl, string name, GameInstall? g)
    {
        lbl.Text = g != null ? $"{name}   已检测 · {g.Source}" : $"{name}   未检测到";
        lbl.ForeColor = g != null ? Theme.Signal : Theme.TextMuted;
    }

    // ───────────────────────────── 游戏 / 日志 / 描述 / 截图 ─────────────────────────────

    private void BuildGamesCard(int y)
    {
        var card = Theme.MakeCard(790, 52);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(L.S("出问题的游戏（可多选，勾选后自动附加对应日志）：", "Affected game(s) (multi-select; related logs are attached automatically):"),
            Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 6);
        card.Controls.Add(head);

        BuildCheck(_ck24, "MSFS 2024", 16, card);
        BuildCheck(_ck20, "MSFS 2020 (Beta)", 270, card);
        BuildCheck(_ckxp, "X-Plane 12", 560, card);

        Controls.Add(card);
    }

    private void BuildCheck(Theme.GlassCheck ck, string text, int x, Panel card)
    {
        ck.Text = text;
        ck.Size = new Size(TextRenderer.MeasureText(text, ck.Font).Width + 34, 24);
        ck.Location = new Point(x, 22);
        ck.ForeColor = Theme.TextSecondary;
        ck.CheckedChanged += (_, _) => RescanLogs();
        card.Controls.Add(ck);
    }

    private void BuildLogsCard(int y)
    {
        var card = Theme.MakeCard(790, 124);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(
            L.S("将附加的日志文件（大文件自动只取末尾 256KB）：", "Log files to attach (oversized logs are truncated to the last 256 KB):"),
            Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 6);
        card.Controls.Add(head);

        _lstLogs.CheckOnClick = true;
        _lstLogs.BackColor = Theme.SurfaceRaised;
        _lstLogs.ForeColor = Theme.TextSecondary;
        _lstLogs.BorderStyle = BorderStyle.FixedSingle;
        _lstLogs.Font = new Font(Theme.FontUi, 8.5f);
        _lstLogs.Size = new Size(758, 88);
        _lstLogs.Location = new Point(16, 28);
        _lstLogs.IntegralHeight = false;
        // 自绘条目：绿勾选框替代系统蓝框
        _lstLogs.DrawMode = DrawMode.OwnerDrawFixed;
        _lstLogs.ItemHeight = 22;
        _lstLogs.DrawItem += (s, e) =>
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            var text = _lstLogs.Items[e.Index].ToString() ?? "";
            var isChecked = _lstLogs.GetItemChecked(e.Index);
            var box = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + (e.Bounds.Height - 14) / 2, 14, 14);
            using (var path = Theme.RoundedPath(box, 4))
            {
                using var fill = new SolidBrush(isChecked ? Theme.SignalBg : Theme.Surface);
                e.Graphics.FillPath(fill, path);
                using var pen = new Pen(isChecked ? Theme.SignalBorder : Theme.GlassBorder);
                e.Graphics.DrawPath(pen, path);
            }
            if (isChecked)
            {
                using var pen = new Pen(Theme.Signal, 1.6f);
                e.Graphics.DrawLine(pen, box.X + 3, box.Y + 7, box.X + 6, box.Y + 10);
                e.Graphics.DrawLine(pen, box.X + 6, box.Y + 10, box.X + 11, box.Y + 4);
            }
            TextRenderer.DrawText(e.Graphics, text, e.Font,
                new Rectangle(e.Bounds.X + 22, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height),
                Theme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        };
        card.Controls.Add(_lstLogs);

        Controls.Add(card);
    }

    private void BuildDescCard(int y)
    {
        var card = Theme.MakeCard(790, 140);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(
            L.S("问题描述（必填）：什么现象、何时出现、如何复现", "Description (required): what happens, when, how to reproduce"),
            Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 8);
        card.Controls.Add(head);

        // 可选用户名（同 GSX：留空则匿名提交），放在标题行右侧
        var userLbl = Theme.MakeLabel(L.S("用户名（选填）：", "Username (optional):"), Theme.TextSecondary, 9f);
        userLbl.Location = new Point(398, 12);
        card.Controls.Add(userLbl);

        _txtUsername.MaxLength = MaxUsernameChars;
        _txtUsername.BackColor = Theme.SurfaceRaised;
        _txtUsername.ForeColor = Theme.Text;
        _txtUsername.BorderStyle = BorderStyle.FixedSingle;
        _txtUsername.Font = new Font("Microsoft YaHei UI", 9f);
        _txtUsername.Size = new Size(160, 22);
        _txtUsername.Location = new Point(492, 8);
        _txtUsername.PlaceholderText = L.S("留空则匿名提交", "blank = anonymous");
        card.Controls.Add(_txtUsername);

        _lblCount.ForeColor = Theme.TextMuted;
        _lblCount.Font = new Font("Microsoft YaHei UI", 8f);
        _lblCount.AutoSize = true;
        _lblCount.Location = new Point(726, 12);
        card.Controls.Add(_lblCount);

        _txtDesc.Multiline = true;
        _txtDesc.ScrollBars = RichTextBoxScrollBars.None;
        _txtDesc.MaxLength = MaxDescChars;
        _txtDesc.BackColor = Theme.SurfaceRaised;
        _txtDesc.ForeColor = Theme.Text;
        _txtDesc.BorderStyle = BorderStyle.FixedSingle;
        _txtDesc.Font = new Font("Microsoft YaHei UI", 9f);
        _txtDesc.Size = new Size(758, 100);
        _txtDesc.Location = new Point(16, 32);
        _txtDesc.TextChanged += (_, _) => _lblCount.Text = $"{_txtDesc.Text.Length}/{MaxDescChars}";
        card.Controls.Add(_txtDesc);
        Theme.AttachScrollIndicator(_txtDesc, card, rightInset: 16, topInset: 34, height: 96);

        Controls.Add(card);
    }

    private void BuildShotsCard(int y)
    {
        var card = Theme.MakeCard(790, 84);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(
            L.S("截图（可选，最多 4 张、每张 ≤ 8MB；建议包含游戏内报错/画面异常的画面）",
                "Screenshots (optional, up to 4, each ≤ 8 MB; in-game errors or glitches are most helpful)"),
            Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 6);
        card.Controls.Add(head);

        _btnAdd.Text = L.S("添加截图...", "Add screenshots...");
        _btnAdd.Size = new Size(120, 26);
        _btnAdd.Location = new Point(16, 30);
        _btnAdd.Click += (_, _) => AddShots();
        card.Controls.Add(_btnAdd);

        _btnClear.Text = L.S("清除", "Clear");
        _btnClear.Size = new Size(76, 26);
        _btnClear.Location = new Point(144, 30);
        _btnClear.Click += (_, _) => { _shots.Clear(); RefreshShots(); };
        card.Controls.Add(_btnClear);

        _lstShots.BackColor = Theme.SurfaceRaised;
        _lstShots.ForeColor = Theme.TextSecondary;
        _lstShots.BorderStyle = BorderStyle.FixedSingle;
        _lstShots.Font = new Font("Microsoft YaHei UI", 8.5f);
        _lstShots.Size = new Size(542, 48);
        _lstShots.Location = new Point(232, 30);
        _lstShots.IntegralHeight = false;
        card.Controls.Add(_lstShots);

        Controls.Add(card);
    }

    private void BuildSubmitRow(int y)
    {
        _btnSubmit = Theme.MakeButton(L.S("提交反馈", "Submit Feedback"), primary: true);
        _btnSubmit.Size = new Size(150, 36);
        _btnSubmit.Location = new Point(36, y);
        _btnSubmit.Click += (_, _) => { if (_submitted) ResetForm(); else _ = SubmitAsync(); };
        Controls.Add(_btnSubmit);

        _lblStatus.AutoSize = false;
        _lblStatus.Size = new Size(470, 34);
        _lblStatus.Location = new Point(192, y + 2);
        _lblStatus.ForeColor = Theme.TextMuted;
        _lblStatus.Font = new Font("Microsoft YaHei UI", 8.75f);
        _lblStatus.Text = L.S("提交前请确认已勾选出问题的游戏并填写问题描述。",
                              "Before submitting, pick the affected game(s) and fill in the description.");
        Controls.Add(_lblStatus);

        // 提交成功后出现：复制反馈码（同 GSX）
        _btnCopyCode.Text = L.S("复制反馈码", "Copy code");
        _btnCopyCode.Size = new Size(100, 28);
        _btnCopyCode.Location = new Point(668, y + 4);
        _btnCopyCode.Visible = false;
        _btnCopyCode.Click += (_, _) => CopyCode();
        Controls.Add(_btnCopyCode);
    }

    // ───────────────────────────── 反馈码查询（同 GSX） ─────────────────────────────

    private void BuildQueryCard(int y)
    {
        var card = Theme.MakeCard(790, 112);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(L.S("查询反馈进度", "Query feedback status"), Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 6);
        card.Controls.Add(head);

        var hint = Theme.MakeLabel(
            L.S("输入提交后获得的反馈码，查看处理状态与管理员回复（每天最多查询 60 次）。",
                "Enter the feedback code you received to check status and admin reply (60 queries/day)."),
            Theme.TextMuted, 8.25f);
        hint.AutoSize = false;
        hint.Size = new Size(560, 16);
        hint.Location = new Point(130, 10);
        card.Controls.Add(hint);

        _txtQuery.BackColor = Theme.SurfaceRaised;
        _txtQuery.ForeColor = Theme.Text;
        _txtQuery.BorderStyle = BorderStyle.FixedSingle;
        _txtQuery.Font = new Font("Microsoft YaHei UI", 9f);
        _txtQuery.Size = new Size(320, 24);
        _txtQuery.Location = new Point(16, 30);
        _txtQuery.PlaceholderText = L.S("输入反馈码，例如 FB-A1B2C3", "Feedback code, e.g. FB-A1B2C3");
        _txtQuery.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = QueryAsync(); } };
        card.Controls.Add(_txtQuery);

        _btnQuery.Text = L.S("查询", "Query");
        _btnQuery.Size = new Size(80, 24);
        _btnQuery.Location = new Point(344, 30);
        _btnQuery.Click += (_, _) => _ = QueryAsync();
        card.Controls.Add(_btnQuery);

        _lblQueryState.AutoSize = false;
        _lblQueryState.Size = new Size(758, 18);
        _lblQueryState.Location = new Point(16, 62);
        _lblQueryState.ForeColor = Theme.TextMuted;
        _lblQueryState.Font = new Font("Microsoft YaHei UI", 8.75f);
        card.Controls.Add(_lblQueryState);

        _lblQueryReply.AutoSize = false;
        _lblQueryReply.Size = new Size(758, 18);
        _lblQueryReply.Location = new Point(16, 84);
        _lblQueryReply.ForeColor = Theme.TextSecondary;
        _lblQueryReply.Font = new Font("Microsoft YaHei UI", 8.75f);
        _lblQueryReply.AutoEllipsis = true;
        card.Controls.Add(_lblQueryReply);

        Controls.Add(card);
    }

    private async Task QueryAsync()
    {
        var code = _txtQuery.Text.Trim();
        if (code.Length == 0)
        {
            _lblQueryState.ForeColor = Theme.Danger;
            _lblQueryState.Text = L.S("请先输入反馈码。", "Enter a feedback code first.");
            _lblQueryReply.Text = "";
            return;
        }
        if (_querying) return;
        _querying = true;
        _btnQuery.Enabled = false;
        _lblQueryState.ForeColor = Theme.TextMuted;
        _lblQueryState.Text = L.S("正在查询…", "Querying...");
        _lblQueryReply.Text = "";
        try
        {
            var r = await FeedbackClient.QueryAsync(code);
            var user = string.IsNullOrEmpty(r.Username) ? L.S("匿名", "Anonymous") : r.Username;
            switch (r.StatusCode)
            {
                case "NOT_FOUND":
                    _lblQueryState.ForeColor = Theme.Danger;
                    _lblQueryState.Text = L.S("反馈码不存在，请检查后重新输入。", "Feedback code not found. Please check and retry.");
                    break;
                case "EXPIRED":
                    _lblQueryState.ForeColor = Theme.TextMuted;
                    _lblQueryState.Text = L.S("该反馈已处理完毕并超过保留期被清理。", "This feedback was processed and removed after the retention period.");
                    break;
                case "PROCESSED":
                    _lblQueryState.ForeColor = Theme.Signal;
                    _lblQueryState.Text = L.S($"已处理 · 提交时间：{FmtTime(r.CreatedAt)} · {user}",
                                              $"Processed · Submitted: {FmtTime(r.CreatedAt)} · {user}");
                    _lblQueryReply.ForeColor = Theme.Text;
                    _lblQueryReply.Text = string.IsNullOrEmpty(r.AdminReply)
                        ? L.S("管理员已处理该反馈。", "An administrator has processed this feedback.")
                        : L.S("管理员回复：", "Admin reply: ") + r.AdminReply;
                    break;
                default: // PENDING
                    _lblQueryState.ForeColor = Theme.TextSecondary;
                    _lblQueryState.Text = L.S($"未处理 · 提交时间：{FmtTime(r.CreatedAt)} · {user}",
                                              $"Pending · Submitted: {FmtTime(r.CreatedAt)} · {user}");
                    _lblQueryReply.ForeColor = Theme.TextMuted;
                    _lblQueryReply.Text = L.S("我们会尽快处理你的反馈，处理完成后可在此看到管理员回复。",
                                              "We will process your feedback soon; the admin reply will appear here once done.");
                    break;
            }
        }
        catch (Exception ex)
        {
            _lblQueryState.ForeColor = Theme.Danger;
            _lblQueryState.Text = ex.Message;
        }
        finally
        {
            _querying = false;
            _btnQuery.Enabled = true;
        }
    }

    private static string FmtTime(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return "-";
        try
        {
            if (DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t))
                return t.Kind == DateTimeKind.Utc ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : t.ToString("yyyy-MM-dd HH:mm");
        }
        catch { }
        return iso.Length > 16 ? iso[..16] : iso;
    }

    // ───────────────────────────── 数据填充与扫描 ─────────────────────────────

    /// <summary>主窗体检测完成后调用：填充环境信息并重扫日志。</summary>
    public void SetEnvironment(GpuInfo gpu, GameInstall? g24, GameInstall? g20, GameInstall? xp)
    {
        _gpu = gpu;
        _g24 = g24;
        _g20 = g20;
        _gxp = xp;

        _lblApp.Text = $"DLSS5Patcher v{Updater.CurrentVersion}";
        _lblOs.Text = OsText();
        _lblGpu.Text = gpu.IsNvidia ? $"{gpu.Name}   |   驱动 {gpu.Driver}   |   显存 {gpu.VramText}   |   {gpu.GenerationCn}"
                                    : L.S("未检测到 NVIDIA 显卡", "No NVIDIA GPU detected");
        SetGameState(_lblG24, "MSFS 2024", g24);
        SetGameState(_lblG20, "MSFS 2020", g20);
        SetGameState(_lblGxp, "X-Plane 12", xp);

        RescanLogs();
    }

    private static string OsText()
    {
        try
        {
            var name = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", null) as string;
            var display = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", null) as string;
            if (!string.IsNullOrEmpty(name))
                return $"{name}{(string.IsNullOrEmpty(display) ? "" : " " + display)} ({Environment.OSVersion.Version}) ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})";
        }
        catch { }
        return $"Windows {Environment.OSVersion.Version} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})";
    }

    private sealed record LogItem(string Label, string Path, long Size)
    {
        public override string ToString() => $"{Label}   ({Math.Max(1, Size / 1024)} KB)";
    }

    /// <summary>按勾选的游戏重扫可附加的日志文件（默认全选）。</summary>
    private void RescanLogs()
    {
        _lstLogs.BeginUpdate();
        _lstLogs.Items.Clear();

        void AddIfExists(string label, string path)
        {
            if (File.Exists(path))
                _lstLogs.Items.Add(new LogItem(label, path, new FileInfo(path).Length), true);
        }

        AddIfExists(L.S("本次运行日志（自动附加）", "Current session log (attached automatically)"),
            AppLog.SessionPath ?? "");

        if (_ck24.Checked || _ck20.Checked)
        {
            AddIfExists(L.S("MSFS 安装清单（工具生成）", "MSFS install manifest (generated by this tool)"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DLSS5Patcher", "manifest.json"));
        }
        if (_ckxp.Checked)
        {
            AddIfExists(L.S("XP12 安装清单（工具生成）", "XP12 install manifest (generated by this tool)"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DLSS5Patcher", "manifest_xp12.json"));
        }

        void AddGameLogs(GameInstall? g, string title)
        {
            if (g == null || !Directory.Exists(g.GameDir)) return;
            try
            {
                foreach (var f in Directory.EnumerateFiles(g.GameDir, "*.log").OrderBy(f => f).Take(5))
                    AddIfExists($"{title} — {Path.GetFileName(f)}", f);
            }
            catch { /* 目录不可读则跳过 */ }
        }

        if (_ck24.Checked) AddGameLogs(_g24, "MSFS 2024");
        if (_ck20.Checked) AddGameLogs(_g20, "MSFS 2020");
        if (_ckxp.Checked)
        {
            if (_gxp != null) AddIfExists("X-Plane 12 — Log.txt", Path.Combine(_gxp.GameDir, "Log.txt"));
            AddGameLogs(_gxp, "X-Plane 12");
        }

        _lstLogs.EndUpdate();
    }

    private void AddShots()
    {
        if (_shots.Count >= MaxShots)
        {
            MessageBox.Show(this, L.S($"最多附加 {MaxShots} 张截图。", $"Up to {MaxShots} screenshots."),
                L.S("提示", "Notice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new OpenFileDialog
        {
            Title = L.S("选择截图（可多选）", "Pick screenshots (multi-select)"),
            Filter = L.S("图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg", "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"),
            Multiselect = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        foreach (var f in dlg.FileNames)
        {
            if (_shots.Contains(f)) continue;
            if (new FileInfo(f).Length > MaxShotBytes)
            {
                MessageBox.Show(this,
                    L.S($"「{Path.GetFileName(f)}」超过 8MB，已跳过。", $"\"{Path.GetFileName(f)}\" exceeds 8 MB and was skipped."),
                    L.S("提示", "Notice"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                continue;
            }
            if (_shots.Count >= MaxShots) break;
            _shots.Add(f);
        }
        RefreshShots();
    }

    private void RefreshShots()
    {
        _lstShots.BeginUpdate();
        _lstShots.Items.Clear();
        foreach (var f in _shots)
            _lstShots.Items.Add($"{Path.GetFileName(f)}   ({new FileInfo(f).Length / 1024} KB)");
        _lstShots.EndUpdate();
    }

    // ───────────────────────────── 提交 ─────────────────────────────

    private void SetFormEnabled(bool enabled)
    {
        // 文本框用 ReadOnly（保持深色背景，禁用态 RichTextBox 会变白底）；其余控件直接禁用
        _txtDesc.ReadOnly = !enabled;
        _txtUsername.ReadOnly = !enabled;
        _lstLogs.Enabled = enabled;
        _lstShots.Enabled = enabled;
        _btnAdd.Enabled = enabled;
        _btnClear.Enabled = enabled;
        _ck24.Enabled = enabled;
        _ck20.Enabled = enabled;
        _ckxp.Enabled = enabled;
    }

    /// <summary>提交成功后的展示态（同 GSX）：锁定表单，展示反馈码并支持复制，「继续填写」复位。</summary>
    private void EnterSubmittedState(string code)
    {
        _submitted = true;
        _lastCode = code;
        SetFormEnabled(false);
        _btnSubmit.Text = L.S("继续填写", "New feedback");
        _lblStatus.ForeColor = Theme.Signal;
        _lblStatus.Text = L.S(
            $"✓ 反馈已提交！反馈码：{code} —— 凭反馈码可在下方随时查询处理进度与管理员回复。",
            $"✓ Submitted! Feedback code: {code} — use it below to check status and the admin reply anytime.");
        _btnCopyCode.Visible = true;
    }

    private void ResetForm()
    {
        _submitted = false;
        _lastCode = "";
        SetFormEnabled(true);
        _txtDesc.Clear();
        _txtUsername.Text = "";
        _shots.Clear();
        RefreshShots();
        _lblCount.Text = "";
        _lblStatus.ForeColor = Theme.TextMuted;
        _lblStatus.Text = L.S("提交前请确认已勾选出问题的游戏并填写问题描述。",
                              "Before submitting, pick the affected game(s) and fill in the description.");
        _btnSubmit.Text = L.S("提交反馈", "Submit Feedback");
        _btnCopyCode.Visible = false;
    }

    private void CopyCode()
    {
        if (string.IsNullOrEmpty(_lastCode)) return;
        try
        {
            Clipboard.SetText(_lastCode);
            _btnCopyCode.Text = L.S("已复制 ✓", "Copied ✓");
            var timer = new System.Windows.Forms.Timer { Interval = 1600 };
            timer.Tick += (_, _) => { timer.Stop(); timer.Dispose(); _btnCopyCode.Text = L.S("复制反馈码", "Copy code"); };
            timer.Start();
        }
        catch { /* 剪贴板被占用等，忽略 */ }
    }

    private async Task SubmitAsync()
    {
        if (_submitting || _submitted) return;

        var desc = _txtDesc.Text.Trim();
        if (desc.Length == 0)
        {
            MessageBox.Show(this,
                L.S("请先填写问题描述（什么现象、何时出现、如何复现），这样才便于定位与修复。",
                    "Please describe the problem first (what happens, when, how to reproduce) so it can be diagnosed."),
                L.S("缺少描述", "Description required"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtDesc.Focus();
            return;
        }

        var logs = new List<FeedbackClient.LogEntry>();
        foreach (LogItem item in _lstLogs.CheckedItems.OfType<LogItem>())
        {
            try
            {
                var (content, size, truncated) = FeedbackClient.ReadTail(item.Path);
                logs.Add(new FeedbackClient.LogEntry(item.Label, size, truncated, content));
            }
            catch (Exception ex)
            {
                AppLog.Warn($"日志读取失败（跳过）{item.Path}: {ex.Message}");
            }
        }

        var shots = new List<FeedbackClient.ShotEntry>();
        foreach (var f in _shots)
        {
            try { shots.Add(new FeedbackClient.ShotEntry(Path.GetFileName(f), new FileInfo(f).Length, File.ReadAllBytes(f))); }
            catch (Exception ex) { AppLog.Warn($"截图读取失败（跳过）{f}: {ex.Message}"); }
        }

        var games = new List<FeedbackClient.GameLine>();
        void AddGame(bool ck, string name, GameInstall? g, string stateText)
        {
            if (!ck) return;
            games.Add(new FeedbackClient.GameLine(name, g?.GameDir ?? "", g?.Source ?? L.S("未检测到", "not detected"), stateText));
        }
        AddGame(_ck24.Checked, "MSFS 2024", _g24, _g24 != null ? UnlockedInstaller.DetectState(_g24.GameDir) : L.S("未检测到", "not detected"));
        AddGame(_ck20.Checked, "MSFS 2020", _g20, _g20 != null ? UnlockedInstaller.DetectState(_g20.GameDir) : L.S("未检测到", "not detected"));
        AddGame(_ckxp.Checked, "X-Plane 12", _gxp, _gxp != null ? XP12Installer.DetectState(_gxp.GameDir) : L.S("未检测到", "not detected"));

        var username = _txtUsername.Text.Trim();
        var report = new FeedbackClient.Report(
            Updater.CurrentVersion, OsText(), $".NET {Environment.Version}",
            _gpu, games, desc, username, logs, shots);

        _submitting = true;
        _btnSubmit.Enabled = false;
        _lblStatus.ForeColor = Theme.TextMuted;
        _lblStatus.Text = L.S("正在提交反馈（含日志与截图，视网速需数秒到一两分钟）…",
                              "Submitting feedback (logs + screenshots; may take a while)...");
        AppLog.Info("反馈提交开始");

        try
        {
            var (id, code) = await FeedbackClient.SubmitAsync(report);
            AppLog.Info($"反馈提交成功 {id} code={code}");
            EnterSubmittedState(string.IsNullOrEmpty(code) ? id : code);   // 旧服务端无短码时退回长编号
        }
        catch (Exception ex)
        {
            AppLog.Error("反馈提交失败", ex);
            _lblStatus.ForeColor = Theme.Danger;
            _lblStatus.Text = L.S($"提交失败：{ex.Message}", $"Submit failed: {ex.Message}");
            MessageBox.Show(this,
                L.S($"提交失败：{ex.Message}", $"Submit failed: {ex.Message}"),
                L.S("错误", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _submitting = false;
            _btnSubmit.Enabled = true;
        }
    }
}
