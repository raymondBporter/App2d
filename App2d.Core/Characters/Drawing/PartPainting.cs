using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using System.Numerics;

namespace App2d.Core.Characters.Drawing;

/// <summary>Paint lives in rest artwork coordinates and follows the same triangles as fill, including folding membranes.</summary>
internal static class PartPainting
{
    public static void Add(CharacterMesh mesh, PartGeometry part, LayeredFrame2D frame, PartContour contour, TriangleMesh2D? triangles, Frame3D? spatial = null, float physicalZ = 0, Color4b? tint = null)
    {
        if (part.RenderMaterial.Paint is not { } paint)
        {
            return;
        }

        Vector3 PlaceSpatial(Vector3 point) => spatial is { } placed
            ? placed.TransformPoint(point + Vector3.UnitZ * physicalZ) : point;
        for (var layer = 0; layer < paint.Count; layer++)
        {
            var patch = paint[layer];
            var color = PuppetDrawing.Tinted(ColorExtensions.FromHexRgb(patch.Material!.Fill!), tint);
            var bias = .00001f * (layer + 1);
            if (triangles is null)
            {
                var bounds = ShapeBounds2D.Calculate((IShape2D)part.Geometry!);
                var points = patch.Points.ConvertAll(p => frame.Frame.TransformPoint(bounds.Center + p * new Vector2(bounds.Width, bounds.Height)));
                var outline = contour.Select(p => p.XY).ToArray();
                var clipped = PolygonClipping2D.ClipConvex(points, outline);
                var points3D = new PartContour([.. clipped.Select(p => PlaceSpatial(new Vector3(p, 0)))], frame.Layer - bias);
                mesh.Polygon(points3D, color, null, 0);
                continue;
            }
            foreach (var (ia, ib, ic) in MeshTopology.Triangles(triangles.Indices))
            {
                var a = triangles.Vertices[ia];
                var b = triangles.Vertices[ib];
                var c = triangles.Vertices[ic];
                var clipped = PolygonClipping2D.ClipConvex(patch.Points, [a, b, c]);
                var ab = b - a;
                var ac = c - a;
                var determinant = ab.Cross(ac);
                (Vector3 Position, float Layer) Place(Vector2 localPoint)
                {
                    var d = localPoint - a;
                    var u = d.Cross(ac) / determinant;
                    var v = ab.Cross(d) / determinant;
                    var point = contour[ia] * (1 - u - v) + contour[ib] * u + contour[ic] * v;
                    if (spatial is { } placed)
                    {
                        point = placed.TransformPoint(point + Vector3.UnitZ * physicalZ);
                    }
                    var layer = contour.LayerAt(ia) * (1 - u - v) + contour.LayerAt(ib) * u + contour.LayerAt(ic) * v - bias;
                    return (point, layer);
                }
                var placedPoints = clipped.Select(Place).ToArray();
                mesh.Polygon(new PartContour([.. placedPoints.Select(p => p.Position)], layers: [.. placedPoints.Select(p => p.Layer)]), color, null, 0);
            }
        }
    }
}
