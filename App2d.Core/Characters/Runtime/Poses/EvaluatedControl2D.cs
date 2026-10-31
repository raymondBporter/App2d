using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// One evaluated control: its affine XY frame, independent draw layer and unwrapped angle.
/// The angle retains full turns which cannot be recovered from the frame's axes.
/// </summary>
public readonly record struct EvaluatedControl2D(LayeredFrame2D Placement, float Angle)
{
    public Frame2D Frame => Placement.Frame;
    public Vector2 Position => Frame.Origin;
    public float Layer => Placement.Layer;
}
