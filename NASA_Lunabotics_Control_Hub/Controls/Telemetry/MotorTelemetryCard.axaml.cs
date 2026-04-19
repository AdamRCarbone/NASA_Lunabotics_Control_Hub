using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NASA_Lunabotics_Control_Hub.Controls.Telemetry;

public partial class MotorTelemetryCard : UserControl
{
    public static readonly StyledProperty<string> SideNameProperty =
        AvaloniaProperty.Register<MotorTelemetryCard, string>(nameof(SideName));

    public static readonly StyledProperty<string> W1NameProperty =
        AvaloniaProperty.Register<MotorTelemetryCard, string>(nameof(W1Name));

    public static readonly StyledProperty<string> W1RPMProperty =
        AvaloniaProperty.Register<MotorTelemetryCard, string>(nameof(W1RPM));

    public static readonly StyledProperty<string> W2NameProperty =
        AvaloniaProperty.Register<MotorTelemetryCard, string>(nameof(W2Name));

    public static readonly StyledProperty<string> W2RPMProperty =
        AvaloniaProperty.Register<MotorTelemetryCard, string>(nameof(W2RPM));

    public static readonly StyledProperty<string> W3NameProperty =
        AvaloniaProperty.Register<MotorTelemetryCard, string>(nameof(W3Name));

    public static readonly StyledProperty<string> W3RPMProperty =
        AvaloniaProperty.Register<MotorTelemetryCard, string>(nameof(W3RPM));

    public static readonly StyledProperty<string> CurrentValuesProperty =
        AvaloniaProperty.Register<MotorTelemetryCard, string>(nameof(CurrentValues));

    public MotorTelemetryCard()
    {
        InitializeComponent();
    }

    public string SideName
    {
        get => GetValue(SideNameProperty);
        set => SetValue(SideNameProperty, value);
    }

    public string W1Name
    {
        get => GetValue(W1NameProperty);
        set => SetValue(W1NameProperty, value);
    }

    public string W1RPM
    {
        get => GetValue(W1RPMProperty);
        set => SetValue(W1RPMProperty, value);
    }

    public string W2Name
    {
        get => GetValue(W2NameProperty);
        set => SetValue(W2NameProperty, value);
    }

    public string W2RPM
    {
        get => GetValue(W2RPMProperty);
        set => SetValue(W2RPMProperty, value);
    }

    public string W3Name
    {
        get => GetValue(W3NameProperty);
        set => SetValue(W3NameProperty, value);
    }

    public string W3RPM
    {
        get => GetValue(W3RPMProperty);
        set => SetValue(W3RPMProperty, value);
    }

    public string CurrentValues
    {
        get => GetValue(CurrentValuesProperty);
        set => SetValue(CurrentValuesProperty, value);
    }
}
