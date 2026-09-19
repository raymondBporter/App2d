using App2d.Levels;
using App2d.Core;
using App2d.Editor;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Persistence;
using App2d.Gameplay.Player;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
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
    private static readonly TraversalMetrics2D Traversal = TraversalMetricsLoader2D.Load(AssetPaths.Root);

    private readonly SideScrollerSimulation2D _simulation;
    private readonly SideScrollerSession2D _session;
    private readonly SideScrollerClient2D _client;
    private readonly SoundEffectBank2D _sounds;
    private readonly TileEditor2D _editor;
    private readonly PlayerSaveStore2D _saveStore;
    private readonly ViewportTerrainSource2D _terrainSource;

    public SideScrollerGame()
    {
        _sounds = new SoundEffectBank2D(Path.Combine(AssetPaths.Root, "audio", "sfx"));
        DeveloperConsole.RegisterVariable("sfx_volume", () => _sounds.Volume, value => _sounds.Volume = value,
            "Set sound-effect volume from 0 (muted) to 1 (full volume).");

        var loadedLevel = LevelBootstrap2D.Load();
        var tileMap = loadedLevel.TileMap;
        _terrainSource = new ViewportTerrainSource2D(tileMap);
        _saveStore = PlayerSaveStore2D.CreateDefault();
        var loadedSave = _saveStore.TryLoad();

        // The same recipe a server or predicting client will use; the host only adds I/O and presentation.
        _simulation = SideScrollerSimulation2D.Create(new SideScrollerSessionDefinition2D(
            Traversal, tileMap,
            loadedLevel.MovingPlatforms.Select(ThingTypeRegistry2D.ToRuntime).ToArray(),
            loadedLevel.PositionThings
                .Where(thing => ThingTypeRegistry2D.Require(thing.TypeKey).WorldKind is not null)
                .Select(ThingTypeRegistry2D.ToRuntime).ToArray())
        {
            PlayerMaximumHealth = PlayerMaximumHealth,
            SavedProgress = loadedSave is null ? null : new SavedProgress2D(loadedSave.SavePointId, loadedSave.HitPoints),
        });
        _session = _simulation.Session;
        RegisterDebugPhysicsWorld(_simulation.Physics);
        RegisterDebugAttackShapes(_simulation.Arsenal.GetActiveAttackHitboxes);
        RegisterDebugAttackShapes(_simulation.Level.EnemySystem.GetActiveAttackHitboxes);

        var snapshot = _session.CaptureSnapshot();
        var playerId = _session.PlayerIds[0];
        var startPosition = snapshot.FindPlayer(playerId)!.Value.Person.Position;
        var cameraOrigin = tileMap.Origin;
        var cameraTileSize = tileMap.TileSize;
        var cameraGroundHeights = (int[])_simulation.GroundHeights.Clone();
        float CameraFloorY(float worldX)
        {
            ArgGuard.ThrowIfNotFinite(worldX);
            var tileX = (int)MathF.Floor((worldX - cameraOrigin.X) / cameraTileSize);
            tileX = Math.Clamp(tileX, 0, cameraGroundHeights.Length - 1);
            return cameraOrigin.Y + cameraGroundHeights[tileX] * cameraTileSize;
        }
        Camera.ReferenceViewportHeight = 1080f;
        var cameraController = new SideScrollerCamera2D(Scene, Camera,
            tileMap.WorldBounds, startPosition, CameraFloorY);
        DeveloperConsole.RegisterVariable("camera_zoom", () => Camera.Zoom, value =>
        {
            ArgGuard.ThrowIfNotPositive(value);
            Camera.Zoom = value;
        }, "Camera zoom (0.05 to 20; default 1.35). Larger values zoom in.");

        // Only editor mode opens a writable database handle.
        _editor = new TileEditor2D(tileMap, LevelBootstrap2D.OpenForEditing, Camera, tileMap.Origin, Traversal.TileSize);
        _editor.ThingsChanged += things =>
            _simulation.Level.ReloadMovingPlatforms(things.Select(ThingTypeRegistry2D.ToRuntime).ToArray());

        _client = new SideScrollerClient2D(snapshot, playerId, Scene, Camera,
            cameraController, Textures, _sounds, Traversal);
        _client.CheckpointActivated += checkpoint =>
            _client.ShowSaveResult(_saveStore.TrySave(new PlayerSave2D(checkpoint.CheckpointId, checkpoint.HitPoints)), checkpoint.Position);
        DeveloperConsole.RegisterVariable("draw_traversal_metrics", () => _client.ShowTraversalDebug,
            value => _client.ShowTraversalDebug = value, "Draw jump arcs and tile-relative movement metrics.");
        DeveloperConsole.RegisterVariable("ballistics_speed", () => _client.Ballistics.SpeedTilesPerSecond,
            value => _client.Ballistics.SpeedTilesPerSecond = value,
            "Reference launch speed in tiles/s (0.1 to 100, default 10). Press F4 to fire with new settings.");
        DeveloperConsole.RegisterVariable("ballistics_angle", () => _client.Ballistics.AngleDegrees,
            value => _client.Ballistics.AngleDegrees = value,
            "Reference launch angle above horizontal in degrees (1 to 90, default 45).");
        DeveloperConsole.RegisterVariable("ballistics_gravity", () => _client.Ballistics.ReferenceGravityTilesPerSecondSquared,
            value => _client.Ballistics.ReferenceGravityTilesPerSecondSquared = value,
            "Yellow reference gravity in tiles/s^2 (0.1 to 200, default 10). Pink uses current world gravity.");
    }

    public override string WindowTitle =>
        $"App2d Side Scroller | PAD: {(_client.IsControllerConnected ? "XBOX" : "OFF")} | GEAR: {_client.WeaponName} | HP: {_client.State.Person.HitPoints}/{_client.State.Person.MaximumHitPoints} | enemies: {_simulation.Combat.DefeatedEnemies}/{_simulation.Level.EnemySystem.Count} | chunks: {_simulation.Level.ActiveChunkCount}/{SideScrollerLevel2D.MaximumActiveChunkCount} | colliders: {_simulation.Level.LoadedColliderCount} | broad pairs: {_simulation.Physics.LastCandidatePairCount}{(_client.State.ReachedGoal ? " | GOAL! BRO!" : string.Empty)}";

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
            _simulation.Level.UpdateStreaming(_editor.CameraFocus);
            _simulation.Level.FlushDirtyChunks();
            _client.RefreshEditorWorld(_session.CaptureEnemies(), _session.CaptureContent(), _session.CaptureWorld());
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
        // BeginFrame has set the actual viewport; include the final camera position and shake.
        _client.SetVisibleTerrain(_terrainSource.Capture(Camera.VisibleWorldBounds));
        renderer.Clear(new XnaColor(103, 196, 235));
        renderer.Draw(Scene);
        _client.Draw(renderer);
        TileEditorView2D.Draw(renderer, _editor, _simulation.Level.TileMap.WorldBounds, _simulation.Level.TileMap.TileSize, Textures);
    }

    public override void Dispose()
    {
        _terrainSource.Dispose();
        _simulation.Dispose();
        _client.Dispose();
        _editor.Dispose();
        _sounds.Dispose();
        base.Dispose();
    }
}
