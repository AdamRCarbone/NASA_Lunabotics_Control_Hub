using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NASA_Lunabotics_Control_Hub.Converters;

public class ViewportIdToIsActiveConverter : IValueConverter
{
    public static readonly ViewportIdToIsActiveConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string activeViewportId && parameter is string cardViewportId)
        {
            return activeViewportId == cardViewportId;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
