param([string]$OutDir, [int]$GapMs = 120, [int[]]$ShotsAtMs = @(250, 700), [switch]$Single)
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace K -Name I -MemberDefinition @'
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
[DllImport("user32.dll")] public static extern System.IntPtr GetDC(System.IntPtr h);
[DllImport("user32.dll")] public static extern int ReleaseDC(System.IntPtr h, System.IntPtr dc);
[DllImport("gdi32.dll")] public static extern bool BitBlt(System.IntPtr d, int x, int y, int w, int h, System.IntPtr s, int sx, int sy, uint rop);
'@
[void][K.I]::SetProcessDPIAware()
$UP = 2
function Tap { [K.I]::keybd_event(0x20, 0, 0, [UIntPtr]::Zero); [K.I]::keybd_event(0x20, 0, $UP, [UIntPtr]::Zero) }
[K.I]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
[K.I]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
Tap
if (-not $Single) { Start-Sleep -Milliseconds $GapMs; Tap }
[K.I]::keybd_event(0x12, 0, $UP, [UIntPtr]::Zero)
[K.I]::keybd_event(0x11, 0, $UP, [UIntPtr]::Zero)
$w = [K.I]::GetSystemMetrics(0); $h = [K.I]::GetSystemMetrics(1)
$sw = [Diagnostics.Stopwatch]::StartNew()
foreach ($t in $ShotsAtMs) {
  while ($sw.ElapsedMilliseconds -lt $t) { Start-Sleep -Milliseconds 5 }
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $screenDc = [K.I]::GetDC([IntPtr]::Zero); $hdc = $g.GetHdc()
  [void][K.I]::BitBlt($hdc, 0, 0, $w, $h, $screenDc, 0, 0, 0x40CC0020)
  $g.ReleaseHdc($hdc); [void][K.I]::ReleaseDC([IntPtr]::Zero, $screenDc)
  $bmp.Save((Join-Path $OutDir "shot_$t.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
}
"screen ${w}x${h}"
