using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Rendering.Characters;

/// <summary>Plain primitives over an evaluated pose; independent of source clips and anatomy names.</summary>
public sealed class PuppetDrawing
{
    public CharacterMesh Mesh { get; } = new();
    public void Build(PuppetDefinition definition, PuppetPose pose)
    {
        Mesh.Clear(); var ink = CharacterJson.Color(definition.Ink);
        foreach (var part in definition.Parts)
        {
            var a = pose.World(part.A); var b = part.B is { } end ? pose.World(end) : a + Vector3.UnitY;
            if (part.Kind == "stroke")
            {
                var depth = new Vector3(0, 0, part.Depth); Mesh.Line(a + depth, b + depth, part.Width, ink); continue;
            }
            var direction = new Vector2(b.X - a.X, b.Y - a.Y);
            var up = direction.LengthSquared() > 1e-10f ? Vector2.Normalize(direction) : Vector2.UnitY;
            var right = new Vector2(up.Y, -up.X);
            Vector3 At(Vector2 p) => a + new Vector3(right * (p.X + part.OffsetX) + up * (p.Y + part.OffsetY), part.Depth);
            var contour = new List<Vector3>();
            if (part.Kind == "ellipse")
                for (var i = 0; i < 48; i++) contour.Add(At(new(MathF.Cos(i * MathF.Tau / 48) * part.Width / 2, MathF.Sin(i * MathF.Tau / 48) * part.Height / 2)));
            else
            {
                var radius = Math.Min(part.Width, part.Height) * .5f * part.Roundness;
                for (var corner = 0; corner < 4; corner++)
                {
                    var angle = corner * MathF.PI / 2;
                    var center = new Vector2((corner is 0 or 3 ? 1 : -1) * (part.Width / 2 - radius), (corner < 2 ? 1 : -1) * (part.Height / 2 - radius));
                    for (var i = 0; i <= 8; i++) contour.Add(At(center + new Vector2(MathF.Cos(angle + i * MathF.PI / 16), MathF.Sin(angle + i * MathF.PI / 16)) * radius));
                }
            }
            Mesh.Polygon(contour, CharacterJson.Color(part.Fill), ink, definition.LineWidth);
            if (part.Face != "none") FaceDrawing.Build(Mesh, FaceExpressions.Get(part.Face),
                p => At(new(p.X * part.Width + part.FaceX, -p.Y * part.Height)) - new Vector3(0, 0, .002f), definition.LineWidth * .6f, ink);
        }
    }
}
