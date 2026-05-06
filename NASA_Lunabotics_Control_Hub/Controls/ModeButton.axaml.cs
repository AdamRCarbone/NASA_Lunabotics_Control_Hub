using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace NASA_Lunabotics_Control_Hub.Controls;

public partial class ModeButton : UserControl
{
    public static readonly StyledProperty<string> ModeNameProperty =
        AvaloniaProperty.Register<ModeButton, string>(nameof(ModeName), "Mode");

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ModeButton, bool>(nameof(IsSelected), false);

    public static readonly StyledProperty<bool> IsInStateProperty =
        AvaloniaProperty.Register<ModeButton, bool>(nameof(IsInState), false);

    public static readonly StyledProperty<IBrush> ModeForegroundProperty =
        AvaloniaProperty.Register<ModeButton, IBrush>(nameof(ModeForeground), Brushes.Gray);

    public static readonly StyledProperty<IBrush> StatusIndicatorColorProperty =
        AvaloniaProperty.Register<ModeButton, IBrush>(nameof(StatusIndicatorColor), Brushes.Gray);

    public ModeButton()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public string ModeName
    {
        get => GetValue(ModeNameProperty);
        set
        {
            SetValue(ModeNameProperty, value);
            UpdateVisualState();
        }
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set
        {
            SetValue(IsSelectedProperty, value);
            UpdateVisualState();
        }
    }

    public bool IsInState
    {
        get => GetValue(IsInStateProperty);
        set
        {
            SetValue(IsInStateProperty, value);
            UpdateVisualState();
        }
    }

    public IBrush ModeForeground
    {
        get => GetValue(ModeForegroundProperty);
        set => SetValue(ModeForegroundProperty, value);
    }

    public IBrush StatusIndicatorColor
    {
        get => GetValue(StatusIndicatorColorProperty);
        set => SetValue(StatusIndicatorColorProperty, value);
    }

    private void UpdateVisualState()
    {
        ModeForeground = IsSelected ? Brushes.White : Brushes.LightGray;
        StatusIndicatorColor = IsInState ? Brushes.Green : Brushes.Gray;
    }
}
