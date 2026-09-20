using App2d.Core.Characters;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

internal sealed class AuthoredMelee2D(EntityTypeDefinition type, PointLibrary library, float halfHeight)
{
    private readonly EntityPose _pose = new(library);
    public EntityAction Action => type.Actions["attack"];
    public IShape2D Shape => AxisAlignedRectangle2D.FromSize(new Vector2(Action.HitWidth, Action.HitHeight) * EntityCatalog.WorldUnits);
    public MeleeAttackProfile2D Profile => new(Action.Duration, Action.ActiveStart * Action.Duration,
        Action.ActiveEnd * Action.Duration, .1f, 0);
    public Vector2 Offset(float seconds, float facing)
    {
        _pose.Evaluate(type, Action, seconds, facing < 0);
        return Bounds2D.FromPoints(_pose.Hit.Points.ToArray()).Center * EntityCatalog.WorldUnits - new Vector2(0, halfHeight);
    }
}
