using System.Numerics;
using App2d.Collision;
using App2d.Core;
using App2d.Gameplay;
using App2d.Gameplay.Audio;
using App2d.Physics;
using App2d.Rendering;
using SkiaSharp;

namespace App2d;

public sealed class SideScrollerGame : Game2D
{
    private const uint WorldLayer = 1u << 0;
    private const uint PlayerLayer = 1u << 1;
    private const uint EnemyLayer = 1u << 2;

    private static readonly TraversalMetrics2D Traversal =
        TraversalMetrics2D.FromPlayerAsset(
            Path.Combine(AppContext.BaseDirectory, "Assets"));

    private readonly CollisionSystem2D _collision = new();
    private readonly PhysicsWorld2D _physics;
    private readonly SideScrollerLevel2D _level;
    private readonly PlayerInputMapper2D _inputMapper = new();
    private readonly PlayerCharacter2D _player;
    private readonly PlayerPresentation2D _playerPresentation;
    private readonly CombatSystem2D _combat;
    private readonly PlayerArsenal2D _arsenal;
    private readonly SideScrollerCamera2D _cameraController;
    private readonly TraversalDebugRenderer2D _traversalDebug = new(Traversal);
    private readonly SoundEffectBank2D _sounds;
    private bool _reachedGoal;
    private bool _showTraversalDebug;

    public SideScrollerGame()
    {
        _physics = new PhysicsWorld2D(_collision)
        {
            Gravity = new Vector2(0f, -Traversal.Gravity),
            MaxSubstepSeconds = 1f / 120f,
            PositionIterations = 3,
            VelocityIterations = 2
        };
        Traversal.ValidateScaleContract();
        _sounds = new SoundEffectBank2D(Path.Combine(AppContext.BaseDirectory, "Assets", "audio", "sfx"));
        RegisterDebugPhysicsWorld(_physics);
        DeveloperConsole.RegisterVariable("sfx_volume", () => _sounds.Volume, value => _sounds.Volume = value, "Set sound-effect volume from 0 (muted) to 1 (full volume).");
        DeveloperConsole.RegisterVariable("draw_traversal_metrics", () => _showTraversalDebug, value => _showTraversalDebug = value, "Draw jump arcs and tile-relative movement metrics.");

        _level = new SideScrollerLevel2D(Traversal);
        _cameraController = new SideScrollerCamera2D(Scene, Camera, _level.TileMap.WorldBounds, _level.SpawnPoint, _level.GetCameraFloorY);

        _level.CreateEnvironment(Scene, _collision, _physics, Textures, WorldLayer, PlayerLayer, EnemyLayer);

        _player = new PlayerCharacter2D(_collision, _physics, Traversal, _level.SpawnPoint, PlayerLayer, WorldLayer, EnemyLayer, _sounds);
        _playerPresentation = new PlayerPresentation2D(Scene, Textures, Traversal);
        _player.Damaged += _playerPresentation.PlayHit;

        _level.CreateMechanicsPlaygroundEnemies(Textures, _sounds);
        _combat = new CombatSystem2D(_collision, EnemyLayer, _sounds);
        _arsenal = new PlayerArsenal2D(Scene, _player.Body, Textures, _collision, WorldLayer, _combat, _playerPresentation, _sounds);
        RegisterDebugAttackShapes(_arsenal.GetActiveAttackHitboxes);
        RegisterDebugAttackShapes(_level.EnemySystem.GetActiveAttackHitboxes);

        _playerPresentation.Update(0f, 0, _player.Position, 0f, _player.Facing, _player.IsGrounded, _player.IsWallGripping, _player.IsDashing, isShieldBlocking: false, _player.Body.LinearVelocity.Y, _player.LandingSpeedThisFrame, _arsenal.IsMeleeAttackActive, _player.InvulnerabilitySeconds);
    }

    public override string WindowTitle =>
        $"App2d Side Scroller | PAD: {(_inputMapper.IsControllerConnected ? "XBOX" : "OFF")} | Q/Y: switch weapon | J/CLICK or X: attack | A: jump | Shift/controller B: dash | keyboard B: shield block | WEAPON: {_arsenal.WeaponName} | HP: {_player.Health.Current}/{_player.Health.Maximum} | enemies: {_combat.DefeatedEnemies}/{_level.EnemySystem.Count} | chunks: {_level.ActiveChunkCount}/{SideScrollerLevel2D.MaximumActiveChunkCount} | colliders: {_level.LoadedColliderCount} | broad pairs: {_physics.LastCandidatePairCount}{(_reachedGoal ? " | GOAL! BRO!" : string.Empty)}";

    public override void Update(FrameTime time, InputState input)
    {
        var dt = time.DeltaSeconds;
        _level.UpdateStreaming(_player.Position);
        _player.BeginFrame(dt);
        _arsenal.BeginFrame(dt);

        var command = _inputMapper.Capture(input, Camera, _player.Position);
        if (command.ToggleTraversalDebug)
            _showTraversalDebug = !_showTraversalDebug;
        if (command.SwitchWeapon)
            _arsenal.SelectNextWeapon();

        _level.EnemySystem.Update(dt, _player.Position);

        _player.UpdateBeforePhysics(command.Movement, dt);
        if (command.UseWeapon)
            _player.Face(_arsenal.UseWeapon(command.AimTarget, _player.Facing));

        _arsenal.UpdateBeforePhysics(dt);
        _physics.Step(dt);
        _player.UpdateAfterPhysics(dt);
        _level.EnemySystem.SyncAfterPhysics();

        _arsenal.UpdateAfterPhysics(dt, _player.Facing);
        var playerDefeated = _level.EnemySystem.TryResolvePlayerHits(_player);

        if (playerDefeated ||
            _player.ResolveEnemyTouches())
        {
            _player.Health.Reset();
            Respawn();
        }

        if (_player.Position.Y < _level.TileMap.WorldBounds.Min.Y - 260f)
            Respawn();
        if (!_reachedGoal && _player.Position.X >= _level.GoalX)
        {
            _reachedGoal = true;
            _sounds.Play(SoundEffect2D.GoalReached);
        }

        _playerPresentation.Update(
            dt,
            time.FrameNumber,
            _player.Position,
            command.Movement.MoveX,
            _player.Facing,
            _player.IsGrounded,
            _player.IsWallGripping,
            _player.IsDashing,
            input.IsKeyDown(Keys.B),
            _player.Body.LinearVelocity.Y,
            _player.LandingSpeedThisFrame,
            _arsenal.IsMeleeAttackActive,
            _player.InvulnerabilitySeconds);
        _arsenal.ReleasePendingWeapons(_player.Facing);
        _cameraController.Update(_player.Position, _player.Body.LinearVelocity, _player.IsGrounded, dt);
    }

    public override void Render(Renderer2D renderer)
    {
        renderer.Clear(new SKColor(103, 196, 235));
        renderer.Draw(Scene);
        PlayerHud2D.Draw(renderer, _player.Health.Current, _player.Health.Maximum, _arsenal.WeaponHudTexture, _arsenal.WeaponStatus);

        if (_showTraversalDebug)
            _traversalDebug.Draw(renderer, _player.Position, _player.Facing);
    }

    private void Respawn()
    {
        _player.Reset(_level.SpawnPoint);
        _arsenal.Reset();
        _playerPresentation.Reset();
        _cameraController.Reset(_level.SpawnPoint);
        _reachedGoal = false;
        _sounds.Play(SoundEffect2D.PlayerRespawn);
    }

    public override void Dispose()
    {
        _playerPresentation.Dispose();
        _sounds.Dispose();
        base.Dispose();
    }
}
