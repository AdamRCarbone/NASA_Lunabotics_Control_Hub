using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml;

namespace NASA_Lunabotics_Control_Hub.Controls.Cameras;

public partial class CameraStatusCard : UserControl
{
    public static readonly StyledProperty<string> CameraNameProperty =
        AvaloniaProperty.Register<CameraStatusCard, string>(nameof(CameraName));

    public static readonly StyledProperty<string> CameraTypeProperty =
        AvaloniaProperty.Register<CameraStatusCard, string>(nameof(CameraType));

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<CameraStatusCard, string>(nameof(StatusText));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<CameraStatusCard, bool>(nameof(IsActive));

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

    public string CameraType
    {
        get => GetValue(CameraTypeProperty);
        set { SetValue(CameraTypeProperty, value); UpdateVisualState(); }
    }

    public string StatusText
    {
        get => GetValue(StatusTextProperty);
        set { SetValue(StatusTextProperty, value); UpdateVisualState(); }
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set { SetValue(IsActiveProperty, value); UpdateVisualState(); }
    }

    private void UpdateVisualState()
    {
        var rootBorder = this.FindControl<Border>("RootBorder");
        var typeBadge = this.FindControl<Border>("TypeBadge");
        var typeText = this.FindControl<TextBlock>("TypeText");
        var statusBadge = this.FindControl<Border>("StatusBadge");
        var statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
        var indicator = this.FindControl<Border>("Indicator");

        // Set type badge colors based on camera type
        if (CameraType == "Depth")
        {
            typeBadge?.SetValue(BackgroundProperty, new SolidColorBrush(Color.Parse("#00643C")));
            typeText?.SetValue(ForegroundProperty, Brushes.White);
        }
        else if (CameraType == "RGB")
        {
            typeBadge?.SetValue(BackgroundProperty, new SolidColorBrush(Color.Parse("#2D2D2D")));
            typeText?.SetValue(ForegroundProperty, new SolidColorBrush(Color.Parse("#C0C0C0")));
        }
        else if (CameraType == "AprilTag")
        {
            typeBadge?.SetValue(BackgroundProperty, new SolidColorBrush(Color.Parse("#3D3D3D")));
            typeText?.SetValue(ForegroundProperty, new SolidColorBrush(Color.Parse("#A0A0A0")));
        }

        // Set active/offline state
        if (IsActive)
        {
            rootBorder?.Classes.Add("camera-status-active");
            statusBadge?.SetValue(BackgroundProperty, new SolidColorBrush(Color.Parse("#00643C")));
            statusTextBlock?.SetValue(ForegroundProperty, Brushes.White);
            indicator?.SetValue(BackgroundProperty, new SolidColorBrush(Color.Parse("#4ade80")));
        }
        else
        {
            if (rootBorder?.Classes.Contains("camera-status-active") == true)
                rootBorder.Classes.Remove("camera-status-active");
            statusBadge?.SetValue(BackgroundProperty, new SolidColorBrush(Color.Parse("#2D2D2D")));
            statusTextBlock?.SetValue(ForegroundProperty, new SolidColorBrush(Color.Parse("#606060")));
            indicator?.SetValue(BackgroundProperty, new SolidColorBrush(Color.Parse("#606060")));
        }
    }
}
