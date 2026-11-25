using App2d.Core.Geometry.Bounds;
using App2d.Core.Geometry.Primitives;

namespace App2d.Core.Geometry.Shapes;

/// <summary>Editor conversions between authored shape kinds.</summary>
public static class ShapeEditing2D
{
    /// <summary>
    /// Re-authors this definition as another kind fitted to the same bounds, for editors that switch kinds: a circle
    /// takes the smaller extent, a capsule runs along the longer one, and triangles and polygons take the box corners.
    /// </summary>
    /// <param name="kind">One of <see cref="Serialization.GeometryJson2D.ShapeKinds"/>.</param>
    /// <returns>This definition when the kind already matches, otherwise a new definition of that kind.</returns>
    /// <exception cref="ArgumentException">The kind is unknown.</exception>
    /// <exception cref="InvalidOperationException">This definition has no finite bounds to fit.</exception>
    public static IShape2D WithKind(this IShape2D definition, GeometryKind kind)
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
            GeometryKind.Rectangle => Rectangle2D.Builder.FromSize(size, center),
            GeometryKind.AxisAlignedRectangle => AxisAlignedRectangle2D.Builder.FromSize(size, center),
            GeometryKind.RoundedRectangle => RoundedRectangle2D.Builder.FromSize(size, MathF.Min(size.X, size.Y) * .1f, center),
            GeometryKind.Circle => new Circle2D { Center = center, Radius = MathF.Min(size.X, size.Y) / 2f }.Validated(),
            GeometryKind.Ellipse => new Ellipse2D { Center = center, Radii = size / 2f }.Validated(),
            GeometryKind.Capsule => size.X >= size.Y
                ? new Capsule2 { Start = center - new Vector2f(reach, 0f), End = center + new Vector2f(reach, 0f), Radius = size.Y / 2f }.Validated()
                : new Capsule2 { Start = center - new Vector2f(0f, reach), End = center + new Vector2f(0f, reach), Radius = size.X / 2f }.Validated(),
            GeometryKind.Triangle => new Triangle2D { A = bounds.BottomLeft, B = bounds.BottomRight, C = bounds.TopCenter }.Validated(),
            GeometryKind.ConvexPolygon => new ConvexPolygon2D { Vertices = [bounds.BottomLeft, bounds.BottomRight, bounds.TopRight, bounds.TopLeft] }.Validated(),
            GeometryKind.SimplePolygon => new SimplePolygon2D { Vertices = [bounds.BottomLeft, bounds.BottomRight, bounds.TopRight, bounds.TopLeft] }.Validated(),
            GeometryKind.HalfSpace => new HalfSpace2D { Normal = new(0f, 1f), Offset = bounds.Top }.Validated(),
            GeometryKind.Composite => new CompositeShape2D { Parts = [definition] }.Validated(),
            _ => throw new ArgumentException($"Unknown shape kind '{kind}'.", nameof(kind))
        };
    }
}
