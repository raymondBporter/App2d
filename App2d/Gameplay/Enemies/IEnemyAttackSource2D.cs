using App2d.Core.Spatial;
using App2d.Gameplay.Persons;

namespace App2d.Gameplay.Enemies;

public interface IEnemyAttackSource2D
{
    bool TryResolvePlayerHit(Person2D player);

    IEnumerable<SpatialObject2D> GetActiveAttackHitboxes();
}
