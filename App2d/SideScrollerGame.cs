using App2d.Contracts.Player;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Assets;
using App2d.Core.Audio;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Hosting;
using App2d.Core.Input;
using App2d.Core.Rendering.Textures;
using App2d.Core.Vegetation;
using App2d.Editor;
using App2d.Gameplay.Player;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Persistence;
using App2d.Presentation.Audio;
using App2d.Presentation.World;
using App2d.Rendering;
using App2d.Things;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d;

/// <summary>Local composition and scheduling; gameplay decisions live in the session.</summary>
public sealed class SideScrollerGame : Game2D
{
    private readonly ResourceManager2D _resources = new();
    private readonly AuthoredCatalog _authored;
    private static readonly System.Text.Json.JsonSerializerOptions SpellJsonOptions = new() { PropertyNameCaseInsensitive = true };
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
    private readonly MapEditorUi2D _editorUi;
    private readonly PlayerSaveStore2D _saveStore;
    private readonly ViewportTerrainSource2D _terrainSource;
    private readonly App2d.Levels.WorldAsset2D _loadedWorld;
    private readonly LevelPicker2D _levelPicker;

    public SideScrollerGame(string? worldManifest = null)
    {
        _resources.Register("characters/authored", LoadAuthored, AssetPaths.Current.AuthoredCharacters);
        _authored = _resources.GetLoad<AuthoredCatalog>("characters/authored", "simulation");
        _resources.Get<AuthoredCatalog>("characters/authored", "presentation");
        _resources.Register("textures/runtime", () => Textures, Textures.ContentRoot, ownsResource: false);
        var textures = _resources.GetLoad<TextureCache2D>("textures/runtime", "presentation");
        _resources.Register("audio/sfx", () => new SoundEffectBank2D(AssetPaths.Current.SoundEffects), AssetPaths.Current.SoundEffects);
        var hero = _authored.Entities.GetValueOrDefault(Gameplay.Persons.Actions.AuthoredHero2D.EntityId)
            ?? throw new InvalidDataException("The authored 'hero' entity, the game's player, is missing.");
        // Fit the movement body to the level's four-unit clearance grid, preserving traversal tuning.

        var movement = ShapeBounds2D.Calculate(hero.MovementShape);
        var height = MathF.Round(GameWorldUnits2D.AuthoredToWorld(movement.Height) / 4) * 4;
        Traversal = TraversalMetrics2D.FromGeometry(new(128), .9f, new(GameWorldUnits2D.AuthoredToWorld(movement.Width), height), GameWorldUnits2D.AuthoredToWorld(movement.Center.X));
        _sounds = _resources.GetLoad<SoundEffectBank2D>("audio/sfx", "presentation");
        DebugPanel.Section("Audio").Slider("SFX volume", () => _sounds.Volume, value => _sounds.Volume = value, 0f, 1f,
            "Master sound-effect level from 0 (muted) to 1 (full volume).");

        _resources.Register("level/cavern", () => worldManifest is null ? LevelBootstrap2D.Load() : LevelBootstrap2D.LoadWorld(worldManifest),
            worldManifest ?? LevelBootstrap2D.CavernLevelPath);
        var loadedLevel = _resources.GetLoad<App2d.Levels.WorldAsset2D>("level/cavern", "simulation");
        _loadedWorld = loadedLevel;
        _levelPicker = new(loadedLevel.Maps);
        var tileMap = loadedLevel.TileMap;
        _terrainSource = new ViewportTerrainSource2D(tileMap);
        _saveStore = worldManifest is null ?
            loadedLevel.StartAtArrival ? new PlayerSaveStore2D(PlayerSaveStore2D.CreateDefault().Path + ".playground") : PlayerSaveStore2D.CreateDefault() :
            new PlayerSaveStore2D(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(worldManifest))!, "player-save.json"));
        // Explicit world launches are playtests: always begin at the map's arrival.
        // The local store still records checkpoints for deaths during this session.
        var loadedSave = worldManifest is null && !loadedLevel.StartAtArrival ? _saveStore.TryLoad() : null;

        // The one construction recipe for the simulation; the host only adds I/O and presentation.
        _simulation = SideScrollerSimulation2D.Create(new SideScrollerSessionDefinition2D(
            Traversal, tileMap,
            [.. loadedLevel.MovingPlatforms.Select(ThingTypeRegistry2D.ToRuntime)],
            [.. loadedLevel.PositionThings
                .Where(thing => ThingTypeRegistry2D.Require(thing.TypeKey).WorldKind is not null)
                .Select(ThingTypeRegistry2D.ToRuntime)])
        {
            PlayerMaximumHealth = hero.Asset.Health,
            Spells = System.Text.Json.JsonSerializer.Deserialize<SpellTuning2D>(
                File.ReadAllText(Path.Combine(AssetPaths.Current.Runtime, "gameplay", "player-spells.json")),
                SpellJsonOptions)
                ?? throw new InvalidDataException("Player spell tuning is missing."),
            AuthoredCharacters = _authored,
            Zones = loadedLevel.Zones,
            GiantTrees = loadedLevel.GiantTrees,
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
        SideScrollerBackground2D.AddTo(Background);
        var cameraController = new SideScrollerCamera2D(Camera,
            tileMap.WorldBounds, startPosition);
        DebugPanel.Section("Camera").Slider("Zoom", () => Camera.Zoom, value => Camera.Zoom = value, 0.05f, 20f,
            "Camera zoom; default 1.35. Larger values zoom in.", logarithmic: true);

        // Only editor mode opens a writable database handle.
        _editor = new TileEditor2D(loadedLevel.Maps[0].TileMap, () => Levels.LevelDatabase2D.Open(loadedLevel.Maps[0].DatabasePath), Camera);
        _resources.Get<App2d.Levels.WorldAsset2D>("level/cavern", "editor");
        _editorUi = new(_editor, Textures, DebugPanel);
        _resources.Get<TextureCache2D>("textures/runtime", "editor");
        _editor.ConfigureMaps(loadedLevel.Maps);
        _editor.ThingsChanged += things =>
        {
            loadedLevel.Maps[_editor.SelectedMapIndex].MovingPlatforms = [.. things];
            _simulation.Level.ReloadMovingPlatforms([.. loadedLevel.MovingPlatforms.Select(ThingTypeRegistry2D.ToRuntime)]);
        };

        DebugPanel.Section("World").Button("Reload giant trees", () =>
        {
            var trees = loadedLevel.LoadGiantTrees();
            _simulation.Level.ReloadGiantTrees(trees);
            return $"Loaded {trees.Length} giant trees with {trees.Sum(tree => tree.Branches.Length)} branch platforms.";
        }, "Reload giant-trees.json: positions and dimensions are in tiles; ranges are [min, max].");

        _client = new SideScrollerClient2D(snapshot, playerId, Scene, Camera,
            cameraController, textures, _sounds, Traversal, Presentation.Persons.PersonMoves.From(_authored));
        _resources.Register("audio/soundtrack", () => WorldSoundtrack2D.Load(AssetPaths.Current.Music, loadedLevel.Zones),
            Path.Combine(AssetPaths.Current.Music, "soundtrack.json"));
        var soundtrack = _resources.GetLoad<WorldSoundtrack2D>("audio/soundtrack", "music");
        _music = new MusicPlayer2D(soundtrack.Cues);
        _musicDirector = new(soundtrack, _music.Select);
        _musicDirector.Update(snapshot.Content, startPosition, 0f);
        var moods = soundtrack.Cues.Values.SelectMany(cue => cue.Moods.Keys).Distinct()
            .Where(mood => soundtrack.Cues.Values.All(cue => cue.Moods.ContainsKey(mood))).Order();
        DebugPanel.Section("Audio")
            .Slider("Music volume", () => _music.Volume, value => _music.Volume = value, 0f, 1f,
                "Music level from 0 to 1; default 0.45. Independent of sound effects.")
            .Choice("Music mood", () => _musicDirector.MoodOverride, value => _musicDirector.MoodOverride = value, ["auto", .. moods],
                "auto follows the zone; any other mood auditions it.")
            .Choice("Music piece", () => _musicDirector.PieceOverride, value => _musicDirector.PieceOverride = value, ["auto", .. soundtrack.Cues.Keys.Order()],
                "auto follows zones; a cue ID auditions that piece.")
            .Readout("Now playing", () =>
                $"Zone: {_musicDirector.CurrentZone?.Name ?? "outside zones"} ({_musicDirector.CurrentZone?.Id ?? "default"})\nMusic: {_musicDirector.CurrentSelection?.Piece} / {_musicDirector.CurrentSelection?.Mood}");
        DebugPanel.Section("Draw").Toggle("Zones", () => _showZones, value => _showZones = value,
            "Draw authored zone boundaries; the player's zone is gold.");
        DebugPanel.Section("Resources").Readout("Registered", () => string.Join('\n', _resources.Snapshot()
            .Select(resource => $"{resource.Key} [{resource.Type.Name}] {(resource.IsLoaded ? "loaded" : "unloaded")} | used by: {string.Join(", ", resource.Owners)} | source: {resource.Source ?? "in memory"}")));
        _client.CheckpointActivated += checkpoint =>
            _client.ShowSaveResult(_saveStore.TrySave(new PlayerSave2D(checkpoint.CheckpointId, checkpoint.HitPoints)), checkpoint.Position);
        DebugPanel.Section("Draw").Toggle("Traversal metrics", () => _client.ShowTraversalDebug, value => _client.ShowTraversalDebug = value,
            "Draw jump arcs and tile-relative movement metrics (also F3).");
        DebugPanel.Section("Ballistics")
            .Slider("Speed (tiles/s)", () => _client.Ballistics.SpeedTilesPerSecond, value => _client.Ballistics.SpeedTilesPerSecond = value, 0.1f, 100f,
                "Reference launch speed; default 10. Press F4 to fire with new settings.")
            .Slider("Angle (degrees)", () => _client.Ballistics.AngleDegrees, value => _client.Ballistics.AngleDegrees = value, 1f, 90f,
                "Launch angle above horizontal; default 45.")
            .Slider("Reference gravity (tiles/s^2)", () => _client.Ballistics.ReferenceGravityTilesPerSecondSquared, value => _client.Ballistics.ReferenceGravityTilesPerSecondSquared = value, 0.1f, 200f,
                "Yellow marker gravity; default 10. Pink uses current world gravity.");
    }

    public override string WindowTitle =>
        $"App2d Side Scroller | PAD: {(_isControllerConnected ? "XBOX" : "OFF")} | GEAR: {_client.WeaponName} | HP: {_client.State.Person.HitPoints}/{_client.State.Person.MaximumHitPoints} | enemies: {_simulation.Combat.DefeatedEnemies}/{_simulation.Level.EnemySystem.Count} | chunks: {_simulation.Level.ActiveChunkCount}/{SideScrollerLevel2D.MaximumActiveChunkCount} | colliders: {_simulation.Level.LoadedColliderCount} | broad pairs: {_simulation.Physics.LastCandidatePairCount}{(_client.State.ReachedGoal ? " | GOAL! BRO!" : string.Empty)}";

    public override IGameToolUi2D? ToolUi => _editorUi;
    protected override XnaColor BackgroundColor => new(103, 196, 235);

    public override void Update(FrameTime time, InputState input)
    {
        _isControllerConnected = input.Gamepad.IsConnected;
        if (!_editor.IsActive)
        {
            var wasOpen = _levelPicker.IsOpen;
            if (_levelPicker.Update(input) is { } destination)
            {
                _session.TravelForPlaytesting(Levels.PlaygroundMaps2D.Arrival(
                    _loadedWorld.Maps[destination], Traversal.PlayerColliderSize.Y, Traversal.GroundProbeDistance));
            }

            if (wasOpen != _levelPicker.IsOpen)
            {
                input.CancelButtons();
            }

            if (_levelPicker.IsOpen)
            {
                _session.SetPaused(true);
                _client.Suspend();
                return;
            }
        }
        var wasEditing = _editor.IsActive;
        _editor.Update(input);
        if (!wasEditing && _editor.IsActive)
        {
            var position = _client.State.Person.Position;
            var index = _loadedWorld.Maps.ToList().FindIndex(m =>
                position.X >= m.TileMap.WorldBounds.Min.X && position.X < m.TileMap.WorldBounds.Max.X &&
                position.Y >= m.TileMap.WorldBounds.Min.Y && position.Y < m.TileMap.WorldBounds.Max.Y);
            if (index >= 0 && index != _editor.SelectedMapIndex)
            {
                _editor.CycleMap(index - _editor.SelectedMapIndex);
            }
        }
        if (wasEditing != _editor.IsActive)
        {
            input.CancelButtons();
        }

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
        if (!_editor.IsActive && !_levelPicker.IsOpen)
        {
            _client.AdvancePresentation(time.DeltaSeconds);
        }

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
        TileEditorView2D.DrawWorldDebug(renderer, _editor);
        if (_showZones)
        {
            foreach (var zone in _client.Content.Zones)
            {
                var b = zone.Bounds;
                if (!b.Intersects(Camera.VisibleWorldBounds))
                {
                    continue;
                }

                Span<Vector2> outline = [b.Min, new(b.Max.X, b.Min.Y), b.Max, new(b.Min.X, b.Max.Y), b.Min];
                renderer.DrawWorldPolyline(outline, zone.Id == _musicDirector.CurrentZone?.Id ? XnaColor.Gold : XnaColor.Cyan, 2f);
            }
        }
    }

    public override void RenderUI(Renderer2D renderer, FrameTime time)
    {
        _client.DrawUI(renderer);
        if (!_editor.IsActive)
        {
            _levelPicker.Draw(renderer);
        }

        base.RenderUI(renderer, time);
    }

    /// <summary>Authored entities that fail to compile are not played; the game refuses to start and names each problem instead.</summary>
    private static AuthoredCatalog LoadAuthored()
    {
        var catalog = AuthoredCatalog.Load(AssetPaths.Current.AuthoredCharacters);
        if (catalog.Errors.Count > 0)
        {
            throw new InvalidDataException("Authored character assets have errors:" + Environment.NewLine + string.Join(Environment.NewLine, catalog.Errors));
        }

        return catalog;
    }

    public override void Dispose()
    {
        _terrainSource.Dispose();
        _simulation.Dispose();
        _client.Dispose();
        _editor.Dispose();
        _editorUi.Dispose();
        _music.Dispose();
        _resources.Dispose();
        base.Dispose();
    }
}
