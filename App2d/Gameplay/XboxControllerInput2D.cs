using System.Numerics;
using System.Runtime.InteropServices;

namespace App2d.Gameplay;

internal sealed class XboxControllerInput2D
{
    private const int MaximumControllerCount = 4;
    private const float AimDistance = 700f;
    private const float LeftStickDeadZone = 7_849f;
    private const float RightStickDeadZone = 8_689f;
    private const byte TriggerThreshold = 30;
    private const uint Success = 0;

    private int _controllerIndex = -1;
    private XInputButtons _previousButtons;
    private bool _leftTriggerWasDown;
    private bool _rightTriggerWasDown;

    public bool IsConnected => _controllerIndex >= 0;

    public XboxControllerFrame2D Capture(Vector2 playerPosition)
    {
        if (!TryGetGamepad(out var gamepad))
        {
            ResetButtonHistory();
            return default;
        }

        var buttons = gamepad.Buttons;
        var pressed = buttons & ~_previousButtons;
        var jumpHeld = buttons.HasFlag(XInputButtons.A);
        var jumpPressed = pressed.HasFlag(XInputButtons.A);
        var jumpReleased =
            _previousButtons.HasFlag(XInputButtons.A) && !jumpHeld;

        var leftTriggerDown = gamepad.LeftTrigger > TriggerThreshold;
        var rightTriggerDown = gamepad.RightTrigger > TriggerThreshold;
        var aim = ApplyRadialDeadZone(
            gamepad.RightThumbX,
            gamepad.RightThumbY,
            RightStickDeadZone);

        var moveX = ApplyRadialDeadZone(
            gamepad.LeftThumbX,
            gamepad.LeftThumbY,
            LeftStickDeadZone).X;
        if (buttons.HasFlag(XInputButtons.DPadLeft))
            moveX = -1f;
        else if (buttons.HasFlag(XInputButtons.DPadRight))
            moveX = 1f;

        _previousButtons = buttons;
        var frame = new XboxControllerFrame2D(
            moveX,
            jumpPressed,
            jumpHeld,
            jumpReleased,
            pressed.HasFlag(XInputButtons.LeftShoulder),
            pressed.HasFlag(XInputButtons.RightShoulder),
            leftTriggerDown && !_leftTriggerWasDown,
            rightTriggerDown && !_rightTriggerWasDown,
            aim == Vector2.Zero ? null : playerPosition + aim * AimDistance);
        _leftTriggerWasDown = leftTriggerDown;
        _rightTriggerWasDown = rightTriggerDown;
        return frame;
    }

    public void Reset()
    {
        _controllerIndex = -1;
        ResetButtonHistory();
    }

    private bool TryGetGamepad(out XInputGamepad gamepad)
    {
        if (_controllerIndex >= 0 &&
            XInputGetState((uint)_controllerIndex, out var activeState) == Success)
        {
            gamepad = activeState.Gamepad;
            return true;
        }

        _controllerIndex = -1;
        for (var index = 0; index < MaximumControllerCount; index++)
        {
            if (XInputGetState((uint)index, out var state) != Success)
                continue;

            _controllerIndex = index;
            gamepad = state.Gamepad;
            return true;
        }

        gamepad = default;
        return false;
    }

    private void ResetButtonHistory()
    {
        _previousButtons = XInputButtons.None;
        _leftTriggerWasDown = false;
        _rightTriggerWasDown = false;
    }

    private static Vector2 ApplyRadialDeadZone(short rawX, short rawY, float deadZone)
    {
        var stick = new Vector2(rawX, rawY);
        var magnitude = stick.Length();
        if (magnitude <= deadZone)
            return Vector2.Zero;

        var direction = stick / magnitude;
        var normalizedMagnitude = Math.Clamp(
            (magnitude - deadZone) / (short.MaxValue - deadZone),
            0f,
            1f);
        return direction * normalizedMagnitude;
    }

#pragma warning disable SYSLIB1054
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);
#pragma warning restore SYSLIB1054

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct XInputState
    {
        public readonly uint PacketNumber;
        public readonly XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct XInputGamepad
    {
        public readonly XInputButtons Buttons;
        public readonly byte LeftTrigger;
        public readonly byte RightTrigger;
        public readonly short LeftThumbX;
        public readonly short LeftThumbY;
        public readonly short RightThumbX;
        public readonly short RightThumbY;
    }

    [Flags]
    private enum XInputButtons : ushort
    {
        None = 0,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        A = 0x1000,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200
    }
}

internal readonly record struct XboxControllerFrame2D(
    float MoveX,
    bool JumpPressed,
    bool JumpHeld,
    bool JumpReleased,
    bool CycleLeftWeapon,
    bool CycleRightWeapon,
    bool UseLeftWeapon,
    bool UseRightWeapon,
    Vector2? AimTarget);
