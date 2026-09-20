using System.Numerics;

namespace App2d.Core.Characters;

public sealed record EntityRegion(string Id, IReadOnlyList<Vector2> Points)
{
    public bool Overlaps(EntityRegion other, Vector2 position, Vector2 otherPosition)
    {
        bool Separated(IReadOnlyList<Vector2> axes)
        {
            for (var i = 0; i < axes.Count; i++)
            {
                var edge = axes[(i + 1) % axes.Count] - axes[i]; var axis = new Vector2(-edge.Y, edge.X);
                if (axis.LengthSquared() < 1e-12f) continue;
                var minA = float.PositiveInfinity; var maxA = float.NegativeInfinity; var minB = minA; var maxB = maxA;
                foreach (var p in Points) { var d = Vector2.Dot(p + position, axis); minA = Math.Min(minA, d); maxA = Math.Max(maxA, d); }
                foreach (var p in other.Points) { var d = Vector2.Dot(p + otherPosition, axis); minB = Math.Min(minB, d); maxB = Math.Max(maxB, d); }
                if (maxA < minB || maxB < minA) return true;
            }
            return false;
        }
        return !Separated(Points) && !Separated(other.Points);
    }
    public static EntityRegion Box(string id, Vector2 center, Vector2 size) => new(id, [center + new Vector2(-size.X, -size.Y) / 2, center + new Vector2(size.X, -size.Y) / 2, center + size / 2, center + new Vector2(-size.X, size.Y) / 2]);
}

/// <summary>Graphics-free evaluation shared by the action editor and fixed-step playtest.</summary>
public sealed class EntityPose
{
    private readonly PointLibrary _library;
    private readonly PersonPose? _person;
    private readonly HoundPose? _hound;
    private readonly Vector3[] _raw;
    private readonly List<EntityRegion> _hurt = [];
    public IReadOnlyList<EntityRegion> Hurt => _hurt;
    public EntityRegion Movement { get; private set; } = EntityRegion.Box("movement", new(0, 1), new(.5f, 2));
    public EntityRegion Hit { get; private set; } = EntityRegion.Box("hit", Vector2.Zero, Vector2.One);
    public Vector2 Offset { get; private set; }
    public Vector2 Muzzle { get; private set; }
    public Vector2 Aim { get; private set; } = Vector2.UnitX;
    public CharacterAppearance Look { get; private set; } = new();
    public double ClipTime { get; private set; }
    public EntityPose(PointLibrary library)
    {
        _library = library; _raw = new Vector3[library.PointNames.Count];
        if (library.Anatomy == "person") _person = new(library);
        else if (library.Anatomy == "hound") _hound = new(library);
        else throw new InvalidDataException("Entity pose requires Person or Quadruped.");
    }
    private void Transform(CharacterAppearance look) { if (_person is not null) _person.Transform(_raw, look); else _hound!.Transform(_raw, look); }
    private Vector2 P(int i) { var p = _person!.Point(i); return new(p.X, p.Y); }
    private Vector2 H(string name) { var p = _hound!.Point(name); return new(p.X, p.Y); }
    private Vector2 Root => _person is not null ? (P(1) + P(2)) / 2 : H("Back:head");
    public void Evaluate(EntityTypeDefinition type, EntityAction action, double seconds, bool facingLeft)
    {
        Look = type.Appearance with { Flip = facingLeft };
        if (action.Weapon != "inherit") { Look.Weapons = action.Weapon != "none"; if (Look.Weapons) Look.Weapon = action.Weapon; }
        var clip = _library.Clips[action.Clip];
        clip.Sample(action.ClipStart * clip.Duration, _raw, true); Transform(Look); var start = Root;
        ClipTime = action.ClipTime(clip, seconds); clip.Sample(ClipTime, _raw, true); Transform(Look);
        Offset = new(action.RemoveTravel ? start.X - Root.X : 0, type.GroundOffset);
        var flip = facingLeft ? -1f : 1f; _hurt.Clear();
        Vector2 head, hand; Vector2[] body, legs, arms, headShape;
        if (_person is not null)
        {
            head = P(0); hand = P(16); var radius = _person.Radius(Look);
            var up = SafeDirection(P(17) - head, Vector2.UnitY); var right = new Vector2(up.Y, -up.X) * flip;
            headShape = Look.CustomHead is { } custom ? custom.Contour().Select(p => head + right * p.X * radius / .6f - up * p.Y * radius / .6f).ToArray() : Circle(head, radius);
            body = [P(2), P(1), P(3), P(4)]; legs = [P(5), P(6), P(7), P(8), P(9), P(10)]; arms = [P(11), P(12), P(13), P(14), P(15), P(16)];
            Aim = SafeDirection(hand - P(15), new(flip, 0));
            Muzzle = hand + Aim * (.48f * Look.BladeLength) + Offset;
        }
        else
        {
            head = H("Head:head"); hand = head;
            var rear = H("Back:head") - new Vector2(0, .23f * Look.Size); var front = H("Torso3:head") - new Vector2(0, .20f * Look.Size);
            var center = (rear + front) / 2; var axis = SafeDirection(front - rear, new(flip, 0)); var up = new Vector2(-axis.Y, axis.X);
            var half = Vector2.Distance(front, rear) / 2 + .1f * Look.Size; var full = Look.Body * Look.Size;
            body = Enumerable.Range(0, 12).Select(i => center + axis * MathF.Cos(i / 12f * MathF.Tau) * half + up * MathF.Sin(i / 12f * MathF.Tau) * full).ToArray();
            Aim = SafeDirection(H("Head:tail") - head, new(flip, 0)); var down = new Vector2(Aim.Y, -Aim.X) * flip; var radius = .3f * Look.Head * Look.Size;
            headShape = Look.CustomHead is { } custom ? custom.Contour().Select(p => head + Aim * p.X * radius / .6f + down * p.Y * radius / .6f).ToArray() : Enumerable.Range(0, 16).Select(i => head + Aim * MathF.Cos(i / 16f * MathF.Tau) * radius * Look.HeadWidth + down * MathF.Sin(i / 16f * MathF.Tau) * radius * Look.HeadHeight).ToArray();
            legs = [H("FrontUpperLeg.L:head"), H("FF.L:tail"), H("FrontUpperLeg.R:head"), H("FF.R:tail"), H("BackLeg.L:head"), H("FFB.L:tail"), H("BackLeg.R:head"), H("FFB.R:tail")]; arms = [];
            Muzzle = head + Aim * radius + Offset;
        }
        foreach (var (id, settings) in type.Regions)
        {
            if (settings.Mode == "disabled") continue;
            var source = id switch { "body" => body, "head" => headShape, "legs" => legs, "arms" => arms, _ => [] };
            if (source.Length == 0) continue;
            var center = source.Aggregate(Vector2.Zero, (sum, p) => sum + p) / source.Length;
            var regionOffset = new Vector2(settings.OffsetX * flip, settings.OffsetY) + Offset;
            if (settings.Mode == "custom") _hurt.Add(EntityRegion.Box(id, center + regionOffset, new(settings.Width, settings.Height)));
            else
            {
                var hull = Hull(source);
                var expanded = hull.Select(p => { var d = p - center; return center + new Vector2(d.X * settings.ScaleX, d.Y * settings.ScaleY) + SafeDirection(d, Vector2.UnitY) * settings.Padding + regionOffset; }).ToArray();
                _hurt.Add(new(id, expanded));
            }
        }
        Movement = EntityRegion.Box("movement", new(type.Movement.OffsetX * flip, type.Movement.Height / 2), new(type.Movement.Width, type.Movement.Height));
        var anchor = action.Attachment switch { "head" => head + Offset, "hand" => hand + Offset, "muzzle" => Muzzle, _ => Vector2.Zero };
        Hit = EntityRegion.Box("hit", anchor + new Vector2(action.HitX * flip, action.HitY), new(action.HitWidth, action.HitHeight));
    }
    private static Vector2 SafeDirection(Vector2 p, Vector2 fallback) => p.LengthSquared() < 1e-10f ? fallback : Vector2.Normalize(p);
    private static Vector2[] Circle(Vector2 center, float radius) => Enumerable.Range(0, 16).Select(i => center + new Vector2(MathF.Cos(i / 16f * MathF.Tau), MathF.Sin(i / 16f * MathF.Tau)) * radius).ToArray();
    private static Vector2[] Hull(Vector2[] points)
    {
        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var sorted = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToArray(); if (sorted.Length <= 2) return EntityRegion.Box("", sorted[0], new(.1f)).Points.ToArray();
        var hull = new List<Vector2>();
        foreach (var p in sorted) { while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        var count = hull.Count;
        for (var i = sorted.Length - 2; i >= 0; i--) { var p = sorted[i]; while (hull.Count > count && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        hull.RemoveAt(hull.Count - 1); return hull.ToArray();
    }
}
