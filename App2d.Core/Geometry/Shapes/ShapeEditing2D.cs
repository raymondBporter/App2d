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
    public static IShape2D WithKind(this IShape2D definition, string kind)
    {
        if (kind == definition.GetKind())
        {
            return definition;
        }

        var bounds = ShapeBounds2D.Calculate(definition.Validated());
        if (!bounds.IsFinite)
        {
            throw new InvalidOperationException($"A {definition.GetKind()} has no finite bounds to re-author from.");
        }

        var center = bounds.Center;
        var size = bounds.Size;
        var reach = MathF.Abs(size.X - size.Y) / 2f;
        return kind switch
        {
            Rectangle2D.Kind => Rectangle2D.Builder.FromSize(size, center),
            AxisAlignedRectangle2D.Kind => AxisAlignedRectangle2D.Builder.FromSize(size, center),
            RoundedRectangle2D.Kind => RoundedRectangle2D.Builder.FromSize(size, MathF.Min(size.X, size.Y) * .1f, center),
            Circle2D.Kind => new Circle2D { Center = center, Radius = MathF.Min(size.X, size.Y) / 2f }.Validated(),
            Ellipse2D.Kind => new Ellipse2D { Center = center, Radii = size / 2f }.Validated(),
            Capsule2D.Kind => size.X >= size.Y
                ? new Capsule2D { Start = center - new Vector2f(reach, 0f), End = center + new Vector2f(reach, 0f), Radius = size.Y / 2f }.Validated()
                : new Capsule2D { Start = center - new Vector2f(0f, reach), End = center + new Vector2f(0f, reach), Radius = size.X / 2f }.Validated(),
            Triangle2D.Kind => new Triangle2D { A = bounds.BottomLeft, B = bounds.BottomRight, C = bounds.TopCenter }.Validated(),
            ConvexPolygon2D.Kind => new ConvexPolygon2D { Vertices = [bounds.BottomLeft, bounds.BottomRight, bounds.TopRight, bounds.TopLeft] }.Validated(),
            SimplePolygon2D.Kind => new SimplePolygon2D { Vertices = [bounds.BottomLeft, bounds.BottomRight, bounds.TopRight, bounds.TopLeft] }.Validated(),
            HalfSpace2D.Kind => new HalfSpace2D { Normal = new(0f, 1f), Offset = bounds.Top }.Validated(),
            CompositeShape2D.Kind => new CompositeShape2D { Parts = [definition] }.Validated(),
            _ => throw new ArgumentException($"Unknown shape kind '{kind}'.", nameof(kind))
        };
    }
}
