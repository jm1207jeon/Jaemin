using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using QmsWeaver.Core.Models;

namespace QmsWeaver.App.Controls;

/// <summary>문서 타입 → 범례용 미니 형상 Geometry (12x12 기준). 그래프 노드 형상과 동일한 문법.</summary>
public sealed class TypeShapeConverter : IValueConverter
{
    public static readonly TypeShapeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var type = value as string ?? "";
        return Geometry.Parse(type switch
        {
            NodeTypes.Manual => "M6,1 A5,5 0 1 0 6,11 A5,5 0 1 0 6,1 M6,3 A3,3 0 1 0 6,9 A3,3 0 1 0 6,3", // 이중 링
            NodeTypes.Procedure => "M6,1.5 A4.5,4.5 0 1 0 6,10.5 A4.5,4.5 0 1 0 6,1.5",
            NodeTypes.Sop or NodeTypes.WorkStandard => "M3,1.5 L9,1.5 A1.5,1.5 0 0 1 10.5,3 L10.5,9 A1.5,1.5 0 0 1 9,10.5 L3,10.5 A1.5,1.5 0 0 1 1.5,9 L1.5,3 A1.5,1.5 0 0 1 3,1.5 Z",
            NodeTypes.Form => "M6,1 L11,6 L6,11 L1,6 Z",
            NodeTypes.Standard => "M6,1.5 L10.8,10 L1.2,10 Z",
            NodeTypes.Regulation => "M1.2,2 L10.8,2 L6,10.5 Z",
            NodeTypes.TechDoc => "M8.5,1.7 L11,6 L8.5,10.3 L3.5,10.3 L1,6 L3.5,1.7 Z",
            _ => "M6,1.5 A4.5,4.5 0 1 0 6,10.5 A4.5,4.5 0 1 0 6,1.5",
        });
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
