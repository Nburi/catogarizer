using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Catogarizer.App.Converters;

/// <summary>[count, isLoading] → Visible only when the list is empty and nothing is loading (an empty state that never flashes during a load).</summary>
public sealed class EmptyAndIdleToVisibleConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [int count, bool isLoading] && count == 0 && !isLoading ? Visibility.Visible : Visibility.Collapsed;

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
