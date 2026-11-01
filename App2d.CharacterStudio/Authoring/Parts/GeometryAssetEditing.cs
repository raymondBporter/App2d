using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.CharacterStudio.Authoring;

/// <summary>Point edits construct candidates before validation, preserving the last valid polygon during a drag.</summary>
public static class GeometryAssetEditing
{
    public static IReadOnlyList<Vector2> Points(GeometryAsset2D asset) => asset.Mesh is { } mesh
        ? [.. (mesh.Outline ?? mesh.Vertices).Select(p => p.XY.ToVector2())]
        : asset.Geometry switch
        {
            SimplePolygon2D p => p.Vertices.ToArray(),
            ConvexPolygon2D p => p.Vertices.ToArray(),
            Triangle2D t => [t.A, t.B, t.C],
            LineSegmentCurve2D l => [l.Start, l.End],
            PolylineCurve2D l => l.Points.ToArray(),
            RoundedPolylineCurve2D l => l.Points.ToArray(),
            BSpline2D s => s.ControlPoints.ToArray(),
            HeightFieldShape2D h => h.Surface.ToArray(),
            QuadraticBezier2D or CubicBezier2D => CurveControlPoints2D.Get((ICurve2D)asset.Geometry),
            _ => [],
        };

    public static GeometryAsset2D WithPoints(GeometryAsset2D asset, IReadOnlyList<Vector2> points)
    {
        if (asset.Mesh is { } mesh)
        {
            var original = mesh.Outline ?? mesh.Vertices;
            var values = points.Select((p, i) => new Core.Mathematics.Vector3f(p.X, p.Y, i < original.Count ? original[i].Z : 0)).ToList();
            if (mesh.Outline is null && values.Count != mesh.Vertices.Count) throw new InvalidDataException("Indexed meshes retain their vertex count; import a new mesh to change topology.");
            return asset with { Mesh = mesh.Outline is null ? mesh with { Vertices = values } : mesh with { Outline = values, Vertices = [.. mesh.Vertices], Triangles = [.. mesh.Triangles] } };
        }
        return asset with
        {
            Geometry = asset.Geometry switch
            {
                SimplePolygon2D p => p with { Vertices = [.. points] },
                ConvexPolygon2D p => p with { Vertices = [.. points] },
                Triangle2D t when points.Count == 3 => t with { A = points[0], B = points[1], C = points[2] },
                LineSegmentCurve2D l when points.Count == 2 => l with { Start = points[0], End = points[1] },
                PolylineCurve2D l => l with { Points = [.. points] },
                RoundedPolylineCurve2D l => l with { Points = [.. points] },
                BSpline2D s => BSpline2D.Builder.Create(points, s.Degree),
                HeightFieldShape2D h => h with { Surface = [.. points] },
                QuadraticBezier2D or CubicBezier2D => CurveControlPoints2D.With((ICurve2D)asset.Geometry, points),
                _ => throw new InvalidDataException("This geometry uses shape parameters rather than editable points."),
            }
        };
    }

    public static bool CanInsert(GeometryAsset2D asset) => asset.Geometry is SimplePolygon2D or ConvexPolygon2D or PolylineCurve2D or RoundedPolylineCurve2D or BSpline2D or HeightFieldShape2D || asset.Mesh?.Outline is not null;
    public static int MinimumPoints(GeometryAsset2D asset) => asset.Geometry switch
    {
        PolylineCurve2D or RoundedPolylineCurve2D or HeightFieldShape2D => 2,
        BSpline2D s => s.Degree + 1,
        _ => 3,
    };

    public static GeometryAsset2D InsertAfter(GeometryAsset2D asset, int index)
    {
        if (!CanInsert(asset)) throw new InvalidDataException("This geometry has a fixed number of points.");
        var points = Points(asset).ToList();
        var next = index + 1 < points.Count ? points[index + 1] : asset.Geometry is ICurve2D or HeightFieldShape2D ? points[index] + new Vector2(.1f, 0) : points[0];
        points.Insert(index + 1, (points[index] + next) / 2);
        return WithPoints(asset, points);
    }

    public static GeometryAsset2D RemovePoint(GeometryAsset2D asset, int index)
    {
        var points = Points(asset).ToList();
        if (!CanInsert(asset) || points.Count <= MinimumPoints(asset)) throw new InvalidDataException("This geometry needs its remaining points.");
        points.RemoveAt(index);
        return WithPoints(asset, points);
    }

    public static GeometryAsset2D TransformPoints(GeometryAsset2D asset, Matrix3x2 transform) => WithPoints(asset, [.. Points(asset).Select(p => Vector2.Transform(p, transform))]);
}
