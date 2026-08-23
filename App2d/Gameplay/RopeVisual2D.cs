using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Rendering;

namespace App2d.Gameplay;

// Purely cosmetic rope made of stretched capsule links laid along a sagging curve.
// It never touches the physics world, so it can never yank anything.
public sealed class RopeVisual2D
{
    private const float MaxSagDepth = 110f;

    private readonly WorldObject2D[] _links;
    private readonly float _linkBaseLength;

    public RopeVisual2D(
        Scene2D scene,
        IShader2D shader,
        int linkCount,
        float thickness,
        float linkBaseLength = 30f)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentOutOfRangeException.ThrowIfLessThan(linkCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(linkBaseLength, 0f);
        if (!float.IsFinite(linkBaseLength))
            throw new ArgumentOutOfRangeException(nameof(linkBaseLength));

        _linkBaseLength = linkBaseLength;
        _links = new WorldObject2D[linkCount];
        for (var i = 0; i < linkCount; i++)
        {
            var link = new WorldObject2D(
                new Capsule2D(Vector2.Zero, new Vector2(linkBaseLength, 0f), thickness),
                shader)
            {
                IsVisible = false
            };
            _links[i] = link;
            scene.Add(link);
        }
    }

    public void Show()
    {
        foreach (var link in _links)
            link.IsVisible = true;
    }

    public void Hide()
    {
        foreach (var link in _links)
            link.IsVisible = false;
    }

    // Slack is how much unused rope length there is; more slack draws a deeper sag.
    public void Update(Vector2 start, Vector2 end, float slack)
    {
        var sagDepth = Math.Clamp(slack * 0.5f, 0f, MaxSagDepth);
        var control = (start + end) / 2f - new Vector2(0f, sagDepth);

        var previous = start;
        for (var i = 0; i < _links.Length; i++)
        {
            var t = (i + 1f) / _links.Length;
            var point = QuadraticBezier(start, control, end, t);
            PlaceLink(_links[i], previous, point);
            previous = point;
        }
    }

    private void PlaceLink(WorldObject2D link, Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var length = segment.Length();
        link.Transform.Position = from;
        link.Transform.Rotation = length > float.Epsilon
            ? MathF.Atan2(segment.Y, segment.X)
            : 0f;
        link.Transform.Scale = new Vector2(
            Math.Max(length / _linkBaseLength, 0.001f),
            1f);
    }

    private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        var inverse = 1f - t;
        return start * (inverse * inverse) + control * (2f * inverse * t) + end * (t * t);
    }
}
