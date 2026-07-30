using Catogarizer.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catogarizer.App.ViewModels;

/// <summary>One checkbox row in a CloseApps action's app picker.</summary>
public sealed partial class AppCloseSelection : ObservableObject
{
    public AppEntry App { get; }

    [ObservableProperty]
    private bool _isChecked;

    public AppCloseSelection(AppEntry app, bool isChecked)
    {
        App = app;
        _isChecked = isChecked;
    }
}
