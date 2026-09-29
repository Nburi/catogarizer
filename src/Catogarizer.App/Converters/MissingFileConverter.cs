using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Catogarizer.App.Converters;

/// <summary>True when the bound executable path points at a file that no longer exists.</summary>
[ValueConversion(typeof(string), typeof(bool))]
public sealed class MissingFileConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string path && !File.Exists(path);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
