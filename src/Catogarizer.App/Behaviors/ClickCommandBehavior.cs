using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Catogarizer.App.Behaviors;

/// <summary>
/// Lets a non-clickable element (e.g. a Border used as a card) distinguish a single click
/// from a double click and route each to its own bound command. WPF has no built-in way to
/// tell these apart without a delay: a genuine double click always raises a single-click
/// MouseLeftButtonDown first, so the single-click command is deferred until the system's
/// double-click window passes without a second click arriving.
/// </summary>
public static class ClickCommandBehavior
{
    public static readonly DependencyProperty SingleClickCommandProperty =
        DependencyProperty.RegisterAttached("SingleClickCommand", typeof(ICommand), typeof(ClickCommandBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty SingleClickCommandParameterProperty =
        DependencyProperty.RegisterAttached("SingleClickCommandParameter", typeof(object), typeof(ClickCommandBehavior));

    public static readonly DependencyProperty DoubleClickCommandProperty =
        DependencyProperty.RegisterAttached("DoubleClickCommand", typeof(ICommand), typeof(ClickCommandBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty DoubleClickCommandParameterProperty =
        DependencyProperty.RegisterAttached("DoubleClickCommandParameter", typeof(object), typeof(ClickCommandBehavior));

    private static readonly DependencyProperty PendingTimerProperty =
        DependencyProperty.RegisterAttached("PendingTimer", typeof(DispatcherTimer), typeof(ClickCommandBehavior));

    public static void SetSingleClickCommand(DependencyObject element, ICommand? value) => element.SetValue(SingleClickCommandProperty, value);
    public static ICommand? GetSingleClickCommand(DependencyObject element) => (ICommand?)element.GetValue(SingleClickCommandProperty);

    public static void SetSingleClickCommandParameter(DependencyObject element, object? value) => element.SetValue(SingleClickCommandParameterProperty, value);
    public static object? GetSingleClickCommandParameter(DependencyObject element) => element.GetValue(SingleClickCommandParameterProperty);

    public static void SetDoubleClickCommand(DependencyObject element, ICommand? value) => element.SetValue(DoubleClickCommandProperty, value);
    public static ICommand? GetDoubleClickCommand(DependencyObject element) => (ICommand?)element.GetValue(DoubleClickCommandProperty);

    public static void SetDoubleClickCommandParameter(DependencyObject element, object? value) => element.SetValue(DoubleClickCommandParameterProperty, value);
    public static object? GetDoubleClickCommandParameter(DependencyObject element) => element.GetValue(DoubleClickCommandParameterProperty);

    [DllImport("user32.dll")]
    private static extern int GetDoubleClickTime();

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;

        element.MouseLeftButtonDown -= OnMouseLeftButtonDown;
        element.MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var element = (DependencyObject)sender;
        CancelPendingSingleClick(element);

        if (e.ClickCount >= 2)
        {
            var doubleClickCommand = GetDoubleClickCommand(element);
            var parameter = GetDoubleClickCommandParameter(element);
            if (doubleClickCommand?.CanExecute(parameter) == true)
                doubleClickCommand.Execute(parameter);
            return;
        }

        var singleClickCommand = GetSingleClickCommand(element);
        if (singleClickCommand is null)
            return;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime()) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            element.ClearValue(PendingTimerProperty);

            var parameter = GetSingleClickCommandParameter(element);
            if (singleClickCommand.CanExecute(parameter))
                singleClickCommand.Execute(parameter);
        };

        element.SetValue(PendingTimerProperty, timer);
        timer.Start();
    }

    private static void CancelPendingSingleClick(DependencyObject element)
    {
        if (element.GetValue(PendingTimerProperty) is DispatcherTimer timer)
        {
            timer.Stop();
            element.ClearValue(PendingTimerProperty);
        }
    }
}
