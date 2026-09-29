using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Catogarizer.App.Services;

namespace Catogarizer.App.Controls;

/// <summary>
/// An app's real icon, from its exe path or a running window's process. Shows a neutral
/// letter tile immediately (loading state and fallback) and fades the icon in once loaded.
/// </summary>
public partial class AppIconView : UserControl
{
    public static readonly DependencyProperty ExecutablePathProperty = DependencyProperty.Register(
        nameof(ExecutablePath), typeof(string), typeof(AppIconView), new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty ProcessIdProperty = DependencyProperty.Register(
        nameof(ProcessId), typeof(int), typeof(AppIconView), new PropertyMetadata(0, OnSourceChanged));

    public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register(
        nameof(DisplayName), typeof(string), typeof(AppIconView), new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize), typeof(double), typeof(AppIconView), new PropertyMetadata(24.0, OnSizeChanged));

    private int _generation;

    public string? ExecutablePath { get => (string?)GetValue(ExecutablePathProperty); set => SetValue(ExecutablePathProperty, value); }
    public int ProcessId { get => (int)GetValue(ProcessIdProperty); set => SetValue(ProcessIdProperty, value); }
    public string? DisplayName { get => (string?)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
    public double IconSize { get => (double)GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    public AppIconView()
    {
        InitializeComponent();
        ApplySize();
    }

    private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AppIconView)d).ApplySize();

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => _ = ((AppIconView)d).ReloadAsync();

    private void ApplySize()
    {
        Root.Width = Root.Height = IconSize;
        Tile.CornerRadius = new CornerRadius(IconSize * 0.26);
        Letters.FontSize = Math.Max(7, IconSize * 0.38);
    }

    private async Task ReloadAsync()
    {
        var generation = ++_generation;
        Letters.Text = Initials(DisplayName);
        Icon.BeginAnimation(OpacityProperty, null);
        Icon.Opacity = 0;
        Icon.Source = null;
        Tile.Visibility = Visibility.Visible;

        var load = !string.IsNullOrWhiteSpace(ExecutablePath) ? AppIconCache.ForExecutable(ExecutablePath!)
            : ProcessId > 0 ? AppIconCache.ForProcess(ProcessId)
            : null;
        if (load is null) return;

        // Already cached: show at once, so re-rendered lists don't flicker. Fresh: fade in.
        var wasCached = load.IsCompleted;
        var image = await load;
        if (image is null || generation != _generation) return;

        Icon.Source = image;
        if (wasCached || !SystemParameters.ClientAreaAnimation)
        {
            Icon.Opacity = 1;
            Tile.Visibility = Visibility.Hidden;
            return;
        }

        var fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(150)));
        fade.Completed += (_, _) => { if (generation == _generation) Tile.Visibility = Visibility.Hidden; };
        Icon.BeginAnimation(OpacityProperty, fade);
    }

    private static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var words = name.Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 2
            ? string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])))
            : char.ToUpperInvariant(words[0][0]).ToString();
    }
}
