using App2d.Core.Characters;
using System.Collections.Immutable;

namespace App2d.Core.Rendering.Characters;

/// <summary>
/// Draws a character: a pose and the pieces to paint on it. Whoever plays the character hands over the same pose its
/// colliders were placed from, so what is drawn is what was hit. The visual's transform places the feet; the pose's facing
/// mirrors it.
/// </summary>
public sealed class CharacterShader : IShader2D
{
    public CharacterPose? Pose { get; set; }
    public ImmutableArray<Attachment> Drawn { get; set; } = [];
    /// <summary>A live blade trail in the character's own space, drawn behind it.</summary>
    public BladeSwoosh? Swoosh { get; set; }
    public Color4b BaseColor => Color4b.White;
}
