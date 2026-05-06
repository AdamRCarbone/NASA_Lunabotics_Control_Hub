# Manual Control Widget Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Manual Control widget to the left panel that shows WASD/arrow key state, a vertical arm slider, and a rotary bucket dial — replacing the existing KeyInputGrid — and wires a stubbed send call into the 50 ms main timer.

**Architecture:** New `Controls/Manual/` folder holds four focused UserControls composed inside `ManualControl`. A plain C# `ManipulatorInputState` class in `Components/` owns arrow-key state and integrates ArmValue/BucketValue each tick. The existing `_keyUpdateTimer` in `MainView.axaml.cs` drives visual refresh + the stubbed `SendManipulatorCommandAsync` call. The widget greys out when mode ≠ Manual.

**Tech Stack:** Avalonia UI 11 (XAML + code-behind pattern), C# 12, existing `NetworkProtocol` lean binary framing.

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Modify | `Components/NetworkProtocol.cs` | Add `TYPE_MANIPULATOR`, `EncodeManipulator` |
| Modify | `Components/NetworkModeClient.cs` | Add `SendManipulatorCommandAsync` stub |
| Create | `Components/ManipulatorInputState.cs` | Key state flags + ArmValue/BucketValue integration + bitfield builder |
| Create | `Controls/Manual/KeyButton.axaml` + `.cs` | Single key cap: `KeyLabel` string, `IsActive` bool |
| Create | `Controls/Manual/ArmSlider.axaml` + `.cs` | Vertical slider + tick labels + ↑/↓ KeyButtons |
| Create | `Controls/Manual/BucketDial.axaml` + `.cs` | Rotary dial canvas + ←/→ KeyButtons |
| Create | `Controls/Manual/ManualControl.axaml` + `.cs` | Top-level container: composes children, `IsActive`, key routing |
| Modify | `Views/MainView.axaml` | Swap `KeyInputGrid` for `ManualControl`, add xmlns |
| Modify | `Views/MainView.axaml.cs` | Forward keys to ManualControl, update timer, wire mode state |
| Delete | `Controls/Controls/KeyInputGrid.axaml` + `.cs` | Replaced |

---

### Task 1: Wire Protocol — NetworkProtocol + NetworkModeClient

**Files:**
- Modify: `NASA_Lunabotics_Control_Hub/Components/NetworkProtocol.cs`
- Modify: `NASA_Lunabotics_Control_Hub/Components/NetworkModeClient.cs`

- [ ] **Step 1: Add TYPE_MANIPULATOR constant and EncodeManipulator to NetworkProtocol.cs**

Open `Components/NetworkProtocol.cs`. After the existing `TYPE_FAULT` constant line, add:

```csharp
public const byte TYPE_MANIPULATOR = 0x4D; // 'M'
```

After the existing `EncodeCommand` method, add:

```csharp
/// <summary>
/// Encode a Manipulator frame (Ground → Rover)
/// Format: [O][M][1][bitfield][crc] — 5 bytes
/// Bitfield: bit0=W, bit1=A, bit2=S, bit3=D, bit4=↑, bit5=↓, bit6=←, bit7=→
/// TODO: un-comment WriteAsync in NetworkModeClient once ROS parser is confirmed.
/// </summary>
public static byte[] EncodeManipulator(byte keyBitfield)
{
    var frame = new byte[5];
    frame[0] = MAGIC;
    frame[1] = TYPE_MANIPULATOR;
    frame[2] = 1;
    frame[3] = keyBitfield;
    frame[4] = CalcCrc8(frame, 4);
    return frame;
}
```

- [ ] **Step 2: Add SendManipulatorCommandAsync stub to NetworkModeClient.cs**

Open `Components/NetworkModeClient.cs`. Add this method after `SendModeCommandAsync`:

```csharp
/// <summary>
/// Send manipulator key state to rover.
/// TODO: un-comment WriteAsync once ROS-side 'M' frame parser is confirmed.
/// </summary>
public Task SendManipulatorCommandAsync(byte keyBitfield)
{
    // var frame = NetworkProtocol.EncodeManipulator(keyBitfield);
    // await _stream.WriteAsync(frame, 0, frame.Length, _cancelSource.Token);
    return Task.CompletedTask;
}
```

- [ ] **Step 3: Build to confirm no compile errors**

```
dotnet build NASA_Lunabotics_Control_Hub.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Components/NetworkProtocol.cs \
        NASA_Lunabotics_Control_Hub/Components/NetworkModeClient.cs
git commit -m "feat: add manipulator wire protocol type and send stub"
```

---

### Task 2: ManipulatorInputState

**Files:**
- Create: `NASA_Lunabotics_Control_Hub/Components/ManipulatorInputState.cs`

- [ ] **Step 1: Create ManipulatorInputState.cs**

Create `NASA_Lunabotics_Control_Hub/Components/ManipulatorInputState.cs`:

```csharp
using Avalonia.Input;

namespace NASA_Lunabotics_Control_Hub.Components;

public class ManipulatorInputState
{
    // Integrated values: arm 0.0 (down) – 1.0 (up), bucket -1.0 (left) – 1.0 (right)
    public double ArmValue { get; private set; } = 0.0;
    public double BucketValue { get; private set; } = 0.0;

    // Live key-held flags
    public bool UpHeld { get; private set; }
    public bool DownHeld { get; private set; }
    public bool LeftHeld { get; private set; }
    public bool RightHeld { get; private set; }

    // Units per second for full-range traversal in 2 s
    private const double RampRate = 0.5;

    public void HandleKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Up:    UpHeld    = true; break;
            case Key.Down:  DownHeld  = true; break;
            case Key.Left:  LeftHeld  = true; break;
            case Key.Right: RightHeld = true; break;
        }
    }

    public void HandleKeyUp(Key key)
    {
        switch (key)
        {
            case Key.Up:    UpHeld    = false; break;
            case Key.Down:  DownHeld  = false; break;
            case Key.Left:  LeftHeld  = false; break;
            case Key.Right: RightHeld = false; break;
        }
    }

    public void Tick(double dtSeconds)
    {
        double armDelta    = ((UpHeld    ? 1.0 : 0.0) - (DownHeld  ? 1.0 : 0.0)) * RampRate * dtSeconds;
        double bucketDelta = ((RightHeld ? 1.0 : 0.0) - (LeftHeld  ? 1.0 : 0.0)) * RampRate * dtSeconds;

        ArmValue    = Math.Clamp(ArmValue    + armDelta,    0.0,  1.0);
        BucketValue = Math.Clamp(BucketValue + bucketDelta, -1.0, 1.0);
    }

    /// <summary>
    /// Returns 1-byte bitfield: bit0=W, bit1=A, bit2=S, bit3=D, bit4=↑, bit5=↓, bit6=←, bit7=→
    /// WASD bits are always 0 here — ManualControl ORs them in from the joystick active keys.
    /// </summary>
    public byte GetArrowBitfield()
    {
        byte b = 0;
        if (UpHeld)    b |= 0x10; // bit 4
        if (DownHeld)  b |= 0x20; // bit 5
        if (LeftHeld)  b |= 0x40; // bit 6
        if (RightHeld) b |= 0x80; // bit 7
        return b;
    }
}
```

- [ ] **Step 2: Build to confirm no errors**

```
dotnet build NASA_Lunabotics_Control_Hub.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Components/ManipulatorInputState.cs
git commit -m "feat: add ManipulatorInputState for arrow key integration"
```

---

### Task 3: KeyButton UserControl

**Files:**
- Create: `NASA_Lunabotics_Control_Hub/Controls/Manual/KeyButton.axaml`
- Create: `NASA_Lunabotics_Control_Hub/Controls/Manual/KeyButton.axaml.cs`

- [ ] **Step 1: Create KeyButton.axaml**

Create `NASA_Lunabotics_Control_Hub/Controls/Manual/KeyButton.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             x:Class="NASA_Lunabotics_Control_Hub.Controls.Manual.KeyButton"
             mc:Ignorable="d" d:DesignWidth="44" d:DesignHeight="44">
    <Border x:Name="KeyBorder" Classes="key-cap" HorizontalAlignment="Center">
        <TextBlock x:Name="KeyLabelText" Classes="key-cap-text"
                   HorizontalAlignment="Center" VerticalAlignment="Center"/>
    </Border>
</UserControl>
```

- [ ] **Step 2: Create KeyButton.axaml.cs**

Create `NASA_Lunabotics_Control_Hub/Controls/Manual/KeyButton.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

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

        this.GetObservable(KeyLabelProperty).Subscribe(v => _keyLabelText.Text = v);
        this.GetObservable(IsActiveProperty).Subscribe(v => _keyBorder.Classes.Set("active", v));
    }
}
```

- [ ] **Step 3: Build**

```
dotnet build NASA_Lunabotics_Control_Hub.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Controls/Manual/KeyButton.axaml \
        NASA_Lunabotics_Control_Hub/Controls/Manual/KeyButton.axaml.cs
git commit -m "feat: add KeyButton control for active/inactive key cap display"
```

---

### Task 4: ArmSlider UserControl

**Files:**
- Create: `NASA_Lunabotics_Control_Hub/Controls/Manual/ArmSlider.axaml`
- Create: `NASA_Lunabotics_Control_Hub/Controls/Manual/ArmSlider.axaml.cs`

- [ ] **Step 1: Create ArmSlider.axaml**

Create `NASA_Lunabotics_Control_Hub/Controls/Manual/ArmSlider.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:manual="clr-namespace:NASA_Lunabotics_Control_Hub.Controls.Manual"
             x:Class="NASA_Lunabotics_Control_Hub.Controls.Manual.ArmSlider"
             mc:Ignorable="d" d:DesignWidth="80" d:DesignHeight="220">
    <StackPanel HorizontalAlignment="Center" Spacing="4">
        <!-- Up key button -->
        <manual:KeyButton x:Name="UpBtn" KeyLabel="↑" HorizontalAlignment="Center"/>

        <!-- Slider + tick labels side by side -->
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <Slider x:Name="ArmSliderCtrl"
                    Grid.Column="0"
                    Orientation="Vertical"
                    Minimum="0" Maximum="1"
                    IsDirectionReversed="True"
                    IsHitTestVisible="False"
                    Height="140" Width="28"
                    Foreground="#00643C"
                    Background="#333333"
                    VerticalAlignment="Stretch"/>

            <!-- Tick labels: 5 equal rows spanning slider height -->
            <Grid Grid.Column="1" Height="140" Width="36" Margin="4,0,0,0">
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>
                <TextBlock Grid.Row="0" Text="100%" Foreground="#606060" FontSize="9"
                           FontFamily="Consolas" VerticalAlignment="Top"/>
                <TextBlock Grid.Row="1" Text=" 75%" Foreground="#606060" FontSize="9"
                           FontFamily="Consolas" VerticalAlignment="Center"/>
                <TextBlock Grid.Row="2" Text=" 50%" Foreground="#606060" FontSize="9"
                           FontFamily="Consolas" VerticalAlignment="Center"/>
                <TextBlock Grid.Row="3" Text=" 25%" Foreground="#606060" FontSize="9"
                           FontFamily="Consolas" VerticalAlignment="Center"/>
                <TextBlock Grid.Row="4" Text="  0%" Foreground="#606060" FontSize="9"
                           FontFamily="Consolas" VerticalAlignment="Bottom"/>
            </Grid>
        </Grid>

        <!-- Down key button -->
        <manual:KeyButton x:Name="DownBtn" KeyLabel="↓" HorizontalAlignment="Center"/>
    </StackPanel>
</UserControl>
```

- [ ] **Step 2: Create ArmSlider.axaml.cs**

Create `NASA_Lunabotics_Control_Hub/Controls/Manual/ArmSlider.axaml.cs`:

```csharp
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
```

- [ ] **Step 3: Build**

```
dotnet build NASA_Lunabotics_Control_Hub.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Controls/Manual/ArmSlider.axaml \
        NASA_Lunabotics_Control_Hub/Controls/Manual/ArmSlider.axaml.cs
git commit -m "feat: add ArmSlider control with tick labels and arrow key indicators"
```

---

### Task 5: BucketDial UserControl

**Files:**
- Create: `NASA_Lunabotics_Control_Hub/Controls/Manual/BucketDial.axaml`
- Create: `NASA_Lunabotics_Control_Hub/Controls/Manual/BucketDial.axaml.cs`

- [ ] **Step 1: Create BucketDial.axaml**

The dial uses a Canvas: a dark circle as background and a thin `Rectangle` as the pointer. `RenderTransformOrigin="0.5,1.0"` makes the transform pivot at the rectangle's bottom-center, which sits at canvas center (40, 40). `RotateTransform.Angle` = `BucketValue * 90.0`.

Create `NASA_Lunabotics_Control_Hub/Controls/Manual/BucketDial.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:manual="clr-namespace:NASA_Lunabotics_Control_Hub.Controls.Manual"
             x:Class="NASA_Lunabotics_Control_Hub.Controls.Manual.BucketDial"
             mc:Ignorable="d" d:DesignWidth="100" d:DesignHeight="140">
    <StackPanel HorizontalAlignment="Center" Spacing="6">

        <!-- Rotary dial canvas -->
        <Canvas Width="80" Height="80" HorizontalAlignment="Center">
            <!-- Dial ring -->
            <Ellipse Width="80" Height="80"
                     Fill="#1A1A1A" Stroke="#00643C" StrokeThickness="2"/>
            <!-- Pointer: bottom-center pivot at (40,40), points up at Angle=0 -->
            <Rectangle x:Name="DialPointer"
                       Width="3" Height="28"
                       Fill="#00643C"
                       Canvas.Left="38.5" Canvas.Top="12"
                       RenderTransformOrigin="0.5,1.0">
                <Rectangle.RenderTransform>
                    <RotateTransform x:Name="DialRotation" Angle="0"/>
                </Rectangle.RenderTransform>
            </Rectangle>
            <!-- Center dot -->
            <Ellipse Width="8" Height="8" Fill="#00643C"
                     Canvas.Left="36" Canvas.Top="36"/>
            <!-- Neutral tick at top -->
            <Rectangle Width="2" Height="6" Fill="#606060"
                       Canvas.Left="39" Canvas.Top="2"/>
        </Canvas>

        <!-- Left/Right key buttons -->
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Center">
            <manual:KeyButton x:Name="LeftBtn"  KeyLabel="←"/>
            <manual:KeyButton x:Name="RightBtn" KeyLabel="→"/>
        </StackPanel>

    </StackPanel>
</UserControl>
```

- [ ] **Step 2: Create BucketDial.axaml.cs**

Create `NASA_Lunabotics_Control_Hub/Controls/Manual/BucketDial.axaml.cs`:

```csharp
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
        _dialRotation = (RotateTransform)pointer.RenderTransform!;
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
```

- [ ] **Step 3: Build**

```
dotnet build NASA_Lunabotics_Control_Hub.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Controls/Manual/BucketDial.axaml \
        NASA_Lunabotics_Control_Hub/Controls/Manual/BucketDial.axaml.cs
git commit -m "feat: add BucketDial control with rotary pointer and arrow key indicators"
```

---

### Task 6: ManualControl Container

**Files:**
- Create: `NASA_Lunabotics_Control_Hub/Controls/Manual/ManualControl.axaml`
- Create: `NASA_Lunabotics_Control_Hub/Controls/Manual/ManualControl.axaml.cs`

- [ ] **Step 1: Create ManualControl.axaml**

Create `NASA_Lunabotics_Control_Hub/Controls/Manual/ManualControl.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:manual="clr-namespace:NASA_Lunabotics_Control_Hub.Controls.Manual"
             x:Class="NASA_Lunabotics_Control_Hub.Controls.Manual.ManualControl"
             mc:Ignorable="d" d:DesignWidth="268" d:DesignHeight="360">

    <Border x:Name="ContainerBorder" Classes="card-secondary" Padding="12">
        <StackPanel Spacing="10">

            <TextBlock Text="MANUAL CONTROL" Classes="section-header"/>

            <!-- Drive sub-card: WASD d-pad -->
            <Border Classes="card-secondary" Padding="10">
                <StackPanel Spacing="6">
                    <TextBlock Text="DRIVE" Classes="sub-header"/>
                    <Grid HorizontalAlignment="Center">
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <manual:KeyButton Grid.Row="0" Grid.Column="1" x:Name="WKey" KeyLabel="W"/>
                        <manual:KeyButton Grid.Row="1" Grid.Column="0" x:Name="AKey" KeyLabel="A"/>
                        <manual:KeyButton Grid.Row="1" Grid.Column="1" x:Name="SKey" KeyLabel="S"/>
                        <manual:KeyButton Grid.Row="1" Grid.Column="2" x:Name="DKey" KeyLabel="D"/>
                    </Grid>
                </StackPanel>
            </Border>

            <!-- Arm + Bucket sub-cards side by side -->
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="8"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>

                <!-- Arm sub-card -->
                <Border Grid.Column="0" Classes="card-secondary" Padding="10">
                    <StackPanel Spacing="6" HorizontalAlignment="Center">
                        <TextBlock Text="ARM" Classes="sub-header" HorizontalAlignment="Center"/>
                        <manual:ArmSlider x:Name="ArmSliderWidget"/>
                    </StackPanel>
                </Border>

                <!-- Bucket sub-card -->
                <Border Grid.Column="2" Classes="card-secondary" Padding="10">
                    <StackPanel Spacing="6" HorizontalAlignment="Center">
                        <TextBlock Text="BUCKET" Classes="sub-header" HorizontalAlignment="Center"/>
                        <manual:BucketDial x:Name="BucketDialWidget"/>
                    </StackPanel>
                </Border>
            </Grid>

        </StackPanel>
    </Border>
</UserControl>
```

- [ ] **Step 2: Create ManualControl.axaml.cs**

Create `NASA_Lunabotics_Control_Hub/Controls/Manual/ManualControl.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using NASA_Lunabotics_Control_Hub.Components;
using System.Collections.Generic;

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
    public void HandleKeyUp(Key key)   => _state.HandleKeyUp(key);

    /// <summary>Integrate values and push to child visuals. dt = elapsed seconds.</summary>
    public void Tick(double dtSeconds)
    {
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
```

- [ ] **Step 3: Build**

```
dotnet build NASA_Lunabotics_Control_Hub.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Controls/Manual/ManualControl.axaml \
        NASA_Lunabotics_Control_Hub/Controls/Manual/ManualControl.axaml.cs
git commit -m "feat: add ManualControl container with WASD d-pad, arm slider, bucket dial"
```

---

### Task 7: MainView Integration

**Files:**
- Modify: `NASA_Lunabotics_Control_Hub/Views/MainView.axaml`
- Modify: `NASA_Lunabotics_Control_Hub/Views/MainView.axaml.cs`

- [ ] **Step 1: Update MainView.axaml — swap KeyInputGrid for ManualControl**

In `Views/MainView.axaml`:

**Add namespace** to the `<UserControl>` opening tag (after the existing `xmlns:ctrl` line):

```xml
xmlns:manual="clr-namespace:NASA_Lunabotics_Control_Hub.Controls.Manual"
```

**Replace** the entire `<ctrl:KeyInputGrid x:Name="KeyInputGrid" />` line with:

```xml
<manual:ManualControl x:Name="ManualControlCard" />
```

- [ ] **Step 2: Update MainView.axaml.cs — key forwarding**

In `Views/MainView.axaml.cs`, replace the existing `HandleKeyDown` and `HandleKeyUp` methods:

```csharp
public void HandleKeyDown(Key key)
{
    var joystick      = this.FindControl<Controls.JoystickControl>("KeyTrackingJoystick");
    var manualControl = this.FindControl<ManualControl>("ManualControlCard");
    joystick?.HandleKeyDown(key);
    manualControl?.HandleKeyDown(key);
}

public void HandleKeyUp(Key key)
{
    var joystick      = this.FindControl<Controls.JoystickControl>("KeyTrackingJoystick");
    var manualControl = this.FindControl<ManualControl>("ManualControlCard");
    joystick?.HandleKeyUp(key);
    manualControl?.HandleKeyUp(key);
}
```

Add the `Manual` using alias at the top of the file alongside the existing usings:

```csharp
using NASA_Lunabotics_Control_Hub.Controls.Manual;
```

- [ ] **Step 3: Update MainView.axaml.cs — timer tick**

Replace the existing `KeyUpdateTimer_Tick` method body entirely:

```csharp
private void KeyUpdateTimer_Tick(object? sender, EventArgs e)
{
    var joystick      = this.FindControl<Controls.JoystickControl>("KeyTrackingJoystick");
    var manualControl = this.FindControl<ManualControl>("ManualControlCard");

    if (joystick == null || manualControl == null) return;

    var activeKeys = joystick.GetActiveKeys();
    manualControl.UpdateFromJoystick(activeKeys);
    manualControl.Tick(0.050);

    if (manualControl.IsActive)
    {
        byte bitfield = manualControl.GetKeyBitfield(activeKeys);
        _ = _networkClient.SendManipulatorCommandAsync(bitfield);
    }
}
```

- [ ] **Step 4: Update MainView.axaml.cs — wire mode state to ManualControl.IsActive**

In the `MainView` constructor, after `DataContext = _mainViewModel;`, add:

```csharp
_mainViewModel.PropertyChanged += (_, args) =>
{
    if (args.PropertyName == nameof(MainViewModel.ManualStatus))
    {
        var manualControl = this.FindControl<ManualControl>("ManualControlCard");
        if (manualControl != null)
            manualControl.IsActive =
                (_mainViewModel.ManualStatus == NASA_Lunabotics_Control_Hub.ViewModels.ModeState.Confirmed);
    }
};
```

- [ ] **Step 5: Build**

```
dotnet build NASA_Lunabotics_Control_Hub.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Run the app and verify visually**

```
dotnet run --project NASA_Lunabotics_Control_Hub.Desktop/NASA_Lunabotics_Control_Hub.Desktop.csproj
```

Check:
- Left panel shows Manual Control card with WASD d-pad, Arm slider, Bucket dial
- No `KeyInputGrid` visible
- Press W/A/S/D → WASD key buttons in the Drive cluster light up green
- Press ↑/↓ → arm slider knob moves up/down; ↑ and ↓ key buttons light up
- Press ←/→ → bucket dial pointer rotates; ← and → key buttons light up
- Click **Manual** mode button → widget becomes fully opaque and interactive
- Click **Standby** → widget greys out to 40% opacity

- [ ] **Step 7: Commit**

```bash
git add NASA_Lunabotics_Control_Hub/Views/MainView.axaml \
        NASA_Lunabotics_Control_Hub/Views/MainView.axaml.cs
git commit -m "feat: integrate ManualControl into MainView, wire keys, timer, and mode state"
```

---

### Task 8: Cleanup

**Files:**
- Delete: `NASA_Lunabotics_Control_Hub/Controls/Controls/KeyInputGrid.axaml`
- Delete: `NASA_Lunabotics_Control_Hub/Controls/Controls/KeyInputGrid.axaml.cs`

- [ ] **Step 1: Delete KeyInputGrid files**

```bash
git rm NASA_Lunabotics_Control_Hub/Controls/Controls/KeyInputGrid.axaml \
       NASA_Lunabotics_Control_Hub/Controls/Controls/KeyInputGrid.axaml.cs
```

- [ ] **Step 2: Build — confirm no dangling references**

```
dotnet build NASA_Lunabotics_Control_Hub.sln
```

Expected: Build succeeded, 0 errors. If the build mentions `KeyInputGrid` not found, grep for remaining usages:

```bash
grep -r "KeyInputGrid" NASA_Lunabotics_Control_Hub/ --include="*.cs" --include="*.axaml"
```

Remove any remaining references.

- [ ] **Step 3: Commit**

```bash
git commit -m "chore: delete obsolete KeyInputGrid (replaced by ManualControl)"
```

---

## Post-Implementation Checklist

- [ ] Build passes: `dotnet build NASA_Lunabotics_Control_Hub.sln`
- [ ] App runs and left panel shows Manual Control with all three sub-sections
- [ ] WASD highlights work in Drive cluster
- [ ] Arm slider moves on ↑/↓
- [ ] Bucket dial rotates on ←/→
- [ ] Widget greys out in Standby/Autonomous mode
- [ ] `SendManipulatorCommandAsync` stub called each tick in Manual mode (verify via console: no errors thrown)
- [ ] No remaining `KeyInputGrid` references

## Wire Protocol TODO (fill in when ROS side is ready)

In `Components/NetworkModeClient.cs` `SendManipulatorCommandAsync`, un-comment the three lines:
```csharp
var frame = NetworkProtocol.EncodeManipulator(keyBitfield);
await _stream.WriteAsync(frame, 0, frame.Length, _cancelSource.Token);
await _stream.FlushAsync(_cancelSource.Token);
```
