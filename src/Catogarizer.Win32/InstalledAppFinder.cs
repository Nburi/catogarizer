using Catogarizer.Core.Services;
using Microsoft.Win32;

namespace Catogarizer.Win32;

public sealed class InstalledAppFinder : IInstalledAppFinder
{
    public IReadOnlyList<InstalledApp> FindInstalledApps()
    {
        // Keyed by path+arguments, not path alone - every PWA installed under the same
        // browser/profile shares one proxy exe path, differing only in its --app-id
        // argument, so keying on path alone would collapse them into a single entry.
        var byKey = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

        // Start Menu shortcuts first - their names are the ones users actually recognize.
        foreach (var app in FindFromStartMenuShortcuts())
            byKey[DedupeKey(app)] = app;

        // Registry Uninstall entries fill in anything a shortcut didn't catch.
        foreach (var app in FindFromUninstallRegistry())
            byKey.TryAdd(DedupeKey(app), app);

        return byKey.Values.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string DedupeKey(InstalledApp app) => $"{app.ExecutablePath}|{app.Arguments}";

    private static IEnumerable<InstalledApp> FindFromStartMenuShortcuts()
    {
        var shell = TryCreateShellComObject();
        if (shell is null) yield break;

        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        };

        foreach (var folder in folders.Where(Directory.Exists))
        {
            IEnumerable<string> shortcuts;
            try { shortcuts = Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories); }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var lnk in shortcuts)
            {
                var resolved = TryResolveShortcut(shell, lnk);
                if (resolved is not { } shortcut) continue;
                if (!string.Equals(Path.GetExtension(shortcut.Target), ".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(shortcut.Target)) continue;
                if (LooksLikeUninstaller(shortcut.Target)) continue;

                yield return new InstalledApp(Path.GetFileNameWithoutExtension(lnk), shortcut.Target, shortcut.Arguments);
            }
        }
    }

    /// <summary>Common uninstaller executable naming patterns (Inno Setup, NSIS, generic).</summary>
    private static bool LooksLikeUninstaller(string exePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(exePath);
        return fileName.StartsWith("unins", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("uninstall", StringComparison.OrdinalIgnoreCase);
    }

    private static dynamic? TryCreateShellComObject()
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            return type is null ? null : Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    private static (string Target, string? Arguments)? TryResolveShortcut(dynamic shell, string lnkPath)
    {
        try
        {
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            string target = shortcut.TargetPath;
            if (string.IsNullOrWhiteSpace(target)) return null;

            string arguments = shortcut.Arguments;
            return (target, string.IsNullOrWhiteSpace(arguments) ? null : arguments);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<InstalledApp> FindFromUninstallRegistry()
    {
        var roots = new (RegistryKey Root, string Path)[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        foreach (var (root, path) in roots)
        {
            using var uninstallKey = TryOpenSubKey(root, path);
            if (uninstallKey is null) continue;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                var app = TryReadUninstallEntry(uninstallKey, subKeyName);
                if (app is not null) yield return app;
            }
        }
    }

    private static RegistryKey? TryOpenSubKey(RegistryKey root, string path)
    {
        try { return root.OpenSubKey(path); }
        catch { return null; }
    }

    private static InstalledApp? TryReadUninstallEntry(RegistryKey uninstallKey, string subKeyName)
    {
        try
        {
            using var subKey = uninstallKey.OpenSubKey(subKeyName);
            var name = subKey?.GetValue("DisplayName") as string;
            var icon = subKey?.GetValue("DisplayIcon") as string;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(icon)) return null;

            // Windows' own "Apps & features" list hides entries flagged as system
            // components (runtimes, redistributables) - honor the same flag.
            if (subKey?.GetValue("SystemComponent") is int and not 0) return null;

            // DisplayIcon is often "C:\...\app.exe,0" (icon-index suffix) or quoted.
            var exePath = icon.Split(',')[0].Trim('"');
            if (!string.Equals(Path.GetExtension(exePath), ".exe", StringComparison.OrdinalIgnoreCase)) return null;
            if (!File.Exists(exePath)) return null;
            if (LooksLikeUninstaller(exePath)) return null;

            return new InstalledApp(name, exePath);
        }
        catch
        {
            return null;
        }
    }
}
