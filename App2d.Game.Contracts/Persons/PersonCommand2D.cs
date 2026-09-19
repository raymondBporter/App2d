namespace App2d.Gameplay.Persons;

/// <summary>
/// One tick of player intent as held state only. Edges (pressed/released) are derived by
/// the simulation from the previous tick's command, so a lost or repeated command cannot
/// drop or double a press. Axes are clamped to [-1, 1] by the session before use.
/// </summary>
public readonly record struct PersonCommand2D(
    float MoveX,
    float ClimbY,
    bool JumpHeld,
    bool DashHeld,
    bool DownHeld,
    bool PrimaryHeld,
    bool SecondaryHeld,
    bool SwitchHeld = false);
