using App2d.Core;
using App2d.Rendering.Vegetation;
using System.Numerics;

namespace App2d.Game.Presentation.World.Presentation;

/// <summary>Short-lived visual motion only; no rigid bodies or gameplay collisions.</summary>
internal sealed class GrassClipping2D(VegetationBladeTip2D shape, Vector2 velocity, float spin, float phase, float scale)
{
    public const float LifetimeSeconds = 1f;
    private const float FadeSeconds = 0.22f;
    public VegetationBladeTip2D Shape { get; } = shape;
    public Vector2 Position { get; private set; } = shape.ReleasePosition;
    public Vector2 Velocity { get; private set; } = velocity;
    public float Rotation { get; private set; }
    public float Age { get; private set; }
    public bool IsExpired => Age >= LifetimeSeconds;
    public float Opacity => Math.Clamp((LifetimeSeconds - Age) / FadeSeconds, 0f, 1f);

    public void Advance(float dt, VegetationWind2D wind)
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(dt);
        var targetAge = Math.Min(LifetimeSeconds, Age + dt);
        var remaining = targetAge - Age;
        var startTime = wind.TotalSeconds - dt;
        var elapsed = 0f;
        // Small steps keep the toss consistent at high and low frame rates.
        while (remaining > 0f)
        {
            var step = Math.Min(remaining, 1f / 120f);
            var field = wind with { TotalSeconds = startTime + elapsed + step };
            var drift = field.Offset(Position.X, 1f, phase, 1f) * 5f;
            var flutter = MathF.Sin(Age * 13f + phase) * 34f * scale;
            var acceleration = new Vector2(drift + flutter - Velocity.X * 1.8f, -280f * scale);
            Position += Velocity * step + acceleration * (0.5f * step * step);
            Velocity += acceleration * step;
            Rotation += (spin + MathF.Sin(Age * 9f + phase) * 1.4f) * step;
            Age = Math.Min(LifetimeSeconds, Age + step);
            remaining -= step;
            elapsed += step;
        }
        Age = targetAge;
    }
}
