using System.Windows;
using Catogarizer.App.ViewModels;
using Catogarizer.Core.Persistence;

namespace Catogarizer.App;

public partial class MainWindow : Window
{
    public MainWindow(IConfigStore configStore)
    {
        InitializeComponent();
        DataContext = new MainViewModel(configStore);
    }
}
