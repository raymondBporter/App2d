using App2d.Core;
using App2d.Gameplay.Player;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Diagnostics;

/// <summary>
/// Local free-flight reference, independent of character control and session state.
/// One tile is interpreted as one meter for this experiment only.
/// </summary>
internal sealed class BallisticsDebug2D(TraversalMetrics2D traversal)
{
    private readonly TraversalMetrics2D _traversal = ArgGuard.RequireNotNull(traversal);
    private float _speed = 10f;
    private float _angle = 45f;
    private float _referenceGravity = 10f;
    private float _elapsed;

    public float SpeedTilesPerSecond
    {
        get => _speed;
        set { ArgGuard.ThrowIfNotInClosedRange(value, 0.1f, 100f); _speed = value; }
    }

    public float AngleDegrees
    {
        get => _angle;
        set { ArgGuard.ThrowIfNotInClosedRange(value, 1f, 90f); _angle = value; }
    }

    public float ReferenceGravityTilesPerSecondSquared
    {
        get => _referenceGravity;
        set { ArgGuard.ThrowIfNotInClosedRange(value, 0.1f, 200f); _referenceGravity = value; }
    }

    public Shot? Reference { get; private set; }
    public Shot? World { get; private set; }
    public float ElapsedSeconds => _elapsed;

    public void Launch(Vector2 origin, float facing)
    {
        ArgGuard.ThrowIfNotFinite(origin);
        ArgGuard.ThrowIfNotFinite(facing);
        if (facing == 0f) throw new ArgumentOutOfRangeException(nameof(facing));
        var angle = _angle * MathF.PI / 180f;
        var velocity = _speed * _traversal.TileSize * new Vector2(
            MathF.Cos(angle) * MathF.Sign(facing), MathF.Sin(angle));
        Reference = new Shot(origin, velocity, _referenceGravity * _traversal.TileSize);
        World = new Shot(origin, velocity, _traversal.Gravity);
        _elapsed = 0f;
    }

    public void Clear()
    {
        Reference = World = null;
        _elapsed = 0f;
    }

    public void Advance(float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        if (Reference is { } reference && World is { } world)
            _elapsed = Math.Min(_elapsed + deltaSeconds, Math.Max(reference.FlightSeconds, world.FlightSeconds));
    }

    public void Draw(Renderer2D renderer)
    {
        if (Reference is not { } reference || World is not { } world) return;
        var referenceColor = new XnaColor(255, 220, 65);
        var worldColor = new XnaColor(255, 100, 180);
        DrawShot(renderer, reference, referenceColor);
        DrawShot(renderer, world, worldColor);

        var speed = reference.Velocity.Length() / _traversal.TileSize;
        var angle = MathF.Atan2(reference.Velocity.Y, MathF.Abs(reference.Velocity.X)) * 180f / MathF.PI;
        renderer.DrawScreenLabel(
            $"F4 FIRE / F6 CLEAR | BALLISTICS {speed:0.0} tiles/s @ {angle:0} deg | 1 tile = 1 m (reference only)",
            new Vector2(24f, 220f));
        renderer.DrawScreenLabel(Describe("YELLOW reference", reference), new Vector2(24f, 260f));
        renderer.DrawScreenLabel(Describe("PINK world", world), new Vector2(24f, 300f));
        renderer.DrawScreenLabel("FREE FLIGHT: ignores terrain; stops at launch height", new Vector2(24f, 340f));
    }

    private string Describe(string label, Shot shot) =>
        $"{label}: g={shot.Gravity / _traversal.TileSize:0.00} m/s^2 | rise {shot.Rise / _traversal.TileSize:0.00}m | " +
        $"range {shot.Range / _traversal.TileSize:0.00}m | t={Math.Min(_elapsed, shot.FlightSeconds):0.00}/{shot.FlightSeconds:0.00}s";

    private void DrawShot(Renderer2D renderer, Shot shot, XnaColor color)
    {
        var elapsed = Math.Min(_elapsed, shot.FlightSeconds);
        Span<Vector2> trail = stackalloc Vector2[97];
        for (var i = 0; i < trail.Length; i++)
            trail[i] = shot.PositionAt(elapsed * i / (trail.Length - 1));
        renderer.DrawWorldPolyline(trail, color, 2f);
        renderer.DrawWorldCircle(trail[^1], _traversal.TileSize * 0.12f, color, 3f);

        // A fixed launch-height baseline makes the range visible even over empty space.
        var end = shot.PositionAt(shot.FlightSeconds);
        renderer.DrawWorldPolyline([shot.Origin, end], new XnaColor(color.R, color.G, color.B, (byte)80));
        renderer.DrawWorldCircle(shot.Origin, _traversal.TileSize * 0.08f, color);
        renderer.DrawWorldCircle(end, _traversal.TileSize * 0.08f, color);
    }

    internal readonly record struct Shot(Vector2 Origin, Vector2 Velocity, float Gravity)
    {
        public float FlightSeconds => 2f * Velocity.Y / Gravity;
        public float Rise => Velocity.Y * Velocity.Y / (2f * Gravity);
        public float Range => MathF.Abs(Velocity.X) * FlightSeconds;

        // Exact constant-acceleration trajectory: no integration error, drag, fall cap,
        // inherited player velocity, apex assistance, or collision response.
        public Vector2 PositionAt(float seconds) =>
            Origin + Velocity * seconds - new Vector2(0f, 0.5f * Gravity * seconds * seconds);
    }
}
