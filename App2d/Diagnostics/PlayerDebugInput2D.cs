namespace App2d.Diagnostics;

/// <summary>Temporary player inspection controls, separate from gameplay bindings.</summary>
internal readonly record struct PlayerDebugInput2D(
    bool ToggleTraversal, bool ShieldPose, bool FireBallistics, bool ClearBallistics)
{
    public static PlayerDebugInput2D Capture(InputState input) => input.IsSuppressed ? default : new(
        input.WasKeyPressed(Keys.F3), input.IsKeyDown(Keys.B),
        input.WasKeyPressed(Keys.F4), input.WasKeyPressed(Keys.F6));
}
