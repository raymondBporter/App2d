using App2d.Core;

namespace App2d.Gameplay;

public interface IEnemyAttackSource2D
{
    bool TryResolvePlayerHit(PlayerCharacter2D player);

    IEnumerable<SpatialObject2D> GetActiveAttackHitboxes();
}
