using App2d.Contracts.Combat;
using App2d.Core.Mathematics;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Presentation.World.Presentation;

/// <summary>Short, directional ink marks from confirmed sword damage. No simulation clocks or actor poses are changed.</summary>
public sealed class CombatContactPresentation2D(Scene2D scene) : IDisposable
{
    private readonly List<Burst> _bursts = [];
    private const int MaximumBursts = 32;
    private static readonly float[] Angles = [-1.05f, -.45f, .12f, .72f, 2.7f];
    private static readonly float[] Lengths = [10, 17, 20, 12, 9];
    private static readonly ConvexPolygon2D Shard = new([new(-.5f, 0), new(0, -.5f), new(.5f, 0), new(0, .5f)]);

    public bool Enabled { get; set; } = true;

    /// <summary>The caller delivers each session event once; a miss never creates an effect.</summary>
    public void Present(CombatDamage2D damage)
    {
        if (damage.Contact is not { Kind: CombatImpactKind2D.Sword } contact) return;
        var burst = _bursts.FirstOrDefault(b => b.Age >= b.Duration);
        if (burst is null)
        {
            if (_bursts.Count == MaximumBursts)
            {
                burst = _bursts.MaxBy(b => b.Age / b.Duration)!;
            }
            else
            {
                burst = new Burst(scene);
                _bursts.Add(burst);
            }
        }
        burst.Position = contact.Position;
        burst.Angle = contact.Direction.AngleRadians;
        burst.Size = damage.WasKilled ? 1.3f : 1;
        burst.Duration = damage.WasKilled ? .16f : .115f;
        burst.Age = 0;
        Draw(burst);
    }

    public void Advance(float dt)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(dt);
        foreach (var burst in _bursts) { burst.Age += dt; Draw(burst); }
    }

    private void Draw(Burst burst)
    {
        var live = Enabled && burst.Age < burst.Duration;
        var t = Math.Clamp(burst.Age / burst.Duration, 0, 1);
        for (var i = 0; i < burst.Pieces.Length; i++)
        {
            var (edge, fill) = burst.Pieces[i];
            edge.IsVisible = fill.IsVisible = live;
            if (!live) continue;
            var angle = burst.Angle + Angles[i];
            var axis = Polar2D.Direction(angle);
            var distance = (3 + 17 * t) * burst.Size;
            var length = Lengths[i] * burst.Size * (1 - .65f * t);
            var width = (i == 2 ? 5 : 3.6f) * burst.Size * (1 - .75f * t);
            edge.Transform.Position = fill.Transform.Position = burst.Position + axis * distance;
            edge.Transform.Rotation = fill.Transform.Rotation = angle;
            edge.Transform.Scale = new(length + 2, width + 2);
            fill.Transform.Scale = new(length, width);
            var alpha = (byte)(255 * (1 - MathF.Pow(t, 3)));
            ((Ink)edge.Shader).BaseColor = new Color(24, 27, 32, (int)alpha);
            ((Ink)fill.Shader).BaseColor = new Color(255, 250, 222, (int)alpha);
        }
    }

    public void Reset()
    {
        foreach (var burst in _bursts) { burst.Age = burst.Duration; Draw(burst); }
    }

    public void Dispose()
    {
        foreach (var burst in _bursts)
            foreach (var (edge, fill) in burst.Pieces) { scene.Remove(edge); scene.Remove(fill); }
        _bursts.Clear();
    }

    private sealed class Ink : IShader2D { public Color BaseColor { get; set; } }

    private sealed class Burst
    {
        public readonly (WorldObject2D Edge, WorldObject2D Fill)[] Pieces;
        public Vector2 Position;
        public float Age, Duration, Angle, Size;
        public Burst(Scene2D scene)
        {
            Pieces = new (WorldObject2D, WorldObject2D)[Angles.Length];
            for (var i = 0; i < Pieces.Length; i++)
            {
                var edge = new WorldObject2D(Shard, new Ink()) { ZIndex = 5, IsVisible = false };
                var fill = new WorldObject2D(Shard, new Ink()) { ZIndex = 6, IsVisible = false };
                scene.Add(edge); scene.Add(fill); Pieces[i] = (edge, fill);
            }
        }
    }
}
