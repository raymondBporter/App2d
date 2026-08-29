using App2d.Engine;

namespace App2d.Gameplay;

public interface IEnemyAttackSource2D
{
    bool TryResolvePlayerHit(PlayerCharacter2D player);

    IEnumerable<SpatialObject2D> GetActiveAttackHitboxes();
}
