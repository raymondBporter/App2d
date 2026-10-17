using App2d.Core.Characters;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Mathematics;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using System.Numerics;

namespace App2d.Core.Characters.Drawing;

/// <summary>Paint lives in rest artwork coordinates and follows the same triangles as fill, including folding membranes.</summary>
internal static class PartPainting
{
    public static void Add(CharacterMesh mesh, PuppetPart part, PartFrame2D frame, IReadOnlyList<Vector3> contour, TriangleMesh2D? triangles)
    {
        if (part.RenderMaterial.Paint is not { } paint)
        {
            return;
        }

        for (var layer = 0; layer < paint.Count; layer++)
        {
            var patch = paint[layer];
            var color = ColorExtensions.FromHexRgb(patch.Material!.Fill!);
            var bias = .00001f * (layer + 1);
            if (triangles is null)
            {
                var points = patch.Points.ConvertAll(p => frame.Coordinates.TransformPoint(p * new Vector2(part.Width, part.Height)));
                var outline = contour.Select(p => p.XY).ToArray();
                var clipped = PolygonClipping2D.ClipConvex(points, outline);
                var z = frame.OriginZ + frame.Depth - bias;
                mesh.Polygon([.. clipped.Select(p => new Vector3(p, z))], color, null, 0);
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
                Vector3 Place(Vector2 point)
                {
                    var d = point - a;
                    var u = d.Cross(ac) / determinant;
                    var v = ab.Cross(d) / determinant;
                    return contour[ia] * (1 - u - v) + contour[ib] * u + contour[ic] * v - new Vector3(0, 0, bias);
                }
                mesh.Polygon([.. clipped.Select(Place)], color, null, 0);
            }
        }
    }
}
