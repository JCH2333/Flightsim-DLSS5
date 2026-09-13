using System.Runtime.InteropServices;
using DLSS5Patcher.Core;
using DLSS5Patcher.Ui;

namespace DLSS5Patcher;

/// <summary>
/// 主窗体：自绘玻璃标题栏 + 左侧 218px 导航（一键安装 / 教程 / 反馈 / 设置）+ 右侧环境光晕内容区。
/// 视觉对齐 GSX 汉化 2.0.0（毛玻璃 + 简约高级感），中英双语；支持 MSFS 2024 / 2020(Beta) / X-Plane 12。
/// </summary>
public sealed class MainForm : Form
{
    private const int ClientW = 1080;
    private const int ClientH = 838;          // 含 38px 自绘标题栏
    private const int TitleBarH = 38;
    private const int SidebarW = 218;

    private GpuInfo _gpu = new("", "", GpuGeneration.Unknown);
    private GameInstall? _game2024;
    private GameInstall? _game2020;
    private GameInstall? _gameXp12;

    private readonly HomePage _home = new();
    private readonly TutorialPage _tutorial = new();
    private readonly FeedbackPage _feedback = new();
    private readonly SponsorPage _sponsor = new();
    private readonly AboutPage _about = new();
    private readonly AnnouncementsPage _ann = new();
    private readonly Control[] _pages = new Control[6];
    private readonly Theme.NavButton[] _nav = new Theme.NavButton[5];
    private Panel _annDot = new();
    private readonly List<Control> _titleButtons = new();

    public MainForm()
    {
        Text = L.S("DLSS5 神经渲染安装器 — MSFS 2024 / X-Plane 12（RTX 20-50 系）",
                   "DLSS5 Neural Render Patcher — MSFS 2024 / X-Plane 12 (RTX 20-50 series)");
        Font = new Font(Theme.FontUi, 9F);
        BackColor = Theme.TitleBar;
        ClientSize = new Size(ClientW, ClientH);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        _pages[0] = _home;
        _pages[1] = _tutorial;
        _pages[2] = _feedback;
        _pages[3] = _sponsor;
        _pages[4] = _about;
        _pages[5] = _ann;

        BuildTitleBar();
        BuildSidebar();
        Theme.WheelRouter.Install();   // 悬停即滚：教程/公告/反馈描述等 RichTextBox 无需焦点即可滚轮

        var content = new Panel
        {
            Location = new Point(SidebarW, TitleBarH),
            Size = new Size(ClientW - SidebarW, ClientH - TitleBarH),
            BackColor = Theme.Bg,
        };
        foreach (var p in _pages)
        {
            p.Location = new Point(0, 0);
            p.Size = content.Size;
            content.Controls.Add(p);
        }
        Controls.Add(content);

        WireEvents();

        // 高 DPI：布局按 96DPI 设计，此处按真实 DPI 整体缩放（字体本身随 DPI 渲染，无需缩放字体）
        float dpiFactor;
        using (var g = CreateGraphics()) dpiFactor = g.DpiX / 96f;
        if (dpiFactor > 1.01f)
        {
            Scale(new SizeF(dpiFactor, dpiFactor));
            ClientSize = new Size((int)(ClientW * dpiFactor), (int)(ClientH * dpiFactor));
        }

        SelectNav(0);
        _ = RefreshAsync();

        Updater.CleanLeftovers();
        AppLog.Info(L.S($"主窗体就绪（v{Updater.CurrentVersion}）", $"Main form ready (v{Updater.CurrentVersion})"));
        // 先做强制更新检查（可能弹模态框），完成后拉公告：更新未读点 + 弹窗逐条展示
        Shown += async (_, _) =>
        {
            await RunUpdateCheckAsync(startup: true);
            await LoadAnnouncementsAsync();
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyWindowChrome(this);   // Win11 圆角 + 深色属性
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    // 拖动自绘标题栏（避开窗口按钮区）
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        base.WndProc(ref m);
        if (m.Msg == WM_NCHITTEST && m.Result.ToInt32() == 1)   // HTCLIENT
        {
            short x = (short)(m.LParam.ToInt64() & 0xFFFF);
            short y = (short)((m.LParam.ToInt64() >> 16) & 0xFFFF);
            var p = PointToClient(new Point(x, y));
            if (p.Y < TitleBarH && p.X < ClientSize.Width - 92)
                m.Result = (IntPtr)2;                            // HTCAPTION
        }
    }

    private void AttachTitleBarDrag(Control target)
    {
        // 标签类子控件（HTCLIENT）用 MouseDown 转发拖动
        target.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
            }
        };
    }

    // ───────────────────────────── 外壳 UI ─────────────────────────────

    /// <summary>标题栏面板：整块命中测试为 HTCAPTION。注意：Panel 自身是个子窗口，
    /// 系统会把 HTCAPTION 拖动发给它导致"标题栏在窗口里滑动"，必须把
    /// WM_NCLBUTTONDOWN 转发回主窗体，让系统拖动主窗口。</summary>
    private sealed class TitleBarPanel : Panel
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int WM_NCLBUTTONDOWN = 0xA1;

            // NCHITTEST：把面板客户区报告为 HTCAPTION（标题栏）
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if (m.Result.ToInt32() == 1)
                    m.Result = (IntPtr)HTCAPTION;
                return;
            }

            // 关键：必须在 base 之前拦截 WM_NCLBUTTONDOWN+HTCAPTION，
            // 否则 base 的默认处理会进入拖动循环把面板自己拖走（阻塞到松开鼠标，转发永远来不及）
            if (m.Msg == WM_NCLBUTTONDOWN && m.WParam.ToInt32() == HTCAPTION)
            {
                ReleaseCapture();
                SendMessage(FindForm().Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
                return;
            }

            base.WndProc(ref m);
        }
    }

    private void BuildTitleBar()
    {
        var bar = new TitleBarPanel
        {
            Location = new Point(0, 0),
            Size = new Size(ClientW, TitleBarH),
            BackColor = Theme.TitleBar,
        };
        bar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
        };

        var logo = Theme.MakeLabel("DLSS5", Theme.Text, 10.5f, bold: true, fontFamily: Theme.FontBrand);
        logo.Location = new Point(13, 10);
        bar.Controls.Add(logo);

        var ver = Theme.MakeLabel("v" + Updater.CurrentVersion, Theme.TextMuted, 8f, fontFamily: Theme.FontBrand);
        ver.Location = new Point(logo.Right + 8, 12);
        bar.Controls.Add(ver);

        var dragHint = Theme.MakeLabel(Text, Theme.TextMuted, 8f);
        dragHint.Location = new Point(13, 22);
        dragHint.Visible = false;   // 仅保留无障碍文本；视觉走极简
        bar.Controls.Add(dragHint);

        AttachTitleBarDrag(bar);
        AttachTitleBarDrag(logo);
        AttachTitleBarDrag(ver);

        Controls.Add(bar);
        _titleButtons.Add(bar);

        AddWindowButton(bar, "─", minimizeGlyph: true, Theme.TextSecondary, Theme.SurfaceHover, (_, _) => WindowState = FormWindowState.Minimized);
        AddWindowButton(bar, "✕", minimizeGlyph: false, Theme.TextSecondary, Theme.DangerHover, (_, _) => Close());
    }

    private void AddWindowButton(Panel bar, string glyph, bool minimizeGlyph, Color fg, Color hoverBg, EventHandler onClick)
    {
        var b = new Theme.GlassButton
        {
            Text = glyph,
            Size = new Size(46, TitleBarH - 1),
            Location = new Point(ClientSize.Width - (minimizeGlyph ? 92 : 46), 0),
            Font = new Font(Theme.FontUi, 9.5f, FontStyle.Regular),
            ForeColor = fg,
            BackColor = Theme.TitleBar,
        };
        b.Primary = false;
        // 悬停底色（close 红）与直角样式由绘制覆写
        b.Paint += (_, e) =>
        {
            if (b.ClientRectangle.Contains(b.PointToClient(Cursor.Position)))
            {
                using var br = new SolidBrush(hoverBg);
                e.Graphics.FillRectangle(br, 0, 0, b.Width, b.Height);
                TextRenderer.DrawText(e.Graphics, glyph, b.Font, b.ClientRectangle,
                    hoverBg == Theme.DangerHover ? Color.White : Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };
        b.Click += onClick;
        Controls.Add(b);
        b.BringToFront();
        _titleButtons.Add(b);
    }

    private void BuildSidebar()
    {
        var sidebar = new Panel
        {
            Location = new Point(0, TitleBarH),
            Size = new Size(SidebarW, ClientH - TitleBarH),
            BackColor = Theme.TitleBar,
        };
        sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };
        Controls.Add(sidebar);
        sidebar.BringToFront();

        var brand = Theme.MakeLabel("DLSS5", Theme.Text, 19f, bold: true, fontFamily: Theme.FontBrand);
        brand.Location = new Point(22, 26);
        sidebar.Controls.Add(brand);

        var brandSub = Theme.MakeLabel("MSFS · XP12 NEURAL", Theme.TextMuted, 8.25f, bold: true, fontFamily: Theme.FontBrand);
        brandSub.Location = new Point(24, 56);
        sidebar.Controls.Add(brandSub);

        // 公告铃铛（GSX brand-bell 同位：品牌块右上），未读公告时右上角绿色小点
        var bell = new Theme.GlassButton
        {
            Text = "🔔",
            Font = new Font("Segoe UI Emoji", 10.5f),
            Size = new Size(32, 32),
            Location = new Point(SidebarW - 48, 28),
            TabStop = false,
        };
        bell.Click += (_, _) => SelectNav(5);
        sidebar.Controls.Add(bell);

        _annDot = new Panel
        {
            Size = new Size(9, 9),
            BackColor = Theme.Signal,
            Location = new Point(bell.Right - 7, bell.Top - 2),
        };
        _annDot.Visible = false;
        sidebar.Controls.Add(_annDot);
        _annDot.BringToFront();

        string[] navTexts = { L.S("一键安装", "Install"), L.S("使用教程", "Tutorial"), L.S("问题反馈", "Feedback"), L.S("赞助", "Sponsor"), L.S("设置 · 关于", "Settings · About") };
        for (int i = 0; i < navTexts.Length; i++)
        {
            int idx = i; // for 循环变量是共享的，闭包必须捕获局部副本
            var b = new Theme.NavButton
            {
                Text = navTexts[i],
                Size = new Size(SidebarW - 28, 44),
                Location = new Point(14, 104 + i * 50),
                TabStop = false,
            };
            b.Click += (_, _) => SelectNav(idx);
            _nav[i] = b;
            sidebar.Controls.Add(b);
        }

        var separator = new Panel { BackColor = Theme.Border, Location = new Point(14, ClientH - TitleBarH - 96), Size = new Size(SidebarW - 28, 1) };
        sidebar.Controls.Add(separator);

        var dot = new Panel
        {
            Size = new Size(8, 8),
            Location = new Point(20, ClientH - TitleBarH - 72),
            BackColor = Theme.Signal,
        };
        sidebar.Controls.Add(dot);

        var free = Theme.MakeLabel(L.S("完全免费 · 禁止倒卖", "Free forever · No reselling"), Theme.TextSecondary, 9f, bold: true);
        free.Location = new Point(38, ClientH - TitleBarH - 76);
        sidebar.Controls.Add(free);

        var ver = Theme.MakeLabel("DLSS5Patcher v" + Updater.CurrentVersion, Theme.TextMuted, 8f);
        ver.Location = new Point(38, ClientH - TitleBarH - 56);
        sidebar.Controls.Add(ver);
    }

    private void SelectNav(int idx)
    {
        _pages[idx].BringToFront();
        for (int i = 0; i < _nav.Length; i++)
        {
            _nav[i].Active = i == idx;
            _nav[i].Invalidate();
        }
        if (idx == 3) _ = _sponsor.LoadAsync();   // 每次进入赞助页都重新拉码（服务端换码即时生效）
        if (idx == 5) _ = _ann.ReloadAsync();     // 进入公告页：刷新列表并标记已读
    }

    private void WireEvents()
    {
        _home.RefreshRequested += () => _ = RefreshAsync();
        _home.MsfsInstallRequested += () => _ = InstallMsfsAsync(_game2024, beta: false);
        _home.MsfsUninstallRequested += () => _ = UninstallMsfsAsync(_game2024, beta: false);
        _home.Msfs2020InstallRequested += () => _ = InstallMsfsAsync(_game2020, beta: true);
        _home.Msfs2020UninstallRequested += () => _ = UninstallMsfsAsync(_game2020, beta: true);
        _home.XpInstallRequested += () => _ = InstallXp12Async();
        _home.XpUninstallRequested += () => _ = UninstallXp12Async();
        _about.MsfsBrowseRequested += () => BrowseForGame(0);
        _about.Msfs2020BrowseRequested += () => BrowseForGame(1);
        _about.XpBrowseRequested += () => BrowseForGame(2);
        _about.XpPickKitRequested += PickKitDir;
        _about.CheckUpdateRequested += () => _ = RunUpdateCheckAsync(startup: false);
        _ann.Read += maxId =>
        {
            if (maxId <= AppConfig.AnnReadId) return;
            AppConfig.AnnReadId = maxId;   // 已看完全部公告，未读点熄灭
            AppConfig.Save();
            _annDot.Visible = false;
        };
    }

    /// <summary>启动时拉取公告：更新未读点 + 逐条弹窗展示未看过的弹窗公告（GSX 同款）。</summary>
    private async Task LoadAnnouncementsAsync()
    {
        try
        {
            var listTask = AnnouncementsClient.FetchListAsync();
            var popupTask = AnnouncementsClient.FetchPopupAsync();
            var list = await listTask;
            var popups = await popupTask;

            if (IsHandleCreated)
                BeginInvoke(() =>
                {
                    _annDot.Visible = list.Ok && list.Announcements.Any(a => a.Id > AppConfig.AnnReadId);
                    if (popups.Ok && popups.Announcements.Count > 0)
                        AnnouncementDialog.ShowChain(this, popups.Announcements);
                });
        }
        catch (Exception ex)
        {
            AppLog.Warn($"公告加载失败（忽略）: {ex.Message}");
        }
    }

    private void Log(string s)
    {
        // 运行日志静默记录到后台日志文件（%LOCALAPPDATA%\DLSS5Patcher\logs），提交反馈时自动附带
        AppLog.Info(s);
    }

    // ───────────────────────────── 自动更新 ─────────────────────────────

    /// <summary>
    /// 检查 GitHub Release 更新。启动时静默：网络失败仅记日志（检测不到更新就无法强制）。
    /// 一旦确认有新版本，弹出不可关闭的强制更新对话框，直至更新完成重启或用户退出程序。
    /// </summary>
    private async Task RunUpdateCheckAsync(bool startup)
    {
        try
        {
            var info = await Task.Run(() => Updater.CheckAsync());
            if (info == null)
            {
                if (!startup)
                    MessageBox.Show(this,
                        L.S($"当前已是最新版本（v{Updater.CurrentVersion}）。", $"You already have the latest version (v{Updater.CurrentVersion})."),
                        L.S("检查更新", "Check for Updates"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Log(L.S($"检测到新版本 {info.Tag}（当前 v{Updater.CurrentVersion}），需要更新后才能继续使用。",
                    $"Update {info.Tag} found (current v{Updater.CurrentVersion}) — update required to continue."));
            using var dlg = new UpdateDialog(info);
            dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            if (startup)
            {
                Log(L.S("自动更新检查失败（网络不可达），本次跳过。", "Update check failed (network unreachable) — skipped this time."));
            }
            else
            {
                MessageBox.Show(this,
                    L.S($"检查更新失败：{ex.Message}", $"Update check failed: {ex.Message}"),
                    L.S("检查更新", "Check for Updates"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    // ───────────────────────────── 检测 ─────────────────────────────

    /// <summary>手动指定的 exe / 目录有效则用之，否则自动检测（目录为旧版配置的兼容入口）。</summary>
    private static GameInstall? FindGame(string manualDir, string manualExe, int targetIdx)
    {
        var exeName = GameLocator.Targets[targetIdx].ExeName;
        if (manualExe.Length > 0)
        {
            var g = GameLocator.FromManualExe(manualExe, exeName);
            if (g != null) return g;
        }
        if (manualDir.Length > 0)
        {
            var g = GameLocator.FromManualDir(manualDir, exeName);
            if (g != null) return g;
        }
        return GameLocator.Find(GameLocator.Targets[targetIdx].AppId, exeName);
    }

    private async Task RefreshAsync()
    {
        _home.SetBusy(true, L.S("正在检测...", "Detecting..."));
        try
        {
            await Task.Run(() =>
            {
                _gpu = GpuInfo.Detect();
                // 手动指定的 exe / 目录优先（存于 AppConfig，刷新不丢）；否则自动检测
                _game2024 = FindGame(AppConfig.ManualMsfs2024Dir, AppConfig.ManualExe24, 0);
                _game2020 = FindGame(AppConfig.ManualMsfs2020Dir, AppConfig.ManualExe20, 1);
                _gameXp12 = FindGame("", AppConfig.ManualXp12Exe, 2);
            });

            _home.SetGpu(
                L.S($"GPU: {(_gpu.Name.Length > 0 ? _gpu.Name : "未检测到 NVIDIA 显卡")}   |   驱动: {(_gpu.Driver.Length > 0 ? _gpu.Driver : "未知")}",
                    $"GPU: {(_gpu.Name.Length > 0 ? _gpu.Name : "No NVIDIA GPU detected")}   |   Driver: {(_gpu.Driver.Length > 0 ? _gpu.Driver : "unknown")}") +
                (_gpu.IsNvidia ? $"   |   {_gpu.GenerationCn}   |   " + L.S($"显存 {_gpu.VramText}", $"VRAM {_gpu.VramText}") : ""),
                _gpu.SupportedThisVersion);

            UpdateMsfs2024Card();
            UpdateMsfs2020Card();
            UpdateXp12Card();

            var checks = new List<string>();
            if (!_gpu.IsNvidia)
                checks.Add(L.S("✘ 未检测到 NVIDIA RTX 显卡，无法使用 DLSS", "✘ No NVIDIA RTX GPU detected — DLSS unavailable"));
            else if (!_gpu.SupportedThisVersion)
                checks.Add(L.S($"✘ {_gpu.GenerationCn}：不支持（仅支持 RTX 20-50 系）",
                               $"✘ {_gpu.GenerationCn}: not supported (RTX 20-50 series only)"));
            else
                checks.Add(L.S($"✔ {_gpu.GenerationCn}（OptiScaler 路线自动适配：50 系用原版 runtime，20/30/40 系用跨代补丁版）",
                               $"✔ {_gpu.GenerationCn} (OptiScaler route auto-adapts: 50 series uses the original runtime, 20/30/40 series the cross-gen patched build)"));

            if (_gpu.IsNvidia && !_gpu.DriverOk)
                checks.Add(L.S("✘ 驱动过低：DLSS5 神经渲染需要 616.56+，请先更新驱动",
                               "✘ Driver too old: DLSS5 neural rendering needs 616.56+ — update your driver first"));
            else if (_gpu.IsNvidia)
                checks.Add(L.S("✔ 驱动版本满足要求（≥ 616.56）", "✔ Driver meets the requirement (≥ 616.56)"));

            var msfsLocked = _game2024 != null && UnlockedInstaller.CoreFileLocked(_game2024.GameDir);
            var msfs2020Locked = _game2020 != null && UnlockedInstaller.CoreFileLocked(_game2020.GameDir);
            var xpLocked = _gameXp12 != null && XP12Installer.CoreFileLocked(_gameXp12.GameDir);
            if (msfsLocked || msfs2020Locked || xpLocked)
                checks.Add(L.S("⚠ 游戏文件被占用（游戏未完全关闭或残留僵尸进程），请关闭游戏后重试",
                               "⚠ Game files are locked (game not fully closed or leftover processes) — close the game and retry"));
            else
                checks.Add(L.S("✔ 游戏文件未被占用", "✔ Game files are not locked"));

            if (_game2024 == null && _game2020 == null && _gameXp12 == null)
                checks.Add(L.S("✘ 未定位到任何支持的游戏", "✘ No supported game located"));
            _home.SetChecks(string.Join(Environment.NewLine, checks), checks.All(c => c.StartsWith('✔')));

            if (_gpu.SupportedThisVersion) _about.SetRecommendedScale(_gpu.RecommendedWorkingScale);

            _about.SetManualPaths(
                _game2024 != null ? $"{_game2024.GameDir}   [{_game2024.Source}]" : L.S("未检测到（可点击右侧按钮指定游戏主程序）", "Not detected (use the button on the right to pick the game executable)"),
                _game2020 != null ? $"{_game2020.GameDir}   [{_game2020.Source}]" : L.S("未检测到（可点击右侧按钮指定游戏主程序）", "Not detected (use the button on the right to pick the game executable)"),
                _gameXp12 != null ? $"{_gameXp12.GameDir}   [{_gameXp12.Source}]" : L.S("未检测到（可点击右侧按钮指定 X-Plane.exe）", "Not detected (use the button on the right to pick X-Plane.exe)"));

            _feedback.SetEnvironment(_gpu, _game2024, _game2020, _gameXp12);
        }
        catch (Exception ex)
        {
            Log(L.S("检测失败: ", "Detection failed: ") + ex.Message);
        }
        finally
        {
            _home.SetBusy(false);
        }
    }

    private void UpdateMsfs2024Card()
    {
        if (_game2024 == null)
        {
            _home.SetCard(HomePage.CardMsfs2024,
                L.S("未检测到安装", "Not detected"),
                L.S("未检测到安装。可在设置页「手动配置」中手动指定游戏目录。",
                    "No installation detected. You can set the game folder under \"Manual setup\" in Settings."),
                canInstall: false, canUninstall: false);
            return;
        }

        var state = UnlockedInstaller.DetectState(_game2024.GameDir);
        var aa = UserCfg.ReadAntiAliasing(UserCfg.PathFor(_game2024.ExeName) ?? "");
        var stateText = $"{state}   |   " + L.S($"游戏抗锯齿: {aa ?? "未知"}（需为 DLSS/DLAA）",
                                                $"In-game AA: {aa ?? "unknown"} (must be DLSS/DLAA)");
        var locked = UnlockedInstaller.CoreFileLocked(_game2024.GameDir);
        var canInstall = _gpu.SupportedThisVersion && _gpu.DriverOk && !locked;
        var canUninstall = !locked &&
            (File.Exists(Path.Combine(_game2024.GameDir, "OptiScaler.ini")) || UnlockedInstaller.LoadManifest() != null);
        _home.SetCard(HomePage.CardMsfs2024, stateText,
            $"{_game2024.GameDir}   [{_game2024.Source}]", canInstall, canUninstall);
    }

    private void UpdateMsfs2020Card()
    {
        if (_game2020 == null)
        {
            _home.SetCard(HomePage.CardMsfs2020,
                L.S("未检测到安装", "Not detected"),
                L.S("未检测到安装。可在设置页「手动配置」中手动指定游戏目录。",
                    "No installation detected. You can set the game folder under \"Manual setup\" in Settings."),
                canInstall: false, canUninstall: false);
            return;
        }

        var state = UnlockedInstaller.DetectState(_game2020.GameDir);
        var aa = UserCfg.ReadAntiAliasing(UserCfg.PathFor(_game2020.ExeName) ?? "");
        var stateText = $"{state}   |   " + L.S($"游戏抗锯齿: {aa ?? "未知"}（需为 DLSS/DLAA）",
                                                $"In-game AA: {aa ?? "unknown"} (must be DLSS/DLAA)");
        var locked = UnlockedInstaller.CoreFileLocked(_game2020.GameDir);
        var canInstall = _gpu.SupportedThisVersion && _gpu.DriverOk && !locked;
        var canUninstall = !locked &&
            (File.Exists(Path.Combine(_game2020.GameDir, "OptiScaler.ini")) || UnlockedInstaller.LoadManifest() != null);
        _home.SetCard(HomePage.CardMsfs2020, stateText,
            $"{_game2020.GameDir}   [{_game2020.Source}]", canInstall, canUninstall);
    }

    /// <summary>XP12 组件包状态文案（手动覆盖目录优先显示，否则显示缓存状态）。</summary>
    private static string KitStatusText()
    {
        if (Directory.Exists(AppConfig.KitDir))
            return AppConfig.KitDir + L.S("   [手动覆盖]", "   [manual override]");
        return PackageDownloader.IsCachedValid(PackageCatalog.Xp12Kit)
            ? L.S("已缓存（可离线重装）", "cached (offline reinstall available)")
            : L.S("安装时自动下载（约 150MB）", "auto-downloaded on install (~150MB)");
    }

    private void UpdateXp12Card()
    {
        if (_gameXp12 == null)
        {
            _home.SetCard(HomePage.CardXp12, L.S("未检测到安装", "Not detected"),
                L.S("未检测到安装。可在设置页「手动配置」中指定 X-Plane.exe 主程序。",
                    "No installation detected. Pick X-Plane.exe under \"Manual setup\" in Settings."),
                canInstall: false, canUninstall: false);
            return;
        }

        var state = XP12Installer.DetectState(_gameXp12.GameDir);
        var locked = XP12Installer.CoreFileLocked(_gameXp12.GameDir);
        var canInstall = !locked;
        var canUninstall = !locked &&
            (File.Exists(Path.Combine(_gameXp12.GameDir, "dlss5-feed.addon64")) || XP12Installer.LoadManifest() != null);
        _home.SetCard(HomePage.CardXp12, state,
            $"{_gameXp12.GameDir}   [{_gameXp12.Source}]   |   " + L.S($"组件包: {KitStatusText()}", $"Kit: {KitStatusText()}"),
            canInstall, canUninstall);
    }

    /// <summary>手动指定游戏主程序（idx: 0=MSFS 2024, 1=MSFS 2020, 2=XP12）。直接选 exe：exe 所在目录即游戏目录，商店版同样适用。持久化到 AppConfig。</summary>
    private void BrowseForGame(int targetIdx)
    {
        var t = GameLocator.Targets[targetIdx];
        var cnName = L.S(t.CnName, t.EnName);
        var cur = targetIdx switch { 0 => _game2024, 1 => _game2020, _ => _gameXp12 };
        using var dlg = new OpenFileDialog
        {
            Title = L.S($"选择 {cnName} 主程序（{t.ExeName}）", $"Select the {cnName} executable ({t.ExeName})"),
            Filter = $"{t.ExeName}|{t.ExeName}|{L.S("所有程序 (*.exe)|*.exe", "All executables (*.exe)|*.exe")}",
            // 不让对话框真的打开文件做校验（CheckFileExists 会尝试读取所选文件，
            // 在 ACL 受限的游戏目录会误报"没有打开该文件的权限"）；存在性由 FromManualExe 自行校验
            CheckFileExists = false,
            CheckPathExists = true,
        };
        if (cur != null && Directory.Exists(cur.GameDir)) dlg.InitialDirectory = cur.GameDir;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        // 注意不能用 File.Exists：ACL 受限目录里对真实存在的 exe 会误报 false（视为“不存在”）
        if (!GameLocator.FileExistsLoose(dlg.FileName))
        {
            MessageBox.Show(this,
                L.S("所选文件不存在或当前账户无法访问。请确认路径正确；若游戏目录权限受限，请以管理员身份运行本工具后重试。",
                    "The selected file does not exist or is not accessible. Verify the path; if the game folder has restricted permissions, run this tool as administrator and retry."),
                L.S("无效的程序文件", "Invalid executable"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var g = GameLocator.FromManualExe(dlg.FileName, t.ExeName);
        if (g == null)
        {
            MessageBox.Show(this,
                L.S($"请选择 {cnName} 的主程序 {t.ExeName}（微软商店版在 ...\\XboxGames\\...\\Content 目录内）。",
                    $"Please select {cnName}'s main executable {t.ExeName} (inside ...\\XboxGames\\...\\Content for the Store version)."),
                L.S("无效的程序文件", "Invalid executable"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        switch (targetIdx)
        {
            case 0: AppConfig.ManualExe24 = g.ExePath; break;
            case 1: AppConfig.ManualExe20 = g.ExePath; break;
            default: AppConfig.ManualXp12Exe = g.ExePath; break;
        }
        AppConfig.Save();
        Log(L.S($"已手动指定 {cnName} 主程序：{g.ExePath}", $"Manually set the {cnName} executable: {g.ExePath}"));
        _ = RefreshAsync();
    }

    private void PickKitDir()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = L.S("可选覆盖：指定 DLSS5-Feeder 组件包目录（留空/取消则自动下载官方组件包）。\n目录须含 dlss5-feed.addon64、deep-fried-chicken.addon64、nvngx_dlss*.dll、reshade-shaders。",
                              "Optional override: pick a DLSS5-Feeder kit folder (cancel to auto-download the official kit).\nThe folder must contain dlss5-feed.addon64, deep-fried-chicken.addon64, nvngx_dlss*.dll and reshade-shaders."),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var missing = XP12Installer.RequiredKitFiles
            .Where(f => !File.Exists(Path.Combine(dlg.SelectedPath, f))).ToList();
        if (missing.Count > 0)
        {
            MessageBox.Show(this, L.S("组件包不完整，缺少：", "The kit is incomplete, missing:") + "\n" + string.Join("\n", missing),
                L.S("无效组件包", "Invalid kit"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        AppConfig.KitDir = dlg.SelectedPath;
        AppConfig.Save();
        _ = RefreshAsync();
    }

    // ───────────────────────────── MSFS 安装 / 卸载（2024 正式版；2020 Beta，流程相同） ─────────────────────────────

    private async Task InstallMsfsAsync(GameInstall? game, bool beta)
    {
        if (game == null) return;
        if (!_gpu.SupportedThisVersion || !_gpu.DriverOk)
        {
            MessageBox.Show(this,
                L.S("当前 GPU 或驱动不满足要求（需 RTX 20-50 系、驱动 ≥ 616.56）。",
                    "Your GPU or driver does not meet the requirements (RTX 20-50 series and driver ≥ 616.56 required)."),
                L.S("无法安装", "Cannot install"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var name = beta ? L.S("MSFS 2020（Beta 实验版）", "MSFS 2020 (Beta, experimental)")
                        : L.S("MSFS 2024", "MSFS 2024");
        var betaNote = beta
            ? L.S("• ⚠ Beta 实验功能：流程与 2024 完全相同，但尚未经过 MSFS 2020 实机测试\n",
                  "• ⚠ BETA, experimental: identical flow to 2024, but not yet field-tested on MSFS 2020\n")
            : "";
        var confirm = MessageBox.Show(this,
            L.S($"即将为 {name} 安装 DLSS5 神经渲染（DLSS Unlocked / OptiScaler 路线）。\n\n" +
                "• 首次安装需联网下载组件包（约 440MB，自有服务器 + GitHub 多源自动切换）\n" +
                "• 下载后本地缓存，之后可离线重装；SHA256 校验保证安全\n" +
                "• 自动备份被修改的文件与配置，可一键回滚\n" +
                "• 安装后：游戏内按 Insert 键打开 OptiScaler 菜单\n" +
                betaNote + "\n" +
                "是否继续？",
                $"About to install DLSS5 neural rendering for {name} (DLSS Unlocked / OptiScaler route).\n\n" +
                "• First install downloads the package over the internet (~440MB, own server + GitHub fallback)\n" +
                "• The package is cached locally afterwards — offline reinstalls work; SHA256-verified\n" +
                "• Automatically backs up modified files and configs — one-click rollback\n" +
                "• After install: press Insert in game to open the OptiScaler menu\n" +
                betaNote + "\n" +
                "Continue?"),
            L.S("确认安装", "Confirm Installation"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        _home.SetBusy(true, L.S("准备...", "Preparing..."));
        try
        {
            var progress = new Progress<(long received, long total)>(t => _home.SetProgress(t.received, t.total));

            await UnlockedInstaller.InstallAsync(new UnlockedInstaller.InstallOptions
            {
                GameDir = game.GameDir,
                ExeName = game.ExeName,
                WorkingScale = _about.WorkingScale,
                Generation = _gpu.Generation,
                Log = Log,
                Progress = progress,
            });

            Log("──────────────────────────────");
            Log(L.S("安装完成！使用方法：", "Installation complete! How to use:"));
            Log(L.S("1. 启动游戏，进入飞行（抗锯齿需为 DLSS）", "1. Launch the game and start a flight (Anti-Aliasing must be DLSS)"));
            Log(L.S("2. 按 Insert 键打开 OptiScaler 菜单", "2. Press Insert to open the OptiScaler menu"));
            Log(L.S("3. 展开「DLSS Neural Rendering」→ 应显示 Running - xx ms per frame",
                    "3. Expand \"DLSS Neural Rendering\" → it should show Running - xx ms per frame"));
            Log(L.S("4. 帧数吃紧 → 调低 Model resolution；画面过猛 → 调低 Detail strength",
                    "4. Low FPS → lower Model resolution; too aggressive → lower Detail strength"));
            if (beta) Log(L.S("（MSFS 2020 为 Beta 实验功能，如有异常请一键卸载并在粉丝群反馈）",
                              "(MSFS 2020 support is BETA — if anything misbehaves, uninstall and report in the fan group)"));
            MessageBox.Show(this,
                L.S("安装完成！\n\n启动游戏后按 Insert 键打开 OptiScaler 菜单，\n展开 DLSS Neural Rendering 查看运行状态（应显示 Running）。",
                    "Installation complete!\n\nIn game, press Insert to open the OptiScaler menu,\nthen expand DLSS Neural Rendering to check its status (it should show Running)."),
                L.S("完成", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log(L.S("安装失败: ", "Installation failed: ") + ex.Message);
            MessageBox.Show(this, L.S("安装失败：", "Installation failed: ") + ex.Message,
                L.S("错误", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _home.SetBusy(false);
            await RefreshAsync();
        }
    }

    private async Task UninstallMsfsAsync(GameInstall? game, bool beta)
    {
        if (game == null) return;
        if (MessageBox.Show(this,
                L.S("将删除本工具安装的全部文件并恢复游戏配置。\n是否继续？",
                    "This will remove all files installed by this tool and restore the game's configuration.\nContinue?"),
                L.S("确认卸载", "Confirm Uninstallation"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _home.SetBusy(true, L.S("卸载中...", "Uninstalling..."));
        try
        {
            await Task.Run(() => UnlockedInstaller.Uninstall(game.GameDir, Log));
            Log(L.S("卸载完成。", "Uninstalled."));
        }
        catch (Exception ex)
        {
            Log(L.S("卸载失败: ", "Uninstall failed: ") + ex.Message);
        }
        finally
        {
            _home.SetBusy(false);
            await RefreshAsync();
        }
    }

    // ───────────────────────────── XP12 安装 / 卸载 ─────────────────────────────

    private async Task InstallXp12Async()
    {
        if (_gameXp12 == null) return;

        var confirm = MessageBox.Show(this,
            L.S("即将为 X-Plane 12 安装 DLSS5（DLSS5-Feeder 路线：合成 DLSS 契约 + 运动矢量估算 + 神经渲染）。\n\n" +
                "• 组件包无需手动准备：首次安装自动从服务器下载（约 150MB，SHA256 校验），之后离线可重装\n" +
                "• 注册 ReShade Vulkan 隐式层（机器级）并安装 Feeder/消费者组件\n" +
                "• 运动矢量为着色器估算，快速移动视角会有重影（方案固有特性）\n" +
                "• 安装后：完全重启 X-Plane，DRME 与 DLSS 5 Feed 已由安装器自动启用\n\n" +
                "是否继续？",
                "About to install DLSS5 for X-Plane 12 (DLSS5-Feeder route: synthesized DLSS contract + estimated motion vectors + neural rendering).\n\n" +
                "• No manual kit needed: the first install auto-downloads it (~150MB, SHA256-verified); offline reinstalls afterwards\n" +
                "• Registers the ReShade Vulkan implicit layer (machine-wide) and installs Feeder/consumer components\n" +
                "• Motion vectors are shader-estimated; fast camera moves show ghosting (inherent to the approach)\n" +
                "• After install: fully restart X-Plane — DRME and DLSS 5 Feed are enabled by the installer automatically\n\n" +
                "Continue?"),
            L.S("确认安装", "Confirm Installation"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        _home.SetBusy(true, L.S("XP12 安装中...", "Installing for XP12..."));
        try
        {
            await XP12Installer.InstallAsync(new XP12Installer.InstallOptions
            {
                GameDir = _gameXp12.GameDir,
                ExePath = _gameXp12.ExePath,
                KitDir = AppConfig.KitDir,
                Log = Log,
            });
            MessageBox.Show(this,
                L.S("安装完成！\n\n请完全退出并重新启动 X-Plane（若正在运行），\n按 Home 键确认 DRME 与 DLSS 5 Feed 已勾选；\n若 Deep Fried Chicken 显示 neural feature disabled，\n点 Refresh neural contract 或再次重启游戏即可激活。",
                    "Installation complete!\n\nFully restart X-Plane if it was running,\npress Home to verify DRME and DLSS 5 Feed are ticked;\nif Deep Fried Chicken shows \"neural feature disabled\",\nclick Refresh neural contract or restart the game again."),
                L.S("完成", "Done"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log(L.S("安装失败: ", "Installation failed: ") + ex.Message);
            MessageBox.Show(this, L.S("安装失败：", "Installation failed: ") + ex.Message,
                L.S("错误", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _home.SetBusy(false);
            await RefreshAsync();
        }
    }

    private async Task UninstallXp12Async()
    {
        if (_gameXp12 == null) return;
        if (MessageBox.Show(this,
                L.S("将删除 XP12 中本工具安装的全部文件、注销 Vulkan 层并恢复原状。\n是否继续？",
                    "This will remove all files installed by this tool in XP12, unregister the Vulkan layer and restore the original state.\nContinue?"),
                L.S("确认卸载", "Confirm Uninstallation"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _home.SetBusy(true, L.S("卸载中...", "Uninstalling..."));
        try
        {
            await Task.Run(() => XP12Installer.Uninstall(_gameXp12.GameDir, _gameXp12.ExePath, Log));
            Log(L.S("XP12 卸载完成。", "XP12 uninstalled."));
        }
        catch (Exception ex)
        {
            Log(L.S("卸载失败: ", "Uninstall failed: ") + ex.Message);
        }
        finally
        {
            _home.SetBusy(false);
            await RefreshAsync();
        }
    }
}
