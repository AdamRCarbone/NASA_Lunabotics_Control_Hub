using System;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace NASA_Lunabotics_Control_Hub.Controls.Manual;

public partial class BucketDial : UserControl
{
    private RotateTransform _dialRotation = null!;
    private KeyButton _leftBtn = null!;
    private KeyButton _rightBtn = null!;

    public BucketDial()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        var pointer  = this.FindControl<Rectangle>("DialPointer")!;
        _dialRotation = pointer.RenderTransform as RotateTransform
            ?? throw new InvalidOperationException(
                "BucketDial: DialPointer must have a RotateTransform as its RenderTransform.");
        _leftBtn      = this.FindControl<KeyButton>("LeftBtn")!;
        _rightBtn     = this.FindControl<KeyButton>("RightBtn")!;
    }

    /// <summary>Called from ManualControl each timer tick.</summary>
    public void UpdateState(double value, bool leftHeld, bool rightHeld)
    {
        _dialRotation.Angle  = value * 90.0;
        _leftBtn.IsActive    = leftHeld;
        _rightBtn.IsActive   = rightHeld;
    }
}
