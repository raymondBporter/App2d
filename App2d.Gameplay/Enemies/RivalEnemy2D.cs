using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using SkiaSharp;
using System.Numerics;

namespace App2d.Gameplay;

/// <summary>A hostile person: shared simulation and actions, AI commands.</summary>
public sealed class RivalEnemy2D : IEnemyActor2D, IDisposable
{
    private readonly PersonPresentation2D _presentation;
    private readonly PersonArsenal2D _arsenal;
    private readonly RivalBrain2D _brain;
    private readonly WorldObject2D _hostileMarker;
    private float _lastDeltaSeconds;
    private float _lastMoveX;
    private long _frameNumber;
    private bool _simulationEnabled = true;
    private bool _disposed;

    public RivalEnemy2D(
        Scene2D scene,
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        TextureCache2D textures,
        TraversalMetrics2D traversal,
        CombatSystem2D combat,
        Vector2 position,
        float minimumX,
        float maximumX,
        uint worldLayer,
        uint playerLayer,
        uint enemyLayer,
        ISoundEffectSink2D sounds)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(collision);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNull(traversal);
        ArgGuard.ThrowIfNull(combat);
        ArgGuard.ThrowIfNotFinite(position);
        ArgGuard.ThrowIfGreaterThanOrEqual(minimumX, maximumX);
        ArgGuard.ThrowIfNull(sounds);

        Person = new Person2D(
            collision,
            physics,
            traversal,
            position,
            enemyLayer,
            worldLayer,
            CombatFaction2D.Enemy,
            maximumHealth: 12,
            mass: 1f);
        _presentation = new PersonPresentation2D(scene, textures, traversal);
        _arsenal = new PersonArsenal2D(
            scene,
            Person.Body,
            textures,
            collision,
            worldLayer,
            playerLayer,
            CombatFaction2D.Enemy,
            combat,
            sounds);
        _brain = new RivalBrain2D(minimumX, maximumX);
        Person.AttachActions(_arsenal);

        _arsenal.EquipmentChanged += _presentation.EquipRightHandWeapon;
        _arsenal.MeleeAttackStarted += _presentation.PlayMeleeAttack;
        _arsenal.ShotStarted += _presentation.PlayShot;
        _presentation.EquipRightHandWeapon(_arsenal.EquipmentId);
        Person.Damaged += _presentation.PlayHit;
        Person.Died += HandleDeath;

        _hostileMarker = new WorldObject2D(
            new Circle2D(8f),
            new SolidColorShader(new SKColor(255, 46, 166)))
        {
            ZIndex = 3
        };
        scene.Add(_hostileMarker);
        SyncPresentation();
    }

    public Person2D Person { get; }
    public ICombatant2D Combatant => Person;

    public void SetSimulationEnabled(bool isEnabled)
    {
        _simulationEnabled = isEnabled;
        Person.SetSimulationEnabled(isEnabled);
        _presentation.SetVisible(isEnabled);
        _hostileMarker.IsVisible = isEnabled && Person.IsAlive;
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
            _arsenal,
            targetPosition,
            deltaSeconds);
        Person.ApplyCommand(command, deltaSeconds);
        _lastMoveX = command.Movement.MoveX;
    }

    public void SyncAfterPhysics()
    {
        if (!_simulationEnabled)
            return;

        Person.UpdateAfterPhysics(_lastDeltaSeconds);
        _presentation.Update(
            _lastDeltaSeconds,
            ++_frameNumber,
            Person,
            _lastMoveX,
            isShieldBlocking: false,
            _arsenal.IsMeleeAttackActive);
        SyncPresentation();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _presentation.Dispose();
        GC.SuppressFinalize(this);
    }

    private void HandleDeath()
    {
        _presentation.PlayDeath();
        _hostileMarker.IsVisible = false;
    }

    private void SyncPresentation()
    {
        _hostileMarker.Transform.Position =
            Person.Position + new Vector2(0f, 34f);
        _hostileMarker.IsVisible =
            _simulationEnabled && Person.IsAlive;
    }
}
