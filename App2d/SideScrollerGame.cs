using App2d.Contracts.Player;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Assets;
using App2d.Core.Audio;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Hosting;
using App2d.Core.Input;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Editor;
using App2d.Gameplay.Characters;
using App2d.Gameplay.Player;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Persistence;
using App2d.Presentation.Audio;
using App2d.Presentation.Player;
using App2d.Presentation.World;
using App2d.Rendering;

namespace App2d;

/// <summary>Local composition and scheduling; gameplay decisions live in the session.</summary>
public sealed class SideScrollerGame : Game2D
{
    internal const int CampaignRevision = 2;
    private readonly ResourceManager2D _resources = new();
    private readonly TraversalMetrics2D Traversal;

    private readonly SideScrollerSimulation2D _simulation;
    private readonly SideScrollerSession2D _session;
    private readonly SideScrollerClient2D _client;
    private readonly SoundEffectBank2D<SoundEffect2D> _sounds;
    private readonly MusicPlayer2D _music;
    private readonly WorldMusicDirector2D _musicDirector;
    private bool _showZones;
    private bool _isControllerConnected;
    private readonly TileEditor2D _editor;
    private readonly MapEditorUi2D _editorUi;
    private readonly PlayerSaveStore2D _saveStore;
    private readonly ViewportTerrainSource2D _terrainSource;
    private readonly Levels.WorldAsset2D _loadedWorld;
    private readonly LevelPicker2D _levelPicker;
    private bool _paperDollWasOpen;
    private readonly bool _campaign;
    private readonly bool _hasCheckpoint;
    private CampaignScreen _campaignScreen;
    private bool _savedClear;

    public SideScrollerGame(string? worldManifest = null) : this(worldManifest, campaign: false)
    {
    }

    internal SideScrollerGame(string? worldManifest, bool campaign, bool replay = false, PlayerSaveStore2D? campaignSaveStore = null)
    {
        _campaign = campaign;
        _resources.Register("textures/runtime", () => Textures, Textures.ContentRoot, ownsResource: false);
        var textures = _resources.GetLoad<TextureCache2D>("textures/runtime", "presentation");
        _resources.Register("audio/sfx", () => new SoundEffectBank2D<SoundEffect2D>(AssetPaths.Current.SoundEffects, SoundEffectCues2D.Cues), AssetPaths.Current.SoundEffects);
        var hero = GameCharacters.Hero;
        _sounds = _resources.GetLoad<SoundEffectBank2D<SoundEffect2D>>("audio/sfx", "presentation");
        DebugPanel.Section("Audio").Slider("SFX volume", () => _sounds.Volume, value => _sounds.Volume = value, 0f, 1f,
            "Master sound-effect level from 0 (muted) to 1 (full volume).");

        _resources.Register("level/world", () => campaign ? LevelBootstrap2D.LoadCampaign() :
            worldManifest is null ? LevelBootstrap2D.Load() : LevelBootstrap2D.LoadWorld(worldManifest),
            campaign ? LevelBootstrap2D.CampaignManifestPath : worldManifest ?? LevelBootstrap2D.WorldManifestPath);
        var loadedLevel = _resources.GetLoad<Levels.WorldAsset2D>("level/world", "simulation");
        _loadedWorld = loadedLevel;
        _levelPicker = new(loadedLevel.Maps);
        var tileMap = loadedLevel.TileMap;
        _terrainSource = new ViewportTerrainSource2D(tileMap);
        _saveStore = campaign ? campaignSaveStore ?? PlayerSaveStore2D.CreateCampaign() : worldManifest is null ?
            loadedLevel.StartAtArrival ? new PlayerSaveStore2D(PlayerSaveStore2D.CreateDefault().Path + ".playground") : PlayerSaveStore2D.CreateDefault() :
            new PlayerSaveStore2D(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(worldManifest))!, "player-save.json"));
        // Explicit world launches are playtests: always begin at the map's arrival.
        // The local store still records checkpoints for deaths during this session.
        var loadedSave = campaign ? (replay ? null : _saveStore.TryLoad()) :
            worldManifest is null && !loadedLevel.StartAtArrival ? _saveStore.TryLoad() : null;
        SavedProgress2D? progress = loadedSave is null ? null : new(loadedSave.SavePointId, loadedSave.HitPoints);

        // The one construction recipe for the simulation; the host only adds I/O and presentation.
        var definition = LevelBootstrap2D.CreateSessionDefinition(loadedLevel, progress);
        Traversal = definition.Traversal;
        _simulation = SideScrollerSimulation2D.Create(definition);
        _session = _simulation.Session;
        AttachPhysicsWorld(_simulation.Physics);
        RegisterDebugAttackShapes(_simulation.Arsenal.GetActiveAttackHitboxes);
        RegisterDebugAttackShapes(_simulation.Level.EnemySystem.GetActiveAttackHitboxes);

        var snapshot = _session.CaptureSnapshot();
        var playerId = _session.PlayerIds[0];
        var initialPlayer = snapshot.FindPlayer(playerId)!.Value;
        var startPosition = initialPlayer.Person.Position;
        _hasCheckpoint = initialPlayer.CheckpointId is not null;
        _savedClear = campaign && _hasCheckpoint && loadedSave is { CampaignCompleted: true, CampaignRevision: CampaignRevision };
        _campaignScreen = _savedClear ? CampaignScreen.Cleared : CampaignScreen.Briefing;
        _session.SetPaused(campaign);
        Camera.ReferenceViewportHeight = 1080f;
        var cameraController = new SideScrollerCamera2D(Camera,
            tileMap.WorldBounds, startPosition);
        if (_campaign)
        {
            Camera.Zoom = 1.7f;
            cameraController.Reset(startPosition);
            CampaignBackground2D.AddTo(Background);
        }
        else
        {
            SideScrollerBackground2D.AddTo(Background);
        }

        DebugPanel.Section("Camera").Slider("Zoom", () => Camera.Zoom, value => Camera.Zoom = value, 0.05f, 20f,
            "Camera zoom. Larger values zoom in.", logarithmic: true);

        // Only editor mode opens a writable database handle.
        _editor = new TileEditor2D(loadedLevel.Maps[0].TileMap, () => Levels.LevelDatabase2D.Open(loadedLevel.Maps[0].DatabasePath), Camera);
        _resources.Get<Levels.WorldAsset2D>("level/world", "editor");
        _resources.Get<TextureCache2D>("textures/runtime", "editor");
        _editor.ConfigureMaps(loadedLevel.Maps, loadedLevel.Root);
        _editor.ObjectsChanged += objects =>
        {
            loadedLevel.Maps[_editor.SelectedMapIndex].Objects = objects;
            _simulation.Level.ReloadObjects(loadedLevel.Objects);
        };

        DebugPanel.Section("World").Button("Reload giant trees", () =>
        {
            var trees = loadedLevel.LoadGiantTrees();
            _simulation.Level.ReloadGiantTrees(trees);
            return $"Loaded {trees.Length} giant trees with {trees.Sum(tree => tree.Branches.Length)} branch platforms.";
        }, "Reload giant-trees.json: positions and dimensions are in tiles; ranges are [min, max].");

        _client = new SideScrollerClient2D(snapshot, playerId, Scene,
            cameraController, textures, _sounds, Traversal, new Presentation.Persons.PersonMoves(hero), _terrainSource);
        _editorUi = new(_editor, Textures, DebugPanel, _client.Wardrobe);
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
        _campaign ? CampaignHud2D.Title :
        $"App2d Playground | PAD: {(_isControllerConnected ? "XBOX" : "OFF")} | GEAR: {_client.WeaponName} | HP: {_client.State.Person.HitPoints}/{_client.State.Person.MaximumHitPoints} | enemies: {_simulation.Combat.DefeatedEnemies}/{_simulation.Level.EnemySystem.Count} | chunks: {_simulation.Level.ActiveChunkCount}/{SideScrollerLevel2D.MaximumActiveChunkCount} | colliders: {_simulation.Level.LoadedColliderCount} | broad pairs: {_simulation.Physics.LastCandidatePairCount}{(_client.State.ReachedGoal ? " | Goal reached" : string.Empty)}";

    internal event Action? ReplayRequested;

    public override IGameToolUi2D? ToolUi => _editorUi;
    protected override Color4b BackgroundColor => new(103, 196, 235);

    public override void Update(FrameTime time, InputState input)
    {
        _isControllerConnected = input.Gamepad.IsConnected;
        if (_paperDollWasOpen != _editorUi.IsPaperDollOpen)
        {
            _paperDollWasOpen = _editorUi.IsPaperDollOpen;
            input.CancelButtons();
        }
        if (_editorUi.IsPaperDollOpen)
        {
            _session.SetPaused(true);
            _client.Suspend();
            return;
        }
        if (_campaign && !_editor.IsActive && HandleCampaignMenu(input))
        {
            return;
        }

        if (!_campaign && !_editor.IsActive)
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
            if (_campaign && _client.State.ReachedGoal)
            {
                var state = _client.State;
                var checkpoint = state.CheckpointId ?? frame.Content.Checkpoints.Select(item => (long?)item.ThingId).FirstOrDefault();
                _savedClear = checkpoint is { } id &&
                    _saveStore.TrySave(new PlayerSave2D(id, state.Person.HitPoints) { CampaignCompleted = true, CampaignRevision = CampaignRevision });
                _campaignScreen = CampaignScreen.Cleared;
                _session.SetPaused(true);
                _client.Suspend();
                input.CancelButtons();
            }
        }
    }

    private bool HandleCampaignMenu(InputState input)
    {
        var pause = !input.IsSuppressed && (input.Keyboard.WasPressed(Keys.P) || input.Gamepad.WasPressed(GamepadButton2D.Menu));
        if (_campaignScreen == CampaignScreen.Playing)
        {
            if (!pause)
            {
                return false;
            }

            _campaignScreen = CampaignScreen.Paused;
            _session.SetPaused(true);
            _client.Suspend();
            input.CancelButtons();
            return true;
        }

        var accept = !input.IsSuppressed && (input.Keyboard.WasPressed(Keys.Enter) || input.Gamepad.WasPressed(GamepadButton2D.A) ||
            (input.Mouse.WasPressed(MouseButtons.Left) && CampaignHud2D.HitsAction(Camera.ViewportSize, input.Mouse.PositionDevice)));
        if (_campaignScreen == CampaignScreen.Cleared)
        {
            if (accept)
            {
                ReplayRequested?.Invoke();
            }
            return true;
        }

        if (accept || (_campaignScreen == CampaignScreen.Paused && pause))
        {
            _campaignScreen = CampaignScreen.Playing;
            _session.SetPaused(false);
            input.CancelButtons();
        }

        return true;
    }

    public override void AdvancePresentation(FrameTime time)
    {
        _musicDirector.Update(_client.Content, _client.State.Person.Position, time.DeltaSeconds);
        _client.UpdateFeedback(time.DeltaSeconds);
        if (!_editor.IsActive && !_levelPicker.IsOpen && !_editorUi.IsPaperDollOpen &&
            (!_campaign || _campaignScreen != CampaignScreen.Paused))
        {
            _client.AdvancePresentation(time.DeltaSeconds);
        }

        _client.WorldSounds.Update();
    }

    public override void Render(Renderer2D renderer) => _client.DrawWorld(renderer);

    public override void RenderWorldDebug(Renderer2D renderer)
    {
        base.RenderWorldDebug(renderer);
        _client.DrawWorldDebug(renderer);
        TileEditorView2D.DrawWorldDebug(renderer, _editor);
        if (_showZones)
        {
            Span<Vector2f> outline = stackalloc Vector2f[4];

            foreach (var zone in _client.Content.Zones)
            {
                var b = zone.Bounds;
                if (!b.Intersects(Camera.VisibleWorldBounds))
                {
                    continue;
                }
                VertexGenerator2D.WriteRectangle(outline, b.Min, b.Max);
                renderer.DrawWorldPolyline(outline, zone.Id == _musicDirector.CurrentZone?.Id ? DebugTheme.ActiveZone : DebugTheme.ZoneOutline, 2f);
            }
        }
    }

    public override void RenderUI(Renderer2D renderer, FrameTime time)
    {
        if (!_campaign || _campaignScreen == CampaignScreen.Playing)
        {
            _client.DrawUI(renderer);
        }

        if (_campaign && !_editor.IsActive)
        {
            if (_campaignScreen == CampaignScreen.Playing)
            {
                CampaignHud2D.DrawObjective(renderer, _musicDirector.CurrentZone, _client.IsGoalUnlocked, _isControllerConnected,
                    _client.GoalGuardian, _client.State.Person.Position);
            }
            else
            {
                CampaignHud2D.DrawScreen(renderer, _campaignScreen, _hasCheckpoint, _savedClear, _isControllerConnected);
            }
        }
        else if (!_editor.IsActive)
        {
            _levelPicker.Draw(renderer);
        }

        base.RenderUI(renderer, time);
    }

    public override void Dispose()
    {
        _client.Dispose();
        _terrainSource.Dispose();
        _simulation.Dispose();
        _editor.Dispose();
        _editorUi.Dispose();
        _music.Dispose();
        _resources.Dispose();
        base.Dispose();
    }
}
