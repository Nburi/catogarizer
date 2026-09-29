param([string]$Save, [string]$Compare)
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class WS {
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(P f, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint c);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  public static List<IntPtr> All() { var r = new List<IntPtr>(); EnumWindows((h,l)=>{ r.Add(h); return true; }, IntPtr.Zero); return r; }
  public static string Title(IntPtr h) { var s = new StringBuilder(256); GetWindowText(h, s, 256); return s.ToString(); }
  public static string Cls(IntPtr h) { var s = new StringBuilder(128); GetClassName(h, s, 128); return s.ToString(); }
}
"@
function Describe($h) {
  $pid2 = 0; [void][WS]::GetWindowThreadProcessId($h, [ref]$pid2)
  $pn = try { (Get-Process -Id $pid2 -ErrorAction Stop).ProcessName } catch { "?" }
  [pscustomobject]@{ Handle = [int64]$h; Visible = [WS]::IsWindowVisible($h); Exists = [WS]::IsWindow($h); Process = $pn; Class = [WS]::Cls($h); Title = [WS]::Title($h) }
}
if ($Compare) {
  $before = Get-Content $Compare | ConvertFrom-Json
  $before | ForEach-Object { Describe([IntPtr]$_.Handle) } | Format-Table Visible, Exists, Process, Title -AutoSize | Out-String -Width 200
  return
}
$now = [WS]::All() | Where-Object { [WS]::IsWindowVisible($_) -and [WS]::GetWindow($_, 4) -eq [IntPtr]::Zero -and [WS]::Title($_).Length -gt 0 } | ForEach-Object { Describe $_ }
if ($Save) { $now | ConvertTo-Json | Set-Content $Save -Encoding utf8 }
$now | Format-Table Visible, Process, Class, Title -AutoSize | Out-String -Width 200
