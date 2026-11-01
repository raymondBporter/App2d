namespace App2d.Core.Characters;

/// <summary>
/// Drawing order inside one character. Smaller values are closer to the viewer.
/// This is independent of the scene ZIndex that orders whole world objects.
/// Continuous values preserve the spacing between overlapping shapes and animated layer changes.
/// </summary>
public readonly record struct CharacterLayer2D(float Order)
{
    public static CharacterLayer2D Front => new(-.2f);
    public static CharacterLayer2D Middle => new(0);
    public static CharacterLayer2D Back => new(.2f);
}
