// Throwaway harness for the resource-saving spike (TODO Phase 15, step 0). It exercises the real
// Catogarizer.Win32 classes against processes it starts itself - never against the user's apps.
//   dotnet run -- all            run every check
//   dotnet run -- audio          list processes with an active audio stream (read-only)
//   (internal) child-save | child-tray   a window that asks "Save?" / hides itself when closed
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Catogarizer.Core.Resources;
using Catogarizer.Win32;

var mode = args.FirstOrDefault() ?? "all";
switch (mode)
{
    case "child-save": RunSaveChild(); return;
    case "child-tray": RunTrayChild(); return;
    case "audio": Audio(); return;
    case "close": CloseBehaviour(); return;
}

var table = new ProcessTable();
var governor = new ProcessGovernor();
Console.WriteLine($"System memory load: {new SystemMemory().LoadPercent} %");
Eco(table, governor);
Trim(table, governor);
Freeze(table, governor);
Audio();
CloseBehaviour();
return;

static Process Spawn(string powershellCommand)
{
    var p = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{powershellCommand}\"")
    { CreateNoWindow = true, UseShellExecute = false })!;
    Thread.Sleep(1500);
    return p;
}

static void Eco(ProcessTable table, ProcessGovernor governor)
{
    Console.WriteLine("\n== Eco (efficiency mode + idle priority) ==");
    using var p = Spawn("Start-Sleep 120");
    try
    {
        var before = p.PriorityClass;
        var memento = governor.EnterEco(p.Id);
        Console.WriteLine($"EnterEco: {(memento is null ? "FAILED" : "ok")}  memento={memento}");
        p.Refresh();
        Console.WriteLine($"priority {before} -> {p.PriorityClass} (expected Idle)");
        Console.WriteLine($"throttle state read back: {ReadThrottle(p.Id)} (expected control=1 state=1)");
        if (memento is { } m)
        {
            Console.WriteLine($"LeaveEco: {governor.LeaveEco(p.Id, m)}");
            p.Refresh();
            Console.WriteLine($"priority after leave: {p.PriorityClass} (expected {before}); throttle: {ReadThrottle(p.Id)} (expected control=0 state=0)");
        }
    }
    finally { p.Kill(); }
}

static string ReadThrottle(int pid)
{
    var h = Native.OpenProcess(0x1000, false, pid);
    try
    {
        var s = new Native.State { Version = 1 };
        return Native.GetProcessInformation(h, 4, ref s, 12) ? $"control={s.ControlMask} state={s.StateMask}" : "unreadable";
    }
    finally { Native.CloseHandle(h); }
}

static void Trim(ProcessTable table, ProcessGovernor governor)
{
    Console.WriteLine("\n== Trim (EmptyWorkingSet) ==");
    using var p = Spawn("$a=[byte[]]::new(300MB); for($i=0;$i -lt $a.Length;$i+=4096){$a[$i]=1}; Start-Sleep 120");
    try
    {
        var before = table.Measure(p.Id);
        Console.WriteLine($"private working set before: {Mb(before?.PrivateBytes)} MB");
        Console.WriteLine($"Trim: {governor.Trim(p.Id)}");
        Thread.Sleep(500);
        var after = table.Measure(p.Id);
        Console.WriteLine($"private working set after:  {Mb(after?.PrivateBytes)} MB (expected far below)");
        Console.WriteLine($"start ticks stable: {before?.StartTicks == after?.StartTicks}");
    }
    finally { p.Kill(); }
}

static void Freeze(ProcessTable table, ProcessGovernor governor)
{
    Console.WriteLine("\n== Freeze (NtSuspendProcess) ==");
    var file = Path.Combine(Path.GetTempPath(), "catogarizer-spike-tick.txt");
    File.Delete(file);
    using var p = Spawn($"while($true){{ Add-Content '{file}' 'x'; Start-Sleep -Milliseconds 100 }}");
    try
    {
        long Size() => File.Exists(file) ? new FileInfo(file).Length : 0;
        var running = Size();
        Thread.Sleep(1000);
        Console.WriteLine($"ticks while running: {Size() - running} bytes/s (expected > 0)");
        Console.WriteLine($"Suspend: {governor.Suspend(p.Id)}");
        Thread.Sleep(300);
        var frozenAt = Size();
        Thread.Sleep(1500);
        Console.WriteLine($"ticks while frozen: {Size() - frozenAt} bytes (expected 0)");
        Console.WriteLine($"Resume: {governor.Resume(p.Id)}");
        var resumedAt = Size();
        Thread.Sleep(1000);
        Console.WriteLine($"ticks after resume: {Size() - resumedAt} bytes (expected > 0)");
    }
    finally { p.Kill(); File.Delete(file); }
}

static void Audio()
{
    Console.WriteLine("\n== Audio sessions (read-only) ==");
    var audio = new AudioActivity();
    var ids = audio.PlayingProcessIds();
    Console.WriteLine($"reliable: {audio.IsReliable}; playing: {(ids.Count == 0 ? "(nothing)" : string.Join(", ", ids.Select(i => $"{i}={Name(i)}")))}");
}

static void CloseBehaviour()
{
    Console.WriteLine("\n== WM_CLOSE on a hidden window ==");
    foreach (var child in new[] { "child-save", "child-tray" })
    {
        var exe = Environment.ProcessPath!;
        var p = Process.Start(new ProcessStartInfo(exe, child) { UseShellExecute = false })!;
        try
        {
            Thread.Sleep(2000);
            var main = p.MainWindowHandle;
            Console.WriteLine($"[{child}] window found: {main != IntPtr.Zero}");
            var wm = new WindowManager();
            wm.Hide(main);
            wm.CloseGraceful(main);
            Thread.Sleep(2500);
            var visible = VisibleWindowsOf(p.Id);
            Console.WriteLine($"[{child}] after close: process alive={!p.HasExited}; main IsWindow={wm.IsWindowOpen(main)}; visible windows of process: {(visible.Count == 0 ? "none" : string.Join(" | ", visible))}");
        }
        finally
        {
            if (!p.HasExited) p.Kill();
        }
    }
}

static List<IntPtr> AllWindowsOf(int pid)
{
    var found = new List<IntPtr>();
    Native.EnumWindows((h, _) => { Native.GetWindowThreadProcessId(h, out var owner); if (owner == pid) found.Add(h); return true; }, IntPtr.Zero);
    return found;
}

static List<string> VisibleWindowsOf(int pid) =>
    AllWindowsOf(pid).Where(Native.IsWindowVisible).Select(h => { var sb = new StringBuilder(256); Native.GetWindowText(h, sb, 256); return $"\"{sb}\""; }).ToList();

static string Name(int pid) { try { return Process.GetProcessById(pid).ProcessName; } catch { return "?"; } }
static string Mb(long? bytes) => bytes is { } b ? (b / 1024 / 1024).ToString() : "n/a";

static void RunSaveChild()
{
    var form = new Form { Text = "Spike: asks to save", Width = 320, Height = 160 };
    form.FormClosing += (_, e) =>
    {
        if (MessageBox.Show(form, "Save changes?", "Spike", MessageBoxButtons.YesNo) == DialogResult.No) e.Cancel = true;
    };
    Application.Run(form);
}

static void RunTrayChild()
{
    var form = new Form { Text = "Spike: hides when closed", Width = 320, Height = 160 };
    form.FormClosing += (_, e) => { e.Cancel = true; form.Hide(); };
    Application.Run(form);
}

static class Native
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct State { public uint Version, ControlMask, StateMask; }
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint a, bool i, int pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] public static extern bool GetProcessInformation(IntPtr h, int c, ref State s, uint size);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
}
