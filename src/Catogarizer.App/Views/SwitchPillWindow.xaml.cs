using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Catogarizer.Win32;

namespace Catogarizer.App.Views;

/// <summary>
/// The signature moment (PRINCIPLES.md): double-tapping the hotkey jumps back to the previous
/// category, and this pill in that category's color confirms where you landed. Deliberately
/// more crafted than the rest of the app - not a target for consistency cleanups.
/// </summary>
public partial class SwitchPillWindow : Window
{
    private static readonly Duration Rise = new(TimeSpan.FromMilliseconds(240));
    private static readonly TimeSpan HoldUntil = TimeSpan.FromMilliseconds(1140);
    private static readonly TimeSpan GoneAt = TimeSpan.FromMilliseconds(1380);

    private int _generation;

    public SwitchPillWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => OverlayWindowStyle.MakeClickThrough(new WindowInteropHelper(this).Handle);
    }

    /// <param name="categoryColor">Null for a neutral message (no dot, no tint).</param>
    public void Flash(string text, Color? categoryColor, bool showBackArrow)
    {
        Label.Text = text;
        Arrow.Visibility = showBackArrow ? Visibility.Visible : Visibility.Collapsed;
        Dot.Visibility = categoryColor is null ? Visibility.Collapsed : Visibility.Visible;
        Dot.Fill = categoryColor is { } dot ? new SolidColorBrush(dot) : null;
        Tint.Background = categoryColor is { } tint ? new SolidColorBrush(tint) : null;

        if (!IsVisible) Show();
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Bottom - ActualHeight;

        Animate(++_generation);
    }

    private void Animate(int generation)
    {
        var motion = SystemParameters.ClientAreaAnimation;
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        var settle = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

        var opacity = new DoubleAnimationUsingKeyFrames();
        opacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)), easeOut));
        opacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(HoldUntil)));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(GoneAt), easeOut));
        opacity.Completed += (_, _) =>
        {
            if (generation == _generation) Hide();
        };

        var lift = new DoubleAnimationUsingKeyFrames();
        lift.KeyFrames.Add(new DiscreteDoubleKeyFrame(motion ? 14 : 0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        lift.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(Rise.TimeSpan), settle));
        lift.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(HoldUntil)));
        lift.KeyFrames.Add(new EasingDoubleKeyFrame(motion ? 8 : 0, KeyTime.FromTimeSpan(GoneAt), easeOut));

        var nudge = new DoubleAnimationUsingKeyFrames();
        nudge.KeyFrames.Add(new DiscreteDoubleKeyFrame(motion ? 6 : 0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
        nudge.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(330)), easeOut));

        Pill.BeginAnimation(OpacityProperty, opacity);
        Lift.BeginAnimation(TranslateTransform.YProperty, lift);
        ArrowNudge.BeginAnimation(TranslateTransform.XProperty, nudge);
    }
}
