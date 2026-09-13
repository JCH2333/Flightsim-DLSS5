using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>教程页：安装/使用说明 + 出问题时应向作者提交的文件清单。</summary>
public sealed class TutorialPage : Theme.AmbientPage
{
    private readonly RichTextBox _rtb = new();

    public TutorialPage()
    {
        Size = new Size(862, 800);

        Controls.Add(Theme.MakePageHeader(L.S("TUTORIAL", "TUTORIAL"), L.S("使用教程", "Tutorial")));

        var panel = new Theme.GlassCard
        {
            Location = new Point(36, 100),
            Size = new Size(790, 676),
            Fill = Theme.SurfaceInset,
            BorderColor = Theme.Border,
            Radius = 10,
            Padding = new Padding(16),
        };

        _rtb.Dock = DockStyle.Fill;
        _rtb.BackColor = Theme.SurfaceInset;
        _rtb.BorderStyle = BorderStyle.None;
        _rtb.ReadOnly = true;
        _rtb.Font = new Font(Theme.FontUi, 9.25f);
        _rtb.ScrollBars = RichTextBoxScrollBars.None;
        panel.Controls.Add(_rtb);
        Theme.AttachScrollIndicator(_rtb, panel, rightInset: 12, topInset: 14, height: panel.Height - 28);
        Controls.Add(panel);

        BuildContent();
    }

    private void Add(string text, Color color, float size = 9f, bool bold = false)
    {
        _rtb.SelectionStart = _rtb.TextLength;
        _rtb.SelectionColor = color;
        _rtb.SelectionFont = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
        _rtb.AppendText(text);
    }

    private void Heading(string text) { Add(text + "\r\n", Theme.Signal, 10.5f, bold: true); }
    private void Line(string text) { Add(text + "\r\n", Theme.TextSecondary); }
    private void Gap() { Add("\r\n", Theme.TextSecondary); }

    private void BuildContent()
    {
        Heading(L.S("一、安装前准备（所有游戏通用）", "1. Before You Install (all games)"));
        Line(L.S("1. 显卡需为 NVIDIA RTX 20 / 30 / 40 / 50 系（DLSS5 神经渲染不支持 GTX）。",
                 "1. You need an NVIDIA RTX 20 / 30 / 40 / 50 series GPU (GTX is not supported by DLSS5 neural rendering)."));
        Line(L.S("2. 显卡驱动更新到 616.56 或更高（GeForce Experience 或 nvidia.cn 下载）。",
                 "2. Update your GPU driver to 616.56 or newer (via GeForce Experience or nvidia.com)."));
        Line(L.S("3. 完全关闭游戏后再安装，包括后台残留进程（任务管理器确认）。",
                 "3. Fully close the game before installing, including leftover background processes (check Task Manager)."));
        Line(L.S("4. 如杀毒软件报毒，请将本工具与游戏目录加入信任（补丁需替换游戏目录文件，属正常误报）。",
                 "4. If your antivirus flags the tool, add it and the game folder to trust (patching replaces files inside the game folder — a known false positive)."));
        Gap();

        Heading(L.S("二、微软模拟飞行 2024（OptiScaler 路线）", "2. Microsoft Flight Simulator 2024 (OptiScaler route)"));
        Line(L.S("1. 在「一键安装」页的 MSFS 2024 卡片点击「一键安装」。",
                 "1. On the One-Click Install page, click Install on the MSFS 2024 card."));
        Line(L.S("2. 首次安装自动从服务器下载组件包（约 440MB，服务器 + GitHub 多源自动切换，SHA256 校验）；下载后本地缓存，之后可离线重装。",
                 "2. The first install auto-downloads the package (~440MB; own server with GitHub fallback, SHA256-verified). It is cached locally — offline reinstalls afterwards."));
        Line(L.S("3. 安装过程自动备份被修改的文件与配置，可随时「卸载」一键还原。",
                 "3. Modified files and configs are backed up automatically; Uninstall restores everything at any time."));
        Line(L.S("4. 安装完成后启动游戏，进入「图形」设置，把抗锯齿设为 DLSS 或 DLAA（必须）。",
                 "4. Launch the game, open Graphics settings and set Anti-Aliasing to DLSS or DLAA (required)."));
        Line(L.S("5. 飞行中按 Insert 键打开 OptiScaler 菜单，展开「DLSS Neural Rendering」，",
                 "5. In flight, press Insert to open the OptiScaler menu and expand \"DLSS Neural Rendering\" —"));
        Line(L.S("   应显示 Running - xx ms per frame，即神经渲染已在工作。",
                 "   it should show Running - xx ms per frame, meaning neural rendering is active."));
        Line(L.S("6. 调优建议：帧数吃紧 → 调低 Model resolution；画面锐化过猛 → 调低 Detail strength。",
                 "6. Tuning: low FPS → lower Model resolution; over-sharpened image → lower Detail strength."));
        Line(L.S("7. 游戏内如出现「Update v0.9.4」更新提示，请勿点击（主线版本不含神经渲染，会覆盖补丁）。",
                 "7. If the OptiScaler menu offers \"Update v0.9.4\", do NOT accept (the mainline build lacks neural rendering and would overwrite the patch)."));
        Line(L.S("8. 微软商店 / Xbox 版：游戏装在 <盘>:\\XboxGames 下可自动检测；装在自定义位置时，",
                 "8. Microsoft Store / Xbox version: auto-detection works when the game is under <drive>:\\XboxGames; if installed elsewhere,"));
        Line(L.S("   请在「设置 / 关于」页的「手动配置」中直接选择游戏主程序 exe（商店版 exe 在 Content 目录内）。",
                 "   pick the game's executable directly under \"Manual setup\" on the Settings / About page (the Store exe lives inside the Content folder)."));
        Gap();

        Heading(L.S("三、微软模拟飞行 2020（Beta 实验性）", "3. Microsoft Flight Simulator 2020 (BETA, experimental)"));
        Line(L.S("1. 安装流程与 MSFS 2024 完全相同：在 MSFS 2020 卡片点击「一键安装」即可。",
                 "1. The flow is identical to MSFS 2024: click Install on the MSFS 2020 card."));
        Line(L.S("2. ⚠ Beta 实验功能：尚未经过 MSFS 2020 实机测试（其 DX11 渲染器可能不兼容），",
                 "2. ⚠ BETA, experimental: not yet field-tested on MSFS 2020 (its DX11 renderer may be incompatible)."));
        Line(L.S("   如遇异常请先「一键卸载」还原，并到粉丝群反馈。",
                 "   If anything misbehaves, use Uninstall to restore and report it in the fan group."));
        Gap();

        Heading(L.S("四、X-Plane 12（DLSS5-Feeder · Vulkan 路线）", "4. X-Plane 12 (DLSS5-Feeder · Vulkan route)"));
        Line(L.S("1. 组件包无需手动准备：点击「一键安装」时自动从服务器下载（约 150MB，仅首次，之后离线可重装）。",
                 "1. No manual kit needed: clicking Install auto-downloads it from our server (~150MB, first time only; offline reinstalls afterwards)."));
        Line(L.S("   高级用户可在「设置 / 关于」页手动指定组件包目录（须含 dlss5-feed.addon64、deep-fried-chicken.addon64、nvngx_dlss*.dll、reshade-shaders）。",
                 "   Advanced users can point at a kit folder in Settings · About (it must contain dlss5-feed.addon64, deep-fried-chicken.addon64, nvngx_dlss*.dll and reshade-shaders)."));
        Line(L.S("2. 点击「一键安装」。工具会注册 ReShade Vulkan 隐式层（机器级注册表）并安装组件。",
                 "2. Click Install. The tool registers the ReShade Vulkan implicit layer (machine-wide registry) and installs components."));
        Line(L.S("3. 安装后启动 X-Plane（若之前开着请完全退出后重开），按 Home 键打开 ReShade 菜单，在 DLSS5-Feeder 预设里确认：",
                 "3. Launch X-Plane (fully restart it if it was running), press Home to open the ReShade menu and check the DLSS5-Feeder preset:"));
        Line(L.S("   · DRME [MotionEstimation.fx] 已勾选（着色器运动矢量估算），且排在 DLSS 5 Feed 之上",
                 "   · DRME [MotionEstimation.fx] is ticked (shader motion-vector estimation) and sits above DLSS 5 Feed"));
        Line(L.S("   · DLSS 5 Feed 已勾选（两者默认已由安装器配好，无需手动操作）",
                 "   · DLSS 5 Feed is ticked (both are configured by the installer automatically)"));
        Line(L.S("4. 首次安装后若 Deep Fried Chicken 菜单显示 neural feature disabled until native recreate/restart，",
                 "4. If the Deep Fried Chicken tab shows \"neural feature disabled until native recreate/restart\" on first run,"));
        Line(L.S("   请点击 Refresh neural contract 按钮或完全重启 X-Plane 即可激活。",
                 "   click Refresh neural contract or fully restart X-Plane to activate the neural pipeline."));
        Line(L.S("5. 已知特性：运动矢量为着色器估算，快速移动视角时会有短暂重影，属方案固有行为。",
                 "5. Known trait: motion vectors are shader-estimated, so fast camera moves show brief ghosting — inherent to this approach."));
        Line(L.S("5. XP12 本体 4K 渲染帧间隔约 105ms 属正常（本体渲染耗时），DLSS5 供给仅占约 1.5ms。",
                 "6. A ~105ms frame time at 4K is normal for X-Plane itself (its own rendering cost); the DLSS5 feed adds only ~1.5ms."));
        Gap();

        Heading(L.S("五、卸载与恢复", "5. Uninstall & Restore"));
        Line(L.S("· 各游戏卡片上的「卸载」按钮会删除本工具安装的全部文件、注销注册表项并还原配置。",
                 "· Each game card's Uninstall button removes every file installed by this tool, unregisters registry entries and restores configs."));
        Line(L.S("· 卸载不会动你的存档、插件与官方游戏本体文件（被修改的文件均有备份还原）。",
                 "· Uninstall never touches your saves, add-ons or official game files (everything modified is backed up and restored)."));
        Gap();

        Heading(L.S("六、出问题时应向作者提交的文件", "6. What to Send the Author When Something Goes Wrong"));
        Line(L.S("安装失败或游戏内不生效时，请在 B站 一只剑齿虎呀 粉丝群（QQ 群：615523002）提交以下材料：",
                 "If installation fails or the effect doesn't show in game, please provide the following in the Bilibili fan group of '一只剑齿虎呀' (QQ group: 615523002):"));
        Line(L.S("1. 本工具主页面日志框的全部内容（点进日志框 Ctrl+A 全选复制）。",
                 "1. The full content of the log box on the tool's main page (click into it, Ctrl+A, copy)."));
        Line(L.S("2. 显卡型号与驱动版本（工具顶部已显示，截图即可）。",
                 "2. GPU model and driver version (shown at the top of the tool — a screenshot is fine)."));
        Line(L.S("3. MSFS 2024 问题另附：",
                 "3. For MSFS 2024 issues, also attach:"));
        Line(L.S("   · 游戏目录截图（能看到 dxgi.dll、OptiScaler.ini、nvngx_dlssnr.dll 是否存在）；",
                 "   · A screenshot of the game folder (showing whether dxgi.dll, OptiScaler.ini and nvngx_dlssnr.dll exist);"));
        Line(L.S("   · 游戏内 OptiScaler 菜单「DLSS Neural Rendering」展开后的截图；",
                 "   · The in-game OptiScaler menu with \"DLSS Neural Rendering\" expanded;"));
        Line(L.S("   · 游戏图形设置中抗锯齿选项的截图。",
                 "   · The Anti-Aliasing option in the game's graphics settings."));
        Line(L.S("4. XP12 问题另附：",
                 "4. For XP12 issues, also attach:"));
        Line(L.S("   · X-Plane 12 目录下的 Log.txt（XP 自带日志）；",
                 "   · Log.txt from the X-Plane 12 folder (XP's own log);"));
        Line(L.S("   · 游戏目录截图（能看到 dlss5-feed.addon64、deep-fried-chicken.addon64 是否存在）；",
                 "   · A screenshot of the game folder (showing whether dlss5-feed.addon64 and deep-fried-chicken.addon64 exist);"));
        Line(L.S("   · ReShade 菜单（Home 键）中 Deep Fried Chicken 标签页的截图。",
                 "   · The Deep Fried Chicken tab of the ReShade menu (Home key)."));
        Line(L.S("5. 你的操作系统版本（Win10 / Win11）。",
                 "5. Your Windows version (Win10 / Win11)."));
        Gap();

        Heading(L.S("七、常见问题", "7. FAQ"));
        Line(L.S("· 开 DLSS5 后游戏爆显存崩溃（DXGI_ERROR_DEVICE_REMOVED / 提示资源使用超出GPU内存容量）：",
                 "· Game crashes with VRAM overflow while DLSS5 is on (DXGI_ERROR_DEVICE_REMOVED / \"resources exceed GPU memory\" toast):"));
        Line(L.S("  8GB 显存显卡属高发。解决：设置页把 WorkingScale 降到 0.5（或 0.35）后重装，",
                 "  common on 8GB cards. Fix: set WorkingScale to 0.5 (or 0.35) in Settings and reinstall,"));
        Line(L.S("  并在游戏内降低纹理/地形分辨率；安装器已按显存自动推荐 WorkingScale。",
                 "  and lower texture/terrain resolution in game. The installer now auto-recommends WorkingScale by VRAM."));
        Line(L.S("· 提示「驱动过低」：DLSS5 神经渲染 runtime 要求驱动 ≥ 616.56，先升级驱动。",
                 "· \"Driver too old\": the DLSS5 neural-render runtime needs driver ≥ 616.56 — update first."));
        Line(L.S("· 提示「游戏文件被占用」：游戏未完全关闭或有崩溃残留进程，重启电脑后重试。",
                 "· \"Game files locked\": the game isn't fully closed or a crashed process remains — reboot and retry."));
        Line(L.S("· RTX 50 系：安装时自动使用 NVIDIA 原版神经渲染 runtime（310.8.0）覆盖，无需手动处理。",
                 "· RTX 50 series: the tool automatically uses NVIDIA's original neural-render runtime (310.8.0) — nothing to do manually."));
        Line(L.S("· 4090 机型若帧数异常，可在设置页把 WorkingScale 调到 0.75 重装。",
                 "· If an RTX 4090 shows abnormal FPS, set WorkingScale to 0.75 in Settings and reinstall."));
    }
}
