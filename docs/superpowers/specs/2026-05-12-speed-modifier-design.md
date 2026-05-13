# Speed Modifier — Design Spec
_Date: 2026-05-12_

## Summary

Add `speed_modifier` (uint16, 0–100) to every M-frame sent from the GUI to the rover.
A horizontal slider in the DRIVE card lets the operator set drive speed before and during manual operation.
Default is 100, which preserves existing behaviour (20 % motor speed cap on the rover).

---

## 1. Wire Protocol

**File:** `NASA_Lunabotics_Control_Hub/Components/NetworkProtocol.cs`

The M frame payload grows from 1 byte to 3 bytes:

```
Old: [O][M][1][bitfield][CRC]                        — 5 bytes
New: [O][M][3][bitfield][speed_hi][speed_lo][CRC]    — 7 bytes
```

| Byte | Value      | Description                              |
|------|------------|------------------------------------------|
| 0    | 0x4F       | Magic ('O')                              |
| 1    | 0x4D       | Type ('M')                               |
| 2    | 0x03       | Payload length (always 3)                |
| 3    | bitfield   | Key state bits 0–7 (unchanged)           |
| 4    | speed_hi   | Upper byte of speed_modifier (big-endian)|
| 5    | speed_lo   | Lower byte of speed_modifier             |
| 6    | CRC        | CRC-8 (poly 0x07) over bytes 0–5        |

Signature change:
```csharp
// Before
public static byte[] EncodeManipulator(byte keyBitfield)

// After
public static byte[] EncodeManipulator(byte keyBitfield, ushort speedModifier = 100)
```

`speedModifier` is clamped to 0–100 before encoding (GUI safe range; rover accepts 0–500 but GUI caps at 100 for safe operation).

---

## 2. Plumbing

### `NetworkModeClient.cs`

`SendManipulatorCommandAsync` gains a second parameter:

```csharp
// Before
public async Task SendManipulatorCommandAsync(byte keyBitfield)

// After
public async Task SendManipulatorCommandAsync(byte keyBitfield, ushort speedModifier = 100)
```

The new parameter is passed directly to `NetworkProtocol.EncodeManipulator`.

### `ManualControl.axaml.cs`

- Add `private Slider _speedSlider = null!;` field.
- Wire it in `InitializeComponent`: `_speedSlider = this.FindControl<Slider>("SpeedSlider")!;`
- Expose: `public ushort SpeedModifier => (ushort)Math.Clamp(_speedSlider.Value, 0, 100);`

### `MainView.axaml.cs` — call site (lines 143–144)

```csharp
// Before
byte bitfield = manualControl.GetKeyBitfield(activeKeys);
_ = _networkClient.SendManipulatorCommandAsync(bitfield);

// After
byte bitfield = manualControl.GetKeyBitfield(activeKeys);
_ = _networkClient.SendManipulatorCommandAsync(bitfield, manualControl.SpeedModifier);
```

---

## 3. UI

**File:** `NASA_Lunabotics_Control_Hub/Controls/Manual/ManualControl.axaml`

Inside the DRIVE card `StackPanel`, below the WASD `Grid`, append:

```xml
<TextBlock Text="SPEED" Classes="sub-header" HorizontalAlignment="Center"/>
<Slider x:Name="SpeedSlider"
        Minimum="0" Maximum="100" Value="100"
        Width="80" HorizontalAlignment="Center"/>
<TextBlock HorizontalAlignment="Center">
    <TextBlock.Text>
        <MultiBinding StringFormat="{}{0:0}%">
            <Binding ElementName="SpeedSlider" Path="Value"/>
        </MultiBinding>
    </TextBlock.Text>
</TextBlock>
```

The `TextBlock` gives the operator live readout (e.g. "75%") without needing a separate ViewModel property.

---

## Out of Scope

- Speed above 100 is intentionally not exposed in the GUI (rover accepts up to 500, but 100 = 20 % motor cap is the safe operational ceiling for now).
- Persisting the slider value across sessions.
- Joystick input for speed.
