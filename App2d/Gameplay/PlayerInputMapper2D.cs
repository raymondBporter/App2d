using App2d.Engine;

namespace App2d.Gameplay;

public sealed class PlayerInputMapper2D
{
    private bool _jumpActionHeld;

    public PlayerCommand2D Capture(InputState input, Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(camera);

        var moveX = Axis(input, Keys.A, Keys.D) + Axis(input, Keys.Left, Keys.Right);
        moveX = Math.Clamp(moveX, -1f, 1f);

        var jumpHeld =
            input.IsKeyDown(Keys.Space) ||
            input.IsKeyDown(Keys.W) ||
            input.IsKeyDown(Keys.Up);
        var anyJumpPressed =
            input.WasKeyPressed(Keys.Space) ||
            input.WasKeyPressed(Keys.W) ||
            input.WasKeyPressed(Keys.Up);
        var anyJumpReleased =
            input.WasKeyReleased(Keys.Space) ||
            input.WasKeyReleased(Keys.W) ||
            input.WasKeyReleased(Keys.Up);
        var movement = new PlayerIntent2D(
            moveX,
            anyJumpPressed && !_jumpActionHeld,
            jumpHeld,
            !jumpHeld && (_jumpActionHeld || anyJumpReleased));
        _jumpActionHeld = jumpHeld;

        var usedMouse = input.WasMousePressed(MouseButtons.Left);
        var cycleDirection = input.IsControlDown && input.MouseWheelDelta != 0f
            ? Math.Sign(input.MouseWheelDelta)
            : 0;
        return new PlayerCommand2D(
            movement,
            cycleDirection,
            input.WasKeyPressed(Keys.J) || usedMouse,
            input.WasKeyPressed(Keys.K) || input.WasMousePressed(MouseButtons.Right),
            usedMouse ? camera.DeviceToWorld(input.MousePositionDevice) : null,
            input.WasKeyPressed(Keys.F3));
    }

    public void Reset() => _jumpActionHeld = false;

    private static float Axis(InputState input, Keys negative, Keys positive) =>
        (input.IsKeyDown(positive) ? 1f : 0f) -
        (input.IsKeyDown(negative) ? 1f : 0f);
}
