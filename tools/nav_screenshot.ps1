# Click a sidebar nav item by index (0-3) inside the DLSS5Patcher window, then screenshot the window.
param(
  [Parameter(Mandatory=$true)][int]$NavIndex,
  [Parameter(Mandatory=$true)][string]$OutFile
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class W2 {
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int L, T, R, B; }
}
'@
[W2]::SetProcessDPIAware() | Out-Null

$root = [System.Windows.Automation.AutomationElement]::RootElement
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
$win = $null
foreach ($w in $wins) {
  $p = $w.Current.ProcessId
  $proc = Get-Process -Id $p -ErrorAction SilentlyContinue
  if ($proc -and $proc.ProcessName -eq 'DLSS5Patcher') { $win = $w }
}
if (-not $win) { Write-Error 'window not found'; exit 1 }
$h = [IntPtr]$win.Current.NativeWindowHandle

[W2]::SetWindowPos($h, [IntPtr](-1), 100, 100, 0, 0, 0x0041) | Out-Null
[W2]::SetWindowPos($h, [IntPtr](-2), 100, 100, 0, 0, 0x0041) | Out-Null
[W2]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 600

$r = New-Object W2+RECT
[W2]::GetWindowRect($h, [ref]$r) | Out-Null
$scale = ($r.R - $r.L) / 1080.0
# nav item i: x = 14+109=123, y = 104 + i*50 + 22 (design px, in 1080x838 client)
$dx = [int](100 + 123 * $scale)
$dy = [int](100 + (104 + $NavIndex * 50 + 22) * $scale)
[W2]::SetCursorPos($dx, $dy) | Out-Null
Start-Sleep -Milliseconds 150
[W2]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
[W2]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
Start-Sleep -Seconds 2

$bmp = New-Object System.Drawing.Bitmap(($r.R - $r.L), ($r.B - $r.T))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, (New-Object System.Drawing.Size(($r.R - $r.L), ($r.B - $r.T))))
$g.Dispose()
New-Item -ItemType Directory -Force -Path (Split-Path $OutFile) | Out-Null
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved: $OutFile"
