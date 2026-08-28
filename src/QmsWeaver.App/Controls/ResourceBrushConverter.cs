using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace QmsWeaver.App.Controls;

/// <summary>리소스 키 문자열(예: "TypeManualColor") → 현재 테마의 브러시.</summary>
public sealed class ResourceBrushConverter : IValueConverter
{
    public static readonly ResourceBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current is { } app &&
            app.TryGetResource(key, app.ActualThemeVariant, out var res) && res is Color c)
            return new SolidColorBrush(c);
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
