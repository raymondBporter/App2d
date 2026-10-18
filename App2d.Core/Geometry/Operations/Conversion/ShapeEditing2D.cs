using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Conversion;

/// <summary>Editor conversions between authored shape kinds.</summary>
public static class ShapeEditing2D
{
    /// <summary>
    /// Re-authors this definition as another kind fitted to the same bounds, for editors that switch kinds: a circle
    /// takes the smaller extent, a capsule runs along the longer one, and triangles and polygons take the box corners.
    /// </summary>
    /// <param name="kind">One of <see cref="ShapeKinds2D"/>.</param>
    /// <returns>This definition when the kind already matches, otherwise a new definition of that kind.</returns>
    /// <exception cref="ArgumentException">The kind is unknown.</exception>
    /// <exception cref="InvalidOperationException">This definition has no finite bounds to fit.</exception>
    public static IShape2D WithKind(this IShape2D definition, string kind)
    {
        if (kind == definition.Kind)
        {
            return definition;
        }

        var bounds = ShapeBounds2D.Calculate(definition.Validated());
        if (!bounds.IsFinite)
        {
            throw new InvalidOperationException($"A {definition.Kind} has no finite bounds to re-author from.");
        }

        var center = bounds.Center;
        var size = bounds.Size;
        var reach = MathF.Abs(size.X - size.Y) / 2f;
        return kind switch
        {
            ShapeKinds2D.Rectangle => Rectangle2D.Builder.FromSize(size, center),
            ShapeKinds2D.AxisAlignedRectangle => AxisAlignedRectangle2D.Builder.FromSize(size, center),
            ShapeKinds2D.RoundedRectangle => RoundedRectangle2D.Builder.FromSize(size, MathF.Min(size.X, size.Y) * .1f, center),
            ShapeKinds2D.Circle => new Circle2D { Center = center, Radius = MathF.Min(size.X, size.Y) / 2f }.Validated(),
            ShapeKinds2D.Ellipse => new Ellipse2D { Center = center, Radii = size / 2f }.Validated(),
            ShapeKinds2D.Capsule => size.X >= size.Y
                ? new Capsule2D { Start = center - new Vector2(reach, 0f), End = center + new Vector2(reach, 0f), Radius = size.Y / 2f }.Validated()
                : new Capsule2D { Start = center - new Vector2(0f, reach), End = center + new Vector2(0f, reach), Radius = size.X / 2f }.Validated(),
            ShapeKinds2D.Triangle => new Triangle2D { A = bounds.BottomLeft, B = bounds.BottomRight, C = bounds.TopCenter }.Validated(),
            ShapeKinds2D.ConvexPolygon => new ConvexPolygon2D { Vertices = [bounds.BottomLeft, bounds.BottomRight, bounds.TopRight, bounds.TopLeft] }.Validated(),
            ShapeKinds2D.SimplePolygon => new SimplePolygon2D { Vertices = [bounds.BottomLeft, bounds.BottomRight, bounds.TopRight, bounds.TopLeft] }.Validated(),
            ShapeKinds2D.HeightField => HeightFieldShape2D.Builder.Create([bounds.TopLeft, bounds.TopRight], bounds.Bottom),
            ShapeKinds2D.HalfSpace => new HalfSpace2D { Normal = new(0f, 1f), Offset = bounds.Top }.Validated(),
            ShapeKinds2D.Composite => new CompositeShape2D { Parts = [definition] }.Validated(),
            _ => throw new ArgumentException($"Unknown shape kind '{kind}'.", nameof(kind))
        };
    }
}
