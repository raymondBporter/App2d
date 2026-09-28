using App2d.Contracts.World;
using App2d.Core.Validation;
using App2d.Contracts.Player;
using App2d.Presentation.World;
using App2d.Audio;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Diagnostics;
using App2d.Editor;
using App2d.Presentation.Audio;
using App2d.Gameplay.Player;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Persistence;
using App2d.Rendering;
using App2d.Rendering.Vegetation;
using App2d.Things;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d;

/// <summary>Local composition and scheduling; gameplay decisions live in the session.</summary>
public sealed class SideScrollerGame : Game2D
{
    private readonly AuthoredCatalog _authored = LoadAuthored();
    private readonly TraversalMetrics2D Traversal;

    private readonly SideScrollerSimulation2D _simulation;
    private readonly SideScrollerSession2D _session;
    private readonly SideScrollerClient2D _client;
    private readonly SoundEffectBank2D _sounds;
    private readonly MusicPlayer2D _music;
    private readonly WorldMusicDirector2D _musicDirector;
    private bool _showZones;
    private bool _isControllerConnected;
    private readonly TileEditor2D _editor;
    private readonly PlayerSaveStore2D _saveStore;
    private readonly ViewportTerrainSource2D _terrainSource;

    public SideScrollerGame()
    {
        var hero = _authored.Entities.GetValueOrDefault(Gameplay.Persons.Actions.AuthoredHero2D.EntityId)
            ?? throw new InvalidDataException("The authored 'hero' entity, the game's player, is missing.");
        // Fit the movement body to the level's four-unit clearance grid, preserving traversal tuning.
        var height = MathF.Round(GameWorldUnits2D.AuthoredToWorld(hero.Asset.Movement.Height) / 4) * 4;
        Traversal = TraversalMetrics2D.FromGeometry(new(128), .9f,
            new(GameWorldUnits2D.AuthoredToWorld(hero.Asset.Movement.Width), height),
            GameWorldUnits2D.AuthoredToWorld(hero.Asset.Movement.OffsetX));
        _sounds = new SoundEffectBank2D(AssetPaths.Current.SoundEffects);
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
            [.. loadedLevel.MovingPlatforms.Select(ThingTypeRegistry2D.ToRuntime)],
            [.. loadedLevel.PositionThings
                .Where(thing => ThingTypeRegistry2D.Require(thing.TypeKey).WorldKind is not null)
                .Select(ThingTypeRegistry2D.ToRuntime)])
        {
            PlayerMaximumHealth = hero.Asset.Health,
            AuthoredCharacters = _authored,
            Zones = loadedLevel.Zones,
            SavedProgress = loadedSave is null ? null : new SavedProgress2D(loadedSave.SavePointId, loadedSave.HitPoints),
        });
        _session = _simulation.Session;
        AttachPhysicsWorld(_simulation.Physics);
        RegisterDebugAttackShapes(_simulation.Arsenal.GetActiveAttackHitboxes);
        RegisterDebugAttackShapes(_simulation.Level.EnemySystem.GetActiveAttackHitboxes);

        var snapshot = _session.CaptureSnapshot();
        var playerId = _session.PlayerIds[0];
        var startPosition = snapshot.FindPlayer(playerId)!.Value.Person.Position;
        Camera.ReferenceViewportHeight = 1080f;
        var cameraController = new SideScrollerCamera2D(Scene, Camera,
            tileMap.WorldBounds, startPosition);
        DeveloperConsole.RegisterVariable("camera_zoom", () => Camera.Zoom, value =>
        {
            ArgGuard.ThrowIfNotFiniteOrNotPositive(value);
            Camera.Zoom = value;
        }, "Camera zoom (0.05 to 20; default 1.35). Larger values zoom in.");

        // Only editor mode opens a writable database handle.
        _editor = new TileEditor2D(tileMap, LevelBootstrap2D.OpenForEditing, Camera, tileMap.Origin, Traversal.TileSize);
        _editor.ThingsChanged += things =>
            _simulation.Level.ReloadMovingPlatforms([.. things.Select(ThingTypeRegistry2D.ToRuntime)]);

        _client = new SideScrollerClient2D(snapshot, playerId, Scene, Camera,
            cameraController, Textures, _sounds, Traversal, App2d.Presentation.Persons.PersonMoves.From(_authored));
        var soundtrack = WorldSoundtrack2D.Load(AssetPaths.Current.Music, loadedLevel.Zones);
        _music = new MusicPlayer2D(soundtrack.Cues);
        _musicDirector = new(soundtrack, _music.Select);
        _musicDirector.Update(snapshot.Content, startPosition, 0f);
        DeveloperConsole.RegisterVariable("music_volume", () => _music.Volume, value => _music.Volume = value,
            "Music volume from 0 to 1; default 0.45. Independent of sound effects.");
        DeveloperConsole.RegisterVariable("music_mood", () => _musicDirector.MoodOverride, value => _musicDirector.MoodOverride = value,
            "auto follows the zone; explore, drive or combat auditions a mood.");
        DeveloperConsole.RegisterVariable("music_piece", () => _musicDirector.PieceOverride, value => _musicDirector.PieceOverride = value,
            "auto follows zones; crown-of-embers or copper-circuit auditions a piece.");
        DeveloperConsole.RegisterVariable("draw_zones", () => _showZones, value => _showZones = value,
            "Draw authored zone boundaries. Use music_status to see the zone at the player.");
        DeveloperConsole.RegisterCommand("music_status", "Show the current zone, music selection and available cues.", _ =>
            ConsoleCommandResult.From($"Zone: {_musicDirector.CurrentZone?.Name ?? "outside zones"} ({_musicDirector.CurrentZone?.Id ?? "default"})",
                $"Music: {_musicDirector.CurrentSelection?.Piece} / {_musicDirector.CurrentSelection?.Mood}; volume {_music.Volume:0.00}",
                $"Cues: {string.Join(", ", soundtrack.Cues.Keys)}"));
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
        $"App2d Side Scroller | PAD: {(_isControllerConnected ? "XBOX" : "OFF")} | GEAR: {_client.WeaponName} | HP: {_client.State.Person.HitPoints}/{_client.State.Person.MaximumHitPoints} | enemies: {_simulation.Combat.DefeatedEnemies}/{_simulation.Level.EnemySystem.Count} | chunks: {_simulation.Level.ActiveChunkCount}/{SideScrollerLevel2D.MaximumActiveChunkCount} | colliders: {_simulation.Level.LoadedColliderCount} | broad pairs: {_simulation.Physics.LastCandidatePairCount}{(_client.State.ReachedGoal ? " | GOAL! BRO!" : string.Empty)}";

    internal override Control? OverlayControl => _editor.InspectorView;
    protected override XnaColor BackgroundColor => new(103, 196, 235);

    public override void Update(FrameTime time, InputState input)
    {
        _isControllerConnected = input.Gamepad.IsConnected;
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
        _musicDirector.Update(_client.Content, _client.State.Person.Position, time.DeltaSeconds);
        _client.UpdateFeedback(time.DeltaSeconds);
        if (!_editor.IsActive) _client.AdvancePresentation(time.DeltaSeconds);
        _client.WorldSounds.Update();
    }

    public override void Render(Renderer2D renderer)
    {
        // BeginFrame has set the actual viewport; include the final camera position and shake.
        // Keep off-screen roots loaded while their canopies can still enter the camera.
        var foliageMargin = new Vector2(_simulation.Level.TileMap.TileSize * 10f);
        var visible = Camera.VisibleWorldBounds;
        _client.SetVisibleTerrain(_terrainSource.Capture(new(visible.Min - foliageMargin, visible.Max + foliageMargin)));
        _client.DrawTrees(renderer);
        // Terrain sits at z 0 and characters above it, so back grass slots in between.
        renderer.Draw(Scene, int.MinValue, 0);
        _client.DrawGrass(renderer, VegetationLayer2D.Back);
        renderer.Draw(Scene, 1, int.MaxValue);
        _client.DrawGrass(renderer, VegetationLayer2D.Front);
        _client.DrawWorldEffects(renderer);
    }

    public override void RenderWorldDebug(Renderer2D renderer)
    {
        base.RenderWorldDebug(renderer);
        _client.DrawWorldDebug(renderer);
        TileEditorView2D.DrawWorldDebug(renderer, _editor, _simulation.Level.TileMap.WorldBounds, _simulation.Level.TileMap.TileSize);
        if (_showZones)
            foreach (var zone in _client.Content.Zones)
            {
                var b = zone.Bounds;
                if (!b.Intersects(Camera.VisibleWorldBounds)) continue;
                Span<Vector2> outline = [b.Min, new(b.Max.X, b.Min.Y), b.Max, new(b.Min.X, b.Max.Y), b.Min];
                renderer.DrawWorldPolyline(outline, zone.Id == _musicDirector.CurrentZone?.Id ? XnaColor.Gold : XnaColor.Cyan, 2f);
            }
    }

    public override void RenderUI(Renderer2D renderer, FrameTime time)
    {
        _client.DrawUI(renderer);
        TileEditorView2D.DrawUI(renderer, _editor, Textures);
        base.RenderUI(renderer, time);
    }

    /// <summary>Authored entities that fail to compile are not played; the game refuses to start and names each problem instead.</summary>
    private static AuthoredCatalog LoadAuthored()
    {
        var catalog = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        if (catalog.Errors.Count > 0) throw new InvalidDataException("Authored character assets have errors:" + Environment.NewLine + string.Join(Environment.NewLine, catalog.Errors));
        return catalog;
    }

    public override void Dispose()
    {
        _terrainSource.Dispose();
        _simulation.Dispose();
        _client.Dispose();
        _editor.Dispose();
        _sounds.Dispose();
        _music.Dispose();
        base.Dispose();
    }
}
