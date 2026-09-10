param(
  [Parameter(Mandatory=$true)][string]$Title,      # window title (exact)
  [AllowEmptyString()][Parameter(Mandatory=$true)][string]$ButtonName, # nav button to click ("" = none)
  [Parameter(Mandatory=$true)][string]$OutFile     # png path
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class W {
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int L, T, R, B; }
}
'@
[W]::SetProcessDPIAware() | Out-Null

$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Title)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { Write-Error "window not found: $Title"; exit 1 }
$h = [IntPtr]$win.Current.NativeWindowHandle

# move to primary & focus
[W]::SetWindowPos($h, [IntPtr](-1), 100, 100, 0, 0, 0x0041) | Out-Null   # TOPMOST|NOSIZE|SHOWWINDOW
[W]::SetWindowPos($h, [IntPtr](-2), 100, 100, 0, 0, 0x0041) | Out-Null   # NOTOPMOST
[W]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 600

if ($ButtonName -ne '') {
  $btnCond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)),
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $ButtonName)))
  $btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
  if (-not $btn) { Write-Error "button not found: $ButtonName"; exit 1 }
  ($btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
  Start-Sleep -Seconds 2
}

$r = New-Object W+RECT
[W]::GetWindowRect($h, [ref]$r) | Out-Null
$wd = $r.R - $r.L; $ht = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap($wd, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, (New-Object System.Drawing.Size($wd, $ht)))
$g.Dispose()
New-Item -ItemType Directory -Force -Path (Split-Path $OutFile) | Out-Null
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved: $OutFile ($wd x $ht)"
