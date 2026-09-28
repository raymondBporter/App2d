namespace App2d.Diagnostics;

/// <summary>Temporary player inspection controls, separate from gameplay bindings.</summary>
internal readonly record struct PlayerDebugInput2D(
    bool ToggleTraversal, bool ShieldPose, bool FireBallistics, bool ClearBallistics)
{
    public static PlayerDebugInput2D Capture(InputState input) => input.IsSuppressed ? default : new(
        input.Keyboard.WasPressed(Keys.F3), input.Keyboard.IsDown(Keys.B),
        input.Keyboard.WasPressed(Keys.F4), input.Keyboard.WasPressed(Keys.F6));
}
