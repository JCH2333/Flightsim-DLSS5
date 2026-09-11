using DLSS5Patcher.Core;

namespace DLSS5Patcher;

/// <summary>命令行模式：--detect | --install [scale] [--proxy url] | --uninstall</summary>
internal static class Cli
{
    public static int Run(string[] args)
    {
        void Log(string s) => Console.WriteLine(s);

        var gpu = GpuInfo.Detect();
        Log(L.S($"GPU: {gpu.Name} | 驱动 {gpu.Driver} | {gpu.GenerationCn}", $"GPU: {gpu.Name} | driver {gpu.Driver} | {gpu.GenerationCn}"));

        switch (args[0].ToLowerInvariant())
        {
            case "--detect":
            {
                foreach (var (appId, exe, cn, en) in GameLocator.Targets)
                {
                    var g = GameLocator.Find(appId, exe);
                    if (g == null) { Log($"{L.S(cn, en)}: {L.S("未检测到", "not detected")}"); continue; }
                    Log($"{L.S(cn, en)}: {g.GameDir} [{g.Source}]");
                    var state = exe.Contains("X-Plane")
                        ? XP12Installer.DetectState(g.GameDir)
                        : UnlockedInstaller.DetectState(g.GameDir);
                    Log(L.S($"  状态: {state}", $"  Status: {state}"));
                    if (!exe.Contains("X-Plane"))
                    {
                        var cfg = UserCfg.PathFor(exe);
                        Log(L.S($"  抗锯齿: {UserCfg.ReadAntiAliasing(cfg!) ?? "未知"}", $"  Anti-aliasing: {UserCfg.ReadAntiAliasing(cfg!) ?? "unknown"}"));
                    }
                }
                return 0;
            }
            case "--install":
            {
                var game = GameLocator.Find(GameLocator.Targets[0].AppId, GameLocator.Targets[0].ExeName)
                          ?? throw new InvalidOperationException(L.S("未检测到 MSFS 2024", "MSFS 2024 not detected"));
                if (UnlockedInstaller.CoreFileLocked(game.GameDir))
                {
                    Log(L.S("游戏文件被占用（游戏未完全关闭？），请关闭后重试。", "Game files are locked (game not fully closed?). Close it and retry."));
                    return 1;
                }
                string scale = args.Length > 1 ? args[1] : gpu.RecommendedWorkingScale;
                Log(L.S($"GPU 世代: {gpu.GenerationCn} → 默认 WorkingScale={scale}", $"GPU generation: {gpu.GenerationCn} → default WorkingScale={scale}"));
                var m = UnlockedInstaller.InstallAsync(new UnlockedInstaller.InstallOptions
                {
                    GameDir = game.GameDir,
                    ExeName = game.ExeName,
                    WorkingScale = scale,
                    Generation = gpu.Generation,
                    Log = Log,
                    Progress = null,
                }).GetAwaiter().GetResult();
                Log(L.S($"安装完成：{m.Tag}。启动游戏按 Insert 键打开 OptiScaler 菜单。", $"Install complete: {m.Tag}. In game press Insert to open the OptiScaler menu."));
                return 0;
            }
            case "--uninstall":
            {
                var game = GameLocator.Find(GameLocator.Targets[0].AppId, GameLocator.Targets[0].ExeName);
                if (game == null) { Log(L.S("未检测到 MSFS 2024", "MSFS 2024 not detected")); return 1; }
                if (UnlockedInstaller.CoreFileLocked(game.GameDir))
                {
                    Log(L.S("游戏文件被占用（游戏未完全关闭？），请关闭后重试。", "Game files are locked (game not fully closed?). Close it and retry."));
                    return 1;
                }
                var (files, _) = UnlockedInstaller.Uninstall(game.GameDir, Log);
                Log(L.S($"卸载完成（{files} 个文件）。", $"Uninstall complete ({files} files)."));
                return 0;
            }
            case "--install-xp12":
            {
                var game = GameLocator.Find(GameLocator.Targets[2].AppId, GameLocator.Targets[2].ExeName)
                          ?? throw new InvalidOperationException("未检测到 X-Plane 12");
                if (XP12Installer.CoreFileLocked(game.GameDir))
                {
                    Log(L.S("游戏文件被占用（游戏未完全关闭？），请关闭后重试。", "Game files are locked (game not fully closed?). Close it and retry."));
                    return 1;
                }
                // kit 目录：参数或默认教程包位置
                var kit = args.Length > 2 && args[1] == "--kit" ? args[2]
                    : args.Length > 1 ? args[1]
                    : @"F:\我的世界动画\AI项目\DLSS5\网络资源\DLSS5";
                if (!Directory.Exists(kit)) { Log(L.S($"组件包目录不存在: {kit}", $"Kit folder does not exist: {kit}")); return 1; }
                var m = XP12Installer.InstallAsync(new XP12Installer.InstallOptions
                {
                    GameDir = game.GameDir,
                    ExePath = game.ExePath,
                    KitDir = kit,
                    Log = Log,
                }).GetAwaiter().GetResult();
                Log(L.S($"安装完成：{m.Tag}。完全重启 X-Plane 后按 Home 键确认 DRME 与 DLSS 5 Feed 已勾选；若 DFC 显示 neural feature disabled，点 Refresh neural contract 或再重启一次。", $"Install complete: {m.Tag}. Fully restart X-Plane, press Home to verify DRME and DLSS 5 Feed are ticked; if DFC shows neural feature disabled, click Refresh neural contract or restart once more."));
                return 0;
            }
            case "--uninstall-xp12":
            {
                var game = GameLocator.Find(GameLocator.Targets[2].AppId, GameLocator.Targets[2].ExeName);
                if (game == null) { Log(L.S("未检测到 X-Plane 12", "X-Plane 12 not detected")); return 1; }
                if (XP12Installer.CoreFileLocked(game.GameDir))
                {
                    Log(L.S("游戏文件被占用（游戏未完全关闭？），请关闭后重试。", "Game files are locked (game not fully closed?). Close it and retry."));
                    return 1;
                }
                var (files, _) = XP12Installer.Uninstall(game.GameDir, game.ExePath, Log);
                Log(L.S($"XP12 卸载完成（{files} 个文件）。", $"XP12 uninstall complete ({files} files)."));
                return 0;
            }
            default:
                Log(L.S("用法: --detect | --install [scale] | --uninstall | --install-xp12 [--kit 目录] | --uninstall-xp12", "Usage: --detect | --install [scale] | --uninstall | --install-xp12 [--kit dir] | --uninstall-xp12"));
                return 2;
        }
    }
}
