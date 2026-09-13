# Background click at an ABSOLUTE physical screen point: resolves the deepest window via WindowFromPoint
# and posts WM_LBUTTONDOWN/UP with correct client coords (no focus steal).
param([Parameter(Mandatory=$true)][int]$X, [Parameter(Mandatory=$true)][int]$Y)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class BP {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  public struct POINT { public int X, Y; }
}
'@
[BP]::SetProcessDPIAware() | Out-Null
$pt = New-Object BP+POINT; $pt.X = $X; $pt.Y = $Y
$hwnd = [BP]::WindowFromPoint($pt)
if ($hwnd -eq [IntPtr]::Zero) { throw 'no window at point' }
$c = New-Object BP+POINT; $c.X = $X; $c.Y = $Y
[BP]::ScreenToClient($hwnd, [ref]$c) | Out-Null
$lp = [IntPtr](($c.Y -shl 16) -bor ($c.Y -band 0xFFFF))   # 见下方修正
$lp = [IntPtr](($c.Y * 65536) + ($c.X -band 0xFFFF))
[BP]::PostMessage($hwnd, 0x0201, [IntPtr]1, $lp) | Out-Null
Start-Sleep -Milliseconds 80
[BP]::PostMessage($hwnd, 0x0202, [IntPtr]0, $lp) | Out-Null
Write-Host ("clicked hwnd {0} client ({1},{2})" -f $hwnd, $c.X, $c.Y)
