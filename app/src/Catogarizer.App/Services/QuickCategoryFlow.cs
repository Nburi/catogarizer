using System.Threading.Tasks;
using System.Windows;
using Catogarizer.App.ViewModels;
using Catogarizer.Core.Services;

namespace Catogarizer.App.Services;

/// <summary>
/// The one way to make a quick category, shared by the home, the palette and the tray: a small
/// dialog asks for an optional name, then the category is created and entered.
/// </summary>
public sealed class QuickCategoryFlow
{
    private readonly LibraryService _library;
    private readonly TemporaryCategoryService _temporary;
    private readonly IDialogService _dialogs;
    private bool _isOpen;

    public QuickCategoryFlow(LibraryService library, TemporaryCategoryService temporary, IDialogService dialogs)
    {
        _library = library;
        _temporary = temporary;
        _dialogs = dialogs;
    }

    /// <summary>Asks for a name and starts the category. Returns false if the user cancelled. UI thread only.</summary>
    public async Task<bool> StartAsync()
    {
        // The dialog is modal, but the tray menu and the palette can ask again while it is up.
        if (_isOpen) return false;
        _isOpen = true;
        try
        {
            var viewModel = new CategoryEditDialogViewModel(_library.Categories, quickSuggestion: _library.NextTemporaryName());
            if (_dialogs.ShowCategoryEdit(viewModel) is not { } name) return false;

            try
            {
                await Task.Run(() => _temporary.Start(name));
            }
            catch (ArgumentException)
            {
                // The name was taken in the moment between the dialog and here; an automatic one always works.
                await Task.Run(() => _temporary.Start(null));
            }
            return true;
        }
        finally
        {
            _isOpen = false;
        }
    }

    /// <summary>For surfaces that are closing themselves (palette, tray menu): start once they are out of the way.</summary>
    public void Begin() => Application.Current.Dispatcher.BeginInvoke(new Action(async () => await StartAsync()));
}
