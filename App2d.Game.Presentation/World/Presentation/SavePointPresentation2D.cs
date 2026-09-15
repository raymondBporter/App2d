using App2d.Core;
using App2d.Core.Geometry;
using App2d.Rendering;
using XnaColor = Microsoft.Xna.Framework.Color;
using System.Numerics;

namespace App2d.Gameplay.World.Presentation;

/// <summary>Client-owned checkpoint beacon animation.</summary>
internal sealed class SavePointPresentation2D : IDisposable
{
    private const float OrbHeight = 82f;
    private readonly WorldObject2D _glow;
    private readonly WorldObject2D _orb;
    private readonly Vector2 _basePosition;
    private readonly Scene2D _scene;
    private readonly List<WorldObject2D> _visuals = [];
    private float _animationSeconds;

    public SavePointPresentation2D(Scene2D scene, CheckpointState2D state)
    {
        ArgGuard.ThrowIfNull(scene);
        _scene = scene;
        _basePosition = state.BasePosition;

        var baseStone = new WorldObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(58f, 14f)),
            new LinearGradientShader(new XnaColor(68, 77, 96), new XnaColor(31, 38, 54)))
        {
            ZIndex = 1
        };
        baseStone.Transform.Position = _basePosition + new Vector2(0f, 7f);
        Add(baseStone);

        var pedestal = new WorldObject2D(
            new ConvexPolygon2D(
            [
                new Vector2(-20f, 0f),
                new Vector2(20f, 0f),
                new Vector2(11f, 52f),
                new Vector2(-11f, 52f)
            ]),
            new LinearGradientShader(new XnaColor(91, 103, 125), new XnaColor(37, 45, 62)))
        {
            ZIndex = 1
        };
        pedestal.Transform.Position = _basePosition + new Vector2(0f, 12f);
        Add(pedestal);

        _glow = new WorldObject2D(
            new Circle2D(31f),
            new SolidColorShader(new XnaColor(93, 224, 255, 38)))
        {
            IsVisible = false,
            ZIndex = 1
        };
        _glow.Transform.Position = _basePosition + new Vector2(0f, OrbHeight);
        Add(_glow);

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
        Add(_orb);
        SetActive(state.IsActive);
    }

    public bool IsActive { get; private set; }

    public void Update(float deltaSeconds, bool isActive)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        SetActive(isActive);
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

    }

    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;

        IsActive = active;
        _glow.IsVisible = active;
        _orb.Shader = active ? ActiveOrbShader() : InactiveOrbShader();
    }

    private void Add(WorldObject2D visual) { _scene.Add(visual); _visuals.Add(visual); }
    public void Dispose() { foreach (var visual in _visuals) _scene.Remove(visual); _visuals.Clear(); }

    private static LinearGradientShader ActiveOrbShader() =>
        new LinearGradientShader(new XnaColor(245, 255, 255), new XnaColor(44, 193, 255));

    private static LinearGradientShader InactiveOrbShader() =>
        new LinearGradientShader(new XnaColor(132, 139, 158), new XnaColor(53, 60, 78));
}
