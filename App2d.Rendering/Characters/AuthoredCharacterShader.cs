using App2d.Core.Characters;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

/// <summary>
/// Draws an authored entity from a final pose the simulation already evaluated. It never samples animation itself, so
/// what is drawn is what collision used. The visual's transform places the feet origin; <see cref="Facing"/> mirrors.
/// </summary>
public sealed class AuthoredCharacterShader(ResolvedEntity entity) : IShader2D
{
    public ResolvedEntity Entity { get; } = entity;
    public EvaluatedPose? Pose { get; set; }
    public int Facing { get; set; } = 1;
    public Color BaseColor => Color.White;
}
