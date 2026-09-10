using DLSS5Patcher.Core;
using DLSS5Patcher.Ui;

namespace DLSS5Patcher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
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
