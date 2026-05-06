using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NASA_Lunabotics_Control_Hub.Controls.Telemetry;

public partial class WheelTelemetryCard : UserControl
{
    public static readonly StyledProperty<string> WheelNameProperty =
        AvaloniaProperty.Register<WheelTelemetryCard, string>(nameof(WheelName));

    public static readonly StyledProperty<string> AngularVelocityProperty =
        AvaloniaProperty.Register<WheelTelemetryCard, string>(nameof(AngularVelocity));

    public static readonly StyledProperty<string> PositionProperty =
        AvaloniaProperty.Register<WheelTelemetryCard, string>(nameof(Position));

    public static readonly StyledProperty<string> TorqueProperty =
        AvaloniaProperty.Register<WheelTelemetryCard, string>(nameof(Torque));

    public static readonly StyledProperty<string> CurrentProperty =
        AvaloniaProperty.Register<WheelTelemetryCard, string>(nameof(Current));

    public static readonly StyledProperty<string> VoltageProperty =
        AvaloniaProperty.Register<WheelTelemetryCard, string>(nameof(Voltage));

    public WheelTelemetryCard()
    {
        InitializeComponent();
    }

    public string WheelName
    {
        get => GetValue(WheelNameProperty);
        set => SetValue(WheelNameProperty, value);
    }

    public string AngularVelocity
    {
        get => GetValue(AngularVelocityProperty);
        set => SetValue(AngularVelocityProperty, value);
    }

    public string Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public string Torque
    {
        get => GetValue(TorqueProperty);
        set => SetValue(TorqueProperty, value);
    }

    public string Current
    {
        get => GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    public string Voltage
    {
        get => GetValue(VoltageProperty);
        set => SetValue(VoltageProperty, value);
    }
}
