using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Functions;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

/// <summary>Solid props with flat facet shading and visible silhouette/crease ink. Coplanar triangulation never becomes ink.</summary>
internal static class PropDrawing
{
    private static readonly Vector3 LightDirection = Vector3.Normalize(new(-.3f, .5f, -1));

    public static void Add(CharacterMesh mesh, PropAsset prop, SocketFrame frame)
    {
        if (!Orientation3D.TryGetHandedness(frame.Along3, frame.Across3, frame.Normal3, out var handedness)) return;
        foreach (var solid in prop.Solids)
        {
            var vertices = solid.Vertices.Select(p => ActorPose.PropPoint(frame, prop, p)).ToArray();
            var analysis = new TriangleMeshAnalysis3D(vertices, solid.Triangles, handedness);
            var fill = CharacterJson.Color(solid.Fill); var ink = CharacterJson.Color(prop.Ink);
            for (var i = 0; i < solid.Triangles.Count; i += 3)
            {
                var a = solid.Triangles[i];
                var b = solid.Triangles[i + 1];
                var c = solid.Triangles[i + 2];
                if (!analysis.TryGetFaceNormal(i / 3, out var normal)) continue;
                var light = .72f + .28f * MathF.Max(0, Vector3.Dot(normal, LightDirection));
                mesh.Triangle(vertices[a], vertices[b], vertices[c], new Color((int)(fill.R * light), (int)(fill.G * light), (int)(fill.B * light)));
            }
            if (!solid.Outlined) continue;
            var bias = new Vector3(0, 0, .0005f);
            foreach (var edge in analysis.FeatureEdges(-Vector3.UnitZ, .7f, 1e-5f))
                mesh.Line(vertices[edge.A] - bias, vertices[edge.B] - bias, prop.LineWidth, ink);
        }
    }
}
