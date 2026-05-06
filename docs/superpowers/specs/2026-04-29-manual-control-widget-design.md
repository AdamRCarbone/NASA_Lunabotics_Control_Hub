# Manual Control Widget — Design Spec
*Date: 2026-04-29*

## Overview

Add a "Manual Control" widget to the left panel of the GUI. It replaces the existing (failed) `KeyInputGrid` and provides:

- Visual WASD d-pad cluster showing drive key state
- Vertical slider with ticks for excavator arm raise/lower (↑/↓)
- Rotary dial for bucket pitch (←/→)
- Arrow key button indicators adjacent to slider and dial
- Entire widget greys out (disabled) when mode ≠ Manual

The send pipeline is stubbed — the bitfield encoding and frame are in place, but the actual `WriteAsync` is commented out until ROS-side wire format is confirmed.

---

## File Layout

```
Controls/Manual/
├── ManualControl.axaml          # top-level container
├── ManualControl.axaml.cs
├── KeyButton.axaml              # single key cap, active/inactive visual
├── KeyButton.axaml.cs
├── ArmSlider.axaml              # vertical slider + ticks + ↑/↓ KeyButtons
├── ArmSlider.axaml.cs
├── BucketDial.axaml             # rotary dial + ←/→ KeyButtons
└── BucketDial.axaml.cs

Components/
└── ManipulatorInputState.cs     # key state + integrated arm/bucket values
```

**Deleted:** `Controls/Controls/KeyInputGrid.axaml` and `KeyInputGrid.axaml.cs` and all references.

---

## Visual Layout

Inside the left panel, `ManualControl` replaces `KeyInputGrid`:

```
VNC card  →  ManualControl  →  DataUsageGraph  →  FaultConsole
```

Within `ManualControl` (card-secondary styling, dark theme):

```
┌─ MANUAL CONTROL ────────────────────────┐
│                                         │
│  ┌─ Drive ──────────────────────────┐   │
│  │          [W]                     │   │
│  │      [A] [S] [D]                 │   │
│  └──────────────────────────────────┘   │
│                                         │
│  ┌─ Arm ───────────┐ ┌─ Bucket ─────┐   │
│  │  [↑]            │ │    [↺ dial]  │   │
│  │   ┃ ━ 100%      │ │  ╭───────╮   │   │
│  │   ┃ ━  75%      │ │  │  ◐    │   │   │
│  │   ┃ ━  50%      │ │  ╰───────╯   │   │
│  │   ┃ ━  25%      │ │             │   │
│  │   ◯ (knob)      │ │  [←]   [→]  │   │
│  │  [↓]            │ │             │   │
│  └─────────────────┘ └─────────────┘   │
└─────────────────────────────────────────┘
```

- **Drive cluster:** four `KeyButton` instances (W above, A/S/D in a row). Read-only. Green when held, dim when not.
- **Arm slider:** vertical, range 0.0–1.0. Five tick marks (0%, 25%, 50%, 75%, 100%) for reference — not snap targets. `↑` button at top, `↓` at bottom. Knob position reflects `ManipulatorInputState.ArmValue`.
- **Bucket dial:** rotary, range -1.0 to 1.0 (neutral = 0, pointer points up). `←` and `→` buttons below. Pointer angle reflects `ManipulatorInputState.BucketValue`.
- **Disabled state:** `Opacity="0.4"`, `IsHitTestVisible="False"`. Input short-circuits when not in Manual mode.

---

## Input State — `ManipulatorInputState`

Plain C# class implementing `INotifyPropertyChanged`.

```
Properties:
  ArmValue    : double  (0.0–1.0, clamped)
  BucketValue : double  (-1.0–1.0, clamped)
  UpHeld      : bool
  DownHeld    : bool
  LeftHeld    : bool
  RightHeld   : bool

Methods:
  HandleKeyDown(Key)
  HandleKeyUp(Key)
  Tick(double dtSeconds)      — integrates arm/bucket values
    ArmValue    += (UpHeld ? 1 : 0 - DownHeld ? 1 : 0) * RampRate * dt
    BucketValue += (RightHeld ? 1 : 0 - LeftHeld ? 1 : 0) * RampRate * dt

Config:
  RampRate = 0.5 (traverses full range in 2 s; tunable)
```

---

## Data Flow

### Keystroke path

```
User presses ↑
    │
    ▼
MainView.HandleKeyDown(Key.Up)
    │
    ├──► KeyTrackingJoystick.HandleKeyDown(Key.Up)   [existing, no-op for arrows]
    │
    └──► ManualControl.HandleKeyDown(Key.Up)
                │
                ▼
         ManipulatorInputState.HandleKeyDown(Key.Up)
                │
                ▼  (next 50 ms tick)
         Tick(0.050) → ArmValue integrates → PropertyChanged → ArmSlider knob moves
```

### Timer (50 ms — reuses existing `_keyUpdateTimer` in `MainView`)

Per tick, while in Manual mode:
1. `manualControl.UpdateVisuals()` — refresh KeyButton highlights
2. `manipulatorInputState.Tick(0.050)` — integrate arm/bucket
3. `_networkClient.SendManipulatorCommandAsync(bitfield)` — send stub

---

## Wire Protocol

Follows existing `NetworkProtocol.cs` lean binary format:
`[MAGIC][TYPE][LEN][PAYLOAD][CRC]`

**New type:** `TYPE_MANIPULATOR = 0x4D` (`'M'`)

**Payload:** 1 byte bitfield

```
bit 7  6  5  4  3  2  1  0
    →  ←  ↓  ↑  D  S  A  W
```

**Frame:** `[O][M][1][bitfield][crc]` = **5 bytes**

Sent every 50 ms while in Manual mode (all-zero when no keys held = rover fail-safe heartbeat).

### `NetworkProtocol.cs` addition

```csharp
public const byte TYPE_MANIPULATOR = 0x4D; // 'M'

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

### `NetworkModeClient.cs` stub

```csharp
public Task SendManipulatorCommandAsync(byte keyBitfield)
{
    // TODO: un-comment once ROS-side parser is confirmed
    // var frame = NetworkProtocol.EncodeManipulator(keyBitfield);
    // await _stream.WriteAsync(frame, 0, frame.Length, _cancelSource.Token);
    return Task.CompletedTask;
}
```

---

## Mode Integration

`ManualControl` exposes `IsActive : bool` (styled property).

In `MainView`, bound to `_mainViewModel.ManualStatus == ModeState.Confirmed`.

When `IsActive = false`:
- `Border` renders at 40% opacity
- `IsHitTestVisible = false`
- `HandleKeyDown` short-circuits (no state mutation)
- `SendManipulatorCommandAsync` not called

---

## Out of Scope (this spec)

- Actual ROS-side parser for `'M'` frames
- Snap-to-tick arm positioning
- Gamepad/joystick input for arm and bucket
- Absolute arm/bucket position telemetry readback from rover
