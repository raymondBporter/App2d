using App2d.Core.Mathematics;
using Color = Microsoft.Xna.Framework.Color;
using Quaternion = System.Numerics.Quaternion;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace App2d.Core.Rendering.Characters;

/// <summary>
/// How a swoosh looks. Lengths along the trail are fractions of its length, measured from the old end; positions across
/// the sheet are fractions from the tip's edge (0) to the inner edge (1). Combo swings share the construction and differ
/// only in these numbers, so a follow-up reads as the same attack family with a different mark.
/// </summary>
public sealed record SwooshStyle
{
    /// <summary>How far back in time the trail reaches while the blade is swinging.</summary>
    public float HistorySeconds { get; init; } = .125f;
    /// <summary>After the swing, how long the old end takes to catch up with the new end.</summary>
    public float ClearSeconds { get; init; } = .075f;
    /// <summary>How much of the blade, from the tip toward the guard, the fresh edge covers.</summary>
    public float Coverage { get; init; } = .95f;
    /// <summary>Width profile exponent: higher narrows the old end for longer.</summary>
    public float Taper { get; init; } = 1.2f;
    /// <summary>How far along the trail the split runs before closing.</summary>
    public float SplitDepth { get; init; } = .27f;
    /// <summary>The split's gap at its open end, as a fraction of the sheet's width.</summary>
    public float SplitWidth { get; init; } = .04f;
    /// <summary>Where the split sits across the sheet.</summary>
    public float SplitPosition { get; init; } = .55f;
    /// <summary>Where the inner tongue starts, as a fraction of the split's depth; it tapers to a point there.</summary>
    public float InnerStart { get; init; } = .45f;
    public string Fill { get; init; } = "#ffffff";
    /// <summary>Outline along the tip's path, the reach cue; zero width draws none.</summary>
    public string Ink { get; init; } = "#222b32";
    public float InkWidth { get; init; } = .018f;
    /// <summary>Longest step between sections along the tip path, in model units.</summary>
    public float Spacing { get; init; } = .035f;

    /// <summary>The opening swing: the settings chosen in the sprite-renderer motion study.</summary>
    public static readonly SwooshStyle Primary = new();
    /// <summary>The combo's second swing: same family, the split nearer the tip and running deeper.</summary>
    public static readonly SwooshStyle FollowUp = new() { SplitPosition = .38f, SplitDepth = .42f, SplitWidth = .06f, InnerStart = .3f };
}

/// <summary>
/// A live blade trail. Feed it the blade each frame; it keeps a short history and builds a filled sheet from the newest
/// blade back along the path, pointed at the old end and attached across the blade at the new end. Nothing is baked:
/// whatever moves the blade (a clip, IK, a layered overlay) shapes the trail. Frames can be far apart during a fast swing,
/// so each step between two recorded blades is rebuilt as the rigid rotation that carries one onto the other, which puts
/// the tip back on its arc instead of cutting a chord. A swing in the screen plane turns about a pivot there; one that
/// swings through depth (a side swing, the blade passing toward or away from the camera) turns about a 3D axis, so its
/// trail narrows where the blade points at the camera instead of looping across the screen.
/// </summary>
public sealed class BladeSwoosh
{
    /// <summary>
    /// A recorded blade and the turn that carried the previous one onto it: <paramref name="Turn"/> radians about
    /// <paramref name="Axis"/>. A step in the screen plane has the Z axis and a signed turn; a step through depth has its
    /// own axis and a positive turn.
    /// </summary>
    private readonly record struct Blade(float Time, Vector3 Guard, Vector3 Tip, float Turn, Vector3 Axis)
    {
        public bool Planar => Axis == Vector3.UnitZ;
    }
    private readonly record struct Section(float Time, Vector3 Guard, Vector3 Tip);

    private readonly List<Blade> _history = [];
    private readonly List<Section> _sections = [];
    private readonly List<float> _distance = [];
    private readonly List<Vector3> _edge = [];
    private bool _swinging;
    private float _start, _end, _now;

    public SwooshStyle Style { get; set; } = SwooshStyle.Primary;
    /// <summary>
    /// Which way the swing turns on screen: -1 clockwise, +1 counter-clockwise, 0 to infer. A swing that covers more than a
    /// quarter turn in its first recorded step has no earlier step to follow, so without this it takes the shorter way
    /// round, which can be underneath instead of over the top. Mirror it with the actor's facing.
    /// </summary>
    public int Sweep { get; set; }
    public bool IsVisible => _history.Count > 1 && (_swinging || _now < _end + Style.ClearSeconds);

    public void Reset() { _history.Clear(); _swinging = false; _now = _end = _start = 0; }

    /// <summary>Records the blade at <paramref name="time"/>. A new swing (<paramref name="swinging"/> turning on) starts a fresh trail.</summary>
    public void Record(float time, Vector3 guard, Vector3 tip, bool swinging)
    {
        _now = time;
        if (swinging)
        {
            if (!_swinging) { _history.Clear(); _start = time; _swinging = true; }
            if (_history.Count > 0 && time <= _history[^1].Time) _history.RemoveAt(_history.Count - 1);
            var (turn, axis) = _history.Count > 0 ? Turn(_history[^1], guard, tip, Sweep) : (0, Vector3.UnitZ);
            _history.Add(new(time, guard, tip, turn, axis));
            var oldest = time - Style.HistorySeconds;
            while (_history.Count > 2 && _history[1].Time <= oldest) _history.RemoveAt(0);
        }
        else if (_swinging) { _swinging = false; _end = _history.Count > 0 ? _history[^1].Time : time; }
    }

    /// <summary>Appends the trail's triangles to <paramref name="mesh"/>. Draw it before the character so the blade and body sit on top.</summary>
    public void Build(CharacterMesh mesh)
    {
        if (!IsVisible) return;
        var style = Style;
        var head = _swinging ? _history[^1].Time : _end;
        var reach = MathF.Max(_start, head - style.HistorySeconds);
        var clear = _swinging ? 0 : Math.Clamp((_now - _end) / style.ClearSeconds, 0, 1);
        var tail = reach + (head - reach) * clear;
        if (head - tail < 1e-4f) return;

        Sections(tail, head);
        if (_sections.Count < 2) return;
        _distance.Clear(); _distance.Add(0);
        for (var i = 1; i < _sections.Count; i++) _distance.Add(_distance[^1] + Vector3.Distance(_sections[i - 1].Tip, _sections[i].Tip));
        var length = _distance[^1];
        if (length < 1e-3f) return;

        var alpha = 1 - Interpolation.SmoothStep(Interpolation.InverseLerpClamped(.55f, 1f, clear));
        var fill = ColorExtensions.FromHexRgb(style.Fill).WithAlpha(alpha);
        float Split(float u) => style.SplitPosition - Gap(style, u) / 2;
        float InnerEdge(float u) => style.SplitPosition + Gap(style, u) / 2;
        for (var i = 1; i < _sections.Count; i++)
        {
            var u0 = _distance[i - 1] / length; var u1 = _distance[i] / length;
            // Main band: the whole sheet past the split, the outer tongue inside it.
            Band(mesh, i, u0, u1, _ => 0, u => u >= style.SplitDepth ? 1 : Split(u), fill);
            // Inner tongue: starts at a point part-way along the split and widens until it closes with the outer tongue.
            if (u0 < style.SplitDepth)
                Band(mesh, i, u0, MathF.Min(u1, style.SplitDepth), InnerEdge, u => InnerEdge(u) + (1 - InnerEdge(u)) * Inner(style, u), fill);
        }
        if (style.InkWidth > 0)
        {
            _edge.Clear(); foreach (var s in _sections) _edge.Add(s.Tip - new Vector3(0, 0, .0005f));
            mesh.Path(_edge, ColorExtensions.FromHexRgb(style.Ink).WithAlpha(alpha), 0, style.InkWidth);
        }
    }

    /// <summary>
    /// One strip of the sheet from section i-1 (trail fraction <paramref name="u0"/>) to trail fraction <paramref name="u1"/>,
    /// between the fractions across the sheet that <paramref name="near"/> and <paramref name="far"/> give at each point.
    /// </summary>
    private void Band(CharacterMesh mesh, int i, float u0, float u1, Func<float, float> near, Func<float, float> far, Color color)
    {
        if (u1 <= u0) return;
        var a = _sections[i - 1]; var b = _sections[i];
        var ua = _distance[i - 1] / _distance[^1]; var ub = _distance[i] / _distance[^1];
        // A band can end mid-step (the inner tongue closing): stand in a section interpolated to its end.
        var end = u1 < ub - 1e-6f ? Lerp(a, b, (u1 - ua) / MathF.Max(1e-6f, ub - ua)) : b;
        (Vector3 Near, Vector3 Far) Edge(Section s, float u)
        {
            var width = Style.Coverage * MathF.Pow(MathF.Sin(Math.Clamp(u, 0, 1) * MathF.PI / 2), Style.Taper);
            var inner = s.Tip + (s.Guard - s.Tip) * width;
            return (Vector3.Lerp(s.Tip, inner, near(u)), Vector3.Lerp(s.Tip, inner, far(u)));
        }
        var (n0, f0) = Edge(a, u0); var (n1, f1) = Edge(end, u1);
        mesh.Triangle(n0, f0, n1, color); mesh.Triangle(f0, f1, n1, color);
    }

    private static float Gap(SwooshStyle style, float u) => u >= style.SplitDepth ? 0 : style.SplitWidth * (1 - u / style.SplitDepth);
    private static float Inner(SwooshStyle style, float u)
    {
        var start = style.SplitDepth * style.InnerStart;
        return u <= start ? 0 : MathF.Pow(Math.Clamp((u - start) / (style.SplitDepth - start), 0, 1), .7f);
    }

    /// <summary>Dense sections from <paramref name="tail"/> to <paramref name="head"/>, each step a rigid rotation between recorded blades.</summary>
    private void Sections(float tail, float head)
    {
        _sections.Clear();
        for (var i = 1; i < _history.Count; i++)
        {
            var a = _history[i - 1]; var b = _history[i];
            if (b.Time <= tail || a.Time >= head) continue;
            var steps = Math.Clamp((int)MathF.Ceiling(ArcLength(a, b) / Style.Spacing), 1, 64);
            for (var k = 0; k <= steps; k++)
            {
                var time = a.Time + (b.Time - a.Time) * k / steps;
                if (time < tail - 1e-6f || time > head + 1e-6f) continue;
                if (_sections.Count > 0 && time <= _sections[^1].Time + 1e-6f) continue;
                _sections.Add(Rotate(a, b, (time - a.Time) / (b.Time - a.Time)));
            }
        }
        // Pin the ends exactly on the tail and head times.
        if (_history.Count > 1)
        {
            var first = Find(tail); if (first is { } f && (_sections.Count == 0 || _sections[0].Time > tail + 1e-6f)) _sections.Insert(0, f);
        }
    }

    private Section? Find(float time)
    {
        for (var i = 1; i < _history.Count; i++)
            if (_history[i].Time >= time) return Rotate(_history[i - 1], _history[i], (time - _history[i - 1].Time) / MathF.Max(1e-6f, _history[i].Time - _history[i - 1].Time));
        return null;
    }

    private static float ArcLength(Blade a, Blade b)
    {
        var turn = b.Turn;
        var chord = Vector3.Distance(a.Tip, b.Tip); var half = MathF.Abs(turn) / 2;
        return half < 5e-4f ? chord : chord * half / MathF.Sin(half);
    }

    /// <summary>
    /// The turn from <paramref name="previous"/> to a new blade. In the screen plane it is signed about Z, and the shorter
    /// way round is right unless the step goes against the swing: past 150 degrees against the way the blade was already
    /// turning (a fast swing can pass that in one frame), or, on the first step, past a quarter turn against
    /// <paramref name="sweep"/>. Through depth it turns about the axis the two blades share, with the same 150 degree rule
    /// against the previous step's axis.
    /// </summary>
    private static (float Turn, Vector3 Axis) Turn(Blade previous, Vector3 guard, Vector3 tip, int sweep)
    {
        var a = previous.Tip - previous.Guard; var b = tip - guard;
        if (a.LengthSquared() < 1e-12f || b.LengthSquared() < 1e-12f) return (0, Vector3.UnitZ);
        var cross = Vector3.Cross(Vector3.Normalize(a), Vector3.Normalize(b));
        var planar = cross.LengthSquared() < 1e-10f || MathF.Abs(cross.Z) > .97f * cross.Length();
        if (planar)
        {
            var d0 = a.XY; var d1 = b.XY;
            var turn = MathF.Atan2(d0.X * d1.Y - d0.Y * d1.X, Vector2.Dot(d0, d1));
            var (expected, limit) = previous.Turn != 0 && previous.Planar ? (MathF.Sign(previous.Turn), 2.6f) : (Math.Sign(sweep), MathF.PI / 2);
            if (expected != 0 && MathF.Sign(turn) != expected && MathF.Abs(turn) > limit) turn -= MathF.Sign(turn) * MathF.Tau;
            return (turn, Vector3.UnitZ);
        }
        var axis = Vector3.Normalize(cross);
        var angle = MathF.Atan2(cross.Length(), Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)));
        if (previous.Turn != 0 && !previous.Planar && Vector3.Dot(axis, previous.Axis) < 0 && angle > 2.6f) (axis, angle) = (-axis, MathF.Tau - angle);
        return (angle, axis);
    }

    /// <summary>The blade a fraction <paramref name="u"/> of the way from <paramref name="a"/> to <paramref name="b"/>, turning about their shared pivot or axis.</summary>
    private static Section Rotate(Blade a, Blade b, float u)
    {
        u = Math.Clamp(u, 0, 1);
        var time = a.Time + (b.Time - a.Time) * u;
        if (!b.Planar)
        {
            // Through depth: the guard moves straight between the two, the blade turns about the step's axis.
            var along = a.Tip - a.Guard; var length = float.Lerp(along.Length(), (b.Tip - b.Guard).Length(), u);
            var direction = Vector3.Transform(Vector3.Normalize(along), Quaternion.CreateFromAxisAngle(b.Axis, b.Turn * u));
            var at = Vector3.Lerp(a.Guard, b.Guard, u);
            return new(time, at, at + direction * length);
        }
        var z = float.Lerp(a.Guard.Z, b.Guard.Z, u); var tipZ = float.Lerp(a.Tip.Z, b.Tip.Z, u);
        var d0 = (a.Tip - a.Guard).XY; var d1 = (b.Tip - b.Guard).XY;
        var flatLength = float.Lerp(d0.Length(), d1.Length(), u);
        var turn = b.Turn;
        var guard = Rotation2D.InterpolateArc(a.Guard.XY, b.Guard.XY, turn, u);
        var flat = Rotation2D.Apply(d0.Length() > 1e-6f ? Vector2.Normalize(d0) : Vector2.UnitX, turn * u);
        return new(time, new(guard, z), new(guard + flat * flatLength, tipZ));
    }

    private static Section Lerp(Section a, Section b, float u) =>
        new(float.Lerp(a.Time, b.Time, u), Vector3.Lerp(a.Guard, b.Guard, u), Vector3.Lerp(a.Tip, b.Tip, u));
}
