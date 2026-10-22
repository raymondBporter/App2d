using App2d.Core.Geometry.Operations.Transforms;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;
using static App2d.Tests.Geometry.VectorComparers;

namespace App2d.Tests.Geometry;

public sealed class ShapePosed2DTests
{
    private static readonly Similarity2D Pose = Similarity2D.FromAxis(new(10, -4), OrientedFrame2D.AxisFromAngle(MathF.PI / 3), scale: 2f);

    public static TheoryData<IShape2D> Shapes =>
    [
        Circle2D.Builder.Create(1, new(1, 2)),
        Capsule2D.Builder.Create(new(0, 0), new(3, 1), .5f),
        Triangle2D.Builder.Create(new(0, 0), new(2, 0), new(0, 1)),
        ConvexPolygon2D.Builder.Create([new(0, 0), new(2, 0), new(2, 1), new(0, 1)]),
        Rectangle2D.Builder.FromSize(3, 1, new(1, 1), .4f),
        RoundedRectangle2D.Builder.FromSize(3, 2, .5f, new(1, 1), .4f),
        Ellipse2D.Builder.Create(new(2, 1), new(1, 1), .4f),
    ];

    [Theory]
    [MemberData(nameof(Shapes))]
    public void PosedShapeSupportsWhereThePosedOriginalSupports(IShape2D shape)
    {
        var posed = Assert.IsAssignableFrom<IConvexShape2D>(ShapeTransforms2D.Posed(shape, Pose));
        Assert.Equal(shape.GetType(), posed.GetType());
        foreach (var angle in new[] { 0f, .9f, 2.2f, 4f })
        {
            var worldDirection = OrientedFrame2D.AxisFromAngle(angle);
            var expected = Pose.TransformPoint(((IConvexShape2D)shape).GetSupportPoint(Pose.TransposeTransformDirection(worldDirection)));
            Assert.Equal(expected, posed.GetSupportPoint(worldDirection), Loose);
        }
        Assert.Equal(shape.Area * 4f, posed.Area, 3);
    }

    [Fact]
    public void IdentityReturnsTheSameGeometry()
    {
        var box = Rectangle2D.Builder.FromSize(3, 1, new(1, 1), .4f);
        var same = Assert.IsType<Rectangle2D>(ShapeTransforms2D.Posed(box, Similarity2D.Identity));
        Assert.Equal(box.Center, same.Center, Near);
        Assert.Equal(box.HalfSize, same.HalfSize, Near);
        Assert.Equal(box.Axis, same.Axis, Near);
    }

    [Fact]
    public void MirrorKeepsOrientedShapesValid()
    {
        var mirrored = Similarity2D.FromAxis(Vector2.Zero, Vector2.UnitX, mirror: true);
        var rounded = Assert.IsType<RoundedRectangle2D>(ShapeTransforms2D.Posed(RoundedRectangle2D.Builder.FromSize(3, 2, .5f, new(1, 1), .4f), mirrored));
        Assert.Equal(new Vector2(1, -1), rounded.Center, Near);
        Assert.Equal(.5f, rounded.Radius);
    }

    [Fact]
    public void AxisAlignedRectangleAcceptsQuarterTurnsAndRefusesOthers()
    {
        var box = AxisAlignedRectangle2D.Builder.Create(new(0, 0), new(2, 1));
        var turned = Assert.IsType<AxisAlignedRectangle2D>(ShapeTransforms2D.Posed(box, Similarity2D.FromAxis(Vector2.Zero, Vector2.UnitY)));
        Assert.Equal(new Vector2(-1, 0), turned.Min, Near);
        Assert.Equal(new Vector2(0, 2), turned.Max, Near);
        Assert.Throws<NotSupportedException>(() => ShapeTransforms2D.Posed(box, Similarity2D.FromAxis(Vector2.Zero, OrientedFrame2D.AxisFromAngle(.5f))));
    }

    [Fact]
    public void CompositeAndHalfSpacePose()
    {
        var composite = CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1), Rectangle2D.Builder.FromSize(2, 2)]);
        var posed = Assert.IsType<CompositeShape2D>(ShapeTransforms2D.Posed(composite, Pose));
        Assert.Equal(2, posed.Parts.Length);
        Assert.Equal(composite.Area * 4f, posed.Area, 3);
        var floor = Assert.IsType<HalfSpace2D>(ShapeTransforms2D.Posed(HalfSpace2D.Builder.Create(Vector2.UnitY, 0f), Similarity2D.FromTranslation(new(0, 3))));
        Assert.Equal(3f, floor.Offset, 5);
    }

    [Fact]
    public void PosedRejectsDegeneratePoses()
    {
        Assert.ThrowsAny<ArgumentException>(() => ShapeTransforms2D.Posed(Circle2D.Builder.Create(1), default));
        Assert.Throws<NotSupportedException>(() => ShapeTransforms2D.Posed(HeightFieldShape2D.Builder.Create([new(0, 0), new(1, 0)], -1f), Pose));
    }
}
