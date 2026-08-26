using System.Numerics;

namespace App2d.Gameplay;

public sealed class TraversalMetrics2D
{
    public const float DesignUnit = 8f;

    public float TileSize { get; init; } = DesignUnit * 4f;
    public Vector2 PlayerColliderSize { get; init; } =
        new(DesignUnit * 7f, DesignUnit * 11f);
    public Vector2 PlayerVisualSize { get; init; } = new(184f, 138f);
    public Vector2 PlayerVisualOffset { get; init; } = new(0f, 3.5f);
    public float RunSpeed { get; init; } = 430f;
    public float GroundAcceleration { get; init; } = 3_600f;
    public float AirAcceleration { get; init; } = 1_450f;
    public float Gravity { get; init; } = 1_900f;
    public float JumpSpeed { get; init; } = 760f;
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
    public float GrappleReach { get; init; } = 430f;
    public float GrappleAimAssist { get; init; } = 6f;
    public float GrappleRangeGrace { get; init; } = 8f;

    public int StandingPassageTiles =>
        (int)MathF.Ceiling(PlayerColliderSize.Y / TileSize);
    public float StandingClearance =>
        StandingPassageTiles * TileSize - PlayerColliderSize.Y;
    public int ReliableJumpRiseTiles { get; init; } = 4;

    public void ValidateScaleContract()
    {
        ArgGuard.ThrowIfNotPositive(TileSize);
        ArgGuard.ThrowIfNotPositive(PlayerColliderSize);
        ArgGuard.ThrowIfNotPositive(PlayerVisualSize);
        ArgGuard.ThrowIfNotFinite(PlayerVisualOffset);

        StateGuard.ThrowIf(
            !IsDesignUnitMultiple(TileSize) ||
            !IsDesignUnitMultiple(PlayerColliderSize.X) ||
            !IsDesignUnitMultiple(PlayerColliderSize.Y),
            $"Tile and player collider dimensions must use the {DesignUnit:0}-unit design grid.");
        StateGuard.ThrowIf(
            StandingClearance < DesignUnit,
            $"The minimum whole-tile standing passage must leave at least " +
            $"{DesignUnit:0} units of clearance.");

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
}

public readonly record struct JumpProfile2D(
    float ApexHeight,
    float TimeToApex,
    float Airtime,
    float HorizontalDistance);
