using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using NASA_Lunabotics_Control_Hub.Components;

namespace NASA_Lunabotics_Control_Hub.Controls.Manual;

public partial class ManualControl : UserControl
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ManualControl, bool>(nameof(IsActive), false);

    private readonly ManipulatorInputState _state = new();

    private Border _containerBorder = null!;
    private KeyButton _wKey = null!, _aKey = null!, _sKey = null!, _dKey = null!;
    private ArmSlider _armSlider = null!;
    private BucketDial _bucketDial = null!;

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public ManualControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _containerBorder = this.FindControl<Border>("ContainerBorder")!;
        _wKey            = this.FindControl<KeyButton>("WKey")!;
        _aKey            = this.FindControl<KeyButton>("AKey")!;
        _sKey            = this.FindControl<KeyButton>("SKey")!;
        _dKey            = this.FindControl<KeyButton>("DKey")!;
        _armSlider       = this.FindControl<ArmSlider>("ArmSliderWidget")!;
        _bucketDial      = this.FindControl<BucketDial>("BucketDialWidget")!;

        this.GetObservable(IsActiveProperty).Subscribe(active =>
        {
            _containerBorder.Opacity          = active ? 1.0 : 0.4;
            _containerBorder.IsHitTestVisible = active;
        });
    }

    public void HandleKeyDown(Key key) => _state.HandleKeyDown(key);

    public void HandleKeyUp(Key key) => _state.HandleKeyUp(key);

    /// <summary>Integrate values and push to child visuals. dt = elapsed seconds.</summary>
    public void Tick(double dtSeconds)
    {
        if (!IsActive)
        {
            _state.Reset();
            _armSlider.UpdateState(0, false, false);
            _bucketDial.UpdateState(0, false, false);
            return;
        }
        _state.Tick(dtSeconds);
        _armSlider.UpdateState(_state.ArmValue, _state.UpHeld, _state.DownHeld);
        _bucketDial.UpdateState(_state.BucketValue, _state.LeftHeld, _state.RightHeld);
    }

    /// <summary>Refresh WASD key caps from live joystick state.</summary>
    public void UpdateFromJoystick(HashSet<Key> activeKeys)
    {
        _wKey.IsActive = activeKeys.Contains(Key.W);
        _aKey.IsActive = activeKeys.Contains(Key.A);
        _sKey.IsActive = activeKeys.Contains(Key.S);
        _dKey.IsActive = activeKeys.Contains(Key.D);
    }

    /// <summary>
    /// Returns full 8-bit bitfield for wire protocol.
    /// bit0=W, bit1=A, bit2=S, bit3=D, bit4=↑, bit5=↓, bit6=←, bit7=→
    /// </summary>
    public byte GetKeyBitfield(HashSet<Key> joystickActiveKeys)
    {
        byte b = _state.GetArrowBitfield();
        if (joystickActiveKeys.Contains(Key.W)) b |= 0x01;
        if (joystickActiveKeys.Contains(Key.A)) b |= 0x02;
        if (joystickActiveKeys.Contains(Key.S)) b |= 0x04;
        if (joystickActiveKeys.Contains(Key.D)) b |= 0x08;
        return b;
    }
}
