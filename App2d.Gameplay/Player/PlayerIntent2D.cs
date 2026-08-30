namespace App2d.Gameplay;

public readonly record struct PlayerIntent2D(
    float MoveX,
    bool JumpPressed,
    bool JumpHeld,
    bool JumpReleased,
    bool DropThroughPressed,
    bool DashPressed);
