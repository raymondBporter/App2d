using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class PrimitiveBufferViewTests
{
    [Fact]
    public void TriangleViewsUseTheSelectedStaticFactoryAndSupportShapeValues()
    {
        Span<Vector2f> points = [Vector2f.Zero, new(4, 0), new(0, 3)];
        var triangles = MeshTopology.TriangleFan(points);
        var custom = triangles.As<FactoryTriangle>().GetEnumerator();
        Assert.True(custom.MoveNext());
        Assert.True(custom.Current.FromFactory);
        Assert.Equal(6d, custom.Current.GetArea());
        Assert.True(custom.Current.Reversed().FromFactory);
        Assert.True(custom.Current.TransformedBy(Matrix3x2.CreateTranslation(7, 9)).FromFactory);

        var shapes = triangles.As<Triangle2D>().GetEnumerator();
        Assert.True(shapes.MoveNext());
        Assert.Equal(Triangle2D.Builder.Create(points[0], points[1], points[2]), shapes.Current);

        var raw = triangles.GetEnumerator();
        Assert.True(raw.MoveNext());
        Assert.Equal(6d, raw.Current.GetArea());
        Assert.True(raw.Current.Contains(new(1, 1)));
        Assert.False(raw.Current.Contains(new(3, 2)));
        Assert.Equal(-raw.Current.SignedAreaTwice(), raw.Current.Reversed().SignedAreaTwice());
        Span<Vector2f> written = [new(99), new(99), new(99), new(99)];
        raw.Current.WriteVertices(written);
        Assert.Equal(points.ToArray(), written[..3].ToArray());
        Assert.Equal(new Vector2f(99), written[3]);
    }

    [Fact]
    public void ThreeDimensionalAndPackedVerticesUseTheSameViews()
    {
        Span<Vector3> points = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.One, Vector3.UnitY, Vector3.UnitZ];
        var actual = new List<Triangle3D>();
        foreach (var triangle in MeshTopology.TriangleList(points).As<Triangle3D>()) actual.Add(triangle);
        Assert.Equal(2, actual.Count);
        Assert.Equal(.5d, actual[0].Area);
        Assert.Equal(Vector3.One, actual[1].A);
        Assert.Throws<ArgumentException>(() => MeshTopology.TriangleList([Vector2f.Zero]));
    }

    [Fact]
    public void SegmentViewsBorrowVerticesAndCanReturnValidatedCurveSegments()
    {
        Span<Vector2f> points = [new(1, 2), new(4, 6), new(7, 8)];
        var outline = MeshTopology.Segments(points);
        points[0] = new(1, 3);
        var edges = new List<Segment<Vector2f, float>>();
        foreach (var edge in outline) edges.Add(edge);
        Assert.Equal(3, edges.Count);
        Assert.Equal(Vector2f.Distance(points[0], points[1]), edges[0].Length());
        Assert.Equal(new Vector2f(2.5f, 4.5f), edges[0].PointAt(.5f));
        Assert.Equal(new Vector2f(6, 9), edges[0].TransformedBy(Matrix3x2.CreateTranslation(5, 6)).Start);
        Assert.Equal(new Segment<Vector2f, float>(points[2].ToVector2f(), points[0].ToVector2f()), edges[2]);
        Assert.Equal(new Segment<Vector2f, float>(points[0].ToVector2f(), points[2].ToVector2f()), edges[2].Reversed());

        var curves = MeshTopology.Segments<LineSegmentCurve2D, Vector2f>(points, closed: false);
        Assert.Equal(2, curves.Count);
        var cursor = curves.GetEnumerator();
        Assert.True(cursor.MoveNext());
        Assert.Equal(Vector2f.Distance(points[0], points[1]), cursor.Current.Length());
        Assert.Equal((points[0] + points[1]) / 2, cursor.Current.PointAt(.5f));
        Assert.Equal(points[0] + new Vector2f(5, 6), cursor.Current.TransformedBy(Matrix3x2.CreateTranslation(5, 6)).Start);
        Assert.Equal(0, MeshTopology.Segments([], closed: false).Count);
        Assert.Equal(0, MeshTopology.Segments([]).Count);
        Assert.Equal(1, MeshTopology.Segments([Vector2f.Zero]).Count);
    }

    [Fact]
    public void MeshBoundaryOmitsSharedEdgesAndPreservesDirectedWinding()
    {
        var mesh = new TriangleMesh2D([Vector2f.Zero, Vector2f.UnitX, Vector2f.One, Vector2f.UnitY], [0, 1, 2, 0, 2, 3]);
        Assert.Equal([(0, 1), (1, 2), (2, 3), (3, 0)], mesh.BoundaryEdges.ToArray());
        var edges = new List<Segment<Vector2f, float>>();
        foreach (var edge in mesh.BoundarySegments) edges.Add(edge);
        Assert.Equal(4, edges.Count);
        Assert.DoesNotContain(new Segment<Vector2f, float>(Vector2f.Zero, Vector2f.One), edges);
        Assert.Equal(new Segment<Vector2f, float>(Vector2f.UnitY, Vector2f.Zero), edges[^1]);
    }

    [Fact]
    public void BoundaryIncludesHoleEdgesWithoutInferringAPerimeterFromVertexStorageOrder()
    {
        var mesh = new TriangleMesh2D(
            [new(-2, -2), new(2, -2), new(2, 2), new(-2, 2), new(-1, -1), new(1, -1), new(1, 1), new(-1, 1)],
            [0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7]);
        Assert.Equal(8, mesh.BoundaryEdges.Length);
        Assert.False(mesh.ContainsPoint(Vector2f.Zero));
        Assert.True(mesh.ContainsPoint(new(1.5f, 0)));
        Assert.Contains((5, 4), mesh.BoundaryEdges.ToArray());
    }

    [Fact]
    public void VertexTransformsHandleInPlaceAndPartiallyOverlappingBuffers()
    {
        var transform = Matrix3x2.CreateRotation(.4f) * Matrix3x2.CreateTranslation(4, 7);
        Vector2f[] original = [new(1, 2), new(3, 4), new(5, 6), new(7, 8)];
        var expected = original.Select(point => Vector2f.Transform(point, transform)).ToArray();
        var inPlace = original.ToArray();
        VertexTransforms2D.Transform(inPlace, inPlace, transform);
        Assert.Equal(expected, inPlace);
        var overlap = original.Append(new Vector2f(99)).ToArray();
        VertexTransforms2D.Transform(overlap.AsSpan(0, 4), overlap.AsSpan(1, 4), transform);
        Assert.Equal(expected, overlap[1..]);
        overlap = [new Vector2f(99), .. original];
        VertexTransforms2D.Transform(overlap.AsSpan(1, 4), overlap.AsSpan(0, 4), transform);
        Assert.Equal(expected, overlap[..4]);
        Assert.Throws<ArgumentOutOfRangeException>(() => VertexTransforms2D.Transform(original, new Vector2f[1], transform));
    }

    [Fact]
    public void RepeatedValueAndCachedBoundaryEnumerationDoesNotAllocate()
    {
        var mesh = new TriangleMesh2D([Vector2f.Zero, Vector2f.UnitX, Vector2f.One, Vector2f.UnitY], [0, 1, 2, 0, 2, 3]);
        _ = Sum(mesh);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0d;
        for (var i = 0; i < 1000; i++) sum += Sum(mesh);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sum > 0);
        Assert.Equal(0, allocated);

        static double Sum(TriangleMesh2D mesh)
        {
            var sum = 0d;
            foreach (var triangle in mesh.Triangles) sum += triangle.GetArea();
            foreach (var segment in mesh.BoundarySegments) sum += segment.Length();
            return sum;
        }
    }

    private readonly record struct FactoryTriangle(Vector2f A, Vector2f B, Vector2f C, bool FromFactory = false)
        : ITriangle<FactoryTriangle, Vector2f>
    {
        public static FactoryTriangle Create(Vector2f a, Vector2f b, Vector2f c) => new(a, b, c, true);
    }
}
