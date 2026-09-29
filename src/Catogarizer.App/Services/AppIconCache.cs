using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Catogarizer.Win32;

namespace Catogarizer.App.Services;

/// <summary>
/// Real app icons, extracted once per exe on a background thread and kept for the app's
/// lifetime (a few dozen small bitmaps at most).
/// </summary>
public static class AppIconCache
{
    // 64 px covers a 32-DIP icon at 200 % scaling, the largest the UI shows.
    private const int IconPixels = 64;

    private static readonly ConcurrentDictionary<string, Task<ImageSource?>> ByPath = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<int, Task<ImageSource?>> ByProcess = new();

    public static Task<ImageSource?> ForExecutable(string path) =>
        ByPath.GetOrAdd(path, p => Task.Run(() => Load(p)));

    public static Task<ImageSource?> ForProcess(int processId) =>
        ByProcess.GetOrAdd(processId, pid => Task.Run(() => Processes.ExecutablePathOf(pid) is { } path ? ForExecutable(path) : Task.FromResult<ImageSource?>(null)));

    private static ImageSource? Load(string path)
    {
        var handle = ShellIcons.Extract(path, IconPixels);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null; // unusual icon formats: the letter tile stays, which is fine
        }
        finally
        {
            ShellIcons.Destroy(handle);
        }
    }
}
