using Catogarizer.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

public partial class AppTileViewModel : ObservableObject
{
    public AppEntry Model { get; }

    public string Name => Model.Name;
    public string Initial => string.IsNullOrEmpty(Model.Name) ? "?" : Model.Name[..1].ToUpperInvariant();

    [ObservableProperty]
    private bool isRunning;

    public AppTileViewModel(AppEntry model)
    {
        Model = model;
    }
}
