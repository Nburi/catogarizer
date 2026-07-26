using System.Windows;
using System.Windows.Input;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(ConfirmDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
