using System.Numerics;
using System.Runtime.InteropServices;

namespace App2d.Core.Input;

/// <summary>Reads physical controls only. Gameplay bindings belong to the player mapper.</summary>
internal sealed class XboxControllerInput2D
{
    private const float StickDeadZone = 7_849f;
    private int _controllerIndex = -1;
    private GamepadButton2D _previousButtons;
    private GamepadButton2D _blockedButtons;
    private bool _needsBaseline = true;
    private bool _rightTriggerDown;

    public GamepadState2D Capture()
    {
        if (!TryGetGamepad(out var gamepad))
        {
            Reset();
            return default;
        }
        return Update(gamepad.Buttons, new Vector2(gamepad.LeftThumbX, gamepad.LeftThumbY), gamepad.RightTrigger,
            new Vector2(gamepad.RightThumbX, gamepad.RightThumbY), gamepad.LeftTrigger);
    }

    internal GamepadState2D Update(ushort physicalButtons, Vector2 rawLeftStick, byte rightTrigger,
        Vector2 rawRightStick = default, byte leftTrigger = 0)
    {
        // Separate thresholds prevent repeated presses around the trigger's activation point.
        _rightTriggerDown = rightTrigger >= (_rightTriggerDown ? 25 : 40);
        var buttons = (GamepadButton2D)physicalButtons;
        if (_rightTriggerDown) buttons |= GamepadButton2D.RightTrigger;
        if (_needsBaseline)
        {
            // Reconnect/resume must not turn an already held button into a fresh action.
            _blockedButtons = buttons;
            _needsBaseline = false;
        }
        _blockedButtons &= buttons;
        buttons &= ~_blockedButtons;
        var state = new GamepadState2D(ApplyDeadZone(rawLeftStick), buttons,
            buttons & ~_previousButtons, _previousButtons & ~buttons)
        {
            IsConnected = true,
            RightStick = ApplyDeadZone(rawRightStick),
            LeftTrigger = leftTrigger / 255f,
            RightTrigger = rightTrigger / 255f
        };
        _previousButtons = buttons;
        return state;
    }

    public void Reset()
    {
        _controllerIndex = -1;
        _previousButtons = _blockedButtons = GamepadButton2D.None;
        _needsBaseline = true;
        _rightTriggerDown = false;
    }

    private bool TryGetGamepad(out XInputGamepad gamepad)
    {
        if (_controllerIndex >= 0 && XInputGetState((uint)_controllerIndex, out var active) == 0)
        {
            gamepad = active.Gamepad;
            return true;
        }
        Reset();
        for (uint index = 0; index < 4; index++)
        {
            if (XInputGetState(index, out var state) != 0) continue;
            _controllerIndex = (int)index;
            gamepad = state.Gamepad;
            return true;
        }
        gamepad = default;
        return false;
    }

    private static Vector2 ApplyDeadZone(Vector2 stick)
    {
        var magnitude = stick.Length();
        return magnitude <= StickDeadZone ? Vector2.Zero : stick / magnitude *
            Math.Clamp((magnitude - StickDeadZone) / (short.MaxValue - StickDeadZone), 0f, 1f);
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

    // Unused native fields still preserve the XInput ABI.
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct XInputGamepad
    {
        public readonly ushort Buttons;
        public readonly byte LeftTrigger;
        public readonly byte RightTrigger;
        public readonly short LeftThumbX;
        public readonly short LeftThumbY;
        public readonly short RightThumbX;
        public readonly short RightThumbY;
    }
}
