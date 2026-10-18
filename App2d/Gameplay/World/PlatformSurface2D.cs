using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>Side-scroller traversal metadata, separate from the engine's rigid body.</summary>
public sealed class PlatformSurface2D
{
    private Vector2? _oneWaySurfaceNormal;

    /// <summary>World-space outward normal of the blocking face; null makes every face solid.</summary>
    public Vector2? OneWaySurfaceNormal
    {
        get => _oneWaySurfaceNormal;
        set
        {
            if (value is not { } normal)
            {
                _oneWaySurfaceNormal = null;
                return;
            }
            ArgGuard.Finite(normal);
            var length = ArgGuard.Positive(normal.Length(), nameof(OneWaySurfaceNormal));
            _oneWaySurfaceNormal = ArgGuard.Finite(normal / length);
        }
    }

    public float OneWaySlop
    {
        get;
        set => field = ArgGuard.NonNegative(value);
    } = 2f;

    public bool IsWallGrippable { get; set; }
}
