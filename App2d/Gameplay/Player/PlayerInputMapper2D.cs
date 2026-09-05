using App2d.Core;
using App2d.Gameplay.Persons;
using App2d.Rendering;

namespace App2d.Gameplay.Player;

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
            !input.IsShiftDown && input.IsKeyDown(Keys.Up) ||
            controller.JumpHeld;
        var anyJumpPressed =
            input.WasKeyPressed(Keys.Space) ||
            input.WasKeyPressed(Keys.W) ||
            !input.IsShiftDown && input.WasKeyPressed(Keys.Up) ||
            controller.JumpPressed;
        var anyJumpReleased =
            input.WasKeyReleased(Keys.Space) ||
            input.WasKeyReleased(Keys.W) ||
            !input.IsShiftDown && input.WasKeyReleased(Keys.Up) ||
            controller.JumpReleased;
        var jumpPressed = anyJumpPressed && !_jumpActionHeld;
        var downHeld =
            input.IsKeyDown(Keys.S) ||
            !input.IsShiftDown && input.IsKeyDown(Keys.Down) ||
            controller.DownHeld;
        var movement = new PersonMovementIntent2D(
            moveX,
            jumpPressed,
            jumpHeld,
            !jumpHeld && (_jumpActionHeld || anyJumpReleased),
            downHeld && jumpPressed,
            input.WasKeyPressed(Keys.ShiftKey) ||
                input.WasKeyPressed(Keys.LShiftKey) ||
                input.WasKeyPressed(Keys.RShiftKey) ||
                controller.DashPressed);
        _jumpActionHeld = jumpHeld;

        var mouseAttackPressed = input.WasMousePressed(MouseButtons.Left);
        var mouseKickPressed = input.WasMousePressed(MouseButtons.Right);
        return new PlayerCommand2D(
            new PersonCommand2D(
                movement,
                input.WasKeyPressed(Keys.J) ||
                    mouseAttackPressed ||
                    controller.UsePrimaryAction,
                mouseAttackPressed || mouseKickPressed
                    ? camera.DeviceToWorld(input.MousePositionDevice)
                    : controller.AimTarget,
                input.WasKeyPressed(Keys.Q) ||
                    controller.SwitchEquipment,
                input.WasKeyPressed(Keys.K) ||
                    mouseKickPressed ||
                    controller.UseSecondaryAction),
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
