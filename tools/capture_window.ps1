# Capture DLSS5Patcher window CONTENT via PrintWindow (background-safe).
# 位图按窗口所在显示器的物理尺寸（系统 DPI 虚拟化修正）。
param([Parameter(Mandatory=$true)][string]$Out)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public class PW {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern int GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
  public struct RECT { public int L, T, R, B; }
}
'@
[PW]::SetProcessDPIAware() | Out-Null
$p = Get-Process DLSS5Patcher -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$h = $p.MainWindowHandle
$r = New-Object PW+RECT
[PW]::GetWindowRect($h, [ref]$r) | Out-Null
$sysDpi = [PW]::GetDpiForSystem()
$winDpi = [PW]::GetDpiForWindow($h)
$scale = $winDpi / $sysDpi
$w = [int](($r.R - $r.L) * $scale); $ht = [int](($r.B - $r.T) * $scale)
$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [PW]::PrintWindow($h, $hdc, 2)
$g.ReleaseHdc($hdc); $g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output ("saved $Out (" + $w + 'x' + $ht + ') printOk=' + $ok + ' winDpi=' + $winDpi)
