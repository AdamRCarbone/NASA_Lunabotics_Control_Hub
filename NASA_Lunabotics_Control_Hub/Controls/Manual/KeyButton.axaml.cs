using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NASA_Lunabotics_Control_Hub.Controls.Manual;

public partial class KeyButton : UserControl
{
    public static readonly StyledProperty<string> KeyLabelProperty =
        AvaloniaProperty.Register<KeyButton, string>(nameof(KeyLabel), "?");

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<KeyButton, bool>(nameof(IsActive), false);

    private Border _keyBorder = null!;
    private TextBlock _keyLabelText = null!;

    public string KeyLabel
    {
        get => GetValue(KeyLabelProperty);
        set
        {
            SetValue(KeyLabelProperty, value);
            UpdateLabel();
        }
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set
        {
            SetValue(IsActiveProperty, value);
            UpdateActiveState();
        }
    }

    public KeyButton()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _keyBorder    = this.FindControl<Border>("KeyBorder")!;
        _keyLabelText = this.FindControl<TextBlock>("KeyLabelText")!;

        UpdateLabel();
        UpdateActiveState();
    }

    private void UpdateLabel()
    {
        _keyLabelText.Text = KeyLabel;
    }

    private void UpdateActiveState()
    {
        _keyBorder.Classes.Set("active", IsActive);
    }
}
