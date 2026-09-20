using App2d.Core.Geometry;

namespace App2d.Gameplay.Combat;

/// <summary>Pose-derived damage geometry, independent of the stable terrain movement body.</summary>
public interface IAuthoredHurt2D
{
    bool OverlapsHurt(Bounds2D hit);
}
