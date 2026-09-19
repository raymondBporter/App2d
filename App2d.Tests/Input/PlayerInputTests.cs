using App2d.Gameplay.Persons;
using App2d.Gameplay.Player;
using App2d.Input;
using System.Numerics;
using Xunit;

namespace App2d.Tests.Input;

public sealed class PlayerInputTests
{
    [Fact]
    public void BriefTapSurvivesUntilOneSimulationTick()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.Space, true);
        input.SetKey(Keys.Space, false);
        // A tap inside one tick is reported as a one-tick hold; the simulation derives the press.
        var first = mapper.Capture(input, default);
        Assert.True(first.JumpHeld);
        input.EndFrame();
        var second = mapper.Capture(input, default);
        Assert.False(second.JumpHeld);
    }

    [Fact]
    public void ReleasingOneAttackBindingDoesNotReleaseAnotherOrStartAnotherAttack()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.F, true);
        Assert.True(mapper.Capture(input, default).PrimaryHeld);
        input.EndFrame();
        input.SetMouseButton(MouseButtons.Left, true);
        Assert.True(mapper.Capture(input, default).PrimaryHeld);
        input.EndFrame();
        input.SetKey(Keys.F, false);
        Assert.True(mapper.Capture(input, default).PrimaryHeld); // The mouse still holds the logical button.
        input.EndFrame();
        input.SetMouseButton(MouseButtons.Left, false);
        Assert.False(mapper.Capture(input, default).PrimaryHeld);
    }

    [Fact]
    public void KeyboardAndControllerShareJumpState()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.Space, true);
        Assert.True(mapper.Capture(input, default).JumpHeld);
        input.EndFrame();
        var pad = new XboxControllerState2D(default, XboxButtons.A, XboxButtons.A, default);
        Assert.True(mapper.Capture(input, pad).JumpHeld);
        input.SetKey(Keys.Space, false);
        pad = pad with { Pressed = default };
        Assert.True(mapper.Capture(input, pad).JumpHeld); // The pad still holds the logical button.
        input.EndFrame();
        // A disconnected controller contributes no held buttons.
        Assert.False(mapper.Capture(input, default).JumpHeld);
    }

    [Fact]
    public void UpOnlyClimbsAndShiftDoesNotDisableVerticalMovement()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.Up, true);
        input.SetKey(Keys.ShiftKey, true);
        var command = mapper.Capture(input, default);
        Assert.Equal(1f, command.ClimbY);
        Assert.True(command.DashHeld);
        Assert.False(command.JumpHeld);
        input.EndFrame();
        input.SetKey(Keys.Up, false);
        input.SetKey(Keys.Down, true);
        input.SetKey(Keys.Space, true);
        command = mapper.Capture(input, default);
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
        var pad = new XboxControllerState2D(default, XboxButtons.B | XboxButtons.Y,
            XboxButtons.B | XboxButtons.Y, default);
        Assert.Equal(default(PersonCommand2D), mapper.Capture(input, pad));
    }

    [Fact]
    public void SuppressionCancelsActionsAndRequiresReleaseBeforeKeyboardRepeat()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.F, true);
        Assert.True(mapper.Capture(input, default).PrimaryHeld);
        input.SetSuppressed(true);
        Assert.Equal(default(PersonCommand2D), mapper.Capture(input, default));
        input.SetSuppressed(false);
        input.SetKey(Keys.F, true); // OS key repeat from the original hold.
        Assert.Equal(default(PersonCommand2D), mapper.Capture(input, default));
        input.SetKey(Keys.F, false);
        input.SetKey(Keys.F, true);
        Assert.True(mapper.Capture(input, default).PrimaryHeld);
    }

    [Fact]
    public void FocusLossClearsPendingEventsAndSuppressesControllerToo()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.Space, true);
        input.SetWindowActive(false);
        var pad = new XboxControllerState2D(default, XboxButtons.X, XboxButtons.X, default);
        Assert.True(input.IsSuppressed);
        Assert.False(input.WasKeyPressed(Keys.Space));
        Assert.Equal(default(PersonCommand2D), mapper.Capture(input, pad));
        input.SetWindowActive(true);
        Assert.False(input.IsSuppressed);
        Assert.Equal(default(PersonCommand2D), mapper.Capture(input, default));
    }

    [Fact]
    public void EditorTransitionDoesNotCarryPaintingIntoAnAttack()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetMouseButton(MouseButtons.Left, true);
        input.CancelButtons();
        Assert.Equal(default(PersonCommand2D), mapper.Capture(input, default));
        input.SetMouseButton(MouseButtons.Left, false);
        input.SetMouseButton(MouseButtons.Left, true);
        Assert.True(mapper.Capture(input, default).PrimaryHeld);
    }

    [Fact]
    public void TriggerDashStaysHeldUntilTriggerCrossesReleaseThreshold()
    {
        // The simulation derives one press per hold, so hysteresis on the held state is what matters.
        var device = new XboxControllerInput2D();
        var mapper = new PlayerInputMapper2D();
        var input = new InputState();
        device.Update(0, default, 0);
        Assert.True(mapper.Capture(input, device.Update(0, default, 40)).DashHeld);
        foreach (byte value in new byte[] { 39, 255, 25 })
            Assert.True(mapper.Capture(input, device.Update(0, default, value)).DashHeld);
        Assert.False(mapper.Capture(input, device.Update(0, default, 24)).DashHeld);
        Assert.True(mapper.Capture(input, device.Update(0, default, 40)).DashHeld);
    }

    [Fact]
    public void ControllerResumeIgnoresHeldButtonsUntilReleased()
    {
        var device = new XboxControllerInput2D();
        Assert.Equal(XboxButtons.None, device.Update((ushort)XboxButtons.X, default, 255).Down);
        device.Update(0, default, 0);
        Assert.Equal(XboxButtons.X, device.Update((ushort)XboxButtons.X, default, 0).Pressed);
        device.Reset();
        Assert.Equal(XboxButtons.None, device.Update((ushort)XboxButtons.X, default, 0).Down);
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
}
