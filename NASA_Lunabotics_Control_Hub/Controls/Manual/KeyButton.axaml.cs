using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

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
        set => SetValue(KeyLabelProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
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

        this.GetObservable(KeyLabelProperty).Subscribe(v =>
        {
            if (_keyLabelText is not null) _keyLabelText.Text = v;
        });
        this.GetObservable(IsActiveProperty).Subscribe(v =>
        {
            if (_keyBorder is not null) _keyBorder.Classes.Set("active", v);
        });
    }
}
