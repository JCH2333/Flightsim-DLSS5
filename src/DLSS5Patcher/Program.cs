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

        Application.Run(new MainForm());
        return 0;
    }
}
