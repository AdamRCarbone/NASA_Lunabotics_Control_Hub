using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NASA_Lunabotics_Control_Hub.Controls.Controls;

public partial class KeyInputGrid : UserControl
{
    public static readonly StyledProperty<string> WKeyActiveClassProperty =
        AvaloniaProperty.Register<KeyInputGrid, string>(nameof(WKeyActiveClass));

    public static readonly StyledProperty<string> AKeyActiveClassProperty =
        AvaloniaProperty.Register<KeyInputGrid, string>(nameof(AKeyActiveClass));

    public static readonly StyledProperty<string> SKeyActiveClassProperty =
        AvaloniaProperty.Register<KeyInputGrid, string>(nameof(SKeyActiveClass));

    public static readonly StyledProperty<string> DKeyActiveClassProperty =
        AvaloniaProperty.Register<KeyInputGrid, string>(nameof(DKeyActiveClass));

    public static readonly StyledProperty<string> UpKeyActiveClassProperty =
        AvaloniaProperty.Register<KeyInputGrid, string>(nameof(UpKeyActiveClass));

    public static readonly StyledProperty<string> DownKeyActiveClassProperty =
        AvaloniaProperty.Register<KeyInputGrid, string>(nameof(DownKeyActiveClass));

    public static readonly StyledProperty<string> LeftKeyActiveClassProperty =
        AvaloniaProperty.Register<KeyInputGrid, string>(nameof(LeftKeyActiveClass));

    public static readonly StyledProperty<string> RightKeyActiveClassProperty =
        AvaloniaProperty.Register<KeyInputGrid, string>(nameof(RightKeyActiveClass));

    public KeyInputGrid()
    {
        InitializeComponent();
    }

    public string WKeyActiveClass
    {
        get => GetValue(WKeyActiveClassProperty);
        set => SetValue(WKeyActiveClassProperty, value);
    }

    public string AKeyActiveClass
    {
        get => GetValue(AKeyActiveClassProperty);
        set => SetValue(AKeyActiveClassProperty, value);
    }

    public string SKeyActiveClass
    {
        get => GetValue(SKeyActiveClassProperty);
        set => SetValue(SKeyActiveClassProperty, value);
    }

    public string DKeyActiveClass
    {
        get => GetValue(DKeyActiveClassProperty);
        set => SetValue(DKeyActiveClassProperty, value);
    }

    public string UpKeyActiveClass
    {
        get => GetValue(UpKeyActiveClassProperty);
        set => SetValue(UpKeyActiveClassProperty, value);
    }

    public string DownKeyActiveClass
    {
        get => GetValue(DownKeyActiveClassProperty);
        set => SetValue(DownKeyActiveClassProperty, value);
    }

    public string LeftKeyActiveClass
    {
        get => GetValue(LeftKeyActiveClassProperty);
        set => SetValue(LeftKeyActiveClassProperty, value);
    }

    public string RightKeyActiveClass
    {
        get => GetValue(RightKeyActiveClassProperty);
        set => SetValue(RightKeyActiveClassProperty, value);
    }
}
