using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ReviFlash.Converters;

public sealed class EditorWidthConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
        {
            return double.NaN;
        }

        var isLocked = values[0] as bool? ?? false;
        var width = values[1] as double? ?? double.NaN;

        return isLocked && width > 0 ? width : double.NaN;
    }
}