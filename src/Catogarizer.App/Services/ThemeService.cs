using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Catogarizer.Core.Models;
using Catogarizer.Core.Theming;
using Catogarizer.Win32;

namespace Catogarizer.App.Services;

/// <summary>
/// Turns a Core <see cref="ThemePalette"/> into the WPF resources every view binds to
/// (DynamicResource), and swaps them live - no restart needed to change theme.
/// </summary>
public sealed class ThemeService
{
    private readonly ResourceDictionary _appResources;
    private ResourceDictionary? _themeResources;

    public ThemePalette Current { get; private set; } = ThemeCatalog.Get(null);

    public event Action? ThemeChanged;

    public ThemeService(ResourceDictionary appResources)
    {
        _appResources = appResources;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => StyleTitleBar((Window)sender)));
    }

    public void Apply(string? themeId)
    {
        Current = ThemeCatalog.Get(themeId);
        var resources = Build(Current);
        if (_themeResources is not null)
            _appResources.MergedDictionaries.Remove(_themeResources);
        _appResources.MergedDictionaries.Insert(0, resources);
        _themeResources = resources;

        foreach (Window window in Application.Current.Windows)
            StyleTitleBar(window);
        ThemeChanged?.Invoke();
    }

    public Color CategoryColor(double hue) => ToColor(Current.CategoryColor(hue));

    /// <summary>A category's color; Unsorted (null) and colorless categories use the muted ink.</summary>
    public Color ColorFor(Category? category) =>
        category?.Hue is { } hue ? CategoryColor(hue) : ToColor(Current.Muted.ToRgb());

    public static Color ToColor(Rgb rgb) => Color.FromRgb(rgb.R, rgb.G, rgb.B);

    /// <summary>Frozen, so it can be shared across threads and bindings without change tracking.</summary>
    public static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void StyleTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        TitleBarStyle.Apply(hwnd, Current.IsDark, Current.Bg.ToRgb(), Current.Ink.ToRgb(), Current.Border.ToRgb());
    }

    private static ResourceDictionary Build(ThemePalette theme)
    {
        var resources = new ResourceDictionary();

        void AddColor(string name, Oklch value, string? brushKey = null)
        {
            var color = ToColor(value.ToRgb());
            resources[name + "Color"] = color;
            resources[brushKey ?? name + "Brush"] = FrozenBrush(color);
        }

        AddColor("Bg", theme.Bg);
        AddColor("Surface", theme.Surface);
        AddColor("SurfaceAlt", theme.SurfaceAlt);
        AddColor("Ink", theme.Ink);
        AddColor("Muted", theme.Muted);
        AddColor("Border", theme.Border, brushKey: "BorderBrush2");
        AddColor("Accent", theme.Accent);
        AddColor("AccentInk", theme.AccentInk);
        AddColor("Danger", theme.Danger);

        var bg = ToColor(theme.Bg.ToRgb());
        resources["OverlayBrush"] = FrozenBrush(Color.FromArgb(0xC0, bg.R, bg.G, bg.B));

        resources["CardRadius"] = new CornerRadius(theme.CardRadius);
        resources["ControlRadius"] = new CornerRadius(theme.ControlRadius);
        resources["SmallRadius"] = new CornerRadius(Math.Max(2, theme.ControlRadius - 2));
        resources["HeadingFont"] = new FontFamily(theme.HeadingFont is { } font
            ? $"{font}, Segoe UI Variable Display, Segoe UI"
            : "Segoe UI Variable Display, Segoe UI");

        return resources;
    }
}
