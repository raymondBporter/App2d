using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Grids;
using App2d.Core.Input;
using App2d.Core.Objects;
using App2d.Core.Rendering;
using App2d.Core.Tiles;
using App2d.Core.Validation;
using App2d.Levels;
using App2d.Things;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace App2d.Editor;

/// <summary>
/// The in-game tile painter. Owns editor mode, tool state, the free camera, and
/// write-through persistence; delegates tile mutation and undo to <see cref="TileEditSession2D"/>.
/// </summary>
internal sealed class TileEditor2D : IDisposable
{
    private const float ZoomStep = 1.1f;

    private EditableTileMap2D _map;
    private Func<LevelDatabase2D> _openDatabase;
    private readonly Camera2D _camera;
    private TileEditSession2D _session;

    private LevelDatabase2D? _database;
    private Vector2 _cameraFocus;
    private Vector2 _savedCameraPosition;
    private float _savedCameraZoom;
    private Vector2 _panAnchorDevice;
    private Vector2 _panAnchorFocus;
    private bool _isPanning;
    private bool _toggleRequested;
    public bool IsPointerOverUi { get; internal set; }
    private Vector2 _lastMouseDevice;
    private int _lastPaintedX;
    private int _lastPaintedY;
    private bool _hasLastPainted;
    private readonly List<ThingDefinitionRecord2D> _thingDefinitions = [];
    private readonly List<PositionThingRecord2D> _positionThings = [];
    private Stack<(Action<LevelDatabase2D> Apply, bool TouchesObjects)> _thingUndo = [];
    private bool _isPlacingThing;
    private PositionThingRecord2D? _positionDragOriginal;
    private ObjectAssetCatalog2D? _objectAssets;
    private readonly List<ObjectPlacement2D> _objectPlacements = [];

    /// <summary>
    /// <paramref name="openDatabase"/> opens a fresh read-write handle to the level file.
    /// It is invoked only on entering editor mode and disposed on leaving it, so a
    /// play-only session never holds a write-through handle open on <c>level.db</c> —
    /// that handle is what makes <c>git checkout level.db</c> fail while the game runs.
    /// </summary>
    public TileEditor2D(
        EditableTileMap2D map,
        Func<LevelDatabase2D> openDatabase,
        Camera2D camera)
    {
        _map = ArgGuard.RequireNotNull(map);
        _openDatabase = ArgGuard.RequireNotNull(openDatabase);
        _camera = ArgGuard.RequireNotNull(camera);
        _session = new TileEditSession2D(map);
        SelectedKind = TileKind2D.Solid;
        InspectorView = new ThingEditorInspector2D(this);
    }

    private IReadOnlyList<WorldMapAsset2D> _maps = [];
    private readonly Dictionary<int, TileEditSession2D> _mapSessions = [];
    private readonly Dictionary<int, Stack<(Action<LevelDatabase2D> Apply, bool TouchesObjects)>> _mapThingUndo = [];
    public int SelectedMapIndex { get; private set; }
    public string MapLabel => _maps.Count == 0 ? "Tile editor" : $"{_maps[SelectedMapIndex].ZoneId} / {_maps[SelectedMapIndex].Name}";
    public int MapCount => _maps.Count;

    /// <summary><paramref name="worldRoot"/> is the folder holding the world's <c>objects</c> assets; without it, object things cannot be shown or placed.</summary>
    internal void ConfigureMaps(IReadOnlyList<WorldMapAsset2D> maps, string? worldRoot = null)
    {
        if (IsActive || maps.Count == 0 || !ReferenceEquals(maps[0].TileMap, _map))
        {
            throw new InvalidOperationException("Configure maps before editing, with the current map first.");
        }

        _maps = maps;
        _objectAssets = worldRoot is null ? null : new ObjectAssetCatalog2D(worldRoot);
        _mapSessions[0] = _session;
        _mapThingUndo[0] = _thingUndo;
    }

    internal void CycleMap(int direction)
    {
        if (!IsActive || _maps.Count < 2)
        {
            return;
        }

        var next = (SelectedMapIndex + direction + _maps.Count) % _maps.Count;
        EndStrokeIfActive();
        CommitThingDrag();
        var asset = _maps[next];
        // Open the destination first: failure leaves the current editing session usable.
        var database = LevelDatabase2D.Open(asset.DatabasePath);
        _database?.Dispose();
        _database = database;
        SelectedMapIndex = next;
        _map = asset.TileMap;
        _openDatabase = () => LevelDatabase2D.Open(asset.DatabasePath);
        if (!_mapSessions.TryGetValue(next, out _session!))
        {
            _mapSessions[next] = _session = new TileEditSession2D(_map);
        }

        if (!_mapThingUndo.TryGetValue(next, out _thingUndo!))
        {
            _mapThingUndo[next] = _thingUndo = [];
        }

        SelectedTilesetIndex = 0;
        SelectedThingId = null;
        SelectedDefinitionId = null;
        _isPlacingThing = false;
        _isPanning = false;
        _cameraFocus = (_map.WorldBounds.Min + _map.WorldBounds.Max) / 2;
        _camera.Position = _cameraFocus;
        ReloadThings(notifyRuntime: false);
    }
    public bool IsActive { get; private set; }
    public LevelEditorMode2D Mode { get; private set; }
    public TileKind2D SelectedKind { get; private set; }
    public byte SelectedTilesetIndex { get; private set; }
    public string SelectedTilesetId => _map.TilesetIds[SelectedTilesetIndex];
    public IReadOnlyList<string> TilesetIds => _map.TilesetIds;
    public Vector2 MouseDevicePosition => _lastMouseDevice;
    public Vector2 CameraFocus => _cameraFocus;
    public Rect2D VisibleWorldBounds => _camera.VisibleWorldBounds;
    public Rect2D MapBounds => _map.WorldBounds;
    public GridGeometry2D GridGeometry => _map.GridGeometry;
    public float TileSize => _map.TileSize;
    public Vector2 VisibleDeviceSize => _camera.ViewportSize;
    public float Zoom => _camera.Zoom;
    public float PixelsToWorldUnits(float pixels) => _camera.PixelsToWorldUnits(pixels);
    public ThingEditorInspector2D InspectorView { get; }
    public IReadOnlyList<ThingDefinitionRecord2D> ThingDefinitions => _thingDefinitions;
    public IReadOnlyList<PositionThingRecord2D> PositionThings => _positionThings;
    /// <summary>The configured world's object asset files; configure maps with a world root first.</summary>
    public ObjectAssetCatalog2D ObjectAssets => StateGuard.RequireNotNull(_objectAssets, "Configure maps with a world root before using object assets.");
    public IReadOnlyList<string> ObjectAssetPaths => _objectAssets?.Paths ?? [];
    /// <summary>The current map's enabled object things as placements with world-wide ids, rebuilt whenever things reload.</summary>
    public IReadOnlyList<ObjectPlacement2D> ObjectPlacements => _objectPlacements;
    public long? SelectedDefinitionId { get; private set; }
    public long? SelectedThingId { get; private set; }
    public PositionThingRecord2D? SelectedPositionThing =>
        SelectedThingId is { } thingId
            ? _positionThings.SingleOrDefault(item => item.ThingId == thingId)
            : null;
    public ThingDefinitionRecord2D? SelectedDefinition =>
        SelectedDefinitionId is { } definitionId
            ? _thingDefinitions.SingleOrDefault(item => item.DefinitionId == definitionId)
            : null;
    public bool HasSelectedThing => SelectedPositionThing is not null;
    public bool IsPlacingThing => _isPlacingThing;
    public bool CanUndoThingEdit => _thingUndo.Count > 0;

    /// <summary>Raised after an object definition or placement changes, with the current map's placements.</summary>
    public event Action<IReadOnlyList<ObjectPlacement2D>>? ObjectsChanged;

    /// <summary>The asset an object definition names, when a world is configured and the file loads.</summary>
    public bool TryGetObjectAsset(string? assetPath, [NotNullWhen(true)] out ObjectAsset2D? asset)
    {
        asset = null;
        return _objectAssets is not null && !string.IsNullOrWhiteSpace(assetPath) && _objectAssets.TryLoad(assetPath, out asset);
    }

    public bool TryGetHoveredTile(out int x, out int y)
    {
        if (Mode != LevelEditorMode2D.Tiles || IsPointerOverUi)
        {
            x = 0;
            y = 0;
            return false;
        }

        var world = _camera.DeviceToWorld(_lastMouseDevice);
        if (!GridGeometry.TryWorldToCell(world, out var cell))
        {
            x = 0;
            y = 0;
            return false;
        }
        x = cell.X;
        y = cell.Y;
        return _map.GridSize.Contains(cell);
    }

    public void Update(InputState input)
    {
        if (_toggleRequested || input.Keyboard.WasPressed(Keys.F1))
        {
            _toggleRequested = false;
            IsActive = !IsActive;
            if (IsActive)
            {
                BeginEditingSession();
            }
            else
            {
                EndEditingSession();
            }
        }

        if (!IsActive)
        {
            return;
        }

        if (input.Keyboard.WasPressed(Keys.PageDown))
        {
            CycleMap(1);
        }

        if (input.Keyboard.WasPressed(Keys.PageUp))
        {
            CycleMap(-1);
        }

        _lastMouseDevice = input.Mouse.PositionDevice;
        if (Mode == LevelEditorMode2D.Things)
        {
            UpdateCamera(input, isPointerOverMenu: IsPointerOverUi);
            UpdateThings(input);
            if (input.Keyboard.IsControlDown && input.Keyboard.WasPressed(Keys.Z))
            {
                UndoThingEdit();
            }

            _camera.Position = _cameraFocus;
            return;
        }

        var isPointerOverMenu = IsPointerOverUi;
        UpdateCamera(input, isPointerOverMenu);
        UpdatePainting(input, isPointerOverMenu);

        // A stroke may still be in progress (mouse button held). TileEditSession2D.Undo()
        // throws if called mid-stroke, so ignore the request rather than let it crash the
        // editor; the in-progress stroke is left untouched either way.
        if (input.Keyboard.IsControlDown && input.Keyboard.WasPressed(Keys.Z) && !_session.IsStrokeActive)
        {
            UndoTiles();
        }

        _camera.Position = _cameraFocus;
    }

    internal void RequestToggle() => _toggleRequested = true;

    internal void UndoTiles()
    {
        if (!_session.IsStrokeActive)
        {
            CommitChunks(_session.Undo());
        }
    }

    internal void SelectTileset(int index)
    {
        if (index < 0 || index >= _map.TilesetIds.Count)
        {
            ArgGuard.ThrowOutOfRange(index, "Tileset selection must exist in the map catalog.");
        }

        SelectedTilesetIndex = (byte)index;
    }

    internal void SelectKind(TileKind2D kind) => SelectedKind = kind;

    internal void SelectMode(LevelEditorMode2D mode)
    {
        EndStrokeIfActive();
        CommitThingDrag();
        Mode = mode;
        _isPlacingThing = false;
        InspectorView.RefreshFromEditor();
    }

    internal void SelectDefinition(long definitionId)
    {
        StateGuard.ThrowIf(_thingDefinitions.All(item => item.DefinitionId != definitionId), $"Thing definition {definitionId} is not loaded.");
        SelectedDefinitionId = definitionId;
        SelectedThingId = null;
        _isPlacingThing = false;
    }

    internal void BeginThingPlacement()
    {
        StateGuard.ThrowIf(SelectedDefinitionId is null, "Select a thing definition before placing it.");
        SelectedThingId = null;
        _isPlacingThing = true;
        InspectorView.RefreshFromEditor();
    }

    internal void ApplyPositionDefinition(long? definitionId, string typeKey, PositionThingDefinitionProperties2D properties, string? asset = null)
    {
        ArgGuard.ThrowIfNull(properties);
        var descriptor = ThingTypeRegistry2D.Require(typeKey);
        StateGuard.ThrowIf(descriptor.WorldKind is null && !descriptor.IsObject, $"Thing type '{typeKey}' is not position-only.");
        if (descriptor.IsObject && !string.IsNullOrWhiteSpace(asset))
        {
            RequireLoadableAsset(asset);
        }

        var database = RequireDatabase();
        var saved = definitionId is { } existingId
            ? database.UpdateThingDefinition(new ThingDefinitionRecord2D(existingId, typeKey, properties.Name, asset))
            : database.CreateThingDefinition(typeKey, properties.Name, asset);
        SelectedDefinitionId = saved.DefinitionId;
        SelectedThingId = null;
        ReloadThings(notifyRuntime: descriptor.IsObject);
    }

    /// <summary>An object definition is written only once its asset loads, so a bad file never leaves the map unloadable.</summary>
    private void RequireLoadableAsset(string asset)
    {
        try
        {
            _ = ObjectAssets.Load(asset);
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            throw new InvalidOperationException($"Asset '{asset}' cannot be used: {e.Message}", e);
        }
    }

    internal void DeleteDefinition(long definitionId)
    {
        RequireDatabase().DeleteThingDefinition(definitionId);
        if (SelectedDefinitionId == definitionId)
        {
            SelectedDefinitionId = null;
        }

        ReloadThings(notifyRuntime: false);
    }

    internal void ApplyPositionThing(long thingId, PositionThingInstanceProperties2D properties)
    {
        ArgGuard.ThrowIfNull(properties);
        var old = _positionThings.SingleOrDefault(item => item.ThingId == thingId) ??
            throw new InvalidOperationException($"Position thing {thingId} is not loaded.");
        var updated = old with
        {
            Name = properties.Name,
            Enabled = properties.Enabled,
            X = properties.PositionX,
            Y = properties.PositionY
        };
        var touchesObjects = ThingTypeRegistry2D.Require(old.TypeKey).IsObject;
        RequireDatabase().UpdatePositionThing(updated);
        _thingUndo.Push((database => database.UpdatePositionThing(old), touchesObjects));
        SelectedThingId = thingId;
        ReloadThings(notifyRuntime: touchesObjects);
    }

    internal void DeleteSelectedThing()
    {
        if (SelectedPositionThing is not { } positionThing)
        {
            return;
        }

        var touchesObjects = ThingTypeRegistry2D.Require(positionThing.TypeKey).IsObject;
        var deleted = RequireDatabase().DeletePositionThing(positionThing.ThingId);
        _thingUndo.Push((database => database.RestorePositionThing(deleted), touchesObjects));
        SelectedThingId = null;
        ReloadThings(notifyRuntime: touchesObjects);
    }

    internal bool TryGetPositionPlacementPreview(out ThingDefinitionRecord2D definition, out Vector2 position)
    {
        definition = null!;
        position = default;
        if (!IsActive || Mode != LevelEditorMode2D.Things || !_isPlacingThing || SelectedDefinition is not { } selected)
        {
            return false;
        }

        var descriptor = ThingTypeRegistry2D.Require(selected.TypeKey);
        if (descriptor.WorldKind is null && !descriptor.IsObject)
        {
            return false;
        }

        definition = selected;
        position = SnapToGrid(_camera.DeviceToWorld(_lastMouseDevice));
        return true;
    }

    private void UpdateCamera(InputState input, bool isPointerOverMenu)
    {
        if (input.Mouse.WasReleased(MouseButtons.Middle))
        {
            _isPanning = false;
        }

        if (isPointerOverMenu)
        {
            _isPanning = false;
            return;
        }

        if (input.Mouse.WasPressed(MouseButtons.Middle))
        {
            _isPanning = true;
            _panAnchorDevice = input.Mouse.PositionDevice;
            _panAnchorFocus = _cameraFocus;
        }

        if (_isPanning)
        {
            var deviceDelta = input.Mouse.PositionDevice - _panAnchorDevice;
            // Transform a displacement without translation, accounting for viewport scale,
            // zoom, rotation, and the device-to-world Y flip.
            _cameraFocus = _panAnchorFocus - Vector2.TransformNormal(deviceDelta, _camera.DeviceToWorldMatrix);
        }

        if (input.Mouse.WheelDelta != 0f)
        {
            // Camera2D.Zoom clamps to its own MinZoom/MaxZoom on assignment; do not
            // add a second clamp here with different bounds.
            var factor = input.Mouse.WheelDelta > 0f ? ZoomStep : 1f / ZoomStep;
            _camera.Zoom *= factor;
        }
    }

    private void UpdatePainting(InputState input, bool isPointerOverMenu)
    {
        if (isPointerOverMenu)
        {
            EndStrokeIfActive();
            return;
        }

        var isPainting = input.Mouse.IsDown(MouseButtons.Left);
        var isErasing = input.Mouse.IsDown(MouseButtons.Right);

        if (input.Mouse.WasPressed(MouseButtons.Left) || input.Mouse.WasPressed(MouseButtons.Right))
        {
            // Pressing the other button mid-stroke (e.g. RMB while LMB is still held)
            // must commit the in-progress stroke rather than discard it: BeginStroke
            // on TileEditSession2D simply clears the current stroke buffer.
            EndStrokeIfActive();
            _session.BeginStroke();
            _hasLastPainted = false;
        }

        if ((isPainting || isErasing) && _session.IsStrokeActive && TryGetHoveredTile(out var x, out var y))
        {
            var tile = isErasing
                ? new TileCell2D(TileKind2D.Empty, 0)
                : new TileCell2D(SelectedKind, SelectedTilesetIndex);
            if (_hasLastPainted)
            {
                _session.PaintLine(_lastPaintedX, _lastPaintedY, x, y, tile);
            }
            else
            {
                _session.Paint(x, y, tile);
            }

            _lastPaintedX = x;
            _lastPaintedY = y;
            _hasLastPainted = true;
        }

        // End on button *state* rather than the release edge: losing window focus or
        // opening the developer console clears held mouse buttons (InputState.CancelButtons)
        // without ever raising a release edge, which would otherwise orphan the stroke —
        // IsStrokeActive stays true forever, painting stops working, and Ctrl+Z is silently
        // swallowed by the "no undo mid-stroke" guard.
        if (_session.IsStrokeActive && !isPainting && !isErasing)
        {
            EndStrokeIfActive();
        }
    }

    private void UpdateThings(InputState input)
    {
        if (_isPlacingThing && input.Mouse.WasPressed(MouseButtons.Left))
        {
            PlaceThing();
            return;
        }

        if (input.Mouse.WasPressed(MouseButtons.Right))
        {
            _isPlacingThing = false;
            InspectorView.RefreshFromEditor();
            return;
        }

        if (input.Mouse.WasPressed(MouseButtons.Left))
        {
            BeginThingSelectionOrDrag();
        }

        if (_positionDragOriginal is not null && input.Mouse.IsDown(MouseButtons.Left))
        {
            UpdateThingDrag();
        }

        if (_positionDragOriginal is not null && !input.Mouse.IsDown(MouseButtons.Left))
        {
            CommitThingDrag();
        }
    }

    private void PlaceThing()
    {
        if (SelectedDefinition is not { } definition)
        {
            return;
        }

        var position = SnapToGrid(_camera.DeviceToWorld(_lastMouseDevice));
        var created = RequireDatabase().CreatePositionThing(new NewPositionThing2D(
            definition.DefinitionId,
            Name: null,
            Enabled: true,
            position.X,
            position.Y));
        var touchesObjects = ThingTypeRegistry2D.Require(definition.TypeKey).IsObject;
        _thingUndo.Push((database => database.DeletePositionThing(created.ThingId), touchesObjects));
        SelectedThingId = created.ThingId;
        _isPlacingThing = false;
        ReloadThings(notifyRuntime: touchesObjects);
    }

    private void BeginThingSelectionOrDrag()
    {
        var world = _camera.DeviceToWorld(_lastMouseDevice);
        var handleRadius = _camera.PixelsToWorldUnits(12f);
        var handleRadiusSquared = handleRadius * handleRadius;
        foreach (var thing in _positionThings.AsEnumerable().Reverse())
        {
            var position = new Vector2(thing.X, thing.Y);
            bool hit;
            if (TryGetObjectAsset(thing.Asset, out var asset))
            {
                var bounds = ShapeBounds2D.Calculate(asset.Collision ?? asset.Geometry!);
                var local = world - position;
                hit = local.X >= bounds.Min.X && local.X <= bounds.Max.X && local.Y >= bounds.Min.Y && local.Y <= bounds.Max.Y;
            }
            else
            {
                hit = Vector2.DistanceSquared(world, position) <= handleRadiusSquared * 2.25f;
            }

            if (hit)
            {
                BeginPositionThingDrag(thing);
                return;
            }
        }

        SelectedThingId = null;
        InspectorView.RefreshFromEditor();
    }

    private void BeginPositionThingDrag(PositionThingRecord2D thing)
    {
        SelectedThingId = thing.ThingId;
        SelectedDefinitionId = thing.DefinitionId;
        _positionDragOriginal = thing;
        InspectorView.ShowPositionThing(thing);
    }

    private void UpdateThingDrag()
    {
        if (SelectedThingId is not { } thingId || _positionDragOriginal is null)
        {
            return;
        }

        var index = _positionThings.FindIndex(item => item.ThingId == thingId);
        if (index >= 0)
        {
            var target = SnapToGrid(_camera.DeviceToWorld(_lastMouseDevice));
            _positionThings[index] = _positionThings[index] with { X = target.X, Y = target.Y };
        }
    }

    private void CommitThingDrag()
    {
        if (_positionDragOriginal is not { } original)
        {
            return;
        }

        _positionDragOriginal = null;
        var moved = SelectedPositionThing;
        if (moved is null || moved == original)
        {
            return;
        }

        var touchesObjects = ThingTypeRegistry2D.Require(moved.TypeKey).IsObject;
        RequireDatabase().UpdatePositionThing(moved);
        _thingUndo.Push((database => database.UpdatePositionThing(original), touchesObjects));
        ReloadThings(notifyRuntime: touchesObjects);
    }

    internal void UndoThingEdit()
    {
        CommitThingDrag();
        if (_thingUndo.Count == 0)
        {
            return;
        }

        var (apply, touchesObjects) = _thingUndo.Pop();
        apply(RequireDatabase());
        SelectedThingId = null;
        ReloadThings(notifyRuntime: touchesObjects);
    }

    private Vector2 SnapToGrid(Vector2 world) => GridGeometry.SnapToNearestCorner(world);

    private void ReloadThings(bool notifyRuntime)
    {
        var database = RequireDatabase();
        _thingDefinitions.Clear();
        _thingDefinitions.AddRange(database.LoadThingDefinitions());
        foreach (var definition in _thingDefinitions)
        {
            _ = ThingTypeRegistry2D.Require(definition.TypeKey);
        }

        _positionThings.Clear();
        _positionThings.AddRange(database.LoadPositionThings().Where(thing =>
        {
            var descriptor = ThingTypeRegistry2D.Require(thing.TypeKey);
            return descriptor.WorldKind is not null || descriptor.IsObject;
        }));
        RebuildObjectPlacements();
        if (SelectedDefinitionId is { } definitionId && _thingDefinitions.All(item => item.DefinitionId != definitionId))
        {
            SelectedDefinitionId = null;
        }

        if (SelectedThingId is { } thingId && _positionThings.All(item => item.ThingId != thingId))
        {
            SelectedThingId = null;
        }

        InspectorView.RefreshFromEditor();
        if (notifyRuntime)
        {
            ObjectsChanged?.Invoke([.. _objectPlacements]);
        }
    }

    private void RebuildObjectPlacements()
    {
        _objectPlacements.Clear();
        if (_objectAssets is null)
        {
            return;
        }

        foreach (var thing in _positionThings)
        {
            if (!thing.Enabled || !ThingTypeRegistry2D.Require(thing.TypeKey).IsObject)
            {
                continue;
            }

            var worldThingId = _maps.Count == 0 ? thing.ThingId : _maps[SelectedMapIndex].WorldThingId(thing.ThingId);
            _objectPlacements.Add(ThingTypeRegistry2D.ToPlacement(thing, _objectAssets.Load(thing.Asset!), worldThingId));
        }
    }

    private LevelDatabase2D RequireDatabase() =>
        StateGuard.RequireNotNull(_database, "Thing editing requires an open level database.");

    private void EndStrokeIfActive()
    {
        if (!_session.IsStrokeActive)
        {
            return;
        }

        CommitChunks(_session.EndStroke());
        _hasLastPainted = false;
    }

    /// <summary>
    /// Called when editor mode is toggled on: opens the write-through database handle and
    /// captures the camera's current position and zoom so gameplay framing can be restored
    /// exactly on exit, even after the free camera pans and zooms while editing.
    /// </summary>
    private void BeginEditingSession()
    {
        _cameraFocus = _camera.Position;
        _savedCameraPosition = _camera.Position;
        _savedCameraZoom = _camera.Zoom;
        _database = _openDatabase();
        // Rebuild authored things at their persisted starts so viewport outlines and
        // runtime visuals coincide even if an object was midway through its path
        // when editing began.
        ReloadThings(notifyRuntime: true);
    }

    /// <summary>
    /// Called when editor mode is toggled off: ends any in-progress stroke, clears the
    /// pan state (so a middle-drag started before exiting never resumes with a stale
    /// anchor on the next entry), restores the camera's pre-edit position and zoom, and
    /// releases the write-through database handle so a play-only session never keeps
    /// <c>level.db</c> locked.
    /// </summary>
    private void EndEditingSession()
    {
        EndStrokeIfActive();
        CommitThingDrag();
        _isPanning = false;
        _isPlacingThing = false;
        _camera.Position = _savedCameraPosition;
        _camera.Zoom = _savedCameraZoom;
        _database?.Dispose();
        _database = null;
    }

    /// <summary>Writes a stroke's changed chunks in one transaction.</summary>
    private void CommitChunks(IReadOnlyCollection<TileChunk2D> chunks)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        StateGuard.RequireNotNull(_database, "A stroke committed with no open editing database.")
            .SaveChunks(_map, chunks.ToArray());
    }

    /// <summary>
    /// Ends any stroke still open when the game shuts down while in editor mode, so the
    /// database is never disposed out from under an uncommitted stroke.
    /// </summary>
    public void Dispose()
    {
        EndStrokeIfActive();
        CommitThingDrag();
        _database?.Dispose();
    }
}
