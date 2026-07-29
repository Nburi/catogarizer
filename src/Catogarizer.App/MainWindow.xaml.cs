using System.Windows;
using Catogarizer.App.Services;
using Catogarizer.App.ViewModels;
using Catogarizer.Core.Services;

namespace Catogarizer.App;

public partial class MainWindow : Window
{
    public MainWindow(LibraryService library, IInstalledAppFinder installedAppFinder, IDialogService dialogService)
    {
        InitializeComponent();
        DataContext = new MainViewModel(library, installedAppFinder, dialogService);
    }
}
