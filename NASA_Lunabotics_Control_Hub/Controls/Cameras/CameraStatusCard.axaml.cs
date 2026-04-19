using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml;

namespace NASA_Lunabotics_Control_Hub.Controls.Cameras;

public partial class CameraStatusCard : UserControl
{
    public static readonly StyledProperty<string> CameraNameProperty =
        AvaloniaProperty.Register<CameraStatusCard, string>(nameof(CameraName));

    public static readonly StyledProperty<string> CameraDescriptionProperty =
        AvaloniaProperty.Register<CameraStatusCard, string>(nameof(CameraDescription));

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<CameraStatusCard, string>(nameof(StatusText));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<CameraStatusCard, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsOnlineProperty =
        AvaloniaProperty.Register<CameraStatusCard, bool>(nameof(IsOnline));

    public static readonly StyledProperty<string> StatusClassProperty =
        AvaloniaProperty.Register<CameraStatusCard, string>(nameof(StatusClass));

    public static readonly StyledProperty<string> IndicatorClassProperty =
        AvaloniaProperty.Register<CameraStatusCard, string>(nameof(IndicatorClass));

    public static readonly StyledProperty<IBrush> TextColorProperty =
        AvaloniaProperty.Register<CameraStatusCard, IBrush>(nameof(TextColor));

    public static readonly StyledProperty<IBrush> StatusBackgroundProperty =
        AvaloniaProperty.Register<CameraStatusCard, IBrush>(nameof(StatusBackground));

    public static readonly StyledProperty<IBrush> StatusTextColorProperty =
        AvaloniaProperty.Register<CameraStatusCard, IBrush>(nameof(StatusTextColor));

    public CameraStatusCard()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public string CameraName
    {
        get => GetValue(CameraNameProperty);
        set { SetValue(CameraNameProperty, value); UpdateVisualState(); }
    }

    public string CameraDescription
    {
        get => GetValue(CameraDescriptionProperty);
        set => SetValue(CameraDescriptionProperty, value);
    }

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set { SetValue(IsActiveProperty, value); UpdateVisualState(); }
    }

    public bool IsOnline
    {
        get => GetValue(IsOnlineProperty);
        set { SetValue(IsOnlineProperty, value); UpdateVisualState(); }
    }

    public string StatusClass
    {
        get => GetValue(StatusClassProperty);
        set => SetValue(StatusClassProperty, value);
    }

    public string IndicatorClass
    {
        get => GetValue(IndicatorClassProperty);
        set => SetValue(IndicatorClassProperty, value);
    }

    public IBrush TextColor
    {
        get => GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public IBrush StatusBackground
    {
        get => GetValue(StatusBackgroundProperty);
        set => SetValue(StatusBackgroundProperty, value);
    }

    public IBrush StatusTextColor
    {
        get => GetValue(StatusTextColorProperty);
        set => SetValue(StatusTextColorProperty, value);
    }

    private void UpdateVisualState()
    {
        if (IsActive)
        {
            StatusClass = "camera-status-active";
            IndicatorClass = "active";
            StatusBackground = Brushes.FromHex("#00643C");
            StatusTextColor = Brushes.White;
            TextColor = Brushes.White;
        }
        else if (IsOnline)
        {
            StatusClass = "status-offline";
            IndicatorClass = "inactive";
            StatusBackground = Brushes.FromHex("#2D2D2D");
            StatusTextColor = Brushes.FromHex("#606060");
            TextColor = Brushes.FromHex("#C0C0C0");
        }
        else
        {
            StatusClass = "status-offline";
            IndicatorClass = "inactive";
            StatusBackground = Brushes.FromHex("#2D2D2D");
            StatusTextColor = Brushes.FromHex("#606060");
            TextColor = Brushes.FromHex("#C0C0C0");
        }
    }
}
