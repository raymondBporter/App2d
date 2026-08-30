using App2d.Core;

namespace App2d.Gameplay;

public interface IEnemyAttackSource2D
{
    bool TryResolvePlayerHit(Person2D player);

    IEnumerable<SpatialObject2D> GetActiveAttackHitboxes();
}
