using App2d.Levels;
using App2d.Collision;
using App2d.Core;
using App2d.Editor;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persistence;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Physics;
using App2d.Rendering;
using App2d.Tiles;
using App2d.Things;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d;

/// <summary>Local composition and scheduling; gameplay decisions live in the session.</summary>
public sealed class SideScrollerGame : Game2D
{
    private const int PlayerMaximumHealth = 5;
    private const uint WorldLayer = 1u << 0;
    private const uint PlayerLayer = 1u << 1;
    private const uint EnemyLayer = 1u << 2;
    private static readonly TraversalMetrics2D Traversal = TraversalMetricsLoader2D.Load(AssetPaths.Root);

    private readonly PhysicsWorld2D _physics;
    private readonly SideScrollerLevel2D _level;
    private readonly CombatSystem2D _combat;
    private readonly SideScrollerSession2D _session;
    private readonly SideScrollerClient2D _client;
    private readonly SoundEffectBank2D _sounds;
    private readonly TileEditor2D _editor;
    private readonly PlayerSaveStore2D _saveStore;

    public SideScrollerGame()
    {
        var collision = new CollisionSystem2D();
        var combatants = new CombatantRegistry2D();
        _physics = new PhysicsWorld2D(collision)
        {
            Gravity = new Vector2(0f, -Traversal.Gravity),
            MaxSubstepSeconds = SideScrollerSession2D.FixedDeltaSeconds,
            PositionIterations = 3,
            VelocityIterations = 2
        };
        Traversal.ValidateScaleContract();
        _sounds = new SoundEffectBank2D(Path.Combine(AssetPaths.Root, "audio", "sfx"));
        RegisterDebugPhysicsWorld(_physics);
        DeveloperConsole.RegisterVariable("sfx_volume", () => _sounds.Volume, value => _sounds.Volume = value,
            "Set sound-effect volume from 0 (muted) to 1 (full volume).");

        var loadedLevel = LevelBootstrap2D.Load();
        var tileMap = loadedLevel.TileMap;
        var groundHeights = TileGroundHeights2D.Derive(tileMap);
        _level = new SideScrollerLevel2D(Traversal, tileMap,
            x => groundHeights[Math.Clamp(x, 0, groundHeights.Length - 1)],
            loadedLevel.MovingPlatforms.Select(ThingTypeRegistry2D.ToRuntime).ToArray(),
            loadedLevel.PositionThings
                .Where(thing => ThingTypeRegistry2D.Require(thing.TypeKey).WorldKind is not null)
                .Select(ThingTypeRegistry2D.ToRuntime).ToArray());
        _saveStore = PlayerSaveStore2D.CreateDefault();
        var loadedSave = _saveStore.TryLoad();
        var loadedSavePoint = loadedSave is { HitPoints: <= PlayerMaximumHealth }
            ? _level.FindSavePoint(loadedSave.SavePointId) : null;
        var respawn = new RespawnState2D(
            loadedSavePoint?.Position ?? _level.SpawnPoint,
            loadedSavePoint is null ? PlayerMaximumHealth : loadedSave!.HitPoints,
            loadedSavePoint?.ThingId);
        var cameraOrigin = tileMap.Origin;
        var cameraTileSize = tileMap.TileSize;
        var cameraGroundHeights = (int[])groundHeights.Clone();
        float CameraFloorY(float worldX)
        {
            ArgGuard.ThrowIfNotFinite(worldX);
            var tileX = (int)MathF.Floor((worldX - cameraOrigin.X) / cameraTileSize);
            tileX = Math.Clamp(tileX, 0, cameraGroundHeights.Length - 1);
            return cameraOrigin.Y + cameraGroundHeights[tileX] * cameraTileSize;
        }
        var cameraController = new SideScrollerCamera2D(Scene, Camera,
            tileMap.WorldBounds, respawn.Position, CameraFloorY);

        // Only editor mode opens a writable database handle.
        _editor = new TileEditor2D(tileMap, LevelBootstrap2D.OpenForEditing, Camera, tileMap.Origin, Traversal.TileSize);
        _level.CreateSimulation(collision, _physics, WorldLayer, PlayerLayer, EnemyLayer);
        _level.UpdateStreaming(respawn.Position);
        _editor.ThingsChanged += things =>
            _level.ReloadMovingPlatforms(things.Select(ThingTypeRegistry2D.ToRuntime).ToArray());

        var player = new Person2D(collision, _physics, Traversal, respawn.Position,
            PlayerLayer, WorldLayer, CombatFaction2D.Player,
            maximumHealth: PlayerMaximumHealth, tileMap: tileMap);
        combatants.Register(player);
        var contactDamage = new ContactDamageSystem2D(collision, EnemyLayer, combatants);
        _combat = new CombatSystem2D(collision, combatants);
        _level.CreateAuthoredWorldThings(_combat);
        var arsenal = new PersonArsenal2D(player.Body, Traversal.GunMuzzleOffset, collision,
            WorldLayer, EnemyLayer, CombatFaction2D.Player, _combat,
            overlapsSpikes: bounds => _level.TryGetSpikeSource(bounds, out _));
        player.AttachActions(arsenal);
        RegisterDebugAttackShapes(arsenal.GetActiveAttackHitboxes);
        RegisterDebugAttackShapes(_level.EnemySystem.GetActiveAttackHitboxes);

        _session = new SideScrollerSession2D(_physics, player, arsenal,
            new SideScrollerSessionWorld2D(_level, contactDamage), respawn, _combat);
        _client = new SideScrollerClient2D(_session.CaptureSnapshot(), Scene, Camera,
            cameraController, Textures, _sounds, Traversal);
        _client.CheckpointActivated += checkpoint =>
            _client.ShowSaveResult(_saveStore.TrySave(new PlayerSave2D(checkpoint.CheckpointId, checkpoint.HitPoints)), checkpoint.Position);
        DeveloperConsole.RegisterVariable("draw_traversal_metrics", () => _client.ShowTraversalDebug,
            value => _client.ShowTraversalDebug = value, "Draw jump arcs and tile-relative movement metrics.");
    }

    public override string WindowTitle =>
        $"App2d Side Scroller | PAD: {(_client.IsControllerConnected ? "XBOX" : "OFF")} | GEAR: {_client.WeaponName} | HP: {_client.State.Person.HitPoints}/{_client.State.Person.MaximumHitPoints} | enemies: {_combat.DefeatedEnemies}/{_level.EnemySystem.Count} | chunks: {_level.ActiveChunkCount}/{SideScrollerLevel2D.MaximumActiveChunkCount} | colliders: {_level.LoadedColliderCount} | broad pairs: {_physics.LastCandidatePairCount}{(_client.State.ReachedGoal ? " | GOAL! BRO!" : string.Empty)}";

    internal override Control? OverlayControl => _editor.InspectorView;

    public override void Update(FrameTime time, InputState input)
    {
        var wasEditing = _editor.IsActive;
        _editor.Update(input);
        if (wasEditing != _editor.IsActive) input.CancelButtons();
        _session.SetPaused(_editor.IsActive);
        if (_editor.IsActive)
        {
            _client.Suspend();
            // The offline editor is an explicit host-side administrative path.
            _level.UpdateStreaming(_editor.CameraFocus);
            _level.FlushDirtyChunks();
            _client.RefreshEditorWorld(_session.CaptureEnemies(), _session.CaptureWorld());
        }
        else
        {
            var command = _client.CaptureInput(input);
            var frame = _session.Advance(command);
            _client.Apply(frame);
        }
    }

    public override void AdvancePresentation(FrameTime time)
    {
        _client.UpdateFeedback(time.DeltaSeconds);
        if (!_editor.IsActive) _client.AdvancePresentation(time.DeltaSeconds);
        _client.WorldSounds.Update();
    }

    public override void Render(Renderer2D renderer)
    {
        renderer.Clear(new XnaColor(103, 196, 235));
        renderer.Draw(Scene);
        _client.Draw(renderer);
        TileEditorView2D.Draw(renderer, _editor, _level.TileMap.WorldBounds, _level.TileMap.TileSize, Textures);
    }

    public override void Dispose()
    {
        _session.Dispose();
        _level.Dispose();
        _client.Dispose();
        _editor.Dispose();
        _sounds.Dispose();
        base.Dispose();
    }
}
