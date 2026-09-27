using App2d.Core;
using System.Numerics;

namespace App2d.Gameplay.Player;

public sealed class TraversalMetrics2D
{
    public const float DesignUnit = 8f;

    private TraversalMetrics2D()
    {
    }

    public static TraversalMetrics2D FromGeometry(
        Vector2 visualSize, float footAnchorYFraction,
        Vector2 standingColliderSize, float colliderCenterOffsetX)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(visualSize);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(standingColliderSize);
        ArgGuard.ThrowIfNotFinite(colliderCenterOffsetX);
        ArgGuard.ThrowIfNotFiniteOrNotInOpenRange(footAnchorYFraction, 0f, 1f);
        return new TraversalMetrics2D
        {
            PlayerColliderSize = standingColliderSize,
            PlayerColliderCenterOffsetX = colliderCenterOffsetX,
            PlayerVisualSize = visualSize,
            PlayerSpriteFootYFraction = footAnchorYFraction
        };
    }

    public float TileSize { get; init; } = DesignUnit * 4f;
    public Vector2 PlayerColliderSize { get; private init; }
    public float PlayerColliderCenterOffsetX { get; private init; }
    public Vector2 PlayerVisualSize { get; private init; }
    public float PlayerSpriteFootYFraction { get; private init; }
    public Vector2 PlayerVisualOffset => new(
        0f,
        PlayerVisualSize.Y * (PlayerSpriteFootYFraction - 0.5f) -
        PlayerColliderSize.Y * 0.5f);
    // Authored pistol socket (418, 206) in the 512px standing frame.
    // Load this configuration once; weapon simulation receives only the world offset.
    public Vector2 GunMuzzleOffset => new(
        (418f / 512f - 0.5f) * PlayerVisualSize.X,
        (PlayerSpriteFootYFraction - 206f / 512f) * PlayerVisualSize.Y - PlayerColliderSize.Y * 0.5f);
    public float RunSpeed { get; init; } = 430f;
    public float LadderClimbSpeed { get; init; } = 180f;
    public float LadderRelatchDelay { get; init; } = 0.2f;
    public float GroundAcceleration { get; init; } = 3_600f;
    public float AirAcceleration { get; init; } = 1_450f;
    public float Gravity { get; init; } = 1_900f;
    public float JumpSpeed { get; init; } = 760f;
    public float DownAttackBounceSpeed { get; init; } = 520f;
    public float AirJumpSpeedMultiplier { get; init; } = 0.6f;
    public float AirJumpSpeed => JumpSpeed * AirJumpSpeedMultiplier;
    public int MaximumJumpCount { get; init; } = 2;
    public float OneWayDropSpeed { get; init; } = 140f;
    public float JumpReleaseSpeedMultiplier { get; init; } = 0.45f;
    public float CoyoteDuration { get; init; } = 0.11f;
    public float JumpBufferDuration { get; init; } = 0.12f;
    public float ApexVelocityThreshold { get; init; } = 105f;
    public float ApexGravityScale { get; init; } = 0.55f;
    public float MaximumFallSpeed { get; init; } = 1_100f;
    /// <summary>Ordinary jumps stay below this band; only fast downward motion meets drag.</summary>
    public float FallDragStartFraction { get; init; } = 0.8f;
    public float HardLandingSpeed => MaximumFallSpeed * 0.95f;

    /// <summary>
    /// Integrates vertical gravity with a soft terminal speed. In the upper band acceleration
    /// falls with the square of the remaining speed, so the final few percent take seconds.
    /// Upward motion and low-speed falls retain normal gravity. Throws above terminal speed
    /// shed their excess gradually instead of being clamped on the next frame.
    /// </summary>
    public float AdvanceVerticalSpeed(float velocityY, float downwardGravity, float seconds)
    {
        if (seconds <= 0f || downwardGravity <= 0f) return velocityY;
        var speed = -velocityY;
        var start = MaximumFallSpeed * FallDragStartFraction;
        if (speed + downwardGravity * seconds <= start)
            return velocityY - downwardGravity * seconds;
        var band = MaximumFallSpeed - start;
        if (speed > MaximumFallSpeed)
            return -(MaximumFallSpeed + (speed - MaximumFallSpeed) * MathF.Exp(-seconds / 0.5f));
        if (speed < start)
        {
            var ordinarySeconds = Math.Min(seconds, (start - speed) / downwardGravity);
            speed += downwardGravity * ordinarySeconds;
            seconds -= ordinarySeconds;
        }
        var remaining = MaximumFallSpeed - speed;
        return -(MaximumFallSpeed - remaining / (1f + downwardGravity * remaining * seconds / (band * band)));
    }

    /// <summary>
    /// Extra hard-landing emphasis, from 95% to 99% of terminal speed. Reciprocal remaining
    /// speed tracks elapsed time in the drag band; saturation avoids amplifying numerical noise
    /// near terminal speed and gives an immediate slam the full response without an airtime gate.
    /// </summary>
    public float HardLandingIntensity(float impactSpeed)
    {
        var fraction = Math.Clamp(impactSpeed / MaximumFallSpeed, 0f, 0.99f);
        return Math.Clamp((1f / (1f - fraction) - 20f) / 80f, 0f, 1f);
    }
    public float GroundProbeDistance { get; init; } = 2f;
    public float LandingSnapDistance { get; init; } = 4f;
    public float HorizontalSupportGrace { get; init; } = 2f;
    public float BalanceOverhangFraction { get; init; } = 0.35f;
    public int UpwardCornerCorrection { get; init; } = 8;
    public float WallGripProbeDistance { get; init; } = 4f;
    public float WallGripMinimumOverlap { get; init; } = DesignUnit;
    public float WallJumpHorizontalSpeed { get; init; } = 380f;
    public float WallJumpRelatchDelay { get; init; } = 0.14f;
    public float DashSpeed { get; init; } = 1_400f;
    public float DashDuration { get; init; } = 0.16f;
    public float DashCooldown { get; init; } = 0.35f;

    public int StandingPassageTiles =>
        (int)MathF.Ceiling(PlayerColliderSize.Y / TileSize);
    public float StandingClearance =>
        StandingPassageTiles * TileSize - PlayerColliderSize.Y;
    public int ReliableJumpRiseTiles { get; init; } = 4;

    public void ValidateScaleContract()
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(TileSize);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(LadderClimbSpeed);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(LadderRelatchDelay);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(PlayerColliderSize);
        ArgGuard.ThrowIfNotFinite(PlayerColliderCenterOffsetX);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(PlayerVisualSize);
        ArgGuard.ThrowIfNotFinite(PlayerVisualOffset);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(AirJumpSpeedMultiplier);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(DownAttackBounceSpeed);
        ArgGuard.ThrowIfNotPositive(MaximumJumpCount);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(OneWayDropSpeed);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(WallGripProbeDistance);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(WallGripMinimumOverlap);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(WallJumpHorizontalSpeed);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(WallJumpRelatchDelay);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(DashSpeed);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(DashDuration);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(DashCooldown);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(MaximumFallSpeed);
        StateGuard.ThrowIf(!float.IsFinite(FallDragStartFraction) || FallDragStartFraction <= 0f || FallDragStartFraction >= 0.95f,
            "Fall drag must begin between zero and the hard-landing speed fraction (0.95).");
        StateGuard.ThrowIf(
            !NumericValidation.IsInOpenRange(BalanceOverhangFraction, 0f, 0.5f),
            "The balance overhang must be a fraction between zero and one half.");

        StateGuard.ThrowIf(AirJumpSpeedMultiplier >= 1f, "The air-jump speed multiplier must be less than one.");
        StateGuard.ThrowIf(
            !NumericValidation.IsInOpenRange(PlayerSpriteFootYFraction, 0f, 1f),
            "The player sprite foot anchor must be a fraction between zero and one.");

        StateGuard.ThrowIf(
            !IsDesignUnitMultiple(TileSize) ||
            !IsHalfDesignUnitMultiple(PlayerColliderSize.Y),
            $"Tile and player collider heights must use half increments of the {DesignUnit:0}-unit design grid.");
        StateGuard.ThrowIf(StandingClearance < DesignUnit, $"The minimum whole-tile standing passage must leave at least {DesignUnit:0} units of clearance.");

        ArgGuard.ThrowIfNotFiniteOrNotPositive(ReliableJumpRiseTiles);
        var requiredJumpHeight = TileSize * ReliableJumpRiseTiles + DesignUnit;
        var standingJump = MeasureJump(0f);
        StateGuard.ThrowIf(
            standingJump.ApexHeight < requiredJumpHeight,
            $"A held jump must clear {ReliableJumpRiseTiles} tiles plus " +
            $"{DesignUnit:0} units of margin.");
    }

    public JumpProfile2D MeasureJump(float initialHorizontalSpeed, float fixedDeltaSeconds = 1f / 120f)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(fixedDeltaSeconds);

        var position = Vector2.Zero;
        var velocity = new Vector2(initialHorizontalSpeed, JumpSpeed);
        var apexHeight = 0f;
        var timeToApex = 0f;
        var elapsed = 0f;

        for (var step = 0; step < 1_200; step++)
        {
            velocity.X = MoveTowards(velocity.X, RunSpeed, AirAcceleration * fixedDeltaSeconds);
            var gravityScale = MathF.Abs(velocity.Y) < ApexVelocityThreshold
                ? ApexGravityScale
                : 1f;
            velocity.Y = AdvanceVerticalSpeed(velocity.Y, Gravity * gravityScale, fixedDeltaSeconds);
            position += velocity * fixedDeltaSeconds;
            elapsed += fixedDeltaSeconds;

            if (position.Y > apexHeight)
            {
                apexHeight = position.Y;
                timeToApex = elapsed;
            }

            if (elapsed > fixedDeltaSeconds && position.Y <= 0f)
                return new JumpProfile2D(apexHeight, timeToApex, elapsed, position.X);
        }

        throw StateGuard.Create("Jump simulation did not return to its starting height.");
    }

    public Vector2[] BuildJumpArc(float initialHorizontalSpeed, float fixedDeltaSeconds = 1f / 120f)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(fixedDeltaSeconds);

        var points = new List<Vector2> { Vector2.Zero };
        var position = Vector2.Zero;
        var velocity = new Vector2(initialHorizontalSpeed, JumpSpeed);

        for (var step = 0; step < 1_200; step++)
        {
            velocity.X = MoveTowards(velocity.X, RunSpeed, AirAcceleration * fixedDeltaSeconds);
            var gravityScale = MathF.Abs(velocity.Y) < ApexVelocityThreshold
                ? ApexGravityScale
                : 1f;
            velocity.Y = AdvanceVerticalSpeed(velocity.Y, Gravity * gravityScale, fixedDeltaSeconds);
            position += velocity * fixedDeltaSeconds;

            if (step % 2 == 1)
                points.Add(position);
            if (step > 0 && position.Y <= 0f)
                break;
        }

        return [.. points];
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }

    private static bool IsDesignUnitMultiple(float value)
    {
        var increments = value / DesignUnit;
        return MathF.Abs(increments - MathF.Round(increments)) < 0.001f;
    }

    private static bool IsHalfDesignUnitMultiple(float value) =>
        IsDesignUnitMultiple(value * 2f);
}

public readonly record struct JumpProfile2D(float ApexHeight, float TimeToApex, float Airtime, float HorizontalDistance);
