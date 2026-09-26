using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

/// <summary>
/// Draws an authored model from a final pose the simulation or presentation already evaluated. It never samples animation
/// itself, so what is drawn is what collision used. The visual's transform places the feet origin; <see cref="Facing"/>
/// mirrors. <see cref="Props"/> are placed on their sockets from the same pose.
/// </summary>
public sealed class AuthoredCharacterShader(ResolvedModel model) : IShader2D
{
    public ResolvedModel Model { get; } = model;
    public EvaluatedPose? Pose { get; set; }
    public int Facing { get; set; } = 1;
    public IReadOnlyList<(PropAsset Prop, ModelSocket Socket)> Props { get; set; } = [];
    /// <summary>A gameplay-blended face drawn instead of the pose's named expression.</summary>
    public FacePose? Face { get; set; }
    /// <summary>A live blade trail in the pose's actor space, drawn behind the character.</summary>
    public BladeSwoosh? Swoosh { get; set; }
    public Color BaseColor => Color.White;
}
