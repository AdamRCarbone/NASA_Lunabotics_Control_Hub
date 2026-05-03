using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NASA_Lunabotics_Control_Hub.Converters;

public class ViewportIdToDisplayNameConverter : IValueConverter
{
    public static readonly ViewportIdToDisplayNameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string viewportId)
        {
            return viewportId switch
            {
                "map" => "Live 3D Map",
                "depth_cam" => "Depth Camera",
                "left_side" => "Left Side Camera",
                "left_front" => "Left Front Camera",
                "right_side" => "Right Side Camera",
                "right_front" => "Right Front Camera",
                "back_rear" => "Back Rear Camera",
                "far_front" => "Far Front Camera",
                "far_right" => "Far Right Camera",
                "far_back" => "Far Back Camera",
                "far_left" => "Far Left Camera",
                "mosaic" => "Mosaic (All Cameras)",
                _ => "Viewport"
            };
        }
        return "Viewport";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
