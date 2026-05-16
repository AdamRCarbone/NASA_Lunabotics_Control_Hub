using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NASA_Lunabotics_Control_Hub.Controls.Telemetry;

public partial class DataCard : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<DataCard, string>(nameof(Label));

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<DataCard, string>(nameof(Value));

    public static readonly StyledProperty<string> SubValueProperty =
        AvaloniaProperty.Register<DataCard, string>(nameof(SubValue));

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<DataCard, string>(nameof(Unit));

    public static readonly StyledProperty<double> ValueFontSizeProperty =
        AvaloniaProperty.Register<DataCard, double>(nameof(ValueFontSize), defaultValue: 16.0);

    public static readonly StyledProperty<string> ValueForegroundProperty =
        AvaloniaProperty.Register<DataCard, string>(nameof(ValueForeground), defaultValue: "#606060");

    public DataCard()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string SubValue
    {
        get => GetValue(SubValueProperty);
        set => SetValue(SubValueProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public double ValueFontSize
    {
        get => GetValue(ValueFontSizeProperty);
        set => SetValue(ValueFontSizeProperty, value);
    }

    public string ValueForeground
    {
        get => GetValue(ValueForegroundProperty);
        set => SetValue(ValueForegroundProperty, value);
    }
}
