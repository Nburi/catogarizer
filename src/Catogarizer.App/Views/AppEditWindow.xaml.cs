using System.Windows;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class AppEditWindow : Window
{
    public AppEditDialogViewModel ViewModel { get; }

    public AppEditWindow(AppEditDialogViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => DialogResult = true;
        Loaded += (_, _) => SearchBox.Focus();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
