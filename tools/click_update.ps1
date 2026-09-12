# Find DLSS5Patcher's update dialog (title = U+53D1 U+73B0 U+65B0 U+7248 U+672C = "发现新版本")
# and click its bottom-right primary button by computed coordinates.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class M {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
'@
[M]::SetProcessDPIAware() | Out-Null

$dlgName = -join @([char]0x53D1, [char]0x73B0, [char]0x65B0, [char]0x7248, [char]0x672C)

$root = [System.Windows.Automation.AutomationElement]::RootElement
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
$found = $null
foreach ($w in $wins) {
  $p = $w.Current.ProcessId
  $proc = Get-Process -Id $p -ErrorAction SilentlyContinue
  if ($proc -and $proc.ProcessName -eq 'DLSS5Patcher' -and $w.Current.Name -eq $dlgName) { $found = $w }
}
if (-not $found) { Write-Output 'NO_DIALOG'; exit 1 }

$r = $found.Current.BoundingRectangle
Write-Output ("DIALOG_RECT {0},{1} {2}x{3}" -f $r.X, $r.Y, $r.Width, $r.Height)

# button layout at 96 DPI design size: W=140 H=34, right margin 24, bottom margin 22 (of 560x400 client)
$kx = $r.Width / 560.0
$ky = $r.Height / 400.0
$bx = [int]($r.X + $r.Width - 24 * $kx - 70 * $kx)
$by = [int]($r.Y + $r.Height - 22 * $ky - 17 * $ky)
Write-Output ("CLICK_AT {0},{1}" -f $bx, $by)
[M]::SetCursorPos($bx, $by) | Out-Null
Start-Sleep -Milliseconds 300
[M]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
[M]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Write-Output 'CLICKED'
