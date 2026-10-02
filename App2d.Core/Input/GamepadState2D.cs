using System.Numerics;

namespace App2d.Core.Input;

/// <summary>One physical gamepad sample, with button edges since the previous simulation tick.</summary>
public readonly record struct GamepadState2D(
    Vector2 LeftStick, GamepadButton2D Down, GamepadButton2D Pressed, GamepadButton2D Released)
{
    public bool IsConnected { get; init; }
    public Vector2 RightStick { get; init; }
    public float LeftTrigger { get; init; }
    public float RightTrigger { get; init; }

    public bool IsDown(GamepadButton2D button) => (Down & button) != 0;
    public bool WasPressed(GamepadButton2D button) => (Pressed & button) != 0;
    public bool WasReleased(GamepadButton2D button) => (Released & button) != 0;
}

[Flags]
public enum GamepadButton2D
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Menu = 0x0010,
    View = 0x0020,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
    RightTrigger = 0x10000
}
