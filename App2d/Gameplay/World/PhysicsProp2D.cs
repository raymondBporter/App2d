using App2d.Contracts.Combat;
using App2d.Contracts.Enemies;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Physics;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>
/// A passive physics prop loaded from the authored catalog; rendering uses the same shape asset.
/// </summary>
internal sealed class PhysicsProp2D : IEnemyActor2D, ICombatant2D
{
    private bool _simulationEnabled = true;
    private readonly Dictionary<EntityId2D, int> _lastAttackIds = [];

    public PhysicsProp2D(EntityId2D id, PhysicsPropAsset asset, PhysicsWorld2D physics, Vector2 position, uint worldLayer, uint propLayer)
    {
        ArgGuard.ThrowIf(!id.IsValid, "A prop requires a valid entity ID.", nameof(id));
        Id = id;
        Asset = ArgGuard.RequireNotNull(asset);
        asset.Validate();
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.Finite(position);

        var visual = new SpatialObject2D(asset.Shape);
        visual.Transform.Position = position;
        WorldObject = visual;

        Body = asset.Body.CreateBody(physics, visual);
        Body.EntityId = Id;
        Body.CollisionLayer = propLayer;
        Body.CollisionMask = worldLayer;
        Health = new Health2D(1_000_000);
    }

    public EntityId2D Id { get; }
    public PhysicsPropAsset Asset { get; }
    public SpatialObject2D WorldObject { get; }
    public RigidBody2D Body { get; }
    public Health2D Health { get; }
    public CombatFaction2D Faction => CombatFaction2D.Neutral;
    public bool IsAlive => true;
    public ICombatant2D Combatant => this;

    public EnemyState2D CaptureState() => new(Id, EnemyKind2D.PhysicsProp, WorldObject.Transform.Position,
        Body.LinearVelocity, WorldObject.Transform.Rotation, 1f, _simulationEnabled, true)
    {
        TypeId = Asset.Id,
        PhysicsProp = Asset
    };

    public void SetSimulationEnabled(bool isEnabled)
    {
        _simulationEnabled = isEnabled;
        Body.IsCollider = isEnabled;
        Body.MotionType = isEnabled ? Asset.Body.MotionType : BodyMotionType2D.Static;
        if (!isEnabled)
        {
            Body.LinearVelocity = Vector2.Zero;
            Body.AngularVelocity = 0f;
        }
    }

    public void Update(float deltaSeconds, Vector2 targetPosition)
    {
    }

    public void SyncAfterPhysics()
    {
    }

    public bool TryRegisterHit(EntityId2D attackSourceId, int attackId)
    {
        ArgGuard.ThrowIf(!attackSourceId.IsValid, "An attack source ID is required.", nameof(attackSourceId));
        if (_lastAttackIds.TryGetValue(attackSourceId, out var lastAttackId) && lastAttackId == attackId)
        {
            return false;
        }

        _lastAttackIds[attackSourceId] = attackId;
        return true;
    }

    public bool TakeDamage(int damage, Vector2 knockback)
    {
        Body.LinearVelocity += knockback * Asset.HitVelocityScale;
        Body.AngularVelocity += -MathF.Sign(knockback.X) * Asset.HitAngularKick;
        return true;
    }
}
