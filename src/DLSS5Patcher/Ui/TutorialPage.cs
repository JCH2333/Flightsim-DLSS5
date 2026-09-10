using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>教程页：安装/使用说明 + 出问题时应向作者提交的文件清单。</summary>
public sealed class TutorialPage : UserControl
{
    private readonly RichTextBox _rtb = new();

    public TutorialPage()
    {
        BackColor = Theme.Bg;
        Size = new Size(880, 800);

        Controls.Add(Theme.MakePageTitle(L.S("使用教程", "Tutorial"), 24, 16));

        var panel = new Panel { Location = new Point(24, 52), Size = new Size(832, 726), BackColor = Theme.Surface, Padding = new Padding(10) };
        Theme.EnableBorder(panel, Theme.Border);

        _rtb.Dock = DockStyle.Fill;
        _rtb.BackColor = Theme.Surface;
        _rtb.BorderStyle = BorderStyle.None;
        _rtb.ReadOnly = true;
        _rtb.Font = new Font("Microsoft YaHei UI", 9f);
        _rtb.ScrollBars = RichTextBoxScrollBars.Vertical;
        panel.Controls.Add(_rtb);
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

    private void Heading(string text) { Add(text + "\r\n", Theme.Accent, 10.5f, bold: true); }
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
        Line(L.S("2. 工具会自动下载约 460MB 组件包并校验（可在代理框填入代理地址，直连失败时使用）。",
                 "2. The tool downloads and verifies a ~460MB component package (fill in the proxy box if a direct connection fails)."));
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
        Gap();

        Heading(L.S("三、X-Plane 12（DLSS5-Feeder · Vulkan 路线）", "3. X-Plane 12 (DLSS5-Feeder · Vulkan route)"));
        Line(L.S("1. 在 XP12 卡片点击「选择组件包...」，选中包含以下文件的组件包目录：",
                 "1. On the XP12 card click Select Kit... and choose the kit folder that contains:"));
        Line(L.S("   dlss5-feed.addon64、deep-fried-chicken.addon64、nvngx_dlss*.dll、reshade-shaders 文件夹。",
                 "   dlss5-feed.addon64, deep-fried-chicken.addon64, nvngx_dlss*.dll and the reshade-shaders folder."));
        Line(L.S("2. 点击「一键安装」。工具会注册 ReShade Vulkan 隐式层（机器级注册表）并安装组件。",
                 "2. Click Install. The tool registers the ReShade Vulkan implicit layer (machine-wide registry) and installs components."));
        Line(L.S("3. 安装后启动 X-Plane，按 Home 键打开 ReShade 菜单，确认：",
                 "3. Launch X-Plane, press Home to open the ReShade menu and verify:"));
        Line(L.S("   · MotionEstimation 已启用（运动矢量估算）",
                 "   · MotionEstimation is enabled (estimated motion vectors)"));
        Line(L.S("   · DLSS5_Feed 已启用（神经渲染数据供给）",
                 "   · DLSS5_Feed is enabled (neural-rendering data feed)"));
        Line(L.S("   · Deep Fried Chicken 标签页显示 standalone neural pipeline active。",
                 "   · The Deep Fried Chicken tab shows standalone neural pipeline active."));
        Line(L.S("4. 已知特性：运动矢量为着色器估算，快速移动视角时会有短暂重影，属方案固有行为。",
                 "4. Known trait: motion vectors are shader-estimated, so fast camera moves show brief ghosting — inherent to this approach."));
        Line(L.S("5. XP12 本体 4K 渲染帧间隔约 105ms 属正常（本体渲染耗时），DLSS5 供给仅占约 1.5ms。",
                 "5. A ~105ms frame time at 4K is normal for X-Plane itself (its own rendering cost); the DLSS5 feed adds only ~1.5ms."));
        Gap();

        Heading(L.S("四、卸载与恢复", "4. Uninstall & Restore"));
        Line(L.S("· 各游戏卡片上的「卸载」按钮会删除本工具安装的全部文件、注销注册表项并还原配置。",
                 "· Each game card's Uninstall button removes every file installed by this tool, unregisters registry entries and restores configs."));
        Line(L.S("· 卸载不会动你的存档、插件与官方游戏本体文件（被修改的文件均有备份还原）。",
                 "· Uninstall never touches your saves, add-ons or official game files (everything modified is backed up and restored)."));
        Gap();

        Heading(L.S("五、出问题时应向作者提交的文件", "5. What to Send the Author When Something Goes Wrong"));
        Line(L.S("安装失败或游戏内不生效时，请在 QQ 群（615523002）提交以下材料：",
                 "If installation fails or the effect doesn't show in game, please provide the following in the QQ group (615523002):"));
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

        Heading(L.S("六、常见问题", "6. FAQ"));
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
