namespace App2d.Gameplay.Persons;

public readonly record struct PersonMovementIntent2D(
    float MoveX,
    bool JumpPressed,
    bool JumpHeld,
    bool JumpReleased,
    bool DropThroughPressed,
    bool DashPressed,
    float ClimbY = 0f,
    bool LadderJumpPressed = false);
