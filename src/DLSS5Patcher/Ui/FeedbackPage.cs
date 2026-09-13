using DLSS5Patcher.Core;
using Microsoft.Win32;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 问题反馈页：自动识别环境（显卡/驱动/系统），勾选出问题的游戏后自动扫描可附加的日志文件，
/// 引导用户填写文字说明并附截图，一键提交到分发服务器（每 IP 每天 10 条、每 10 分钟 1 条，由服务端限流）。
/// </summary>
public sealed class FeedbackPage : UserControl
{
    private const int MaxShots = 4;
    private const long MaxShotBytes = 8 * 1024 * 1024;
    private const int MaxDescChars = 4000;

    private GpuInfo _gpu = new("", "", GpuGeneration.Unknown);
    private GameInstall? _g24, _g20, _gxp;
    private readonly List<string> _shots = new();
    private bool _submitting;

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
    private readonly ListBox _lstShots = new();
    private Theme.GlassButton _btnSubmit = new();
    private readonly Label _lblStatus = new();
    private readonly Label _lblCount = new();

    public FeedbackPage()
    {
        BackColor = Theme.Bg;
        Size = new Size(862, 800);

        Controls.Add(Theme.MakePageHeader(L.S("FEEDBACK", "FEEDBACK"), L.S("问题反馈", "Feedback")));

        BuildEnvCard(100);
        BuildGamesCard(272);
        BuildLogsCard(338);
        BuildDescCard(496);
        BuildShotsCard(664);
        BuildSubmitRow(766);
    }

    // ───────────────────────────── 环境信息 ─────────────────────────────

    private void BuildEnvCard(int y)
    {
        var card = Theme.MakeCard(790, 172);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(L.S("环境信息（自动识别）", "Environment (auto-detected)"), Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 8);
        card.Controls.Add(head);

        AddEnvRow(card, L.S("程序版本：", "App version:"), _lblApp, 34);
        AddEnvRow(card, L.S("操作系统：", "OS:"), _lblOs, 58);
        AddEnvRow(card, L.S("显卡 / 驱动 / 显存：", "GPU / driver / VRAM:"), _lblGpu, 82);
        AddEnvRow(card, "MSFS 2024:", _lblG24, 108);
        AddEnvRow(card, "MSFS 2020:", _lblG20, 130);
        AddEnvRow(card, "X-Plane 12:", _lblGxp, 152);

        Controls.Add(card);
    }

    private static void AddEnvRow(Panel card, string caption, Label value, int y)
    {
        var lbl = Theme.MakeLabel(caption, Theme.TextSecondary, 9f);
        lbl.Location = new Point(16, y);
        card.Controls.Add(lbl);

        value.AutoSize = false;
        value.Size = new Size(660, 18);
        value.Location = new Point(160, y + 1);
        value.ForeColor = Theme.Text;
        value.Font = new Font("Microsoft YaHei UI", 8.5f);
        value.AutoEllipsis = true;
        card.Controls.Add(value);
    }

    // ───────────────────────────── 游戏 / 日志 / 描述 / 截图 ─────────────────────────────

    private void BuildGamesCard(int y)
    {
        var card = Theme.MakeCard(790, 58);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(L.S("出问题的游戏（可多选，勾选后自动附加对应日志）：", "Affected game(s) (multi-select; related logs are attached automatically):"),
            Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 8);
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
        ck.Location = new Point(x, 26);
        ck.ForeColor = Theme.TextSecondary;
        ck.CheckedChanged += (_, _) => RescanLogs();
        card.Controls.Add(ck);
    }

    private void BuildLogsCard(int y)
    {
        var card = Theme.MakeCard(790, 158);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(
            L.S("将附加的日志文件（大文件自动只取末尾 256KB）：", "Log files to attach (oversized logs are truncated to the last 256 KB):"),
            Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 8);
        card.Controls.Add(head);

        _lstLogs.CheckOnClick = true;
        _lstLogs.BackColor = Theme.SurfaceRaised;
        _lstLogs.ForeColor = Theme.TextSecondary;
        _lstLogs.BorderStyle = BorderStyle.FixedSingle;
        _lstLogs.Font = new Font(Theme.FontUi, 8.5f);
        _lstLogs.Size = new Size(758, 112);
        _lstLogs.Location = new Point(16, 32);
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
        var card = Theme.MakeCard(790, 170);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(
            L.S("问题描述（必填）：什么现象、何时出现、如何复现、游戏内设置等", "Description (required): what happens, when, how to reproduce, in-game settings..."),
            Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 8);
        card.Controls.Add(head);

        _lblCount.ForeColor = Theme.TextMuted;
        _lblCount.Font = new Font("Microsoft YaHei UI", 8f);
        _lblCount.AutoSize = true;
        _lblCount.Location = new Point(740, 10);
        card.Controls.Add(_lblCount);

        _txtDesc.Multiline = true;
        _txtDesc.ScrollBars = RichTextBoxScrollBars.None;
        _txtDesc.MaxLength = MaxDescChars;
        _txtDesc.BackColor = Theme.SurfaceRaised;
        _txtDesc.ForeColor = Theme.Text;
        _txtDesc.BorderStyle = BorderStyle.FixedSingle;
        _txtDesc.Font = new Font("Microsoft YaHei UI", 9f);
        _txtDesc.Size = new Size(758, 122);
        _txtDesc.Location = new Point(16, 32);
        _txtDesc.TextChanged += (_, _) => _lblCount.Text = $"{_txtDesc.Text.Length}/{MaxDescChars}";
        card.Controls.Add(_txtDesc);
        Theme.AttachScrollIndicator(_txtDesc, card, rightInset: 16, topInset: 34, height: 118);

        Controls.Add(card);
    }

    private void BuildShotsCard(int y)
    {
        var card = Theme.MakeCard(790, 102);
        card.Location = new Point(36, y);

        var head = Theme.MakeLabel(
            L.S("截图（可选，最多 4 张、每张 ≤ 8MB；建议包含游戏内报错/画面异常的画面）",
                "Screenshots (optional, up to 4, each ≤ 8 MB; in-game errors or glitches are most helpful)"),
            Theme.Text, 9.75f, bold: true);
        head.Location = new Point(16, 8);
        card.Controls.Add(head);

        var btnAdd = Theme.MakeButton(L.S("添加截图...", "Add screenshots..."));
        btnAdd.Size = new Size(120, 28);
        btnAdd.Location = new Point(16, 34);
        btnAdd.Click += (_, _) => AddShots();
        card.Controls.Add(btnAdd);

        var btnClear = Theme.MakeButton(L.S("清除", "Clear"));
        btnClear.Size = new Size(76, 28);
        btnClear.Location = new Point(144, 34);
        btnClear.Click += (_, _) => { _shots.Clear(); RefreshShots(); };
        card.Controls.Add(btnClear);

        _lstShots.BackColor = Theme.SurfaceRaised;
        _lstShots.ForeColor = Theme.TextSecondary;
        _lstShots.BorderStyle = BorderStyle.FixedSingle;
        _lstShots.Font = new Font("Microsoft YaHei UI", 8.5f);
        _lstShots.Size = new Size(596, 58);
        _lstShots.Location = new Point(232, 34);
        _lstShots.IntegralHeight = false;
        card.Controls.Add(_lstShots);

        Controls.Add(card);
    }

    private void BuildSubmitRow(int y)
    {
        _btnSubmit = Theme.MakeButton(L.S("提交反馈", "Submit Feedback"), primary: true);
        _btnSubmit.Size = new Size(150, 40);
        _btnSubmit.Location = new Point(36, y);
        _btnSubmit.Click += (_, _) => _ = SubmitAsync();
        Controls.Add(_btnSubmit);

        _lblStatus.AutoSize = false;
        _lblStatus.Size = new Size(660, 34);
        _lblStatus.Location = new Point(190, y + 2);
        _lblStatus.ForeColor = Theme.TextMuted;
        _lblStatus.Font = new Font("Microsoft YaHei UI", 8.75f);
        _lblStatus.Text = L.S("提交前请确认已勾选出问题的游戏并填写问题描述。",
                              "Before submitting, pick the affected game(s) and fill in the description.");
        Controls.Add(_lblStatus);
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
        _lblG24.Text = GameText(g24);
        _lblG20.Text = GameText(g20);
        _lblGxp.Text = GameText(xp);

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

    private static string GameText(GameInstall? g) =>
        g != null ? $"{g.GameDir}   [{g.Source}]" : L.S("未检测到", "not detected");

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

    private async Task SubmitAsync()
    {
        if (_submitting) return;

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

        var report = new FeedbackClient.Report(
            Updater.CurrentVersion, OsText(), $".NET {Environment.Version}",
            _gpu, games, desc, logs, shots);

        _submitting = true;
        _btnSubmit.Enabled = false;
        _lblStatus.ForeColor = Theme.TextMuted;
        _lblStatus.Text = L.S("正在提交反馈（含日志与截图，视网速需数秒到一两分钟）…",
                              "Submitting feedback (logs + screenshots; may take a while)...");
        AppLog.Info("反馈提交开始");

        try
        {
            var id = await FeedbackClient.SubmitAsync(report);
            AppLog.Info($"反馈提交成功 {id}");
            _lblStatus.ForeColor = Theme.Signal;
            _lblStatus.Text = L.S($"提交成功！反馈编号：{id}。请把编号发到粉丝群，便于跟踪处理。",
                                  $"Submitted! Feedback ID: {id}. Share this ID in the fan group for follow-up.");
            _txtDesc.Clear();
            _shots.Clear();
            RefreshShots();
            MessageBox.Show(this,
                L.S($"反馈提交成功！\n\n反馈编号：{id}\n请把这个编号发到粉丝群（QQ 群 {AboutPage.QqGroup}），维护者会根据编号查看反馈并修复。",
                    $"Feedback submitted!\n\nID: {id}\nShare this ID in the fan group (QQ {AboutPage.QqGroup}); the maintainer will look it up by ID."),
                L.S("提交成功", "Submitted"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
