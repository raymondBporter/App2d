using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Presentation.World;

public sealed class SideScrollerCamera2D
{
    private const float GroundedVerticalOffset = 40f;
    private const float FallingVerticalOffset = -110f;
    private const float HorizontalDeadZoneViewportRatio = 0.22f;
    private const float VerticalDeadZoneViewportRatio = 0.30f;
    private const float FallingVerticalDeadZoneViewportRatio = 0.12f;
    private const float FallActivationVelocity = -150f;
    private const float FallResetVelocity = -60f;
    private const float FallActivationDelay = 0.08f;
    private const float MaximumLookAhead = 210f;
    private const float MaximumShakeStrength = 6f;
    private const float ShakeDecayRate = 11f;
    private const float ShakeFrequency = 40f;
    private const float MinimumShakeStrength = 0.05f;
    private const float VerticalFollowSuppressionRate = 18f;
    private const float MinimumVerticalFollowRateScale = 0.25f;

    private readonly Camera2D _camera;
    private float _lookAhead;
    private float _fallDuration;
    private float _fallBlend;
    private Vector2 _followPosition;
    private float _shakeStrength;
    private float _shakeTime;
    private float _verticalFollowSuppression;
    private float _lastGroundedPlayerY;

    public SideScrollerCamera2D(Camera2D camera, Rect2D levelBounds, Vector2 initialPlayerPosition)
    {
        _camera = ArgGuard.RequireNotNull(camera);
        if (!levelBounds.IsFinite)
        {
            ArgGuard.ThrowOutOfRange(levelBounds, "Value must be finite.");
        }

        _camera.WorldBounds = levelBounds;
        _camera.Zoom = 1.35f;
        Reset(initialPlayerPosition);
    }

    public void Update(Vector2 playerPosition, Vector2 playerVelocity, bool isGrounded, float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        if (isGrounded)
        {
            _lastGroundedPlayerY = playerPosition.Y;
        }

        var halfView = _camera.VisibleWorldBounds.Size / 2f;
        UpdateLookAhead(playerVelocity.X, halfView.X, deltaSeconds);
        UpdateVerticalFraming(playerVelocity.Y, isGrounded, deltaSeconds);

        var focus = new Vector2(playerPosition.X + _lookAhead, GetVerticalFocus(playerPosition));
        var target = _followPosition;
        target.X = KeepInsideDeadZone(target.X, focus.X, halfView.X * HorizontalDeadZoneViewportRatio);
        target.Y = KeepInsideDeadZone(target.Y, focus.Y, halfView.Y * float.Lerp(VerticalDeadZoneViewportRatio, FallingVerticalDeadZoneViewportRatio, _fallBlend));
        target = ClampToLevel(target);

        var distance = Vector2.Abs(target - _followPosition);
        var horizontalRate = CatchUpRate(distance.X, halfView.X, 4.5f, 11f);
        var followingDownward = target.Y < _followPosition.Y;
        var verticalRate = followingDownward
            ? CatchUpRate(distance.Y, halfView.Y, float.Lerp(3.2f, 7f, _fallBlend), float.Lerp(8f, 16f, _fallBlend))
            : CatchUpRate(distance.Y, halfView.Y, 3.2f, 8f);
        verticalRate *= float.Lerp(1f, MinimumVerticalFollowRateScale, _verticalFollowSuppression);
        _followPosition = new Vector2(Damp(_followPosition.X, target.X, horizontalRate, deltaSeconds), Damp(_followPosition.Y, target.Y, verticalRate, deltaSeconds));
        _followPosition = ClampToLevel(_followPosition);
        _verticalFollowSuppression = Damp(_verticalFollowSuppression, 0f, VerticalFollowSuppressionRate, deltaSeconds);
        _camera.Position = _followPosition;
        _camera.Position = _followPosition + UpdateShake(deltaSeconds);
    }

    public void Shake(float strength, bool stabilizeVerticalFollow = false)
    {
        ArgGuard.Positive(strength);
        _shakeStrength = MathF.Min(MaximumShakeStrength, MathF.Max(_shakeStrength, strength));
        _shakeTime = 0f;
        if (stabilizeVerticalFollow)
        {
            _verticalFollowSuppression = 1f;
        }
    }

    public void Reset(Vector2 playerPosition)
    {
        _lookAhead = 0f;
        _fallDuration = 0f;
        _fallBlend = 0f;
        _shakeStrength = 0f;
        _shakeTime = 0f;
        _verticalFollowSuppression = 0f;
        _lastGroundedPlayerY = playerPosition.Y;
        _followPosition = ClampToLevel(new Vector2(playerPosition.X, GetVerticalFocus(playerPosition)));
        _camera.Position = _followPosition;
    }

    private Vector2 UpdateShake(float deltaSeconds)
    {
        if (_shakeStrength <= 0f)
        {
            return Vector2.Zero;
        }

        _shakeTime += deltaSeconds;
        _shakeStrength = Damp(_shakeStrength, 0f, ShakeDecayRate, deltaSeconds);
        if (_shakeStrength < MinimumShakeStrength)
        {
            _shakeStrength = 0f;
            return Vector2.Zero;
        }

        // Two mismatched waves keep the motion from looking mechanical without
        // introducing random jitter or frame-rate-dependent camera drift.
        return new Vector2(
            MathF.Sin(_shakeTime * ShakeFrequency),
            MathF.Sin(_shakeTime * ShakeFrequency * 1.37f + 1.1f)) * _shakeStrength;
    }

    private float GetVerticalFocus(Vector2 playerPosition)
    {
        var playerFocus = playerPosition.Y + float.Lerp(GroundedVerticalOffset, FallingVerticalOffset, _fallBlend);
        var floorAnchoredFocus = MathF.Max(playerFocus, _lastGroundedPlayerY + GroundedVerticalOffset);

        // Keep ordinary jumps framed against the terrain, but let a sustained
        // fall reveal the space below instead of pinning the camera above a pit.
        return float.Lerp(floorAnchoredFocus, playerFocus, _fallBlend);
    }

    private void UpdateVerticalFraming(float verticalVelocity, bool isGrounded, float deltaSeconds)
    {
        if (isGrounded || verticalVelocity >= FallResetVelocity)
        {
            _fallDuration = 0f;
        }
        else if (verticalVelocity <= FallActivationVelocity)
        {
            _fallDuration += deltaSeconds;
        }

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

    private Vector2 ClampToLevel(Vector2 position)
    {
        // Leave enough room for the largest shake so its render-only offset can
        // never expose space beyond the authored level at any viewport edge.
        return _camera.ClampToWorld(position, MaximumShakeStrength);
    }

    private static float KeepInsideDeadZone(float cameraCenter, float focus, float halfSize)
    {
        var offset = focus - cameraCenter;
        if (offset > halfSize)
        {
            return focus - halfSize;
        }

        if (offset < -halfSize)
        {
            return focus + halfSize;
        }

        return cameraCenter;
    }

    private static float CatchUpRate(float distance, float halfViewExtent, float normalRate, float maximumRate)
    {
        if (halfViewExtent <= 0f)
        {
            return maximumRate;
        }

        var urgency = Math.Clamp(distance / (halfViewExtent * 0.7f), 0f, 1f);
        return float.Lerp(normalRate, maximumRate, urgency);
    }

    private static float Damp(float current, float target, float rate, float deltaSeconds)
    {
        var blend = 1f - MathF.Exp(-rate * deltaSeconds);
        return float.Lerp(current, target, blend);
    }

}
