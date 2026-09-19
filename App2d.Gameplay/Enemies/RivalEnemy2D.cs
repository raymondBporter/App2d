using App2d.Collision;
using App2d.Core;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Physics;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

/// <summary>A hostile person: shared simulation and actions, AI commands.</summary>
public sealed partial class RivalEnemy2D : IEnemyActor2D
{
    private readonly List<EnemyEvent2D> _events = [];
    private readonly UnarmedPersonActions2D _actions;
    private readonly RivalBrain2D _brain;
    private float _lastDeltaSeconds;
    private float _lastMoveX;
    private bool _simulationEnabled = true;

    public RivalEnemy2D(
        EntityIdAllocator2D ids,
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        TraversalMetrics2D traversal,
        CombatSystem2D combat,
        Vector2 position,
        float minimumX,
        float maximumX,
        uint worldLayer,
        uint playerLayer,
        uint enemyLayer)
    {
        ArgGuard.ThrowIfNull(ids);
        ArgGuard.ThrowIfNull(collision);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(traversal);
        ArgGuard.ThrowIfNull(combat);
        ArgGuard.ThrowIfNotFinite(position);
        ArgGuard.ThrowIfGreaterThanOrEqual(minimumX, maximumX);
        Person = new Person2D(
            ids.Allocate(),
            collision,
            physics,
            traversal,
            position,
            enemyLayer,
            worldLayer,
            CombatFaction2D.Enemy,
            maximumHealth: 12,
            mass: 1f);
        _actions = new UnarmedPersonActions2D(
            ids,
            Person.Body,
            CombatFaction2D.Enemy,
            playerLayer,
            combat);
        _brain = new RivalBrain2D(minimumX, maximumX);
        Person.AttachActions(_actions);

        _actions.AttackStarted += (kind, duration) => _events.Add(
            new RivalAttackStarted2D(Person.Id, Person.Position, kind, duration));
        Person.Damaged += () => _events.Add(new RivalDamaged2D(Person.Id, Person.Position));
        Person.Died += () => _events.Add(new RivalDied2D(Person.Id, Person.Position));
    }

    public Person2D Person { get; }
    public ICombatant2D Combatant => Person;

    public void SetSimulationEnabled(bool isEnabled)
    {
        _simulationEnabled = isEnabled;
        Person.SetSimulationEnabled(isEnabled);
    }

    public void Update(float deltaSeconds, Vector2 targetPosition)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        ArgGuard.ThrowIfNotFinite(targetPosition);
        _lastDeltaSeconds = deltaSeconds;
        Person.BeginFrame(deltaSeconds);

        if (!_simulationEnabled || !Person.IsAlive)
            return;

        var command = _brain.Decide(
            Person,
            targetPosition,
            deltaSeconds);
        Person.ApplyCommand(command, deltaSeconds);
        _lastMoveX = command.MoveX;
    }

    public void SyncAfterPhysics()
    {
        if (!_simulationEnabled)
            return;

        Person.UpdateAfterPhysics(_lastDeltaSeconds);
    }

    public EnemyState2D CaptureState() => new(Person.Id, EnemyKind2D.Rival,
        Person.Position, Person.Body.LinearVelocity, 0f, Person.Facing, _simulationEnabled, Person.IsAlive)
    { Person = Person.CaptureState(), MoveX = _lastMoveX, IsAttacking = _actions.IsAttackActive };

    public ImmutableArray<EnemyEvent2D> DrainEvents()
    {
        var result = _events.ToImmutableArray();
        _events.Clear();
        return result;
    }
}
