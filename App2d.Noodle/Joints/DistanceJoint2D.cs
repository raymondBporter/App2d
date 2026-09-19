using System.Numerics;

namespace App2d.Noodle.Joints;

/// <summary>An annular distance constraint; equal limits make a rod, zero minimum makes a rope.</summary>
internal sealed class DistanceJoint2D : IJoint2D
{
    public DistanceJoint2D(float minimumDistance, float maximumDistance)
    {
        if (!float.IsFinite(minimumDistance) || minimumDistance < 0f)
            throw new ArgumentOutOfRangeException(nameof(minimumDistance));
        if (!float.IsFinite(maximumDistance) || maximumDistance < minimumDistance)
            throw new ArgumentOutOfRangeException(nameof(maximumDistance));

        MinimumDistance = minimumDistance;
        MaximumDistance = maximumDistance;
    }

    public string Name => MinimumDistance == MaximumDistance ? "DISTANCE / ROD" : "DISTANCE / ROPE";
    public string Constraint => MinimumDistance == MaximumDistance ? "r = L" : "min <= r <= max";
    public float MinimumDistance { get; }
    public float MaximumDistance { get; }

    public Vector2 Constrain(Vector2 parentAnchor, Vector2 requestedChildAnchor)
    {
        var delta = requestedChildAnchor - parentAnchor;
        var distance = delta.Length();
        if (distance <= float.Epsilon)
        {
            return MinimumDistance > 0f
                ? parentAnchor + Vector2.UnitX * MinimumDistance
                : parentAnchor;
        }

        return parentAnchor + delta / distance * Math.Clamp(distance, MinimumDistance, MaximumDistance);
    }
}
