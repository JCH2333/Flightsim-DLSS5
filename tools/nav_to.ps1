# Navigate DLSS5Patcher to a nav item by visible name (coordinate click on the UIA element rect).
param([Parameter(Mandatory=$true)][string]$Name)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class NV {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
'@
[NV]::SetProcessDPIAware() | Out-Null
$root = [System.Windows.Automation.AutomationElement]::RootElement
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
$win = $null
foreach ($w in $wins) {
  $proc = Get-Process -Id $w.Current.ProcessId -ErrorAction SilentlyContinue
  if ($proc -and $proc.ProcessName -eq 'DLSS5Patcher') { $win = $w }
}
if (-not $win) { Write-Error 'window not found'; exit 1 }
$btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)))
if (-not $btn) { Write-Error "nav '$Name' not found"; exit 1 }
$h = [IntPtr]$win.Current.NativeWindowHandle
[NV]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 300
$r = $btn.Current.BoundingRectangle
$cx = [int]($r.X + $r.Width / 2); $cy = [int]($r.Y + $r.Height / 2)
[NV]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 100
[NV]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)   # LEFTDOWN
[NV]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)   # LEFTUP
Start-Sleep -Milliseconds 500
Write-Host "navigated to $Name at $cx,$cy"
