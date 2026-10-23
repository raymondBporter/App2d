using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>A shared XY coordinate frame placed at a character's base Z and drawing depth.</summary>
public readonly record struct PartFrame2D(Frame2D Coordinates, float OriginZ, float Depth)
{
    public PartFrame2D(Vector3 origin, Vector2 right, Vector2 up, float depth)
        : this(new Frame2D(origin.XY, right, up), origin.Z, depth) { }

    public Vector3 Origin => new(Coordinates.Origin, OriginZ);
    public Vector2 Right => Coordinates.XAxis;
    public Vector2 Up => Coordinates.YAxis;
    public Vector3 At(Vector2 local) => new(Coordinates.TransformPoint(local), OriginZ + Depth);
}
