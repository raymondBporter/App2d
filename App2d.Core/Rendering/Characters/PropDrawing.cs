using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;
using System.Runtime.InteropServices;

namespace App2d.Core.Rendering.Characters;

/// <summary>Solid props with flat facet shading and visible silhouette/crease ink. Coplanar triangulation never becomes ink.</summary>
internal static class PropDrawing
{
    private static readonly Vector3 LightDirection = Vector3.Normalize(new(-.3f, .5f, -1));

    public static void Add(CharacterMesh mesh, PropAsset prop, Frame3D frame)
    {
        if (!Orientation3D.TryGetHandedness(frame.XAxis, frame.YAxis, frame.ZAxis, out var handedness))
        {
            return;
        }

        foreach (var solid in prop.Solids)
        {
            var vertices = solid.Vertices.Select(p => ActorPose.PropPoint(frame, prop, p)).ToArray();
            var analysis = new TriangleMeshAnalysis3D(vertices, solid.Triangles, handedness);
            var material = solid.RenderMaterial;
            var fill = material.Fill is { } fillHex ? ColorExtensions.FromHexRgb(fillHex) : (Microsoft.Xna.Framework.Color?)null;
            var outline = material.Outline;
            var ink = outline?.Color is { } outlineHex ? ColorExtensions.FromHexRgb(outlineHex) : ColorExtensions.FromHexRgb(prop.Ink);
            var outlineWidth = outline?.Width ?? prop.LineWidth;
            var face = 0;
            foreach (var (a, b, c) in MeshTopology.Triangles(CollectionsMarshal.AsSpan(solid.Triangles)))
            {
                if (!analysis.TryGetFaceNormal(face++, out var normal))
                {
                    continue;
                }

                var light = .72f + .28f * MathF.Max(0, Vector3.Dot(normal, LightDirection));
                if (fill is { } color)
                {
                    mesh.Triangle(vertices[a], vertices[b], vertices[c], color.ScaleRgb(light));
                }
            }
            if (outline is null || outlineWidth <= 0)
            {
                continue;
            }

            var bias = new Vector3(0, 0, .0005f);
            foreach (var edge in analysis.FeatureEdges(-Vector3.UnitZ, .7f, 1e-5f))
            {
                mesh.Line(vertices[edge.A] - bias, vertices[edge.B] - bias, outlineWidth, ink);
            }
        }
    }
}
