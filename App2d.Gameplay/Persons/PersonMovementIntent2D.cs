namespace App2d.Gameplay.Persons;

/// <summary>Locomotion intent with edges derived from consecutive held-state commands.</summary>
internal readonly record struct PersonMovementIntent2D(
    float MoveX,
    bool JumpPressed,
    bool JumpHeld,
    bool JumpReleased,
    bool DropThroughPressed,
    bool DashPressed,
    float ClimbY = 0f)
{
    public static PersonMovementIntent2D Derive(PersonCommand2D command, PersonCommand2D previous)
    {
        var jumpPressed = command.JumpHeld && !previous.JumpHeld;
        return new(
            command.MoveX,
            jumpPressed,
            command.JumpHeld,
            JumpReleased: !command.JumpHeld && previous.JumpHeld,
            DropThroughPressed: command.DownHeld && jumpPressed,
            DashPressed: command.DashHeld && !previous.DashHeld,
            command.ClimbY);
    }
}
