using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NASA_Lunabotics_Control_Hub.Controls.Manual;

public partial class ArmSlider : UserControl
{
    private Slider _slider = null!;
    private KeyButton _upBtn = null!;
    private KeyButton _downBtn = null!;

    public ArmSlider()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _slider  = this.FindControl<Slider>("ArmSliderCtrl")!;
        _upBtn   = this.FindControl<KeyButton>("UpBtn")!;
        _downBtn = this.FindControl<KeyButton>("DownBtn")!;
    }

    /// <summary>Called from ManualControl each timer tick.</summary>
    public void UpdateState(double value, bool upHeld, bool downHeld)
    {
        _slider.Value    = value;
        _upBtn.IsActive   = upHeld;
        _downBtn.IsActive = downHeld;
    }
}
