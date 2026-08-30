using System.Numerics;

namespace App2d.Gameplay;

public sealed class TraversalMetrics2D
{
    public const float DesignUnit = 8f;

    private TraversalMetrics2D()
    {
    }

    public static TraversalMetrics2D FromPlayerAsset(string contentRoot)
    {
        var geometry = PlayerGeometryAssets2D.Load(contentRoot);
        return new TraversalMetrics2D
        {
            PlayerColliderSize = geometry.StandingColliderSize,
            PlayerColliderCenterOffsetX = geometry.ColliderCenterOffsetX,
            PlayerVisualSize = geometry.VisualSize,
            PlayerSpriteFootYFraction = geometry.FootAnchorYFraction
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
    public float RunSpeed { get; init; } = 430f;
    public float GroundAcceleration { get; init; } = 3_600f;
    public float AirAcceleration { get; init; } = 1_450f;
    public float Gravity { get; init; } = 1_900f;
    public float JumpSpeed { get; init; } = 760f;
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
    public float GroundProbeDistance { get; init; } = 2f;
    public float LandingSnapDistance { get; init; } = 4f;
    public float HorizontalSupportGrace { get; init; } = 2f;
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
        ArgGuard.ThrowIfNotPositive(TileSize);
        ArgGuard.ThrowIfNotPositive(PlayerColliderSize);
        ArgGuard.ThrowIfNotFinite(PlayerColliderCenterOffsetX);
        ArgGuard.ThrowIfNotPositive(PlayerVisualSize);
        ArgGuard.ThrowIfNotFinite(PlayerVisualOffset);
        ArgGuard.ThrowIfNotPositive(AirJumpSpeedMultiplier);
        ArgGuard.ThrowIfNotPositive(MaximumJumpCount);
        ArgGuard.ThrowIfNotPositive(OneWayDropSpeed);
        ArgGuard.ThrowIfNotPositive(WallGripProbeDistance);
        ArgGuard.ThrowIfNotPositive(WallGripMinimumOverlap);
        ArgGuard.ThrowIfNotPositive(WallJumpHorizontalSpeed);
        ArgGuard.ThrowIfNotPositive(WallJumpRelatchDelay);
        ArgGuard.ThrowIfNotPositive(DashSpeed);
        ArgGuard.ThrowIfNotPositive(DashDuration);
        ArgGuard.ThrowIfNotPositive(DashCooldown);

        StateGuard.ThrowIf(AirJumpSpeedMultiplier >= 1f, "The air-jump speed multiplier must be less than one.");
        StateGuard.ThrowIf(
            !float.IsFinite(PlayerSpriteFootYFraction) ||
            PlayerSpriteFootYFraction <= 0f ||
            PlayerSpriteFootYFraction >= 1f,
            "The player sprite foot anchor must be a fraction between zero and one.");

        StateGuard.ThrowIf(
            !IsDesignUnitMultiple(TileSize) ||
            !IsHalfDesignUnitMultiple(PlayerColliderSize.Y),
            $"Tile and player collider heights must use half increments of the {DesignUnit:0}-unit design grid.");
        StateGuard.ThrowIf(StandingClearance < DesignUnit, $"The minimum whole-tile standing passage must leave at least {DesignUnit:0} units of clearance.");

        ArgGuard.ThrowIfNotPositive(ReliableJumpRiseTiles);
        var requiredJumpHeight = TileSize * ReliableJumpRiseTiles + DesignUnit;
        var standingJump = MeasureJump(0f);
        StateGuard.ThrowIf(
            standingJump.ApexHeight < requiredJumpHeight,
            $"A held jump must clear {ReliableJumpRiseTiles} tiles plus " +
            $"{DesignUnit:0} units of margin.");
    }

    public JumpProfile2D MeasureJump(float initialHorizontalSpeed, float fixedDeltaSeconds = 1f / 120f)
    {
        ValidateFixedDelta(fixedDeltaSeconds);

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
            velocity.Y = Math.Max(
                velocity.Y - Gravity * gravityScale * fixedDeltaSeconds,
                -MaximumFallSpeed);
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
        ValidateFixedDelta(fixedDeltaSeconds);

        var points = new List<Vector2> { Vector2.Zero };
        var position = Vector2.Zero;
        var velocity = new Vector2(initialHorizontalSpeed, JumpSpeed);

        for (var step = 0; step < 1_200; step++)
        {
            velocity.X = MoveTowards(velocity.X, RunSpeed, AirAcceleration * fixedDeltaSeconds);
            var gravityScale = MathF.Abs(velocity.Y) < ApexVelocityThreshold
                ? ApexGravityScale
                : 1f;
            velocity.Y = Math.Max(
                velocity.Y - Gravity * gravityScale * fixedDeltaSeconds,
                -MaximumFallSpeed);
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

    private static void ValidateFixedDelta(float fixedDeltaSeconds)
        => ArgGuard.ThrowIfNotPositive(fixedDeltaSeconds);

    private static bool IsDesignUnitMultiple(float value)
    {
        var increments = value / DesignUnit;
        return MathF.Abs(increments - MathF.Round(increments)) < 0.001f;
    }

    private static bool IsHalfDesignUnitMultiple(float value) =>
        IsDesignUnitMultiple(value * 2f);
}

public readonly record struct JumpProfile2D(
    float ApexHeight,
    float TimeToApex,
    float Airtime,
    float HorizontalDistance);
