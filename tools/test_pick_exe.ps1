# E2E test: main window -> third "指定游戏程序..." button -> file dialog -> set filename -> Open.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$mainTitle = -join @([char]0x0044,[char]0x004C,[char]0x0053,[char]0x0053,[char]0x0035) # "DLSS5" prefix marker not used; find by process instead
$root = [System.Windows.Automation.AutomationElement]::RootElement

function Find-AppWindow {
    $wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($w in $wins) {
        $p = $w.Current.ProcessId
        $proc = Get-Process -Id $p -ErrorAction SilentlyContinue
        if ($proc -and $proc.ProcessName -eq 'DLSS5Patcher') { return $w }
    }
    return $null
}

$win = Find-AppWindow
if (-not $win) { Write-Output 'NO_MAIN'; exit 1 }
Write-Output "main: $($win.Current.Name)"

$btnCond = New-Object System.Windows.Automation.AndCondition(
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)),
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $args[0])))
$btns = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
if ($btns.Count -lt 3) { Write-Output "btn count: $($btns.Count)"; exit 1 }
$btn = $btns[2]   # XP12 row
($btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
Write-Output 'invoked pick-exe (3rd)'
Start-Sleep -Seconds 2

# file dialog: find DLSS5Patcher dialog whose name contains the exe name
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
$dlg = $null
foreach ($w in $wins) {
  $p = $w.Current.ProcessId
  $proc = Get-Process -Id $p -ErrorAction SilentlyContinue
  if ($proc -and $proc.ProcessName -eq 'DLSS5Patcher' -and $w.Current.Name -ne $win.Current.Name) { $dlg = $w }
}
if (-not $dlg) { Write-Output 'NO_DIALOG'; exit 1 }
Write-Output "dlg: $($dlg.Current.Name)"

$editCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
$edits = $dlg.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editCond)
if ($edits.Count -eq 0) { Write-Output 'NO_EDIT'; exit 1 }
$edit = $edits[0]
$vp = $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
$vp.SetValue($args[1])
Write-Output "set path: $($args[1])"

$openBtn = $null
$btnAll = $dlg.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
foreach ($b in $btnAll) { if ($b.Current.Name -match '^' + [char]0x6253 + [char]0x5F00) { $openBtn = $b } }   # 打开
if (-not $openBtn) { Write-Output 'NO_OPEN_BTN'; exit 1 }
($openBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
Write-Output 'clicked open'
