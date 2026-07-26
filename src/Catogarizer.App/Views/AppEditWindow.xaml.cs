using System.Windows;
using System.Windows.Input;
using Catogarizer.App.ViewModels;
using Catogarizer.Core.Models;

namespace Catogarizer.App.Views;

public partial class AppEditWindow : Window
{
    private readonly AppEditDialogViewModel _viewModel;
    private readonly Guid? _existingId;
    private readonly int _order;

    public AppEntry? Result { get; private set; }

    public AppEditWindow(AppEditDialogViewModel viewModel, Guid? existingId, int order)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _existingId = existingId;
        _order = order;
        DataContext = viewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TryBuildEntry(_existingId, _order, out var entry))
        {
            Result = entry;
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
