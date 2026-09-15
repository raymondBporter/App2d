using System.Numerics;
using System.Runtime.InteropServices;

namespace App2d.Input;

/// <summary>Reads physical controls only. Gameplay bindings belong to the player mapper.</summary>
internal sealed class XboxControllerInput2D
{
    private const float LeftStickDeadZone = 7_849f;
    private int _controllerIndex = -1;
    private XboxButtons _previousButtons;
    private XboxButtons _blockedButtons;
    private bool _needsBaseline = true;
    private bool _rightTriggerDown;

    public bool IsConnected => _controllerIndex >= 0;

    public XboxControllerState2D Capture()
    {
        if (!TryGetGamepad(out var gamepad))
        {
            Reset();
            return default;
        }
        return Update(gamepad.Buttons, new Vector2(gamepad.LeftThumbX, gamepad.LeftThumbY), gamepad.RightTrigger);
    }

    internal XboxControllerState2D Update(ushort physicalButtons, Vector2 rawLeftStick, byte rightTrigger)
    {
        // Separate thresholds prevent repeated presses around the trigger's activation point.
        _rightTriggerDown = rightTrigger >= (_rightTriggerDown ? 25 : 40);
        var buttons = (XboxButtons)physicalButtons;
        if (_rightTriggerDown) buttons |= XboxButtons.RightTrigger;
        if (_needsBaseline)
        {
            // Reconnect/resume must not turn an already held button into a fresh action.
            _blockedButtons = buttons;
            _needsBaseline = false;
        }
        _blockedButtons &= buttons;
        buttons &= ~_blockedButtons;
        var state = new XboxControllerState2D(ApplyDeadZone(rawLeftStick), buttons,
            buttons & ~_previousButtons, _previousButtons & ~buttons);
        _previousButtons = buttons;
        return state;
    }

    public void Reset()
    {
        _controllerIndex = -1;
        _previousButtons = _blockedButtons = XboxButtons.None;
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
        return magnitude <= LeftStickDeadZone ? Vector2.Zero : stick / magnitude *
            Math.Clamp((magnitude - LeftStickDeadZone) / (short.MaxValue - LeftStickDeadZone), 0f, 1f);
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

internal readonly record struct XboxControllerState2D(
    Vector2 LeftStick, XboxButtons Down, XboxButtons Pressed, XboxButtons Released);

[Flags]
internal enum XboxButtons
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
