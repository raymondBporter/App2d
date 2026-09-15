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
        var first = mapper.Capture(input, default);
        Assert.True(first.Movement.JumpPressed);
        Assert.True(first.Movement.JumpReleased);
        Assert.False(first.Movement.JumpHeld);
        input.EndFrame();
        var second = mapper.Capture(input, default);
        Assert.False(second.Movement.JumpPressed);
        Assert.False(second.Movement.JumpReleased);
    }

    [Fact]
    public void ReleasingOneAttackBindingDoesNotReleaseAnotherOrStartAnotherAttack()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.F, true);
        Assert.True(mapper.Capture(input, default).UsePrimaryAction);
        input.EndFrame();
        input.SetMouseButton(MouseButtons.Left, true);
        var both = mapper.Capture(input, default);
        Assert.False(both.UsePrimaryAction);
        input.EndFrame();
        input.SetKey(Keys.F, false);
        var mouse = mapper.Capture(input, default);
        Assert.True(mouse.PrimaryActionHeld);
        Assert.False(mouse.PrimaryActionReleased);
        input.EndFrame();
        input.SetMouseButton(MouseButtons.Left, false);
        Assert.True(mapper.Capture(input, default).PrimaryActionReleased);
    }

    [Fact]
    public void KeyboardAndControllerShareJumpState()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.Space, true);
        Assert.True(mapper.Capture(input, default).Movement.JumpPressed);
        input.EndFrame();
        var pad = new XboxControllerState2D(default, XboxButtons.A, XboxButtons.A, default);
        Assert.False(mapper.Capture(input, pad).Movement.JumpPressed);
        input.SetKey(Keys.Space, false);
        pad = pad with { Pressed = default };
        Assert.False(mapper.Capture(input, pad).Movement.JumpReleased);
        input.EndFrame();
        // A disconnected controller contributes no held buttons.
        Assert.True(mapper.Capture(input, default).Movement.JumpReleased);
    }

    [Fact]
    public void UpOnlyClimbsAndShiftDoesNotDisableVerticalMovement()
    {
        var input = new InputState();
        var mapper = new PlayerInputMapper2D();
        input.SetKey(Keys.Up, true);
        input.SetKey(Keys.ShiftKey, true);
        var command = mapper.Capture(input, default);
        Assert.Equal(1f, command.Movement.ClimbY);
        Assert.True(command.Movement.DashPressed);
        Assert.False(command.Movement.JumpPressed);
        input.EndFrame();
        input.SetKey(Keys.Up, false);
        input.SetKey(Keys.Down, true);
        input.SetKey(Keys.Space, true);
        command = mapper.Capture(input, default);
        Assert.Equal(-1f, command.Movement.ClimbY);
        Assert.True(command.Movement.DropThroughPressed);
        Assert.True(command.Movement.LadderJumpPressed);
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
        Assert.True(mapper.Capture(input, default).PrimaryActionHeld);
        input.SetSuppressed(true);
        Assert.Equal(default(PersonCommand2D), mapper.Capture(input, default));
        input.SetSuppressed(false);
        input.SetKey(Keys.F, true); // OS key repeat from the original hold.
        Assert.Equal(default(PersonCommand2D), mapper.Capture(input, default));
        input.SetKey(Keys.F, false);
        input.SetKey(Keys.F, true);
        Assert.True(mapper.Capture(input, default).UsePrimaryAction);
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
        Assert.True(mapper.Capture(input, default).UsePrimaryAction);
    }

    [Fact]
    public void TriggerDashFiresOnceUntilTriggerCrossesReleaseThreshold()
    {
        var device = new XboxControllerInput2D();
        var mapper = new PlayerInputMapper2D();
        var input = new InputState();
        device.Update(0, default, 0);
        Assert.True(mapper.Capture(input, device.Update(0, default, 40)).Movement.DashPressed);
        foreach (byte value in new byte[] { 39, 255, 25 })
            Assert.False(mapper.Capture(input, device.Update(0, default, value)).Movement.DashPressed);
        Assert.False(mapper.Capture(input, device.Update(0, default, 24)).Movement.DashPressed);
        Assert.True(mapper.Capture(input, device.Update(0, default, 40)).Movement.DashPressed);
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
