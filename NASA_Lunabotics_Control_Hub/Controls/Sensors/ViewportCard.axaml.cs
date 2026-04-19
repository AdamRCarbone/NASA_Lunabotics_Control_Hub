using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NASA_Lunabotics_Control_Hub.ViewModels;

namespace NASA_Lunabotics_Control_Hub.Controls.Sensors;

public partial class ViewportCard : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ViewportCard, string>(nameof(Label));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ViewportCard, bool>(nameof(IsActive));

    public static readonly StyledProperty<string> ViewportIdProperty =
        AvaloniaProperty.Register<ViewportCard, string>(nameof(ViewportId));

    public ViewportCard()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        private set
        {
            SetValue(IsActiveProperty, value);
            UpdateVisualState();
        }
    }

    public string ViewportId
    {
        get => GetValue(ViewportIdProperty);
        set => SetValue(ViewportIdProperty, value);
    }

    private void UpdateVisualState()
    {
        var indicator = this.FindControl<Border>("ActiveIndicator");
        var border = this.FindControl<Border>("RootBorder");

        if (IsActive)
        {
            indicator?.SetValue(BackgroundProperty, new SolidColorBrush(Color.Parse("#4ade80")));
            border?.SetValue(BorderBrushProperty, new SolidColorBrush(Color.Parse("#4ade80")));
            border?.SetValue(BorderThicknessProperty, new Thickness(2));
        }
        else
        {
            indicator?.SetValue(BackgroundProperty, new SolidColorBrush(Color.Parse("#606060")));
            border?.SetValue(BorderBrushProperty, new SolidColorBrush(Color.Parse("#333333")));
            border?.SetValue(BorderThicknessProperty, new Thickness(1));
        }
    }

    private void Border_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var dataContext = this.DataContext;
        if (dataContext is MainViewModel vm)
        {
            vm.OnViewportSelected(ViewportId);
        }
    }
}
