using App2d.Core.Characters.Authored;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Objects;

/// <summary>Traversal policy applied by the hosting game; not part of the mathematical shape.</summary>
public sealed record ObjectSurface2D
{
    public Vector2? OneWayNormal { get; init; }
    public float OneWaySlop { get; init; } = 2f;

    public void Validate()
    {
        ArgGuard.NonNegative(OneWaySlop);
        if (OneWayNormal is { } normal)
        {
            ArgGuard.Finite(normal);
            ArgGuard.Positive(normal.Length(), nameof(OneWayNormal));
        }
    }
}

/// <summary>Independent placement identity and world origin; the reusable asset remains in local coordinates.</summary>
public sealed record ObjectPlacement2D(string Id, Part Asset, Vector2 Position)
{
    public void Validate()
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(Id);
        ArgGuard.ThrowIfNull(Asset);
        ArgGuard.Finite(Position);
        Asset.Validate();
    }
}
