using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using NASA_Lunabotics_Control_Hub.ViewModels;

namespace NASA_Lunabotics_Control_Hub.Converters
{
    public class ModeStateToBrushConverter : IValueConverter
    {
        public static readonly ModeStateToBrushConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ModeState state)
            {
                // Check if parameter is "text" to return text color instead of LED color
                string? param = parameter?.ToString();
                if (param == "text")
                {
                    return state switch
                    {
                        ModeState.Idle => new SolidColorBrush(Color.Parse("#808080")), // Gray for idle
                        ModeState.Pending => new SolidColorBrush(Color.Parse("#004d28")), // Dark green for pending (like Manual button text)
                        ModeState.Confirmed => new SolidColorBrush(Color.Parse("#00FF88")), // Bright green for confirmed
                        _ => new SolidColorBrush(Color.Parse("#808080"))
                    };
                }

                // Check if parameter is "background" to return background color for selected/pending/confirmed state
                if (param == "background")
                {
                    return state switch
                    {
                        ModeState.Pending => new SolidColorBrush(Color.Parse("#00643C")), // CSU Green for pending (selected)
                        ModeState.Confirmed => new SolidColorBrush(Color.Parse("#00643C")), // CSU Green for confirmed
                        _ => new SolidColorBrush(Color.Parse("#2D2D2D")) // Default dark gray for idle
                    };
                }

                return state switch
                {
                    ModeState.Idle => new SolidColorBrush(Color.Parse("#0D0D0D")), // Very dark gray for LED
                    ModeState.Pending => new SolidColorBrush(Color.Parse("#FF4444")), // Bright red for LED
                    ModeState.Confirmed => new SolidColorBrush(Color.Parse("#00FF88")), // Bright green for LED
                    _ => new SolidColorBrush(Color.Parse("#0D0D0D"))
                };
            }
            return new SolidColorBrush(Color.Parse("#0D0D0D"));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
