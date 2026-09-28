using App2d.Core.Validation;
using App2d.Contracts.Persons;
using App2d.Input;

namespace App2d.Gameplay.Player;

/// <summary>
/// Default gameplay bindings and conversion to device-independent, held-state person
/// commands. Presses and releases are derived by the simulation from consecutive commands.
/// </summary>
public sealed class PlayerInputMapper2D
{
    private static readonly ButtonBinding Left = new([Keys.A, Keys.Left], GamepadButton2D.DPadLeft);
    private static readonly ButtonBinding Right = new([Keys.D, Keys.Right], GamepadButton2D.DPadRight);
    private static readonly ButtonBinding Up = new([Keys.W, Keys.Up], GamepadButton2D.DPadUp);
    private static readonly ButtonBinding Down = new([Keys.S, Keys.Down], GamepadButton2D.DPadDown);
    private static readonly ButtonBinding Jump = new([Keys.Space], GamepadButton2D.A);
    private static readonly ButtonBinding Dash = new([Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey], GamepadButton2D.RightTrigger);
    private static readonly ButtonBinding Primary = new([Keys.F], GamepadButton2D.X, MouseButtons.Left);
    private static readonly ButtonBinding Secondary = new([Keys.Q], GamepadButton2D.RightShoulder, MouseButtons.Right);
    // E / Y are reserved for Interact once gameplay has an interaction command.

    private InputButtonState _jump;
    private InputButtonState _dash;
    private InputButtonState _primary;
    private InputButtonState _secondary;

    public PersonCommand2D Capture(InputState input)
    {
        ArgGuard.ThrowIfNull(input);
        if (input.IsSuppressed)
        {
            Reset();
            return default;
        }

        _jump = Jump.Read(input, _jump);
        _dash = Dash.Read(input, _dash);
        _primary = Primary.Read(input, _primary);
        _secondary = Secondary.Read(input, _secondary);
        var moveX = Axis(Left, Right, input, input.Gamepad.LeftStick.X);
        var climbY = Axis(Down, Up, input, input.Gamepad.LeftStick.Y);
        var downHeld = Down.Read(input).Held || input.Gamepad.LeftStick.Y < -0.5f;

        return new PersonCommand2D(
            moveX,
            climbY,
            JumpHeld: Pulse(_jump),
            DashHeld: Pulse(_dash),
            DownHeld: downHeld,
            PrimaryHeld: Pulse(_primary),
            SecondaryHeld: Pulse(_secondary));
    }

    public void Reset()
    {
        _jump = _dash = _primary = _secondary = default;
    }

    /// <summary>A tap that begins and ends inside one tick still reaches the simulation as a one-tick hold.</summary>
    private static bool Pulse(InputButtonState button) => button.Held || button.Pressed;

    private static float Axis(ButtonBinding negative, ButtonBinding positive,
        InputState input, float analog) =>
        Math.Clamp((positive.Read(input).Held ? 1f : 0f) -
            (negative.Read(input).Held ? 1f : 0f) + analog, -1f, 1f);
}
