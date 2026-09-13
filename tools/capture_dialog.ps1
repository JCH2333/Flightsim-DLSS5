# Capture the DLSS5Patcher popup dialog (small non-main window) via PrintWindow.
param([Parameter(Mandatory=$true)][string]$Out)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class CD {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  public struct RECT { public int L, T, R, B; }
}
'@
[CD]::SetProcessDPIAware() | Out-Null
$target = (Get-Process DLSS5Patcher -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1).Id
$best = $null
$cb = {
  param($h, $l)
  $p2 = 0
  [CD]::GetWindowThreadProcessId($h, [ref]$p2) | Out-Null
  if ($p2 -eq $target -and [CD]::IsWindowVisible($h)) {
    $r = New-Object CD+RECT
    [CD]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.R - $r.L; $ht = $r.B - $r.T
    if ($w -gt 300 -and $w -lt 1100 -and $script:best -eq $null) { $script:best = @($h, $w, $ht) }
  }
  return $true
}
[CD]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($script:best -eq $null) { Write-Host 'no dialog window'; exit 1 }
$h = $script:best[0]; $w = $script:best[1]; $ht = $script:best[2]
$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[CD]::PrintWindow($h, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc); $g.Dispose()
$bmp.Save($Out); $bmp.Dispose()
Write-Host ("saved {0} ({1}x{2})" -f $Out, $w, $ht)
