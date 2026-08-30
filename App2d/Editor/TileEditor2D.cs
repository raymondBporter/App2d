using App2d.Core;
using App2d.Core.Geometry;
using App2d.Levels;
using App2d.Rendering;
using App2d.Tiles;
using App2d.Things;
using System.Numerics;

namespace App2d.Editor;

/// <summary>
/// The in-game tile painter. Owns editor mode, tool state, the free camera, and
/// write-through persistence; delegates tile mutation and undo to <see cref="TileEditSession2D"/>.
/// </summary>
internal sealed class TileEditor2D : IDisposable
{
    private const float ZoomStep = 1.1f;

    private readonly EditableTileMap2D _map;
    private readonly Func<LevelDatabase2D> _openDatabase;
    private readonly Camera2D _camera;
    private readonly TileEditSession2D _session;
    private readonly Vector2 _origin;
    private readonly float _tileSize;

    private LevelDatabase2D? _database;
    private Vector2 _cameraFocus;
    private Vector2 _savedCameraPosition;
    private float _savedCameraZoom;
    private Vector2 _panAnchorDevice;
    private Vector2 _panAnchorFocus;
    private bool _isPanning;
    private Vector2 _lastMouseDevice;
    private int _lastPaintedX;
    private int _lastPaintedY;
    private bool _hasLastPainted;
    private readonly List<MovingPlatformDefinitionRecord2D> _movingPlatformDefinitions = [];
    private readonly List<MovingPlatformThingRecord2D> _movingPlatformThings = [];
    private readonly List<ThingDefinitionRecord2D> _thingDefinitions = [];
    private readonly List<PositionThingRecord2D> _positionThings = [];
    private readonly Stack<Action<LevelDatabase2D>> _thingUndo = [];
    private bool _isPlacingThing;
    private ThingDragHandle2D _thingDragHandle;
    private MovingPlatformThingRecord2D? _thingDragOriginal;

    /// <summary>
    /// <paramref name="openDatabase"/> opens a fresh read-write handle to the level file.
    /// It is invoked only on entering editor mode and disposed on leaving it, so a
    /// play-only session never holds a write-through handle open on <c>level.db</c> —
    /// that handle is what makes <c>git checkout level.db</c> fail while the game runs.
    /// </summary>
    public TileEditor2D(
        EditableTileMap2D map,
        Func<LevelDatabase2D> openDatabase,
        Camera2D camera,
        Vector2 origin,
        float tileSize)
    {
        _map = ArgGuard.RequireNotNull(map);
        _openDatabase = ArgGuard.RequireNotNull(openDatabase);
        _camera = ArgGuard.RequireNotNull(camera);
        ArgGuard.ThrowIfNotPositive(tileSize);
        _origin = origin;
        _tileSize = tileSize;
        _session = new TileEditSession2D(map);
        SelectedKind = TileKind2D.Solid;
        InspectorView = new ThingEditorInspector2D(this);
    }

    public bool IsActive { get; private set; }
    public LevelEditorMode2D Mode { get; private set; }
    public TileKind2D SelectedKind { get; private set; }
    public byte SelectedTilesetIndex { get; private set; }
    public string SelectedTilesetId => _map.TilesetIds[SelectedTilesetIndex];
    public IReadOnlyList<string> TilesetIds => _map.TilesetIds;
    public Vector2 MouseDevicePosition => _lastMouseDevice;
    public Vector2 CameraFocus => _cameraFocus;
    public Bounds2D VisibleWorldBounds => _camera.VisibleWorldBounds;
    public Vector2 VisibleDeviceSize => _camera.ViewportSize;
    public float Zoom => _camera.Zoom;
    public ThingEditorInspector2D InspectorView { get; }
    public IReadOnlyList<MovingPlatformDefinitionRecord2D> MovingPlatformDefinitions => _movingPlatformDefinitions;
    public IReadOnlyList<MovingPlatformThingRecord2D> MovingPlatformThings => _movingPlatformThings;
    public IReadOnlyList<ThingDefinitionRecord2D> ThingDefinitions => _thingDefinitions;
    public IReadOnlyList<PositionThingRecord2D> PositionThings => _positionThings;
    public long? SelectedDefinitionId { get; private set; }
    public long? SelectedThingId { get; private set; }
    public MovingPlatformThingRecord2D? SelectedThing =>
        SelectedThingId is { } thingId
            ? _movingPlatformThings.SingleOrDefault(item => item.ThingId == thingId)
            : null;
    public PositionThingRecord2D? SelectedPositionThing =>
        SelectedThingId is { } thingId
            ? _positionThings.SingleOrDefault(item => item.ThingId == thingId)
            : null;
    public ThingDefinitionRecord2D? SelectedDefinition =>
        SelectedDefinitionId is { } definitionId
            ? _thingDefinitions.SingleOrDefault(item => item.DefinitionId == definitionId)
            : null;
    public bool HasSelectedThing => SelectedThing is not null || SelectedPositionThing is not null;
    public bool IsPlacingThing => _isPlacingThing;
    public bool CanUndoThingEdit => _thingUndo.Count > 0;

    public event Action<IReadOnlyList<MovingPlatformThingRecord2D>>? ThingsChanged;

    public bool TryGetHoveredTile(out int x, out int y)
    {
        if (Mode != LevelEditorMode2D.Tiles || TileEditorMenu2D.Contains(_camera.ViewportSize, _lastMouseDevice))
        {
            x = 0;
            y = 0;
            return false;
        }

        var world = _camera.DeviceToWorld(_lastMouseDevice);
        x = (int)MathF.Floor((world.X - _origin.X) / _tileSize);
        y = (int)MathF.Floor((world.Y - _origin.Y) / _tileSize);
        return x >= 0 && x < _map.Width && y >= 0 && y < _map.Height;
    }

    public void Update(InputState input)
    {
        if (input.WasKeyPressed(Keys.F1))
        {
            IsActive = !IsActive;
            if (IsActive)
                BeginEditingSession();
            else
                EndEditingSession();
        }

        if (!IsActive)
            return;

        _lastMouseDevice = input.MousePositionDevice;
        if (Mode == LevelEditorMode2D.Things)
        {
            UpdateCamera(input, isPointerOverMenu: false);
            UpdateThings(input);
            if (input.IsControlDown && input.WasKeyPressed(Keys.Z))
                UndoThingEdit();
            _camera.Position = _cameraFocus;
            return;
        }

        var isPointerOverMenu = TileEditorMenu2D.Contains(_camera.ViewportSize, _lastMouseDevice);
        if (isPointerOverMenu && input.WasMousePressed(MouseButtons.Left))
        {
            EndStrokeIfActive();
            TileEditorMenu2D.TrySelect(this, _camera.ViewportSize, _lastMouseDevice);
        }

        UpdateCamera(input, isPointerOverMenu);
        UpdatePainting(input, isPointerOverMenu);

        // A stroke may still be in progress (mouse button held). TileEditSession2D.Undo()
        // throws if called mid-stroke, so ignore the request rather than let it crash the
        // editor; the in-progress stroke is left untouched either way.
        if (input.IsControlDown && input.WasKeyPressed(Keys.Z) && !_session.IsStrokeActive)
            CommitChunks(_session.Undo());

        _camera.Position = _cameraFocus;
    }

    internal void SelectTileset(int index)
    {
        if (index < 0 || index >= _map.TilesetIds.Count)
            ArgGuard.ThrowOutOfRange(index, "Tileset selection must exist in the map catalog.");
        SelectedTilesetIndex = (byte)index;
    }

    internal void SelectKind(TileKind2D kind) => SelectedKind = kind;

    internal void SelectMode(LevelEditorMode2D mode)
    {
        EndStrokeIfActive();
        CommitThingDrag();
        Mode = mode;
        _isPlacingThing = false;
        InspectorView.Visible = IsActive && mode == LevelEditorMode2D.Things;
        if (InspectorView.Visible)
        {
            InspectorView.BringToFront();
            InspectorView.RefreshFromEditor();
        }
    }

    internal void SelectDefinition(long definitionId)
    {
        StateGuard.ThrowIf(
            _thingDefinitions.All(item => item.DefinitionId != definitionId),
            $"Thing definition {definitionId} is not loaded.");
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

    internal void ApplyDefinition(long? definitionId, MovingPlatformDefinitionProperties2D properties)
    {
        ArgGuard.ThrowIfNull(properties);
        var database = RequireDatabase();
        var colorArgb = properties.Color.ToArgb();
        var saved = definitionId is { } existingId
            ? database.UpdateMovingPlatformDefinition(new MovingPlatformDefinitionRecord2D(
                existingId,
                properties.Name,
                properties.Width,
                properties.Height,
                colorArgb))
            : database.CreateMovingPlatformDefinition(
                properties.Name,
                properties.Width,
                properties.Height,
                colorArgb);
        SelectedDefinitionId = saved.DefinitionId;
        SelectedThingId = null;
        ReloadThings(notifyRuntime: true);
    }

    internal void ApplyPositionDefinition(
        long? definitionId,
        string typeKey,
        PositionThingDefinitionProperties2D properties)
    {
        ArgGuard.ThrowIfNull(properties);
        var descriptor = ThingTypeRegistry2D.Require(typeKey);
        StateGuard.ThrowIf(descriptor.WorldKind is null, $"Thing type '{typeKey}' is not position-only.");
        var database = RequireDatabase();
        var saved = definitionId is { } existingId
            ? database.UpdateThingDefinition(new ThingDefinitionRecord2D(existingId, typeKey, properties.Name))
            : database.CreateThingDefinition(typeKey, properties.Name);
        SelectedDefinitionId = saved.DefinitionId;
        SelectedThingId = null;
        ReloadThings(notifyRuntime: false);
    }

    internal void DeleteDefinition(long definitionId)
    {
        RequireDatabase().DeleteThingDefinition(definitionId);
        if (SelectedDefinitionId == definitionId)
            SelectedDefinitionId = null;
        ReloadThings(notifyRuntime: false);
    }

    internal void ApplyThing(long thingId, MovingPlatformInstanceProperties2D properties)
    {
        ArgGuard.ThrowIfNull(properties);
        var old = _movingPlatformThings.SingleOrDefault(item => item.ThingId == thingId) ??
            throw new InvalidOperationException($"Moving-platform thing {thingId} is not loaded.");
        var updated = old with
        {
            Name = properties.Name,
            Enabled = properties.Enabled,
            X = properties.PositionX,
            Y = properties.PositionY,
            TravelX = properties.TravelX,
            TravelY = properties.TravelY,
            Speed = properties.Speed
        };
        RequireDatabase().UpdateMovingPlatform(updated);
        _thingUndo.Push(database => database.UpdateMovingPlatform(old));
        SelectedThingId = thingId;
        ReloadThings(notifyRuntime: true);
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
        RequireDatabase().UpdatePositionThing(updated);
        _thingUndo.Push(database => database.UpdatePositionThing(old));
        SelectedThingId = thingId;
        ReloadThings(notifyRuntime: false);
    }

    internal void DeleteSelectedThing()
    {
        if (SelectedThingId is not { } thingId)
            return;
        if (SelectedThing is { } movingPlatform)
        {
            var deleted = RequireDatabase().DeleteMovingPlatform(movingPlatform.ThingId);
            _thingUndo.Push(database => database.RestoreMovingPlatform(deleted));
        }
        else if (SelectedPositionThing is { } positionThing)
        {
            var deleted = RequireDatabase().DeletePositionThing(positionThing.ThingId);
            _thingUndo.Push(database => database.RestorePositionThing(deleted));
        }
        SelectedThingId = null;
        ReloadThings(notifyRuntime: true);
    }

    internal bool TryGetPlacementPreview(out MovingPlatformDefinitionRecord2D definition, out Vector2 position)
    {
        definition = null!;
        position = default;
        if (!IsActive || Mode != LevelEditorMode2D.Things || !_isPlacingThing || SelectedDefinitionId is not { } definitionId)
            return false;
        definition = _movingPlatformDefinitions.SingleOrDefault(item => item.DefinitionId == definitionId)!;
        if (definition is null)
            return false;
        position = SnapToGrid(_camera.DeviceToWorld(_lastMouseDevice));
        return true;
    }

    internal bool TryGetPositionPlacementPreview(out ThingDefinitionRecord2D definition, out Vector2 position)
    {
        definition = null!;
        position = default;
        if (!IsActive || Mode != LevelEditorMode2D.Things || !_isPlacingThing || SelectedDefinition is not { } selected)
            return false;
        if (ThingTypeRegistry2D.Require(selected.TypeKey).WorldKind is null)
            return false;
        definition = selected;
        position = SnapToGrid(_camera.DeviceToWorld(_lastMouseDevice));
        return true;
    }

    private void UpdateCamera(InputState input, bool isPointerOverMenu)
    {
        if (input.WasMouseReleased(MouseButtons.Middle))
            _isPanning = false;

        if (isPointerOverMenu)
        {
            _isPanning = false;
            return;
        }

        if (input.WasMousePressed(MouseButtons.Middle))
        {
            _isPanning = true;
            _panAnchorDevice = input.MousePositionDevice;
            _panAnchorFocus = _cameraFocus;
        }

        if (_isPanning)
        {
            var deviceDelta = input.MousePositionDevice - _panAnchorDevice;
            // Device Y points down while world Y points up (Camera2D.WorldToDeviceMatrix
            // uses CreateScale(Zoom, -Zoom)), so the vertical component must be negated
            // or a drag would pan the view the wrong way.
            _cameraFocus = _panAnchorFocus - new Vector2(deviceDelta.X, -deviceDelta.Y) / _camera.Zoom;
        }

        if (input.MouseWheelDelta != 0f)
        {
            // Camera2D.Zoom clamps to its own MinZoom/MaxZoom on assignment; do not
            // add a second clamp here with different bounds.
            var factor = input.MouseWheelDelta > 0f ? ZoomStep : 1f / ZoomStep;
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

        var isPainting = input.IsMouseDown(MouseButtons.Left);
        var isErasing = input.IsMouseDown(MouseButtons.Right);

        if (input.WasMousePressed(MouseButtons.Left) || input.WasMousePressed(MouseButtons.Right))
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
                _session.PaintLine(_lastPaintedX, _lastPaintedY, x, y, tile);
            else
                _session.Paint(x, y, tile);

            _lastPaintedX = x;
            _lastPaintedY = y;
            _hasLastPainted = true;
        }

        // End on button *state* rather than the release edge: losing window focus or
        // opening the developer console clears held mouse buttons (InputState.ResetButtons)
        // without ever raising WasMouseReleased, which would otherwise orphan the stroke —
        // IsStrokeActive stays true forever, painting stops working, and Ctrl+Z is silently
        // swallowed by the "no undo mid-stroke" guard.
        if (_session.IsStrokeActive && !isPainting && !isErasing)
            EndStrokeIfActive();
    }

    private void UpdateThings(InputState input)
    {
        if (_isPlacingThing && input.WasMousePressed(MouseButtons.Left))
        {
            PlaceThing();
            return;
        }

        if (input.WasMousePressed(MouseButtons.Right))
        {
            _isPlacingThing = false;
            InspectorView.RefreshFromEditor();
            return;
        }

        if (input.WasMousePressed(MouseButtons.Left))
            BeginThingSelectionOrDrag();

        if (_thingDragHandle != ThingDragHandle2D.None && input.IsMouseDown(MouseButtons.Left))
            UpdateThingDrag();

        if (_thingDragHandle != ThingDragHandle2D.None && !input.IsMouseDown(MouseButtons.Left))
            CommitThingDrag();
    }

    private void PlaceThing()
    {
        if (SelectedDefinition is not { } definition)
            return;
        var position = SnapToGrid(_camera.DeviceToWorld(_lastMouseDevice));
        var descriptor = ThingTypeRegistry2D.Require(definition.TypeKey);
        if (descriptor.IsMovingPlatform)
        {
            var created = RequireDatabase().CreateMovingPlatform(new NewMovingPlatformThing2D(
                definition.DefinitionId,
                Name: null,
                Enabled: true,
                position.X,
                position.Y,
                Rotation: 0f,
                TravelX: _tileSize * 3f,
                TravelY: 0f,
                Speed: _tileSize * 1.5f));
            _thingUndo.Push(database => database.DeleteMovingPlatform(created.ThingId));
            SelectedThingId = created.ThingId;
        }
        else
        {
            var created = RequireDatabase().CreatePositionThing(new NewPositionThing2D(
                definition.DefinitionId,
                Name: null,
                Enabled: true,
                position.X,
                position.Y));
            _thingUndo.Push(database => database.DeletePositionThing(created.ThingId));
            SelectedThingId = created.ThingId;
        }
        _isPlacingThing = false;
        ReloadThings(notifyRuntime: true);
    }

    private void BeginThingSelectionOrDrag()
    {
        var world = _camera.DeviceToWorld(_lastMouseDevice);
        var handleRadius = 12f / _camera.Zoom;
        var handleRadiusSquared = handleRadius * handleRadius;

        foreach (var thing in _movingPlatformThings.AsEnumerable().Reverse())
        {
            var start = new Vector2(thing.X, thing.Y);
            var end = start + new Vector2(thing.TravelX, thing.TravelY);
            if (Vector2.DistanceSquared(world, start) <= handleRadiusSquared)
            {
                BeginThingDrag(thing, ThingDragHandle2D.Start);
                return;
            }
            if (Vector2.DistanceSquared(world, end) <= handleRadiusSquared)
            {
                BeginThingDrag(thing, ThingDragHandle2D.End);
                return;
            }
        }

        foreach (var thing in _positionThings.AsEnumerable().Reverse())
        {
            if (Vector2.DistanceSquared(world, new Vector2(thing.X, thing.Y)) <= handleRadiusSquared * 2.25f)
            {
                SelectedThingId = thing.ThingId;
                SelectedDefinitionId = thing.DefinitionId;
                InspectorView.ShowPositionThing(thing);
                return;
            }
        }

        foreach (var thing in _movingPlatformThings.AsEnumerable().Reverse())
        {
            if (MathF.Abs(world.X - thing.X) <= thing.Width / 2f &&
                MathF.Abs(world.Y - thing.Y) <= thing.Height / 2f)
            {
                SelectedThingId = thing.ThingId;
                SelectedDefinitionId = thing.DefinitionId;
                InspectorView.ShowThing(thing);
                return;
            }
        }

        SelectedThingId = null;
        InspectorView.RefreshFromEditor();
    }

    private void BeginThingDrag(MovingPlatformThingRecord2D thing, ThingDragHandle2D handle)
    {
        SelectedThingId = thing.ThingId;
        SelectedDefinitionId = thing.DefinitionId;
        _thingDragOriginal = thing;
        _thingDragHandle = handle;
        InspectorView.ShowThing(thing);
    }

    private void UpdateThingDrag()
    {
        if (SelectedThingId is not { } thingId)
            return;
        var index = _movingPlatformThings.FindIndex(item => item.ThingId == thingId);
        if (index < 0)
            return;
        var current = _movingPlatformThings[index];
        var pointer = SnapToGrid(_camera.DeviceToWorld(_lastMouseDevice));
        var updated = _thingDragHandle switch
        {
            ThingDragHandle2D.Start => current with
            {
                X = pointer.X,
                Y = pointer.Y,
                TravelX = current.X + current.TravelX - pointer.X,
                TravelY = current.Y + current.TravelY - pointer.Y
            },
            ThingDragHandle2D.End => current with
            {
                TravelX = pointer.X - current.X,
                TravelY = pointer.Y - current.Y
            },
            _ => current
        };
        if (updated.TravelX == 0f && updated.TravelY == 0f)
            return;
        _movingPlatformThings[index] = updated;
    }

    private void CommitThingDrag()
    {
        if (_thingDragHandle == ThingDragHandle2D.None || _thingDragOriginal is not { } original)
            return;
        _thingDragHandle = ThingDragHandle2D.None;
        _thingDragOriginal = null;
        var current = SelectedThing;
        if (current is null || current == original)
            return;
        RequireDatabase().UpdateMovingPlatform(current);
        _thingUndo.Push(database => database.UpdateMovingPlatform(original));
        ReloadThings(notifyRuntime: true);
    }

    internal void UndoThingEdit()
    {
        CommitThingDrag();
        if (_thingUndo.Count == 0)
            return;
        _thingUndo.Pop()(RequireDatabase());
        SelectedThingId = null;
        ReloadThings(notifyRuntime: true);
    }

    private Vector2 SnapToGrid(Vector2 world) =>
        _origin + new Vector2(
            MathF.Round((world.X - _origin.X) / _tileSize) * _tileSize,
            MathF.Round((world.Y - _origin.Y) / _tileSize) * _tileSize);

    private void ReloadThings(bool notifyRuntime)
    {
        var database = RequireDatabase();
        _thingDefinitions.Clear();
        _thingDefinitions.AddRange(database.LoadThingDefinitions());
        foreach (var definition in _thingDefinitions)
            _ = ThingTypeRegistry2D.Require(definition.TypeKey);
        _movingPlatformDefinitions.Clear();
        _movingPlatformDefinitions.AddRange(database.LoadMovingPlatformDefinitions());
        _movingPlatformThings.Clear();
        _movingPlatformThings.AddRange(database.LoadMovingPlatforms());
        _positionThings.Clear();
        _positionThings.AddRange(database.LoadPositionThings().Where(
            thing => ThingTypeRegistry2D.Require(thing.TypeKey).WorldKind is not null));
        if (SelectedDefinitionId is { } definitionId && _thingDefinitions.All(item => item.DefinitionId != definitionId))
            SelectedDefinitionId = null;
        if (SelectedThingId is { } thingId &&
            _movingPlatformThings.All(item => item.ThingId != thingId) &&
            _positionThings.All(item => item.ThingId != thingId))
            SelectedThingId = null;
        InspectorView.RefreshFromEditor();
        if (notifyRuntime)
            ThingsChanged?.Invoke(_movingPlatformThings);
    }

    private LevelDatabase2D RequireDatabase() =>
        StateGuard.RequireNotNull(_database, "Thing editing requires an open level database.");

    private void EndStrokeIfActive()
    {
        if (!_session.IsStrokeActive)
            return;

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
        // Rebuild authored things at their persisted starts so viewport handles and
        // runtime visuals coincide even if a platform was midway through its path
        // when editing began.
        ReloadThings(notifyRuntime: true);
        InspectorView.Visible = Mode == LevelEditorMode2D.Things;
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
        InspectorView.Visible = false;
        _camera.Position = _savedCameraPosition;
        _camera.Zoom = _savedCameraZoom;
        _database?.Dispose();
        _database = null;
    }

    /// <summary>Writes a stroke's changed chunks in one transaction.</summary>
    private void CommitChunks(IReadOnlyCollection<TileChunk2D> chunks)
    {
        if (chunks.Count == 0)
            return;

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
        InspectorView.Dispose();
    }

    private enum ThingDragHandle2D
    {
        None,
        Start,
        End
    }
}
