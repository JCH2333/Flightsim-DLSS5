using System.Reflection;
using System.Runtime.InteropServices;

namespace DLSS5Patcher.Core;

/// <summary>
/// 桌面快捷方式创建（WScript.Shell 后期绑定，无外部依赖）。
/// XP12 安装后用它生成带 --allow_reshade 参数的启动快捷方式。
/// 说明：IShellLink 直接互调在本运行时下编组异常，故走 IDispatch 后期绑定（稳定可靠）。
/// </summary>
public static class ShortcutHelper
{
    public static string DesktopPath => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string ShortcutPath(string name) => Path.Combine(DesktopPath, name + ".lnk");

    public static bool Exists(string name) => File.Exists(ShortcutPath(name));

    public static void Delete(string name)
    {
        try { File.Delete(ShortcutPath(name)); } catch { /* 不存在/被占用则忽略 */ }
    }

    public static void Create(string name, string targetPath, string arguments, string workingDir, string description)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell 不可用");
        var shell = Activator.CreateInstance(shellType);
        try
        {
            var lnk = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                new object[] { ShortcutPath(name) });
            var t = lnk.GetType();
            foreach (var (member, value) in new (string Member, object Value)[]
            {
                ("TargetPath", targetPath),
                ("Arguments", arguments ?? ""),
                ("WorkingDirectory", workingDir ?? ""),
                ("Description", description ?? ""),
                ("WindowStyle", 1),
            })
            {
                try { t.InvokeMember(member, BindingFlags.SetProperty, null, lnk, new object[] { value }); }
                catch (Exception ex) { throw new InvalidOperationException($"设置 {member} 失败", ex); }
            }
            t.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
        }
        finally
        {
            if (shell != null) Marshal.ReleaseComObject(shell);
        }
    }
}
