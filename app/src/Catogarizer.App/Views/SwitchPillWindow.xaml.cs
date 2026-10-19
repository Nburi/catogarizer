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
    private static readonly TimeSpan DefaultHold = TimeSpan.FromMilliseconds(1140);
    private static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(240);

    private int _generation;

    public SwitchPillWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => OverlayWindowStyle.MakeClickThrough(new WindowInteropHelper(this).Handle);
    }

    /// <param name="categoryColor">Null for a neutral message (no dot, no tint).</param>
    /// <param name="detail">A second, muted part of the message ("Comms asleep"); null for none.</param>
    /// <param name="longRead">Stays a little longer, for a message with something to read after the first glance.</param>
    public void Flash(string text, Color? categoryColor, bool showBackArrow, string? detail = null, bool longRead = false)
    {
        Label.Text = text;
        Detail.Text = detail;
        Detail.Visibility = detail is null ? Visibility.Collapsed : Visibility.Visible;
        Arrow.Visibility = showBackArrow ? Visibility.Visible : Visibility.Collapsed;
        Dot.Visibility = categoryColor is null ? Visibility.Collapsed : Visibility.Visible;
        Dot.Fill = categoryColor is { } dot ? new SolidColorBrush(dot) : null;
        Tint.Background = categoryColor is { } tint ? new SolidColorBrush(tint) : null;

        if (!IsVisible) Show();
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - ActualWidth) / 2;
        Top = area.Bottom - ActualHeight;

        Animate(++_generation, longRead ? DefaultHold + TimeSpan.FromMilliseconds(1000) : DefaultHold);
    }

    private void Animate(int generation, TimeSpan holdUntil)
    {
        var goneAt = holdUntil + Fade;
        var motion = SystemParameters.ClientAreaAnimation;
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        var settle = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

        var opacity = new DoubleAnimationUsingKeyFrames();
        opacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)), easeOut));
        opacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(holdUntil)));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(goneAt), easeOut));
        opacity.Completed += (_, _) =>
        {
            if (generation == _generation) Hide();
        };

        var lift = new DoubleAnimationUsingKeyFrames();
        lift.KeyFrames.Add(new DiscreteDoubleKeyFrame(motion ? 14 : 0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        lift.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(Rise.TimeSpan), settle));
        lift.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(holdUntil)));
        lift.KeyFrames.Add(new EasingDoubleKeyFrame(motion ? 8 : 0, KeyTime.FromTimeSpan(goneAt), easeOut));

        var nudge = new DoubleAnimationUsingKeyFrames();
        nudge.KeyFrames.Add(new DiscreteDoubleKeyFrame(motion ? 6 : 0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
        nudge.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(330)), easeOut));

        Pill.BeginAnimation(OpacityProperty, opacity);
        Lift.BeginAnimation(TranslateTransform.YProperty, lift);
        ArrowNudge.BeginAnimation(TranslateTransform.XProperty, nudge);
    }
}
