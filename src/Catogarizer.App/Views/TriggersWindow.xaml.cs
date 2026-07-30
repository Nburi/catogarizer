using System.Windows;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class TriggersWindow : Window
{
    public TriggersWindow(TriggersViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
