using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Transforms;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>A body's enabled collision entries use the same bind-pose placement and units as its drawing.</summary>
public static class PartShapes2D
{
    public static IShape2D Collision(Part asset)
    {
        var pivot = asset.Point(asset.Pivot) ?? throw new InvalidDataException("Unknown part pivot.");
        var local = Matrix3x2.CreateTranslation(-pivot.XY.ToVector2()) * Matrix3x2.CreateScale(asset.Scale);
        var rig = asset.IsAnimated ? ResolvedModel.Rig(asset.RigModel()) : null;
        uint? mask = null;
        var shapes = new List<IShape2D>();
        foreach (var entry in asset.Geometry.Where(g => g.Collision is { Enabled: true }))
        {
            if (entry.Geometry is not IShape2D shape || entry.Bindings is { Count: > 0 })
                throw new InvalidDataException("Body collision entries need rigid shapes.");
            if (entry.Socket is not null || entry.Prop is not null)
                throw new InvalidDataException("Body collision entries must attach within their own Part.");
            if (mask is { } previous && previous != entry.Collision!.Mask)
                throw new InvalidDataException("One rigid body's collision entries must share a mask.");
            mask = entry.Collision!.Mask;
            var frame = PartAttachment2D.FrameOf(entry,
                id => rig is null ? asset.LocationFrame(id).Origin : rig.Rest[id],
                id => rig is null ? asset.LocationFrame(id) : rig.RestPlacement(id));
            var matrix = new Matrix3x2(frame.XAxis.X, frame.XAxis.Y, frame.YAxis.X, frame.YAxis.Y, frame.Origin.X, frame.Origin.Y);
            shapes.Add(Place(shape, matrix * local));
        }
        return shapes.Count switch
        {
            0 => throw new InvalidDataException("A physical part needs at least one enabled collision entry."),
            1 => shapes[0],
            _ => CompositeShape2D.Builder.Create(shapes.SelectMany(shape => shape switch
            {
                IConvexShape2D convex => [convex],
                CompositeShape2D composite => composite.Parts.ToArray().Cast<IConvexShape2D>(),
                _ => throw new InvalidDataException("Multiple body collision entries must be convex or composite.")
            }))
        };
    }

    private static IShape2D Place(IShape2D shape, Matrix3x2 matrix)
    {
        shape.Validate();
        if (!ShapeBounds2D.Calculate(shape).IsFinite)
            throw new InvalidDataException("A world part needs finite collision geometry.");
        if (!Similarity2D.TryFromMatrix(matrix, out var pose))
            throw new InvalidDataException("Rigid collision needs uniform geometry scale.");
        return matrix == Matrix3x2.Identity ? shape : ShapeTransforms2D.Posed(shape, pose);
    }
}
