using DLSS5Patcher.Core;
using DLSS5Patcher.Ui;

namespace DLSS5Patcher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        AppLog.Init();
        AppLog.Info($"启动 / launch — args: {(args.Length > 0 ? string.Join(' ', args) : "(GUI)")}");

        // 全局异常都落日志（反馈时自动附带，便于定位）
        Application.ThreadException += (_, e) => AppLog.Error("UI 线程异常", e.Exception);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Error("未处理异常", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("未观察任务异常", e.Exception);
            e.SetObserved();
        };

        ApplicationConfiguration.Initialize();

        AppConfig.Load();
        L.English = AppConfig.Lang == "en";

        // CLI 模式：--detect | --install [WorkingScale] | --uninstall | --install-xp12 | --uninstall-xp12
        if (args.Length > 0)
        {
            return Cli.Run(args);
        }

        // 首次启动：先选择界面语言
        if (AppConfig.Lang is not ("zh" or "en"))
        {
            using var dlg = new LanguageDialog();
            dlg.ShowDialog();
            L.English = AppConfig.Lang == "en";
        }

        // 用户协议：未同意当前修订号时强制展示；不同意则直接退出（滚轮路由需先于弹窗安装）
        if (AppConfig.AgreedRevision != Ui.AgreementContent.Revision)
        {
            Theme.WheelRouter.Install();
            AppLog.Info(L.S("首次使用：展示用户协议与免责声明", "First run: showing the user agreement and disclaimer"));
            using var agree = new AgreementDialog(fromSettings: false);
            if (agree.ShowDialog() != DialogResult.OK)
            {
                AppLog.Info(L.S("用户未同意协议，程序退出", "Agreement declined — exiting"));
                return 0;
            }
            AppLog.Info(L.S($"已同意协议（修订 {AppConfig.AgreedRevision}，{AppConfig.AgreedAt}）", $"Agreement accepted (revision {AppConfig.AgreedRevision}, {AppConfig.AgreedAt})"));
        }

        Application.Run(new MainForm());
        return 0;
    }
}
