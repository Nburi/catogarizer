using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Catogarizer.App.Converters;

/// <summary>Visible when a count is zero - for "this list is empty" messages.</summary>
[ValueConversion(typeof(int), typeof(Visibility))]
public sealed class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
