param([string]$ExePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$oldPids = (Get-Process DLSS5Patcher -ErrorAction SilentlyContinue).Id
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 6
Write-Output "started pid=$($proc.Id)"

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { Write-Error "main window not found"; exit 1 }
Write-Output "window: $($win.Current.Name)"

function Find-Button($scope, $container, $name) {
  $c = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)),
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)))
  $container.FindFirst($scope, $c)
}

# 1. go to Settings page (button name depends on UI language)
$isEnglish = $win.Current.Name -match 'Patcher'
$settingsName = if ($isEnglish) { 'Settings · About' } else { '设置 · 关于' }
$b = Find-Button ([System.Windows.Automation.TreeScope]::Descendants) $win $settingsName
if (-not $b) { Write-Error "settings nav not found"; exit 1 }
($b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
Start-Sleep -Seconds 2

# 2. switch language combo to English
$cbCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$cbs = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cbCond)
$langCb = $null
foreach ($cb in $cbs) { if ($cb.Current.Name -match '语言|Language|Restart|apply|切换') { $langCb = $cb } }
if (-not $langCb) { Write-Error "language combo not found"; exit 1 }
$expand = $langCb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
$expand.Expand(); Start-Sleep -Milliseconds 800
$itemCond = New-Object System.Windows.Automation.AndCondition(
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)),
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $(if ($isEnglish) { '简体中文' } else { 'English' }))))
$item = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $itemCond)
if (-not $item) { $item = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $itemCond) }
if (-not $item) { Write-Error "language item not found"; exit 1 }
# WinForms combo items often time out on SelectionItemPattern - click by coordinates instead
$r = $item.Current.BoundingRectangle
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class M {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
'@
[M]::SetCursorPos([int]($r.X + $r.Width/2), [int]($r.Y + $r.Height/2)) | Out-Null
Start-Sleep -Milliseconds 200
[M]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)   # LEFTDOWN
[M]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)   # LEFTUP
Start-Sleep -Seconds 2
Write-Output "selected English"

# 3. confirm restart dialog ("是")
$yesBtn = $null
$deadline = (Get-Date).AddSeconds(8)
while (-not $yesBtn -and (Get-Date) -lt $deadline) {
  $dlgCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)
  $dlgs = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $dlgCond)
  foreach ($d in $dlgs) {
    if ($d.Current.Name -match '重启|Restart') {
      $yesBtn = Find-Button ([System.Windows.Automation.TreeScope]::Descendants) $d '是'
      if (-not $yesBtn) { $yesBtn = Find-Button ([System.Windows.Automation.TreeScope]::Descendants) $d 'Yes' }
    }
  }
  if (-not $yesBtn) { Start-Sleep -Milliseconds 500 }
}
if (-not $yesBtn) { Write-Error "restart dialog not found"; exit 1 }
($yesBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
Write-Output "confirmed restart"

# 4. wait for new process with English title
Start-Sleep -Seconds 10
$newProc = Get-Process DLSS5Patcher -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $proc.Id } | Select-Object -First 1
if (-not $newProc) {
  # Application.Restart may reuse PID slot only if old exited; check any live process
  $newProc = Get-Process DLSS5Patcher -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (-not $newProc) { Write-Error "no process after restart"; exit 1 }
Write-Output "after restart pid=$($newProc.Id) (old=$($proc.Id))"
$cond2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $newProc.Id)
$win2 = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond2)
if ($win2) { Write-Output "new window title: $($win2.Current.Name)" }
$lang = (Get-Content "$env:LOCALAPPDATA\DLSS5Patcher\config.txt" | Where-Object { $_ -like 'lang=*' })
Write-Output "config: $lang"
