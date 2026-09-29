using System.Windows;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class TriggerEditWindow : Window
{
    public TriggerEditWindow(TriggerEditDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => DialogResult = true;
    }
}
