using System.Globalization;
using Avalonia.Data.Converters;

namespace MailIsland.Converters;

/// <summary>未读 -> 加粗 转换器。</summary>
public class UnreadBoldConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}