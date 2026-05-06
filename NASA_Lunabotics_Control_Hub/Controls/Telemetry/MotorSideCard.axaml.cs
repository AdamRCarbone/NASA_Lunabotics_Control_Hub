using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NASA_Lunabotics_Control_Hub.Controls.Telemetry;

public partial class MotorSideCard : UserControl
{
    public static readonly StyledProperty<string> SideNameProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(SideName));

    public static readonly StyledProperty<string> W1NameProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W1Name));

    public static readonly StyledProperty<string> W1AngularVelProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W1AngularVel));

    public static readonly StyledProperty<string> W1PositionProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W1Position));

    public static readonly StyledProperty<string> W1TorqueProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W1Torque));

    public static readonly StyledProperty<string> W1CurrentProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W1Current));

    public static readonly StyledProperty<string> W1VoltageProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W1Voltage));

    public static readonly StyledProperty<string> W2NameProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W2Name));

    public static readonly StyledProperty<string> W2AngularVelProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W2AngularVel));

    public static readonly StyledProperty<string> W2PositionProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W2Position));

    public static readonly StyledProperty<string> W2TorqueProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W2Torque));

    public static readonly StyledProperty<string> W2CurrentProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W2Current));

    public static readonly StyledProperty<string> W2VoltageProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W2Voltage));

    public static readonly StyledProperty<string> W3NameProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W3Name));

    public static readonly StyledProperty<string> W3AngularVelProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W3AngularVel));

    public static readonly StyledProperty<string> W3PositionProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W3Position));

    public static readonly StyledProperty<string> W3TorqueProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W3Torque));

    public static readonly StyledProperty<string> W3CurrentProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W3Current));

    public static readonly StyledProperty<string> W3VoltageProperty =
        AvaloniaProperty.Register<MotorSideCard, string>(nameof(W3Voltage));

    public MotorSideCard()
    {
        InitializeComponent();
    }

    public string SideName
    {
        get => GetValue(SideNameProperty);
        set => SetValue(SideNameProperty, value);
    }

    #region Wheel 1
    public string W1Name
    {
        get => GetValue(W1NameProperty);
        set => SetValue(W1NameProperty, value);
    }

    public string W1AngularVel
    {
        get => GetValue(W1AngularVelProperty);
        set => SetValue(W1AngularVelProperty, value);
    }

    public string W1Position
    {
        get => GetValue(W1PositionProperty);
        set => SetValue(W1PositionProperty, value);
    }

    public string W1Torque
    {
        get => GetValue(W1TorqueProperty);
        set => SetValue(W1TorqueProperty, value);
    }

    public string W1Current
    {
        get => GetValue(W1CurrentProperty);
        set => SetValue(W1CurrentProperty, value);
    }

    public string W1Voltage
    {
        get => GetValue(W1VoltageProperty);
        set => SetValue(W1VoltageProperty, value);
    }
    #endregion

    #region Wheel 2
    public string W2Name
    {
        get => GetValue(W2NameProperty);
        set => SetValue(W2NameProperty, value);
    }

    public string W2AngularVel
    {
        get => GetValue(W2AngularVelProperty);
        set => SetValue(W2AngularVelProperty, value);
    }

    public string W2Position
    {
        get => GetValue(W2PositionProperty);
        set => SetValue(W2PositionProperty, value);
    }

    public string W2Torque
    {
        get => GetValue(W2TorqueProperty);
        set => SetValue(W2TorqueProperty, value);
    }

    public string W2Current
    {
        get => GetValue(W2CurrentProperty);
        set => SetValue(W2CurrentProperty, value);
    }

    public string W2Voltage
    {
        get => GetValue(W2VoltageProperty);
        set => SetValue(W2VoltageProperty, value);
    }
    #endregion

    #region Wheel 3
    public string W3Name
    {
        get => GetValue(W3NameProperty);
        set => SetValue(W3NameProperty, value);
    }

    public string W3AngularVel
    {
        get => GetValue(W3AngularVelProperty);
        set => SetValue(W3AngularVelProperty, value);
    }

    public string W3Position
    {
        get => GetValue(W3PositionProperty);
        set => SetValue(W3PositionProperty, value);
    }

    public string W3Torque
    {
        get => GetValue(W3TorqueProperty);
        set => SetValue(W3TorqueProperty, value);
    }

    public string W3Current
    {
        get => GetValue(W3CurrentProperty);
        set => SetValue(W3CurrentProperty, value);
    }

    public string W3Voltage
    {
        get => GetValue(W3VoltageProperty);
        set => SetValue(W3VoltageProperty, value);
    }
    #endregion
}
