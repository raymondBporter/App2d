using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>
/// Where a drawing part lies, from a world-position lookup. Drawing, picking and outlines share this one layout, so what the
/// author clicks is exactly what is drawn. Shapes attach to A; B, when set, points their local +Y axis.
/// </summary>
public static class PartGeometry
{
    public readonly record struct Frame(Vector3 Origin, Vector2 Right, Vector2 Up, float Depth)
    {
        public Vector3 At(Vector2 local) => Origin + new Vector3(Right * local.X + Up * local.Y, Depth);
    }

    public static Frame FrameOf(PuppetPart part, Func<string, Vector3> world)
    {
        var a = world(part.A); var b = part.B is { } end ? world(end) : a + Vector3.UnitY;
        var direction = new Vector2(b.X - a.X, b.Y - a.Y);
        var up = direction.LengthSquared() > 1e-10f ? Vector2.Normalize(direction) : Vector2.UnitY;
        return new(a + new Vector3(new Vector2(up.Y, -up.X) * part.OffsetX + up * part.OffsetY, 0), new(up.Y, -up.X), up, part.Depth);
    }

    /// <summary>The closed outline of an ellipse or rounded box, or a stroke's two endpoints.</summary>
    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world)
    {
        if (part.Kind == "stroke")
        {
            var depth = new Vector3(0, 0, part.Depth);
            return [world(part.A) + depth, world(part.B!) + depth];
        }
        var frame = FrameOf(part, world); var contour = new List<Vector3>();
        if (part.Kind == "ellipse")
        {
            for (var i = 0; i < 48; i++) contour.Add(frame.At(new(MathF.Cos(i * MathF.Tau / 48) * part.Width / 2, MathF.Sin(i * MathF.Tau / 48) * part.Height / 2)));
            return contour;
        }
        var radius = Math.Min(part.Width, part.Height) * .5f * part.Roundness;
        for (var corner = 0; corner < 4; corner++)
        {
            var angle = corner * MathF.PI / 2;
            var center = new Vector2((corner is 0 or 3 ? 1 : -1) * (part.Width / 2 - radius), (corner < 2 ? 1 : -1) * (part.Height / 2 - radius));
            for (var i = 0; i <= 8; i++) contour.Add(frame.At(center + new Vector2(MathF.Cos(angle + i * MathF.PI / 16), MathF.Sin(angle + i * MathF.PI / 16)) * radius));
        }
        return contour;
    }

    /// <summary>How near an XY point is to a part: 0 at its centre line, 1 at its edge, above 1 outside.</summary>
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point)
    {
        var p = new Vector2(point.X, point.Y);
        if (part.Kind == "stroke")
        {
            var a = world(part.A); var b = world(part.B!); var ab = new Vector2(b.X - a.X, b.Y - a.Y); var ap = p - new Vector2(a.X, a.Y);
            var t = ab.LengthSquared() < 1e-10f ? 0 : Math.Clamp(Vector2.Dot(ap, ab) / ab.LengthSquared(), 0, 1);
            return Vector2.Distance(ap, ab * t) / MathF.Max(part.Width, .06f);
        }
        var frame = FrameOf(part, world); var local = p - new Vector2(frame.Origin.X, frame.Origin.Y);
        var x = Vector2.Dot(local, frame.Right) / (part.Width / 2); var y = Vector2.Dot(local, frame.Up) / (part.Height / 2);
        return part.Kind == "ellipse" ? MathF.Sqrt(x * x + y * y) : MathF.Max(MathF.Abs(x), MathF.Abs(y));
    }
}
