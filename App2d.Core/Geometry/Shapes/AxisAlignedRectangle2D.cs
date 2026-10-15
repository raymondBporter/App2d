using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A rectangle whose owner intends to keep its world axes aligned.</summary>
public sealed partial class AxisAlignedRectangle2D : Rectangle2D
{
    private AxisAlignedRectangle2D(Vector2 min, Vector2 max) : base(min, max) { }
}
