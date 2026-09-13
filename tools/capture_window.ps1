# Capture DLSS5Patcher window CONTENT via PrintWindow (background-safe: no focus steal, no z-order need).
param([Parameter(Mandatory=$true)][string]$Out)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class PW {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  public struct RECT { public int L, T, R, B; }
}
'@
[PW]::SetProcessDPIAware() | Out-Null
$p = Get-Process DLSS5Patcher -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$h = $p.MainWindowHandle
$r = New-Object PW+RECT
[PW]::GetWindowRect($h, [ref]$r) | Out-Null
$w = [int]($r.R) - [int]($r.L); $ht = [int]($r.B) - [int]($r.T)
$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
# 2 = PW_RENDERFULLCONTENT (需要 DirectUI/硬件加速窗口的完整内容)
$ok = [PW]::PrintWindow($h, $hdc, 2)
$g.ReleaseHdc($hdc)
$g.Dispose()
if (-not $ok) { $bmp.Dispose(); throw 'PrintWindow failed' }
$bmp.Save($Out); $bmp.Dispose()
Write-Host ("saved {0} ({1}x{2})" -f $Out, $w, $ht)
