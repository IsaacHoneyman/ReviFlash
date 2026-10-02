using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ReviFlash.Converters;

public sealed class BoolToOptionMarkerConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "✓" : "○";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}