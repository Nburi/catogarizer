using System.Windows;

namespace Catogarizer.App.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string heading, string message, string confirmText = "Delete")
    {
        InitializeComponent();
        HeadingText.Text = heading;
        Title = heading;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
