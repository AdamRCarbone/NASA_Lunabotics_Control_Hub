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
                return state switch
                {
                    ModeState.Idle => new SolidColorBrush(Color.Parse("#0D0D0D")),     // Very dark gray, clearly visible against #2D2D2D
                    ModeState.Pending => new SolidColorBrush(Color.Parse("#FF4444")),  // Bright red
                    ModeState.Confirmed => new SolidColorBrush(Color.Parse("#00FF88")), // Bright green
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
