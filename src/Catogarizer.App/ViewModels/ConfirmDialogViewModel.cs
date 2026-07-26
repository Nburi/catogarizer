namespace Catogarizer.App.ViewModels;

public sealed class ConfirmDialogViewModel
{
    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }

    public ConfirmDialogViewModel(string title, string message, string confirmText)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
    }
}
