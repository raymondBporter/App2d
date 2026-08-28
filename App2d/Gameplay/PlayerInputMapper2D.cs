using App2d.Engine;

namespace App2d.Gameplay;

public sealed class PlayerInputMapper2D
{
    private readonly XboxControllerInput2D _controller = new();
    private bool _jumpActionHeld;

    public bool IsControllerConnected => _controller.IsConnected;

    public PlayerCommand2D Capture(
        InputState input,
        Camera2D camera,
        System.Numerics.Vector2 playerPosition)
    {
        ArgGuard.ThrowIfNull(input);
        ArgGuard.ThrowIfNull(camera);

        if (input.IsSuppressed)
        {
            Reset();
            return default;
        }

        var controller = _controller.Capture(playerPosition);

        var moveX =
            Axis(input, Keys.A, Keys.D) +
            Axis(input, Keys.Left, Keys.Right) +
            controller.MoveX;
        moveX = Math.Clamp(moveX, -1f, 1f);

        var jumpHeld =
            input.IsKeyDown(Keys.Space) ||
            input.IsKeyDown(Keys.W) ||
            input.IsKeyDown(Keys.Up) ||
            controller.JumpHeld;
        var anyJumpPressed =
            input.WasKeyPressed(Keys.Space) ||
            input.WasKeyPressed(Keys.W) ||
            input.WasKeyPressed(Keys.Up) ||
            controller.JumpPressed;
        var anyJumpReleased =
            input.WasKeyReleased(Keys.Space) ||
            input.WasKeyReleased(Keys.W) ||
            input.WasKeyReleased(Keys.Up) ||
            controller.JumpReleased;
        var movement = new PlayerIntent2D(
            moveX,
            anyJumpPressed && !_jumpActionHeld,
            jumpHeld,
            !jumpHeld && (_jumpActionHeld || anyJumpReleased));
        _jumpActionHeld = jumpHeld;

        var leftMousePressed = input.WasMousePressed(MouseButtons.Left);
        var rightMousePressed = input.WasMousePressed(MouseButtons.Right);
        var isChangingLoadout = input.IsControlDown;
        var usedMouse = leftMousePressed || rightMousePressed;
        return new PlayerCommand2D(
            movement,
            isChangingLoadout && leftMousePressed || controller.CycleLeftWeapon,
            isChangingLoadout && rightMousePressed || controller.CycleRightWeapon,
            input.WasKeyPressed(Keys.J) ||
                leftMousePressed && !isChangingLoadout ||
                controller.UseLeftWeapon,
            input.WasKeyPressed(Keys.K) ||
                rightMousePressed && !isChangingLoadout ||
                controller.UseRightWeapon,
            usedMouse
                ? camera.DeviceToWorld(input.MousePositionDevice)
                : controller.AimTarget,
            input.WasKeyPressed(Keys.F3));
    }

    public void Reset()
    {
        _jumpActionHeld = false;
        _controller.Reset();
    }

    private static float Axis(InputState input, Keys negative, Keys positive) =>
        (input.IsKeyDown(positive) ? 1f : 0f) -
        (input.IsKeyDown(negative) ? 1f : 0f);
}
