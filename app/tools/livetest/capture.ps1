param([string]$OutPrefix, [string]$ProcessName = "Catogarizer.App")
# Captures every visible top-level window of one process via PrintWindow (works for layered
# windows too) and saves each as <OutPrefix>_<w>x<h>.png. Only this process's windows are captured.
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class Cap {
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(P f, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public static List<IntPtr> Of(int pid) { var r = new List<IntPtr>(); EnumWindows((h,l)=>{ int p; GetWindowThreadProcessId(h, out p); if (p==pid && IsWindowVisible(h)) r.Add(h); return true; }, IntPtr.Zero); return r; }
}
"@
[void][Cap]::SetProcessDPIAware()
$pid2 = (Get-Process $ProcessName).Id
foreach ($h in [Cap]::Of($pid2)) {
  $r = New-Object Cap+R; [void][Cap]::GetWindowRect($h, [ref]$r)
  $w = $r.Rt - $r.L; $hh = $r.B - $r.T
  if ($w -lt 20 -or $hh -lt 20) { continue }
  $bmp = New-Object System.Drawing.Bitmap $w, $hh
  $g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
  [void][Cap]::PrintWindow($h, $hdc, 2)
  $g.ReleaseHdc($hdc); $g.Dispose()
  $file = "${OutPrefix}_${w}x${hh}.png"
  $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  "$file  at ($($r.L),$($r.T))"
}
