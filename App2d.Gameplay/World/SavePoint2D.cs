using App2d.Core;
using App2d.Core.Geometry;
using App2d.Rendering;
using SkiaSharp;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>A lightweight code-drawn checkpoint beacon with an entry trigger.</summary>
internal sealed class SavePoint2D
{
    private const float OrbHeight = 82f;
    private readonly WorldObject2D _glow;
    private readonly WorldObject2D _orb;
    private readonly Vector2 _basePosition;
    private bool _playerWasInside;
    private float _animationSeconds;

    public SavePoint2D(Scene2D scene, WorldThingSpec2D spec, float respawnGroundOffset)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(spec);
        ArgGuard.ThrowIfNotPositive(respawnGroundOffset);
        StateGuard.ThrowIf(spec.Kind != WorldThingKind2D.SavePoint, "A save-point visual requires a save-point thing.");

        Spec = spec;
        _basePosition = spec.Position - new Vector2(0f, respawnGroundOffset);

        var baseStone = new WorldObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(58f, 14f)),
            new LinearGradientShader(new SKColor(68, 77, 96), new SKColor(31, 38, 54)))
        {
            ZIndex = 1
        };
        baseStone.Transform.Position = _basePosition + new Vector2(0f, 7f);
        scene.Add(baseStone);

        var pedestal = new WorldObject2D(
            new ConvexPolygon2D(
            [
                new Vector2(-20f, 0f),
                new Vector2(20f, 0f),
                new Vector2(11f, 52f),
                new Vector2(-11f, 52f)
            ]),
            new LinearGradientShader(new SKColor(91, 103, 125), new SKColor(37, 45, 62)))
        {
            ZIndex = 1
        };
        pedestal.Transform.Position = _basePosition + new Vector2(0f, 12f);
        scene.Add(pedestal);

        _glow = new WorldObject2D(
            new Circle2D(31f),
            new SolidColorShader(new SKColor(93, 224, 255, 38)))
        {
            IsVisible = false,
            ZIndex = 1
        };
        _glow.Transform.Position = _basePosition + new Vector2(0f, OrbHeight);
        scene.Add(_glow);

        _orb = new WorldObject2D(
            new ConvexPolygon2D(
            [
                new Vector2(0f, 24f),
                new Vector2(18f, 0f),
                new Vector2(0f, -24f),
                new Vector2(-18f, 0f)
            ]),
            InactiveOrbShader())
        {
            ZIndex = 2
        };
        _orb.Transform.Position = _basePosition + new Vector2(0f, OrbHeight);
        scene.Add(_orb);
    }

    public WorldThingSpec2D Spec { get; }
    public bool IsActive { get; private set; }

    public bool Update(float deltaSeconds, Bounds2D playerBounds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        _animationSeconds += deltaSeconds;

        var bob = MathF.Sin(_animationSeconds * (IsActive ? 4.5f : 2.2f)) * (IsActive ? 5f : 2f);
        var orbPosition = _basePosition + new Vector2(0f, OrbHeight + bob);
        _orb.Transform.Position = orbPosition;
        _glow.Transform.Position = orbPosition;

        if (IsActive)
        {
            var pulse = 1f + MathF.Sin(_animationSeconds * 5.5f) * 0.09f;
            _glow.Transform.Scale = new Vector2(pulse);
        }

        var isInside = TriggerBounds.Intersects(playerBounds);
        var entered = isInside && !_playerWasInside;
        _playerWasInside = isInside;
        return entered;
    }

    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;

        IsActive = active;
        _glow.IsVisible = active;
        _orb.Shader = active ? ActiveOrbShader() : InactiveOrbShader();
    }

    private Bounds2D TriggerBounds => new(
        _basePosition + new Vector2(-64f, -8f),
        _basePosition + new Vector2(64f, 132f));

    private static LinearGradientShader ActiveOrbShader() =>
        new LinearGradientShader(new SKColor(245, 255, 255), new SKColor(44, 193, 255));

    private static LinearGradientShader InactiveOrbShader() =>
        new LinearGradientShader(new SKColor(132, 139, 158), new SKColor(53, 60, 78));
}
