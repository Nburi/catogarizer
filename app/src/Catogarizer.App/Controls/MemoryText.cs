using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Catogarizer.Core.Resources;

namespace Catogarizer.App.Controls;

/// <summary>
/// A memory figure ("1.2 GB") that counts to its new value when it changes - the number of a category falling
/// asleep runs down instead of jumping. The first value, a small change, and a system set to reduced motion
/// all appear at once.
/// </summary>
public sealed class MemoryText : TextBlock
{
    private const double MinAnimatedShare = 0.05;

    public static readonly DependencyProperty BytesProperty = DependencyProperty.Register(
        nameof(Bytes), typeof(long), typeof(MemoryText), new PropertyMetadata(0L, OnBytesChanged));

    private static readonly DependencyProperty ShownProperty = DependencyProperty.Register(
        nameof(Shown), typeof(double), typeof(MemoryText), new PropertyMetadata(0d, OnShownChanged));

    public long Bytes
    {
        get => (long)GetValue(BytesProperty);
        set => SetValue(BytesProperty, value);
    }

    private double Shown
    {
        get => (double)GetValue(ShownProperty);
        set => SetValue(ShownProperty, value);
    }

    private static void OnBytesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var text = (MemoryText)d;
        var from = (long)e.OldValue;
        var to = (long)e.NewValue;

        var small = from > 0 && Math.Abs(to - from) < from * MinAnimatedShare;
        if (from <= 0 || to <= 0 || small || !text.IsLoaded || !SystemParameters.ClientAreaAnimation)
        {
            text.BeginAnimation(ShownProperty, null);
            text.Shown = to;
            return;
        }

        text.BeginAnimation(ShownProperty, new DoubleAnimation(text.Shown, to, TimeSpan.FromMilliseconds(900))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    private static void OnShownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MemoryText)d).Text = ByteSize.Format((long)(double)e.NewValue);
}
