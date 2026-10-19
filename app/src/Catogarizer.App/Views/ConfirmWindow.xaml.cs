using System.Collections.Generic;
using System.Windows;

namespace Catogarizer.App.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string heading, string message, string confirmText = "Delete", IReadOnlyList<string>? items = null)
    {
        InitializeComponent();
        HeadingText.Text = heading;
        Title = heading;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        if (items is { Count: > 0 })
        {
            ItemList.ItemsSource = items;
            ItemScroll.Visibility = Visibility.Visible;
        }
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
