using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.IO;
using App2d.Core.Json;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters;

/// <summary>A convex patch of color in normalized part coordinates; clipped to the part's actual silhouette.</summary>
public sealed record PartPaint
{
    public RenderMaterialDefinition2D? Material { get; set; }
    public List<Vector2> Points { get; set; } = [];

    public static void Check(IReadOnlyList<PartPaint>? paint)
    {
        if (paint is null)
        {
            return;
        }

        if (paint.Count > 64)
        {
            throw new InvalidDataException("A part supports at most 64 paint patches.");
        }

        Span<Vector2> xy = stackalloc Vector2[64];
        foreach (var patch in paint)
        {
            if (patch?.Points is not { Count: >= 3 and <= 64 })
            {
                throw new InvalidDataException("Paint needs 3 to 64 polygon points.");
            }

            if (patch.Material?.Fill is null)
            {
                throw new InvalidDataException("Paint needs a material fill.");
            }

            if (patch.Material.Asset is not null || patch.Material.Paint is not null)
            {
                throw new InvalidDataException("A paint layer needs a plain material fill.");
            }
            patch.Material.Validate("paint.material");
            if (patch.Material.Outline is not null)
            {
                throw new InvalidDataException("Paint patch outlines are not supported.");
            }

            var vertices = xy[..patch.Points.Count];
            for (var i = 0; i < patch.Points.Count; i++)
            {
                var point = patch.Points[i];
                new Limit(-10000, 10000).Check(point.X, "paint.point.x");
                new Limit(-10000, 10000).Check(point.Y, "paint.point.y");
                vertices[i] = point;
            }
            if (!PolygonGeometry2D.IsConvexPerimeter(vertices, 1e-8d))
            {
                throw new InvalidDataException("Paint patches must be convex and have area.");
            }
        }
    }
}
