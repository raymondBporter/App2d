using App2d.Core.Characters;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Polygons;
using System.Numerics;

namespace App2d.Core.Rendering.Characters;

/// <summary>Paint lives in rest artwork coordinates and follows the same triangles as fill, including folding membranes.</summary>
internal static class PartPainting
{
    public static void Add(CharacterMesh mesh, PuppetPart part, PartFrame2D frame, IReadOnlyList<Vector3> contour, TriangleMesh2D? triangles)
    {
        if (part.Paint is null) return;
        for (var layer = 0; layer < part.Paint.Count; layer++)
        {
            var patch = part.Paint[layer]; var color = ColorExtensions.FromHexRgb(patch.Material!.Fill!);
            var bias = new Vector3(0, 0, .00001f * (layer + 1));
            if (triangles is null)
            {
                var points = patch.Points.ConvertAll(p => frame.At(new(p.X * part.Width, p.Y * part.Height)));
                var clipped = PolygonClipping2D.ClipConvexXY(points, contour);
                mesh.Polygon([.. clipped.Select(p => p - bias)], color, null, 0); continue;
            }
            var paint = patch.Points.Select(p => p.XYZ).ToArray();
            for (var i = 0; i < triangles.Indices.Length; i += 3)
            {
                var ia = triangles.Indices[i]; var ib = triangles.Indices[i + 1]; var ic = triangles.Indices[i + 2];
                var a = PartContours2D.CutoutPoints(part)[ia].XY; var b = PartContours2D.CutoutPoints(part)[ib].XY; var c = PartContours2D.CutoutPoints(part)[ic].XY;
                var clipped = PolygonClipping2D.ClipConvexXY(paint, new[] { new Vector3(a, 0), new Vector3(b, 0), new Vector3(c, 0) });
                var ab = b - a; var ac = c - a; var determinant = ab.X * ac.Y - ab.Y * ac.X;
                Vector3 Place(Vector3 point)
                {
                    var d = new Vector2(point.X, point.Y) - a;
                    var u = (d.X * ac.Y - d.Y * ac.X) / determinant; var v = (ab.X * d.Y - ab.Y * d.X) / determinant;
                    return contour[ia] * (1 - u - v) + contour[ib] * u + contour[ic] * v - bias;
                }
                mesh.Polygon([.. clipped.Select(Place)], color, null, 0);
            }
        }
    }
}
