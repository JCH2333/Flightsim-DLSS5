# Walk all 4 nav pages of DLSS5Patcher via UIA-located nav buttons, screenshot each page.
param([Parameter(Mandatory=$true)][string]$OutDir)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class W3 {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int L, T, R, B; }
}
'@
[W3]::SetProcessDPIAware() | Out-Null

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
$pid2 = 0
[W3]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null

[W3]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 400

# locate nav buttons by name (WinForms controls expose their Text as UIA Name)
$names = @()
$names += New-Object psobject -Property @{N=[char]0x4E00+[char]0x952E+[char]0x5B89+[char]0x88C5; F='home'}          # 一键安装
$names += New-Object psobject -Property @{N=[char]0x4F7F+[char]0x7528+[char]0x6559+[char]0x7A0B; F='tutorial'}       # 使用教程
$names += New-Object psobject -Property @{N=[char]0x95EE+[char]0x9898+[char]0x53CD+[char]0x9988; F='feedback'}       # 问题反馈
$names += New-Object psobject -Property @{N=[char]0x8BBE+[char]0x7F6E+[char]0x0020+[char]0x00B7+[char]0x0020+[char]0x5173+[char]0x4E8E; F='about'}  # 设置 · 关于

foreach ($item in $names) {
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $item.N)
  $el = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if (-not $el) { Write-Output "SKIP $($item.F) (not found)"; continue }
  $r = $el.Current.BoundingRectangle
  $cx = [int]($r.X + $r.Width / 2); $cy = [int]($r.Y + $r.Height / 2)

  # ensure our window is foreground before clicking
  $fg = [W3]::GetForegroundWindow()
  $fgpid = 0
  [W3]::GetWindowThreadProcessId($fg, [ref]$fgpid) | Out-Null
  if ($fgpid -ne $pid2) { [W3]::SetForegroundWindow($h) | Out-Null; Start-Sleep -Milliseconds 300 }

  [W3]::SetCursorPos($cx, $cy) | Out-Null
  Start-Sleep -Milliseconds 120
  [W3]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
  [W3]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
  Start-Sleep -Milliseconds 900

  $rect = New-Object W3+RECT
  [W3]::GetWindowRect($h, [ref]$rect) | Out-Null
  $bmp = New-Object System.Drawing.Bitmap(($rect.R - $rect.L), ($rect.B - $rect.T))
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($rect.L, $rect.T, 0, 0, (New-Object System.Drawing.Size(($rect.R - $rect.L), ($rect.B - $rect.T))))
  $g.Dispose()
  $out = Join-Path $OutDir ("walk_" + $item.F + ".png")
  $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Output "saved $out"
}
