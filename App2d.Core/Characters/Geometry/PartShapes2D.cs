using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Transforms;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Rigid collision uses the same local placement and units as the part's drawing.</summary>
public static class PartShapes2D
{
    public static IShape2D Collision(Part asset)
    {
        var pivot = asset.Point(asset.Pivot) ?? throw new InvalidDataException("Unknown part pivot.");
        var local = Matrix3x2.CreateTranslation(-pivot.XY) * Matrix3x2.CreateScale(asset.Scale);
        if (asset.Collision is { } collision) return Place(collision, local);
        if (asset.IsAnimated)
            throw new InvalidDataException("An animated physical part needs an explicit rigid collision shape.");
        var shapes = new List<IShape2D>();
        foreach (var entry in asset.Geometry)
        {
            if (entry.Geometry is not IShape2D shape || entry.Bindings is { Count: > 0 })
                throw new InvalidDataException("Physics needs rigid shapes or an explicit collision shape.");
            var frame = PartAttachment2D.FrameOf(entry, id => asset.Point(id)!.Value.XYZ, asset.LocationFrame);
            var matrix = new Matrix3x2(frame.XAxis.X, frame.XAxis.Y, frame.YAxis.X, frame.YAxis.Y, frame.Origin.X, frame.Origin.Y);
            shapes.Add(Place(shape, matrix * local));
        }
        return shapes.Count switch
        {
            0 => throw new InvalidDataException("A physical part needs collision geometry."),
            1 => shapes[0],
            _ => CompositeShape2D.Builder.Create(shapes.SelectMany(shape => shape switch
            {
                IConvexShape2D convex => new[] { convex },
                CompositeShape2D composite => composite.Parts.ToArray().Cast<IConvexShape2D>(),
                _ => throw new InvalidDataException("Multiple collision entries must be convex; use an explicit collision shape.")
            }))
        };
    }

    private static IShape2D Place(IShape2D shape, Matrix3x2 matrix)
    {
        shape.Validate();
        if (!ShapeBounds2D.Calculate(shape).IsFinite)
            throw new InvalidDataException("A world part needs finite collision geometry.");
        if (!Similarity2D.TryFromMatrix(matrix, out var pose))
            throw new InvalidDataException("Rigid collision needs uniform geometry scale or an explicit collision shape.");
        return matrix == Matrix3x2.Identity ? shape : ShapeTransforms2D.Posed(shape, pose);
    }
}
