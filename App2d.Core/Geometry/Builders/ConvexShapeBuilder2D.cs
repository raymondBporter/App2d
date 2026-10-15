using App2d.Core.Shapes;

namespace App2d.Core.Geometry.Builders;

/// <summary>Construction data that builds a finite convex shape, suitable for composite parts and convex queries.</summary>
public abstract record ConvexShapeBuilder2D : ShapeBuilder2D
{
    public abstract override IConvexShape2D Build();
}
