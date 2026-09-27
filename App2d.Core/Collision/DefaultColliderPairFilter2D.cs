using App2d.Core.Collision.Filtering;

namespace App2d.Core.Collision;

public sealed class DefaultColliderPairFilter2D : IPairFilter2D<Collider2D>
{
    public bool ShouldTest(Collider2D first, Collider2D second) =>
        first.CanCollideWith(second);
}
