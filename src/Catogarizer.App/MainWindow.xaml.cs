using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Catogarizer.App.ViewModels;
using H.NotifyIcon.Core;

namespace Catogarizer.App;

public partial class MainWindow : Window
{
    private bool _isExiting;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        TrayIcon.DataContext = viewModel;
        viewModel.AppBlocked += (_, args) => Dispatcher.BeginInvoke(() =>
            TrayIcon.ShowNotification(
                "Blocked",
                $"{args.ProcessName} was closed because it's on your blocked list.",
                NotificationIcon.Warning));
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        MaximizeRestoreButton.Content = WindowState == WindowState.Maximized ? "▣" : "□";
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            Hide();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
            return;

        e.Cancel = true;
        Hide();
    }

    private void TrayIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e) => RestoreFromTray();

    private void ShowWindow_Click(object sender, RoutedEventArgs e) => RestoreFromTray();

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _isExiting = true;
        TrayIcon.Dispose();
        Close();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
