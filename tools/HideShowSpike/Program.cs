using Catogarizer.Core.Services;
using Catogarizer.Win32;

// Throwaway manual-verification harness for the v2 session-switching spike
// (see TODO.md Phase 13). Not part of the shipped product - no tests, no
// polish. Run against real, currently-open windows and read the console.
//
// Usage:
//   dotnet run -- hide-show <title-or-process-substring>
//   dotnet run -- watch

if (args.Length == 0)
{
    PrintUsage();
    return;
}

var finder = new WindowFinder();
var manager = new WindowManager();

switch (args[0])
{
    case "hide-show" when args.Length > 1:
        RunHideShow(finder, manager, args[1]);
        break;
    case "watch":
        RunWatch(finder);
        break;
    default:
        PrintUsage();
        break;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -- hide-show <title-or-process-substring>");
    Console.WriteLine("  dotnet run -- watch");
}

static void RunHideShow(IWindowFinder finder, IWindowManager manager, string needle)
{
    var matches = finder.FindAllVisibleWindows()
        .Where(w => w.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)
                 || w.ProcessName.Contains(needle, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (matches.Count == 0)
    {
        Console.WriteLine($"No visible window matched \"{needle}\". Nothing to do.");
        return;
    }

    Console.WriteLine($"Found {matches.Count} matching window(s):");
    var before = new Dictionary<IntPtr, (int X, int Y, int Width, int Height)>();
    foreach (var w in matches)
    {
        before[w.Handle] = manager.GetBounds(w.Handle);
        Console.WriteLine($"  [{w.ProcessName}] \"{w.Title}\" handle={w.Handle} bounds={before[w.Handle]}");
    }

    Console.WriteLine();
    Console.WriteLine("Press Enter to HIDE these windows...");
    Console.ReadLine();

    foreach (var w in matches) manager.Hide(w.Handle);

    Console.WriteLine("Hidden. Check the taskbar/Alt-Tab yourself now.");
    foreach (var w in matches)
        Console.WriteLine($"  handle={w.Handle} IsWindowOpen={manager.IsWindowOpen(w.Handle)} IsWindowVisible={manager.IsWindowVisible(w.Handle)}");

    Console.WriteLine();
    Console.WriteLine("Press Enter to SHOW them again...");
    Console.ReadLine();

    foreach (var w in matches) manager.Show(w.Handle);

    Console.WriteLine();
    Console.WriteLine("Shown again. Before/after comparison:");
    foreach (var w in matches)
    {
        var after = manager.GetBounds(w.Handle);
        var stillOpen = manager.IsWindowOpen(w.Handle);
        var visible = manager.IsWindowVisible(w.Handle);
        var unchanged = stillOpen && before[w.Handle] == after;
        Console.WriteLine($"  [{w.ProcessName}] before={before[w.Handle]} after={after} IsWindowOpen={stillOpen} IsWindowVisible={visible} unchanged={unchanged}");
    }
}

static void RunWatch(IWindowFinder finder)
{
    Console.WriteLine("Watching for new top-level windows. Press Enter to stop.");
    using var watcher = new WindowWatcher(finder);
    watcher.WindowAppeared += w =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] NEW WINDOW: [{w.ProcessName}] \"{w.Title}\" handle={w.Handle}");
    watcher.Start();
    Console.ReadLine();
    watcher.Stop();
}
