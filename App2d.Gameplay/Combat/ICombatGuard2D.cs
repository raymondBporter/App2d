using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Gameplay.Combat;

/// <summary>A localized guard consumes a registered physical strike without accepting damage.</summary>
public interface ICombatGuard2D
{
    bool OverlapsGuard(Rect2D attackBounds);
    bool CanBlock(Rect2D attackBounds, Vector2 incomingDirection, Vector2? attackerPosition);
    bool TryBlock(Rect2D attackBounds, Vector2 incomingDirection, Vector2? attackerPosition);
}
