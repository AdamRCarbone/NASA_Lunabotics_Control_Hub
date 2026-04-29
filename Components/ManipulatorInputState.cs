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
