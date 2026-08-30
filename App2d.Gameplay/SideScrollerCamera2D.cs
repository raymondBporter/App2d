using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Rendering;
using SkiaSharp;

namespace App2d.Gameplay;

public sealed class SideScrollerCamera2D
{
    private const float GroundedVerticalOffset = 40f;
    private const float FallingVerticalOffset = -110f;
    private const float FloorClearance = 96f;
    private const float HorizontalDeadZoneViewportRatio = 0.22f;
    private const float VerticalDeadZoneViewportRatio = 0.30f;
    private const float FallingVerticalDeadZoneViewportRatio = 0.12f;
    private const float FallActivationVelocity = -150f;
    private const float FallResetVelocity = -60f;
    private const float FallActivationDelay = 0.08f;
    private const float MaximumLookAhead = 210f;

    private readonly Camera2D _camera;
    private readonly Bounds2D _levelBounds;
    private readonly Func<float, float> _floorHeightAtX;
    private readonly List<ParallaxItem> _parallaxItems = [];
    private float _lookAhead;
    private float _fallDuration;
    private float _fallBlend;

    public SideScrollerCamera2D(Scene2D scene, Camera2D camera, Bounds2D levelBounds, Vector2 initialPlayerPosition, Func<float, float> floorHeightAtX)
    {
        ArgGuard.ThrowIfNull(scene);
        _camera = ArgGuard.RequireNotNull(camera);
        if (!levelBounds.IsFinite)
            ArgGuard.ThrowOutOfRange(levelBounds, "Value must be finite.");
        _levelBounds = levelBounds;
        _floorHeightAtX = ArgGuard.RequireNotNull(floorHeightAtX);

        _camera.Zoom = 1.35f;
        Reset(initialPlayerPosition);
        CreateParallaxBackground(scene);
        UpdateParallax();
    }

    public void Update(Vector2 playerPosition, Vector2 playerVelocity, bool isGrounded, float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
            return;

        var halfView = _camera.ViewportSize / (2f * _camera.Zoom);
        UpdateLookAhead(playerVelocity.X, halfView.X, deltaSeconds);
        UpdateVerticalFraming(playerVelocity.Y, isGrounded, deltaSeconds);

        var focus = new Vector2(playerPosition.X + _lookAhead, GetVerticalFocus(playerPosition));
        var target = _camera.Position;
        target.X = KeepInsideDeadZone(target.X, focus.X, halfView.X * HorizontalDeadZoneViewportRatio);
        target.Y = KeepInsideDeadZone(target.Y, focus.Y, halfView.Y * float.Lerp(VerticalDeadZoneViewportRatio, FallingVerticalDeadZoneViewportRatio, _fallBlend));
        target = ClampToLevel(target, halfView);

        var distance = Vector2.Abs(target - _camera.Position);
        var horizontalRate = CatchUpRate(distance.X, halfView.X, 4.5f, 11f);
        var followingDownward = target.Y < _camera.Position.Y;
        var verticalRate = followingDownward
            ? CatchUpRate(distance.Y, halfView.Y, float.Lerp(3.2f, 7f, _fallBlend), float.Lerp(8f, 16f, _fallBlend))
            : CatchUpRate(distance.Y, halfView.Y, 3.2f, 8f);
        _camera.Position = new Vector2(Damp(_camera.Position.X, target.X, horizontalRate, deltaSeconds), Damp(_camera.Position.Y, target.Y, verticalRate, deltaSeconds));
        _camera.Position = ClampToLevel(_camera.Position, halfView);
        UpdateParallax();
    }

    public void Reset(Vector2 playerPosition)
    {
        _lookAhead = 0f;
        _fallDuration = 0f;
        _fallBlend = 0f;
        var halfView = _camera.ViewportSize / (2f * _camera.Zoom);
        _camera.Position = ClampToLevel(new Vector2(playerPosition.X, GetVerticalFocus(playerPosition)), halfView);
        UpdateParallax();
    }

    private float GetVerticalFocus(Vector2 playerPosition)
    {
        var floorY = _floorHeightAtX(playerPosition.X);
        if (!float.IsFinite(floorY))
            throw new InvalidOperationException("The camera floor height must be finite.");

        var playerFocus = playerPosition.Y + float.Lerp(GroundedVerticalOffset, FallingVerticalOffset, _fallBlend);
        var floorAnchoredFocus = MathF.Max(playerFocus, floorY + FloorClearance);

        // Keep ordinary jumps framed against the terrain, but let a sustained
        // fall reveal the space below instead of pinning the camera above a pit.
        return float.Lerp(floorAnchoredFocus, playerFocus, _fallBlend);
    }

    private void UpdateVerticalFraming(float verticalVelocity, bool isGrounded, float deltaSeconds)
    {
        if (isGrounded || verticalVelocity >= FallResetVelocity)
            _fallDuration = 0f;
        else if (verticalVelocity <= FallActivationVelocity)
            _fallDuration += deltaSeconds;

        var isSustainedFall = !isGrounded && _fallDuration >= FallActivationDelay;
        var target = isSustainedFall ? 1f : 0f;
        var response = isSustainedFall ? 5.5f : isGrounded ? 2.5f : 3.5f;
        _fallBlend = Damp(_fallBlend, target, response, deltaSeconds);
    }

    private void UpdateLookAhead(float horizontalVelocity, float halfViewWidth, float deltaSeconds)
    {
        var maximum = MathF.Min(MaximumLookAhead, halfViewWidth * 0.42f);
        var target = Math.Clamp(horizontalVelocity * 0.32f, -maximum, maximum);

        // Look forward promptly while moving, but let the framing drift back gently
        // when the player stops so the camera does not twitch with every speed change.
        var response = MathF.Abs(horizontalVelocity) > 20f ? 5.5f : 2.2f;
        _lookAhead = Damp(_lookAhead, target, response, deltaSeconds);
    }

    private Vector2 ClampToLevel(Vector2 position, Vector2 halfView)
    {
        return new Vector2(ClampViewCenter(position.X, _levelBounds.Min.X, _levelBounds.Max.X, halfView.X), ClampViewCenter(position.Y, _levelBounds.Min.Y, _levelBounds.Max.Y, halfView.Y));
    }

    private static float KeepInsideDeadZone(float cameraCenter, float focus, float halfSize)
    {
        var offset = focus - cameraCenter;
        if (offset > halfSize)
            return focus - halfSize;
        if (offset < -halfSize)
            return focus + halfSize;
        return cameraCenter;
    }

    private static float CatchUpRate(float distance, float halfViewExtent, float normalRate, float maximumRate)
    {
        if (halfViewExtent <= 0f)
            return maximumRate;

        var urgency = Math.Clamp(distance / (halfViewExtent * 0.7f), 0f, 1f);
        return float.Lerp(normalRate, maximumRate, urgency);
    }

    private static float Damp(float current, float target, float rate, float deltaSeconds)
    {
        var blend = 1f - MathF.Exp(-rate * deltaSeconds);
        return float.Lerp(current, target, blend);
    }

    private void CreateParallaxBackground(Scene2D scene)
    {
        var cloudShader = new SolidColorShader(new SKColor(240, 250, 255, 205));
        var farMountainShader = new SolidColorShader(new SKColor(90, 145, 177));
        var nearHillShader = new SolidColorShader(new SKColor(70, 128, 125));

        for (var i = 0; i < 8; i++)
        {
            var width = 150f + i % 3 * 45f;
            AddParallax(scene, new Capsule2D(new Vector2(-width / 2f, 0f), new Vector2(width / 2f, 0f), 38f), cloudShader, new Vector2(-850f + i * 470f, 210f + i % 3 * 95f), 0.08f, repeatWidth: 3_760f);
        }

        for (var i = 0; i < 12; i++)
        {
            var width = 520f + i % 3 * 90f;
            var height = 360f + i % 4 * 55f;
            var mountain = new ConvexPolygon2D(
            [
                new Vector2(-width / 2f, 0f),
                new Vector2(0f, height),
                new Vector2(width / 2f, 0f)
            ]);
            AddParallax(scene, mountain, farMountainShader, new Vector2(-1_100f + i * 500f, -520f), 0.18f, repeatWidth: 6_000f);
        }

        for (var i = 0; i < 13; i++)
        {
            AddParallax(scene, new Circle2D(185f + i % 3 * 24f), nearHillShader, new Vector2(-1_000f + i * 510f, -525f), 0.42f, new Vector2(1.9f, 1f), repeatWidth: 6_630f);
        }
    }

    private void AddParallax(Scene2D scene, IShape2D shape, IShader2D shader, Vector2 anchor, float scrollFactor, Vector2? scale = null, float repeatWidth = 4_000f)
    {
        var worldObject = new WorldObject2D(shape, shader);
        worldObject.Transform.Scale = scale ?? Vector2.One;
        _parallaxItems.Add(new ParallaxItem(worldObject, anchor, scrollFactor, repeatWidth));
        scene.Add(worldObject);
    }

    private void UpdateParallax()
    {
        foreach (var item in _parallaxItems)
        {
            var relativeX = WrapCentered(item.Anchor.X - _camera.Position.X * item.ScrollFactor, item.RepeatWidth);
            item.Object.Transform.Position = new Vector2(_camera.Position.X + relativeX, item.Anchor.Y + _camera.Position.Y * (1f - item.ScrollFactor * 0.3f));
        }
    }

    private static float WrapCentered(float value, float period) => value - MathF.Floor((value + period / 2f) / period) * period;

    private static float ClampViewCenter(float value, float min, float max, float halfExtent)
    {
        if (max - min <= halfExtent * 2f)
            return (min + max) / 2f;
        return Math.Clamp(value, min + halfExtent, max - halfExtent);
    }

    private readonly record struct ParallaxItem(WorldObject2D Object, Vector2 Anchor, float ScrollFactor, float RepeatWidth);
}
