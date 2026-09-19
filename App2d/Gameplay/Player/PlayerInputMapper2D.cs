using App2d.Core;
using App2d.Gameplay.Persons;
using App2d.Input;

namespace App2d.Gameplay.Player;

/// <summary>
/// Default gameplay bindings and conversion to device-independent, held-state person
/// commands. Presses and releases are derived by the simulation from consecutive commands.
/// </summary>
public sealed class PlayerInputMapper2D
{
    private static readonly ButtonBinding Left = new([Keys.A, Keys.Left], XboxButtons.DPadLeft);
    private static readonly ButtonBinding Right = new([Keys.D, Keys.Right], XboxButtons.DPadRight);
    private static readonly ButtonBinding Up = new([Keys.W, Keys.Up], XboxButtons.DPadUp);
    private static readonly ButtonBinding Down = new([Keys.S, Keys.Down], XboxButtons.DPadDown);
    private static readonly ButtonBinding Jump = new([Keys.Space], XboxButtons.A);
    private static readonly ButtonBinding Dash = new([Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey], XboxButtons.RightTrigger);
    private static readonly ButtonBinding Primary = new([Keys.F], XboxButtons.X, MouseButtons.Left);
    private static readonly ButtonBinding Secondary = new([Keys.Q], XboxButtons.RightShoulder, MouseButtons.Right);
    // E / Y are reserved for Interact once gameplay has an interaction command.

    private readonly XboxControllerInput2D _controller = new();
    private InputButtonState _jump;
    private InputButtonState _dash;
    private InputButtonState _primary;
    private InputButtonState _secondary;

    public bool IsControllerConnected => _controller.IsConnected;

    public PersonCommand2D Capture(InputState input)
    {
        ArgGuard.ThrowIfNull(input);
        return Capture(input, input.IsSuppressed ? default : _controller.Capture());
    }

    internal PersonCommand2D Capture(InputState input, XboxControllerState2D pad)
    {
        if (input.IsSuppressed)
        {
            Reset();
            return default;
        }

        _jump = Jump.Read(input, pad, _jump);
        _dash = Dash.Read(input, pad, _dash);
        _primary = Primary.Read(input, pad, _primary);
        _secondary = Secondary.Read(input, pad, _secondary);
        var moveX = Axis(Left, Right, input, pad, pad.LeftStick.X);
        var climbY = Axis(Down, Up, input, pad, pad.LeftStick.Y);
        var downHeld = Down.Read(input, pad).Held || pad.LeftStick.Y < -0.5f;

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
        _controller.Reset();
    }

    /// <summary>A tap that begins and ends inside one tick still reaches the simulation as a one-tick hold.</summary>
    private static bool Pulse(InputButtonState button) => button.Held || button.Pressed;

    private static float Axis(ButtonBinding negative, ButtonBinding positive,
        InputState input, XboxControllerState2D pad, float analog) =>
        Math.Clamp((positive.Read(input, pad).Held ? 1f : 0f) -
            (negative.Read(input, pad).Held ? 1f : 0f) + analog, -1f, 1f);
}
