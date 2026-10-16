using App2d.Core.Validation;

namespace App2d.Presentation.World.Presentation;

/// <summary>
/// A presentation-only reaction clock. Smooth speed lobes lose and recover equal time; an optional initial hold
/// starts at minimum speed immediately, then eases up into recovery.
/// The result never runs backwards or ahead of simulation, and returns exactly to live time.
/// </summary>
public sealed class ReactionTimeCurve2D
{
    public ReactionTimeCurve2D(float minimumSpeed, float slowdownSeconds, float recoveryPeak, float holdSeconds = 0)
    {
        ArgGuard.InRange(minimumSpeed, 0f, 1f);
        ArgGuard.Positive(slowdownSeconds);
        ArgGuard.GreaterThan(recoveryPeak, 1f);
        ArgGuard.NonNegative(holdSeconds);

        MinimumSpeed = minimumSpeed;
        SlowdownSeconds = slowdownSeconds;
        RecoveryPeak = recoveryPeak;
        HoldSeconds = holdSeconds;
        RecoverySeconds = holdSeconds > 0
            ? (1 - minimumSpeed) * (holdSeconds + slowdownSeconds / 2) * MathF.PI / (2 * (recoveryPeak - 1))
            : (1 - minimumSpeed) * slowdownSeconds / (recoveryPeak - 1);
    }

    public float MinimumSpeed { get; }
    public float SlowdownSeconds { get; }
    public float RecoveryPeak { get; }
    public float RecoverySeconds { get; }
    public float HoldSeconds { get; }
    public float Duration => HoldSeconds + SlowdownSeconds + RecoverySeconds;

    public double Sample(double elapsed)
    {
        ArgGuard.NonNegative(elapsed);

        if (elapsed >= Duration || MinimumSpeed == 1)
        {
            return elapsed;
        }

        if (HoldSeconds > 0)
        {
            if (elapsed <= HoldSeconds)
            {
                return elapsed * MinimumSpeed;
            }

            var t = elapsed - HoldSeconds;
            var debt = (1 - MinimumSpeed) * (HoldSeconds + SlowdownSeconds / 2);
            var heldDelay = t < SlowdownSeconds
                ? (1 - MinimumSpeed) * (HoldSeconds + t / 2 + SlowdownSeconds / (2 * Math.PI) * Math.Sin(Math.PI * t / SlowdownSeconds))
                : debt / 2 * (1 + Math.Cos(Math.PI * (t - SlowdownSeconds) / RecoverySeconds));
            return Math.Clamp(elapsed - heldDelay, 0, elapsed);
        }
        var lost = (1 - MinimumSpeed) * SlowdownSeconds / Math.PI;
        var delay = elapsed < SlowdownSeconds
            ? lost * (1 - Math.Cos(Math.PI * elapsed / SlowdownSeconds))
            : lost * (1 + Math.Cos(Math.PI * (elapsed - SlowdownSeconds) / RecoverySeconds));
        return Math.Clamp(elapsed - delay, 0, elapsed);
    }

    public double Speed(double elapsed)
    {
        if (elapsed < 0 || elapsed >= Duration || MinimumSpeed == 1)
        {
            return 1;
        }

        if (HoldSeconds > 0)
        {
            if (elapsed <= HoldSeconds)
            {
                return MinimumSpeed;
            }

            var t = elapsed - HoldSeconds;
            return t < SlowdownSeconds
                ? 1 - (1 - MinimumSpeed) * (1 + Math.Cos(Math.PI * t / SlowdownSeconds)) / 2
                : 1 + (RecoveryPeak - 1) * Math.Sin(Math.PI * (t - SlowdownSeconds) / RecoverySeconds);
        }
        return elapsed < SlowdownSeconds
            ? 1 - (1 - MinimumSpeed) * Math.Sin(Math.PI * elapsed / SlowdownSeconds)
            : 1 + (RecoveryPeak - 1) * Math.Sin(Math.PI * (elapsed - SlowdownSeconds) / RecoverySeconds);
    }
}
