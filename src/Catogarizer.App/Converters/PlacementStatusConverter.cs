using System.Globalization;
using System.Windows.Data;
using Catogarizer.Core.Models;

namespace Catogarizer.App.Converters;

[ValueConversion(typeof(WindowRect), typeof(string))]
public sealed class PlacementStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is WindowRect r ? $"Opens at {r.Width}×{r.Height}" : "Opens at its default position";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
