using System.Windows;
using System.Windows.Input;
using Catogarizer.App.ViewModels;

namespace Catogarizer.App.Views;

public partial class CommandPaletteWindow : Window
{
    public CommandPaletteViewModel ViewModel { get; }

    public CommandPaletteWindow(CommandPaletteViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();

        Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - Width) / 2);
        Top = SystemParameters.PrimaryScreenHeight * 0.2;

        Loaded += (_, _) => SearchBox.Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && ViewModel.Results.Count > 0)
        {
            ViewModel.OpenCommand.Execute(ViewModel.Results[0]);
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object sender, EventArgs e) => Close();
}
