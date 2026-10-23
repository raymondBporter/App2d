using App2d.Core.Rendering;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Presentation.Player;

/// <summary>A small cloud stomp with three short-lived wisps tracing the initial lift.</summary>
public sealed class AirJumpCloud2D : IDisposable
{
    private const float Duration = .42f;
    private const float CloudScale = .75f;
    private const float WispLifetime = .2f;
    private static readonly float[] WispBirths = [.02f, .045f, .07f];
    private readonly Scene2D _scene;
    private readonly (WorldObject2D Shade, WorldObject2D Fill)[] _puffs;
    private readonly WorldObject2D[] _wisps = new WorldObject2D[9];
    private readonly Vector2[] _wispOrigins = new Vector2[3];
    private static readonly Vector2[] Offsets = [new(-19, -5), new(18, -5), new(-9, -2), new(9, -1), new(0, 0)];
    private static readonly float[] Radii = [10, 11, 13, 12, 14];
    private Vector2 _position;
    private Vector2 _previousFeet;
    private float _age = Duration;

    public AirJumpCloud2D(Scene2D scene)
    {
        _scene = scene;
        _puffs = new (WorldObject2D, WorldObject2D)[Offsets.Length];
        // Draw all shadows before all fills so overlapping lobes form one cloud.
        for (var i = 0; i < _puffs.Length; i++)
        {
            var shade = new WorldObject2D(new Circle2D(1), new CloudInk()) { ZIndex = 5, IsVisible = false };
            var fill = new WorldObject2D(new Circle2D(1), new CloudInk()) { ZIndex = 6, IsVisible = false };
            scene.Add(shade);
            scene.Add(fill);
            _puffs[i] = (shade, fill);
        }
        // Fixed pool: three clouds, each made of three soft lobes. No per-jump allocations.
        for (var i = 0; i < _wisps.Length; i++)
        {
            _wisps[i] = new WorldObject2D(new Circle2D(1), new CloudInk()) { ZIndex = 6, IsVisible = false };
            scene.Add(_wisps[i]);
        }
    }

    public void Present(Vector2 feet)
    {
        _position = feet - new Vector2(0, 9 * CloudScale);
        _previousFeet = feet;
        _age = 0;
        Draw();
    }

    public void Advance(float dt, Vector2 feet)
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(dt);
        var previousAge = _age;
        _age = Math.Min(Duration, _age + dt);
        for (var i = 0; i < WispBirths.Length; i++)
        {
            var birth = WispBirths[i];
            if (previousAge < birth && _age >= birth)
                _wispOrigins[i] = Vector2.Lerp(_previousFeet, feet, (birth - previousAge) / dt)
                    - new Vector2(0, 5);
        }
        _previousFeet = feet;
        Draw();
    }

    private void Draw()
    {
        var t = _age / Duration;
        var spread = 1 + 1.5f * t;
        var size = CloudScale * (1 + .25f * MathF.Sin(t * MathF.PI)) * (1 - .65f * t * t);
        var alpha = (int)(235 * (1 - t) * (1 - t));
        for (var i = 0; i < _puffs.Length; i++)
        {
            var (shade, fill) = _puffs[i];
            shade.IsVisible = fill.IsVisible = _age < Duration;
            var center = _position + new Vector2(Offsets[i].X * spread, Offsets[i].Y - 24 * t) * CloudScale;
            shade.Transform.Position = center - new Vector2(0, 2);
            fill.Transform.Position = center;
            shade.Transform.Scale = new Vector2(Radii[i] + 1.5f, Radii[i] * .65f + 1) * size;
            fill.Transform.Scale = new Vector2(Radii[i], Radii[i] * .65f) * size;
            ((CloudInk)shade.Shader).BaseColor = new Color(157, 185, 207, alpha);
            ((CloudInk)fill.Shader).BaseColor = new Color(242, 249, 255, alpha);
        }
        for (var i = 0; i < _wisps.Length; i++)
        {
            var group = i / 3;
            var lobe = i % 3 - 1;
            var age = _age - WispBirths[group];
            var wisp = _wisps[i];
            wisp.IsVisible = age >= 0 && age < WispLifetime;
            if (!wisp.IsVisible) continue;
            var progress = age / WispLifetime;
            var radius = (7 - group * 1.2f) * (1 - .5f * progress);
            // Wisps inherit a little lift, then linger behind the departing feet.
            wisp.Transform.Position = _wispOrigins[group] + new Vector2(
                lobe * radius * (.7f + progress), 30 * age - 45 * age * age + (lobe == 0 ? 2 : 0));
            wisp.Transform.Scale = new Vector2(radius, radius * .7f);
            ((CloudInk)wisp.Shader).BaseColor = new Color(242, 249, 255,
                (int)((150 - group * 25) * (1 - progress) * (1 - progress)));
        }
    }

    public void Reset() { _age = Duration; Draw(); }

    public void Dispose()
    {
        foreach (var (shade, fill) in _puffs) { _scene.Remove(shade); _scene.Remove(fill); }
        foreach (var wisp in _wisps) _scene.Remove(wisp);
    }

    private sealed class CloudInk : IShader2D { public Color BaseColor { get; set; } }
}
