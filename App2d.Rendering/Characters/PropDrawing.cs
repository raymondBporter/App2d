using App2d.Core.Characters;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

/// <summary>Solid props with flat facet shading and visible silhouette/crease ink. Coplanar triangulation never becomes ink.</summary>
internal static class PropDrawing
{
    public static void Add(CharacterMesh mesh, PropAsset prop, SocketFrame frame)
    {
        foreach (var solid in prop.Solids)
        {
            var vertices = solid.Vertices.Select(p => ActorPose.PropPoint(frame, prop, p)).ToArray();
            var normals = new List<Vector3>();
            var edges = new Dictionary<(int A, int B), List<int>>();
            var fill = CharacterJson.Color(solid.Fill); var ink = CharacterJson.Color(prop.Ink);
            // Reflection reverses triangle winding but not the physical outward normal.
            var handedness = MathF.Sign(Vector3.Dot(Vector3.Cross(frame.Along3, frame.Across3), frame.Normal3));
            for (var i = 0; i < solid.Triangles.Count; i += 3)
            {
                var a = solid.Triangles[i]; var b = solid.Triangles[i + 1]; var c = solid.Triangles[i + 2];
                var normal = Vector3.Normalize(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a])) * handedness;
                var face = normals.Count; normals.Add(normal);
                var light = .72f + .28f * MathF.Max(0, Vector3.Dot(normal, Vector3.Normalize(new(-.3f, .5f, -1))));
                mesh.Triangle(vertices[a], vertices[b], vertices[c], new Color((int)(fill.R * light), (int)(fill.G * light), (int)(fill.B * light)));
                foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
                {
                    var edge = u < v ? (u, v) : (v, u);
                    if (!edges.TryGetValue(edge, out var faces)) edges[edge] = faces = [];
                    faces.Add(face);
                }
            }
            if (!solid.Outlined) continue;
            foreach (var (edge, faces) in edges)
            {
                var visible = faces.Any(f => normals[f].Z < -1e-5f);
                var silhouette = visible && faces.Any(f => normals[f].Z >= -1e-5f);
                var crease = faces.Count == 2 && Vector3.Dot(normals[faces[0]], normals[faces[1]]) < .7f;
                if (!visible || !(faces.Count == 1 || silhouette || crease)) continue;
                var bias = new Vector3(0, 0, .0005f);
                mesh.Line(vertices[edge.A] - bias, vertices[edge.B] - bias, prop.LineWidth, ink);
            }
        }
    }
}
