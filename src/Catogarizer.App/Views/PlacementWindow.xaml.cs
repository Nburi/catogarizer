using System.Windows;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class PlacementWindow : Window
{
    public PlacementDialogViewModel ViewModel { get; }

    public PlacementWindow(PlacementDialogViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
