using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MailIsland.Converters;

/// <summary>任意枚举 -> 其索引值（用于 ComboBox SelectedIndex）。</summary>
public class EnumIndexConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || !value.GetType().IsEnum) return 0;
        var values = Enum.GetValues(value.GetType());
        return Array.IndexOf(values, value);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!targetType.IsEnum) return null;

        var values = Enum.GetValues(targetType);
        if (values.Length == 0) return null;

        return value is int idx && idx >= 0 && idx < values.Length
            ? values.GetValue(idx)
            : values.GetValue(0);
    }
}