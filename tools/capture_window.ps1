# DPI-aware full-window capture of DLSS5Patcher. Usage: capture_window.ps1 -Out <path.png>
param([Parameter(Mandatory=$true)][string]$Out)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class CW {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
  public struct RECT { public int L, T, R, B; }
}
'@
[CW]::SetProcessDPIAware() | Out-Null
$p = Get-Process DLSS5Patcher -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$h = $p.MainWindowHandle
[CW]::SetWindowPos($h, [IntPtr]::Zero, 60, 60, 0, 0, 0x0005) | Out-Null
Start-Sleep -Milliseconds 600
$r = New-Object CW+RECT
[CW]::GetWindowRect($h, [ref]$r) | Out-Null
$w = [int]($r.R) - [int]($r.L)
$ht = [int]($r.B) - [int]($r.T)
$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen([int]$r.L, [int]$r.T, 0, 0, $bmp.Size)
$bmp.Save($Out)
$g.Dispose(); $bmp.Dispose()
Write-Host ("saved {0} ({1}x{2})" -f $Out, $w, $ht)
