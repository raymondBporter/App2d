using App2d.Gameplay.Player;
using App2d.Input;
using System.Numerics;

namespace App2d.Tests.Input;

public sealed class PlayerInputTests
{
    [Theory]
    [InlineData(GamepadButton2D.Y, true)]
    [InlineData(GamepadButton2D.B, false)]
    public void XboxFaceButtonsHoldSpellsAndReleaseCleanly(GamepadButton2D button, bool shot)
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetGamepad(new(default, button, button, default) { IsConnected = true });
        var command = mapper.Capture(input);
        Assert.Equal(shot, command.CastHeld);
        Assert.Equal(!shot, command.HealHeld);
        Assert.False(command.PrimaryHeld);
        Assert.False(command.SecondaryHeld);
        input.SetGamepad(default);
        Assert.Equal(default, mapper.Capture(input));
    }

    [Fact]
    public void DeviceViewsReportTheSameHeldPressedAndReleasedLifecycle()
    {
        var input = new InputState();
        input.SetKey(Keys.Space, true);
        input.SetMouseButton(MouseButtons.Left, true);
        input.SetGamepad(new(default, GamepadButton2D.A, GamepadButton2D.A, default) { IsConnected = true });

        Assert.True(input.Keyboard.IsDown(Keys.Space));
        Assert.True(input.Keyboard.WasPressed(Keys.Space));
        Assert.True(input.Mouse.IsDown(MouseButtons.Left));
        Assert.True(input.Mouse.WasPressed(MouseButtons.Left));
        Assert.True(input.Gamepad.IsDown(GamepadButton2D.A));
        Assert.True(input.Gamepad.WasPressed(GamepadButton2D.A));

        input.EndFrame();
        Assert.True(input.Keyboard.IsDown(Keys.Space));
        Assert.True(input.Mouse.IsDown(MouseButtons.Left));
        Assert.True(input.Gamepad.IsDown(GamepadButton2D.A));
        Assert.False(input.Keyboard.WasPressed(Keys.Space));
        Assert.False(input.Mouse.WasPressed(MouseButtons.Left));
        Assert.False(input.Gamepad.WasPressed(GamepadButton2D.A));

        input.SetKey(Keys.Space, false);
        input.SetMouseButton(MouseButtons.Left, false);
        input.SetGamepad(new(default, default, default, GamepadButton2D.A) { IsConnected = true });
        Assert.True(input.Keyboard.WasReleased(Keys.Space));
        Assert.True(input.Mouse.WasReleased(MouseButtons.Left));
        Assert.True(input.Gamepad.WasReleased(GamepadButton2D.A));
    }

    [Fact]
    public void BriefTapSurvivesUntilOneSimulationTick()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.Space, true);
        input.SetKey(Keys.Space, false);
        // A tap inside one tick is reported as a one-tick hold; the simulation derives the press.
        var first = mapper.Capture(input);
        Assert.True(first.JumpHeld);
        input.EndFrame();
        var second = mapper.Capture(input);
        Assert.False(second.JumpHeld);
    }

    [Fact]
    public void ReleasingOneAttackBindingDoesNotReleaseAnotherOrStartAnotherAttack()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.F, true);
        Assert.True(mapper.Capture(input).PrimaryHeld);
        input.EndFrame();
        input.SetMouseButton(MouseButtons.Left, true);
        Assert.True(mapper.Capture(input).PrimaryHeld);
        input.EndFrame();
        input.SetKey(Keys.F, false);
        Assert.True(mapper.Capture(input).PrimaryHeld); // The mouse still holds the logical button.
        input.EndFrame();
        input.SetMouseButton(MouseButtons.Left, false);
        Assert.False(mapper.Capture(input).PrimaryHeld);
    }

    [Fact]
    public void KeyboardAndControllerShareJumpState()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.Space, true);
        Assert.True(mapper.Capture(input).JumpHeld);
        input.EndFrame();
        var pad = new GamepadState2D(default, GamepadButton2D.A, GamepadButton2D.A, default);
        input.SetGamepad(pad);
        Assert.True(mapper.Capture(input).JumpHeld);
        input.SetKey(Keys.Space, false);
        pad = pad with { Pressed = default };
        input.SetGamepad(pad);
        Assert.True(mapper.Capture(input).JumpHeld); // The pad still holds the logical button.
        input.EndFrame();
        // A disconnected controller contributes no held buttons.
        input.SetGamepad(default);
        Assert.False(mapper.Capture(input).JumpHeld);
    }

    [Fact]
    public void UpOnlyClimbsAndShiftDoesNotDisableVerticalMovement()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.Up, true);
        input.SetKey(Keys.ShiftKey, true);
        var command = mapper.Capture(input);
        Assert.Equal(1f, command.ClimbY);
        Assert.True(command.DashHeld);
        Assert.False(command.JumpHeld);
        input.EndFrame();
        input.SetKey(Keys.Up, false);
        input.SetKey(Keys.Down, true);
        input.SetKey(Keys.Space, true);
        command = mapper.Capture(input);
        Assert.Equal(-1f, command.ClimbY);
        Assert.True(command.DownHeld);
        Assert.True(command.JumpHeld);
    }

    [Fact]
    public void DevAndReservedKeysDoNotProduceGameplayActions()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        foreach (var key in new[] { Keys.E, Keys.B, Keys.F3 }) input.SetKey(key, true);
        var pad = new GamepadState2D(default, GamepadButton2D.LeftShoulder | GamepadButton2D.RightShoulder,
            GamepadButton2D.LeftShoulder | GamepadButton2D.RightShoulder, default);
        input.SetGamepad(pad);
        Assert.Equal(default, mapper.Capture(input));
    }

    [Fact]
    public void SuppressionCancelsActionsAndRequiresReleaseBeforeKeyboardRepeat()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.F, true);
        Assert.True(mapper.Capture(input).PrimaryHeld);
        input.SetSuppressed(true);
        Assert.Equal(default, mapper.Capture(input));
        input.SetSuppressed(false);
        input.SetKey(Keys.F, true); // OS key repeat from the original hold.
        Assert.Equal(default, mapper.Capture(input));
        input.SetKey(Keys.F, false);
        input.SetKey(Keys.F, true);
        Assert.True(mapper.Capture(input).PrimaryHeld);
    }

    [Fact]
    public void FocusLossClearsPendingEventsAndSuppressesControllerToo()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.Space, true);
        input.SetWindowActive(false);
        var pad = new GamepadState2D(default, GamepadButton2D.X, GamepadButton2D.X, default);
        input.SetGamepad(pad);
        Assert.True(input.IsSuppressed);
        Assert.False(input.Keyboard.WasPressed(Keys.Space));
        Assert.Equal(default, mapper.Capture(input));
        input.SetWindowActive(true);
        Assert.False(input.IsSuppressed);
        Assert.Equal(default, mapper.Capture(input));
    }

    [Fact]
    public void EditorTransitionDoesNotCarryPaintingIntoAnAttack()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetMouseButton(MouseButtons.Left, true);
        input.CancelButtons();
        Assert.Equal(default, mapper.Capture(input));
        input.SetMouseButton(MouseButtons.Left, false);
        input.SetMouseButton(MouseButtons.Left, true);
        Assert.True(mapper.Capture(input).PrimaryHeld);
    }

    [Fact]
    public void TriggerDashStaysHeldUntilTriggerCrossesReleaseThreshold()
    {
        // The simulation derives one press per hold, so hysteresis on the held state is what matters.
        var device = new XboxControllerInput2D();
        var mapper = new PlayerInputMapper2D();
        var input = new InputState();
        device.Update(0, default, 0);
        input.SetGamepad(device.Update(0, default, 40));
        Assert.True(mapper.Capture(input).DashHeld);
        foreach (byte value in new byte[] { 39, 255, 25 })
        {
            input.SetGamepad(device.Update(0, default, value));
            Assert.True(mapper.Capture(input).DashHeld);
        }
        input.SetGamepad(device.Update(0, default, 24));
        Assert.False(mapper.Capture(input).DashHeld);
        input.SetGamepad(device.Update(0, default, 40));
        Assert.True(mapper.Capture(input).DashHeld);
    }

    [Fact]
    public void ControllerResumeIgnoresHeldButtonsUntilReleased()
    {
        var device = new XboxControllerInput2D();
        Assert.Equal(GamepadButton2D.None, device.Update((ushort)GamepadButton2D.X, default, 255).Down);
        device.Update(0, default, 0);
        Assert.Equal(GamepadButton2D.X, device.Update((ushort)GamepadButton2D.X, default, 0).Pressed);
        device.Reset();
        Assert.Equal(GamepadButton2D.None, device.Update((ushort)GamepadButton2D.X, default, 0).Down);
    }

    [Fact]
    public void StickDeadZoneRemovesDriftAndKeepsFullDiagonalWithinUnitLength()
    {
        var device = new XboxControllerInput2D();
        Assert.Equal(Vector2.Zero, device.Update(0, new Vector2(100, -100), 0).LeftStick);
        var diagonal = device.Update(0, new Vector2(short.MaxValue, short.MinValue), 0).LeftStick;
        Assert.InRange(diagonal.Length(), 0.999f, 1.001f);
        Assert.True(diagonal.X > 0 && diagonal.Y < 0);
    }

    [Fact]
    public void GamepadStateCarriesBothSticksAndTriggerValues()
    {
        var device = new XboxControllerInput2D();
        var state = device.Update(0, new Vector2(short.MaxValue, 0), 128,
            new Vector2(0, short.MaxValue), 255);

        Assert.True(state.IsConnected);
        Assert.Equal(Vector2.UnitX, state.LeftStick);
        Assert.Equal(Vector2.UnitY, state.RightStick);
        Assert.Equal(1f, state.LeftTrigger);
        Assert.InRange(state.RightTrigger, 0.50f, 0.51f);
    }
}
