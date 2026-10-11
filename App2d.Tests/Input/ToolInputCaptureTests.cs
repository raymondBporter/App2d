using App2d.Core.Input;

namespace App2d.Tests.Input;

public sealed class ToolInputCaptureTests
{
    [Fact]
    public void PanelCaptureRemovesPendingClickAndWheelButLeavesOtherDevicesAvailable()
    {
        var input = new InputState();
        input.SetKey(Keys.Space, true);
        input.SetMouseButton(MouseButtons.Left, true);
        input.Mouse.AddWheelDelta(120);
        input.SetGamepad(new(default, GamepadButton2D.A, GamepadButton2D.A, default) { IsConnected = true });

        input.SetToolCapture(keyboard: false, mouse: true);

        Assert.False(input.Mouse.IsDown(MouseButtons.Left));
        Assert.False(input.Mouse.WasPressed(MouseButtons.Left));
        Assert.Equal(0, input.Mouse.WheelDelta);
        Assert.True(input.Keyboard.WasPressed(Keys.Space));
        Assert.True(input.Gamepad.WasPressed(GamepadButton2D.A));
        Assert.False(input.IsSuppressed);
    }

    [Fact]
    public void KeyboardCaptureBlocksRepeatUntilThePhysicalKeyIsReleased()
    {
        var input = new InputState();
        input.SetKey(Keys.D, true);
        input.SetToolCapture(keyboard: true, mouse: false);
        Assert.False(input.Keyboard.IsDown(Keys.D));
        Assert.False(input.Keyboard.WasPressed(Keys.D));

        input.SetToolCapture(keyboard: false, mouse: false);
        input.SetKey(Keys.D, true);
        Assert.False(input.Keyboard.IsDown(Keys.D));
        input.SetKey(Keys.D, false); input.SetKey(Keys.D, true);
        Assert.True(input.Keyboard.WasPressed(Keys.D));
    }

    [Fact]
    public void LeavingThePanelDoesNotTurnItsHeldClickIntoPainting()
    {
        var input = new InputState();
        input.SetToolCapture(keyboard: false, mouse: true);
        input.SetMouseButton(MouseButtons.Left, true);
        input.SetToolCapture(keyboard: false, mouse: false);
        Assert.False(input.Mouse.IsDown(MouseButtons.Left));
        input.SetMouseButton(MouseButtons.Left, false); input.SetMouseButton(MouseButtons.Left, true);
        Assert.True(input.Mouse.WasPressed(MouseButtons.Left));
    }
}
