using App2d.Collision;
using App2d.Core;
using App2d.Diagnostics;
using App2d.Editor;
using App2d.Gameplay;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Player;
using App2d.Physics;
using App2d.Rendering;
using App2d.Tiles;
using SkiaSharp;
using System.Numerics;

namespace App2d;

public sealed class SideScrollerGame : Game2D
{
    private const float DeathRestartDelaySeconds = 1.1f;
    private const float HardLandingSpeed = 650f;
    private const uint WorldLayer = 1u << 0;
    private const uint PlayerLayer = 1u << 1;
    private const uint EnemyLayer = 1u << 2;

    private static readonly TraversalMetrics2D Traversal =
        TraversalMetrics2D.FromPlayerAsset(AssetPaths.Root);

    private readonly CollisionSystem2D _collision = new();
    private readonly PhysicsWorld2D _physics;
    private readonly SideScrollerLevel2D _level;
    private readonly PlayerInputMapper2D _inputMapper = new();
    private readonly Person2D _player;
    private readonly PersonPresentation2D _playerPresentation;
    private readonly CombatSystem2D _combat;
    private readonly PersonArsenal2D _arsenal;
    private readonly ContactDamageSystem2D _contactDamage;
    private readonly SideScrollerCamera2D _cameraController;
    private readonly TraversalDebugRenderer2D _traversalDebug = new(Traversal);
    private readonly SoundEffectBank2D _sounds;
    private readonly TileEditor2D _editor;
    private bool _reachedGoal;
    private bool _showTraversalDebug;
    private float _restartDelaySeconds;

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
        _sounds = new SoundEffectBank2D(Path.Combine(AssetPaths.Root, "audio", "sfx"));
        RegisterDebugPhysicsWorld(_physics);
        DeveloperConsole.RegisterVariable("sfx_volume", () => _sounds.Volume, value => _sounds.Volume = value, "Set sound-effect volume from 0 (muted) to 1 (full volume).");
        DeveloperConsole.RegisterVariable("draw_traversal_metrics", () => _showTraversalDebug, value => _showTraversalDebug = value, "Draw jump arcs and tile-relative movement metrics.");
        DeveloperConsole.RegisterCommand(
            "level_rebake",
            "Regenerate the cavern level file from the world generator. Restart to see it.",
            _ =>
            {
                LevelBootstrap2D.Bake(Traversal);
                return ConsoleCommandResult.From($"baked {LevelBootstrap2D.CavernLevelPath}");
            });

        var tileMap = LevelBootstrap2D.LoadOrBake(Traversal);
        var groundHeights = TileGroundHeights2D.Derive(tileMap);

        _level = new SideScrollerLevel2D(
            Traversal,
            tileMap,
            x => groundHeights[Math.Clamp(x, 0, groundHeights.Length - 1)]);
        _cameraController = new SideScrollerCamera2D(Scene, Camera, _level.TileMap.WorldBounds, _level.SpawnPoint, _level.GetCameraFloorY);
        _editor = new TileEditor2D(
            tileMap,
            LevelBootstrap2D.OpenForEditing(),
            Camera,
            tileMap.Origin,
            Traversal.TileSize);

        _level.CreateEnvironment(Scene, _collision, _physics, Textures, WorldLayer, PlayerLayer, EnemyLayer);

        _player = new Person2D(
            _collision,
            _physics,
            Traversal,
            _level.SpawnPoint,
            PlayerLayer,
            WorldLayer,
            CombatFaction2D.Player);
        _playerPresentation = new PersonPresentation2D(Scene, Textures, Traversal);
        _player.JumpStarted += () => _sounds.Play(SoundEffect2D.PlayerJump);
        _player.Landed += speed => _sounds.Play(
            speed >= HardLandingSpeed
                ? SoundEffect2D.PlayerLandHard
                : SoundEffect2D.PlayerLandSoft);
        _player.Footstep += () => _sounds.Play(SoundEffect2D.PlayerFootstep);
        _player.Damaged += () =>
        {
            _sounds.Play(SoundEffect2D.PlayerHurt);
            _playerPresentation.PlayHit();
        };
        _contactDamage = new ContactDamageSystem2D(_collision, EnemyLayer);

        _combat = new CombatSystem2D(_collision, _sounds);
        _level.CreateMechanicsPlaygroundEnemies(Textures, _combat, _sounds);
        _arsenal = new PersonArsenal2D(Scene, _player.Body, Textures, _collision, WorldLayer, EnemyLayer, CombatFaction2D.Player, _combat, _sounds);
        _arsenal.EquipmentChanged += _playerPresentation.EquipRightHandWeapon;
        _arsenal.MeleeAttackStarted += _playerPresentation.PlayMeleeAttack;
        _arsenal.ShotStarted += _playerPresentation.PlayShot;
        _player.AttachActions(_arsenal);
        _playerPresentation.EquipRightHandWeapon(_arsenal.EquipmentId);
        RegisterDebugAttackShapes(_arsenal.GetActiveAttackHitboxes);
        RegisterDebugAttackShapes(_level.EnemySystem.GetActiveAttackHitboxes);

        _playerPresentation.Update(
            0f,
            0,
            _player,
            0f,
            isShieldBlocking: false,
            _arsenal.IsMeleeAttackActive);
    }

    public override string WindowTitle =>
        $"App2d Side Scroller | PAD: {(_inputMapper.IsControllerConnected ? "XBOX" : "OFF")} | Q/Y: switch weapon | J/CLICK or X: attack | A: jump | Shift/controller B: dash | keyboard B: shield block | WEAPON: {_arsenal.WeaponName} | HP: {_player.Health.Current}/{_player.Health.Maximum} | enemies: {_combat.DefeatedEnemies}/{_level.EnemySystem.Count} | chunks: {_level.ActiveChunkCount}/{SideScrollerLevel2D.MaximumActiveChunkCount} | colliders: {_level.LoadedColliderCount} | broad pairs: {_physics.LastCandidatePairCount}{(_reachedGoal ? " | GOAL! BRO!" : string.Empty)}";

    public override void Update(FrameTime time, InputState input)
    {
        var dt = time.DeltaSeconds;

        _editor.Update(input);
        if (_editor.IsActive)
        {
            // Stream around the free camera, not the frozen player, or panning away
            // would paint into chunks that never load.
            _level.UpdateStreaming(_editor.CameraFocus);
            _level.FlushDirtyChunks();
            return;
        }

        _level.UpdateStreaming(_player.Position);
        _level.UpdateMovingPlatforms(dt);
        _player.BeginFrame(dt);

        if (_restartDelaySeconds > 0f)
        {
            UpdateDying(time);
            return;
        }

        var command = _inputMapper.Capture(input, Camera, _player.Position);
        if (command.ToggleTraversalDebug)
            _showTraversalDebug = !_showTraversalDebug;
        _level.EnemySystem.Update(dt, _player.Position);

        _player.ApplyCommand(command.Person, dt);
        _physics.Step(dt);
        _player.UpdateAfterPhysics(dt);
        _level.EnemySystem.SyncAfterPhysics();

        var playerDefeated = _level.EnemySystem.TryResolvePlayerHits(_player);
        if (_level.TryGetSpikeSource(_player.WorldObject.WorldBounds, out var spikeSourceX) &&
            _player.TryTakeDamageFromX(
                damage: 1,
                sourceX: spikeSourceX,
                horizontalKnockback: 180f,
                verticalKnockback: 300f))
        {
            playerDefeated = !_player.Health.IsAlive;
        }

        if (playerDefeated ||
            _contactDamage.Resolve(_player))
        {
            BeginDying(time);
            return;
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
            _player,
            command.Person.Movement.MoveX,
            input.IsKeyDown(Keys.B),
            _arsenal.IsMeleeAttackActive);
        _cameraController.Update(_player.Position, _player.Body.LinearVelocity, _player.IsGrounded, dt);
    }

    public override void Render(Renderer2D renderer)
    {
        renderer.Clear(new SKColor(103, 196, 235));
        renderer.Draw(Scene);
        PlayerHud2D.Draw(renderer, _player.Health.Current, _player.Health.Maximum, _arsenal.WeaponHudTexture, _arsenal.WeaponStatus);

        if (_showTraversalDebug)
            _traversalDebug.Draw(renderer, _player.Position, _player.Facing);

        TileEditorView2D.Draw(renderer, _editor, _level.TileMap.Origin, _level.TileMap.TileSize);
    }

    private void Respawn()
    {
        _restartDelaySeconds = 0f;
        _player.Reset(_level.SpawnPoint);
        _playerPresentation.Reset();
        _cameraController.Reset(_level.SpawnPoint);
        _reachedGoal = false;
        _sounds.Play(SoundEffect2D.PlayerRespawn);
    }

    private void BeginDying(FrameTime time)
    {
        _restartDelaySeconds = DeathRestartDelaySeconds;
        _playerPresentation.PlayDeath();
        UpdateDyingPresentation(time);
    }

    private void UpdateDying(FrameTime time)
    {
        var dt = time.DeltaSeconds;
        _restartDelaySeconds = Math.Max(0f, _restartDelaySeconds - dt);

        _level.EnemySystem.Update(dt, _player.Position);
        _physics.Step(dt);
        _player.UpdateAfterPhysics(dt);
        _level.EnemySystem.SyncAfterPhysics();
        UpdateDyingPresentation(time);
        _cameraController.Update(_player.Position, _player.Body.LinearVelocity, _player.IsGrounded, dt);

        if (_restartDelaySeconds <= 0f)
            Respawn();
    }

    private void UpdateDyingPresentation(FrameTime time)
    {
        _playerPresentation.Update(
            time.DeltaSeconds,
            time.FrameNumber,
            _player,
            0f,
            isShieldBlocking: false,
            isMeleeAttackActive: false);
    }

    public override void Dispose()
    {
        _editor.Dispose();
        _playerPresentation.Dispose();
        _sounds.Dispose();
        base.Dispose();
    }
}
