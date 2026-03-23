using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace ReviFlash.Converters;

public static class MathDelimiterHelper
{
    private static readonly Regex InlineMathRegex = new(@"\$\$(.+?)\$\$|\$(.+?)\$", RegexOptions.Singleline | RegexOptions.Compiled);

    public static bool IsDelimitedMath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        return trimmed.Length >= 4 && trimmed.StartsWith("$$", StringComparison.Ordinal) && trimmed.EndsWith("$$", StringComparison.Ordinal);
    }

    public static string StripMathDelimiters(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        if (!IsDelimitedMath(trimmed)) return trimmed;
        return trimmed.Substring(2, trimmed.Length - 4).Trim();
    }

    public static IReadOnlyList<MixedMathSegment> ParseSegments(string? value)
    {
        if (string.IsNullOrEmpty(value)) return Array.Empty<MixedMathSegment>();

        var segments = new List<MixedMathSegment>();
        var input = value;
        var currentIndex = 0;

        foreach (Match match in InlineMathRegex.Matches(input))
        {
            if (match.Index > currentIndex)
            {
                var textPart = input[currentIndex..match.Index];
                if (textPart.Length > 0)
                    segments.Add(new MixedMathSegment(textPart, isMath: false));
            }

            var mathPart = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            segments.Add(new MixedMathSegment(mathPart, isMath: true));
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < input.Length)
        {
            var tailText = input[currentIndex..];
            if (tailText.Length > 0)
                segments.Add(new MixedMathSegment(tailText, isMath: false));
        }

        if (segments.Count == 0)
            segments.Add(new MixedMathSegment(input, isMath: false));

        return segments;
    }
}

public sealed class MixedMathSegment
{
    public MixedMathSegment(string content, bool isMath)
    {
        Content = content;
        IsMath = isMath;
    }

    public string Content { get; }
    public bool IsMath { get; }
    public bool IsText => !IsMath;
}

public sealed class IsMathDelimitedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => MathDelimiterHelper.IsDelimitedMath(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class IsNotMathDelimitedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !MathDelimiterHelper.IsDelimitedMath(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class StripMathDelimitersConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => MathDelimiterHelper.StripMathDelimiters(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class MixedMathSegmentsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => MathDelimiterHelper.ParseSegments(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}