using App2d.Core.Geometry.Primitives;

namespace App2d.Gameplay.Combat;

/// <summary>A localized guard consumes a registered physical strike without accepting damage.</summary>
public interface ICombatGuard2D
{
    bool OverlapsGuard(Rect2f attackBounds);
    bool CanBlock(Rect2f attackBounds, Vector2f incomingDirection, Vector2f? attackerPosition);
    bool TryBlock(Rect2f attackBounds, Vector2f incomingDirection, Vector2f? attackerPosition);
}
