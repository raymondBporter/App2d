using System.Numerics;
using App2d.Engine;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using App2d.Engine.Rendering.Textures;
using App2d.Gameplay;
using SkiaSharp;

namespace App2d;

public sealed class SideScrollerGame : Game2D
{
    private const float TileSize = 64f;
    private const uint WorldLayer = 1u << 0;
    private const uint PlayerLayer = 1u << 1;
    private const uint EnemyLayer = 1u << 2;

    private static readonly TraversalMetrics2D Traversal = new() { TileSize = TileSize };

    private readonly PhysicsWorld2D _physics = new()
    {
        Gravity = new Vector2(0f, -Traversal.Gravity),
        MaxSubstepSeconds = 1f / 120f,
        PositionIterations = 3,
        VelocityIterations = 2
    };
    private readonly SideScrollerLevel2D _level;
    private readonly PlayerInputMapper2D _inputMapper = new();
    private readonly PlayerCharacter2D _player;
    private readonly PlayerPresentation2D _playerPresentation;
    private readonly CombatSystem2D _combat;
    private readonly PlayerArsenal2D _arsenal;
    private readonly SideScrollerCamera2D _cameraController;
    private readonly TraversalDebugRenderer2D _traversalDebug = new(Traversal);
    private bool _reachedGoal;
    private bool _showTraversalDebug;

    public SideScrollerGame()
    {
        RegisterDebugPhysicsWorld(_physics);
        DeveloperConsole.RegisterVariable(
            "draw_traversal_metrics",
            () => _showTraversalDebug,
            value => _showTraversalDebug = value,
            "Draw jump arcs, grapple reach, and tile-relative movement metrics.");

        _level = new SideScrollerLevel2D(TileSize);
        _cameraController = new SideScrollerCamera2D(
            Scene,
            Camera,
            _level.TileMap,
            _level.SpawnPoint);

        var platformShader = new TextureShader2D(
            Textures.Load("mossy-stone.png"),
            new Vector2(512f, 512f));
        _level.CreateEnvironment(
            Scene,
            _physics,
            platformShader,
            WorldLayer,
            PlayerLayer,
            EnemyLayer);

        _player = new PlayerCharacter2D(
            Scene,
            _physics,
            Traversal,
            _level.SpawnPoint,
            PlayerLayer,
            WorldLayer);
        _playerPresentation = new PlayerPresentation2D(Scene, Textures);

        _level.CreateEnemies(Scene, _physics, WorldLayer, EnemyLayer);
        _combat = new CombatSystem2D(_level.Enemies);
        _arsenal = new PlayerArsenal2D(
            Scene,
            _physics,
            _player.Body,
            Textures,
            Traversal,
            _level.Platforms,
            _combat,
            _playerPresentation,
            PlayerLayer,
            WorldLayer);
        RegisterDebugAttackShapes(_arsenal.GetActiveAttackHitboxes);

        _playerPresentation.Update(
            0f,
            0,
            _player.Position,
            0f,
            _player.Facing,
            _player.IsGrounded,
            _arsenal.IsSwordActive,
            _player.InvulnerabilitySeconds);
    }

    public override string WindowTitle =>
        $"App2d Side Scroller | weapon: {_arsenal.ActiveWeaponName} | Ctrl+wheel switch | left click attack | right click fire | HP: {_player.Health.Current}/{_player.Health.Maximum} | enemies: {_combat.DefeatedEnemies}/{_level.Enemies.Count} | broad pairs: {_physics.LastCandidatePairCount}{(_reachedGoal ? " | GOAL! BRO!" : string.Empty)}";

    public override void Update(FrameTime time, InputState input)
    {
        var dt = time.DeltaSeconds;
        _player.BeginFrame(dt);
        _arsenal.BeginFrame(dt);

        var command = _inputMapper.Capture(input, Camera);
        if (command.ToggleTraversalDebug)
            _showTraversalDebug = !_showTraversalDebug;
        _arsenal.CycleWeapon(command.WeaponCycleDirection);

        foreach (var enemy in _level.Enemies)
            enemy.Update(dt);

        _player.UpdateBeforePhysics(command.Movement, dt);
        if (command.UseWeapon)
            _player.Face(_arsenal.UseActiveWeapon(command.AimTarget, _player.Facing));
        if (command.FireProjectile)
            _arsenal.TryStartFireballShot();

        _arsenal.UpdateBeforePhysics(dt);
        _physics.Step(dt);
        _player.UpdateAfterPhysics(dt);

        _arsenal.UpdateAfterPhysics(dt, _player.Facing);
        if (_player.ResolveEnemyTouches(_level.Enemies))
        {
            _player.Health.Reset();
            Respawn();
        }

        if (_player.Position.Y < _level.TileMap.WorldBounds.Min.Y - 260f)
            Respawn();
        if (_player.Position.X >= _level.GoalX)
            _reachedGoal = true;

        _playerPresentation.Update(
            dt,
            time.FrameNumber,
            _player.Position,
            command.Movement.MoveX,
            _player.Facing,
            _player.IsGrounded,
            _arsenal.IsSwordActive,
            _player.InvulnerabilitySeconds);
        _arsenal.ReleasePendingFireball(_player.Facing);
        _cameraController.Update(_player.Position, _player.Body.LinearVelocity, dt);
    }

    public override void Render(Renderer2D renderer)
    {
        renderer.Clear(new SKColor(103, 196, 235));
        renderer.Draw(Scene);
        renderer.DrawScreenLabel(
            $"WEAPON: {_arsenal.ActiveWeaponStatus}   CTRL + WHEEL   F3: METRICS",
            new Vector2(24f, 24f));

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
    }
}
