using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UnifiSmoobuTool.App.Converters;

/// <summary>Colors the Dashboard's reservation status text using the theme's semantic brushes.</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value as string ?? "";
        var key = status switch
        {
            "Cancelled" or "Access revoked" => "ErrorBrush",
            "Access granted" => "SuccessBrush",
            "Needs review" => "WarningBrush",
            _ => "TextSecondaryBrush",
        };

        return System.Windows.Application.Current?.TryFindResource(key) as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
