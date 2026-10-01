using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Gameplay.Combat;

/// <summary>A localized guard consumes a registered physical strike without accepting damage.</summary>
public interface ICombatGuard2D
{
    bool OverlapsGuard(Bounds2D attackBounds);
    bool CanBlock(Bounds2D attackBounds, Vector2 incomingDirection, Vector2? attackerPosition);
    bool TryBlock(Bounds2D attackBounds, Vector2 incomingDirection, Vector2? attackerPosition);
}
