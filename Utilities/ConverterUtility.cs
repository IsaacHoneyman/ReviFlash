using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace ReviFlash.Converters;

public sealed class BoolToOptionMarkerConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "✓" : "○";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary> True when every bound value is equal, e.g. a card's ID and the ID of the card being edited. </summary>
public sealed class AllEqualConverter : IMultiValueConverter
{
    public static readonly AllEqualConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Count > 0 && values.All(value => Equals(value, values[0]));
}
