using App2d.Core;
using App2d.Levels;
using App2d.Rendering;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Editor;

/// <summary>
/// The in-game tile painter. Owns editor mode, tool state, the free camera, and
/// write-through persistence; delegates tile mutation and undo to <see cref="TileEditSession2D"/>.
/// </summary>
internal sealed class TileEditor2D : IDisposable
{
    private const float ZoomStep = 1.1f;

    private static readonly TileKind2D[] SelectableKinds =
    [
        TileKind2D.Empty,
        TileKind2D.Solid,
        TileKind2D.OneWay,
        TileKind2D.Solid | TileKind2D.Grippable,
        TileKind2D.Spikes
    ];

    private readonly EditableTileMap2D _map;
    private readonly LevelDatabase2D _database;
    private readonly Camera2D _camera;
    private readonly TileEditSession2D _session;
    private readonly Vector2 _origin;
    private readonly float _tileSize;

    private Vector2 _cameraFocus;
    private Vector2 _panAnchorDevice;
    private Vector2 _panAnchorFocus;
    private bool _isPanning;
    private Vector2 _lastMouseDevice;
    private int _lastPaintedX;
    private int _lastPaintedY;
    private bool _hasLastPainted;

    public TileEditor2D(
        EditableTileMap2D map,
        LevelDatabase2D database,
        Camera2D camera,
        Vector2 origin,
        float tileSize)
    {
        _map = ArgGuard.RequireNotNull(map);
        _database = ArgGuard.RequireNotNull(database);
        _camera = ArgGuard.RequireNotNull(camera);
        ArgGuard.ThrowIfNotPositive(tileSize);
        _origin = origin;
        _tileSize = tileSize;
        _session = new TileEditSession2D(map);
        SelectedKind = TileKind2D.Solid;
    }

    public bool IsActive { get; private set; }
    public TileKind2D SelectedKind { get; private set; }
    public Vector2 CameraFocus => _cameraFocus;

    public bool TryGetHoveredTile(out int x, out int y)
    {
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
                _cameraFocus = _camera.Position;
            else
                EndEditingSession();
        }

        if (!IsActive)
            return;

        _lastMouseDevice = input.MousePositionDevice;
        UpdateKindSelection(input);
        UpdateCamera(input);
        UpdatePainting(input);

        // A stroke may still be in progress (mouse button held). TileEditSession2D.Undo()
        // throws if called mid-stroke, so ignore the request rather than let it crash the
        // editor; the in-progress stroke is left untouched either way.
        if (input.IsControlDown && input.WasKeyPressed(Keys.Z) && !_session.IsStrokeActive)
            CommitChunks(_session.Undo());

        _camera.Position = _cameraFocus;
    }

    private void UpdateKindSelection(InputState input)
    {
        for (var index = 0; index < SelectableKinds.Length; index++)
        {
            if (input.WasKeyPressed(Keys.D1 + index))
                SelectedKind = SelectableKinds[index];
        }
    }

    private void UpdateCamera(InputState input)
    {
        if (input.WasMousePressed(MouseButtons.Middle))
        {
            _isPanning = true;
            _panAnchorDevice = input.MousePositionDevice;
            _panAnchorFocus = _cameraFocus;
        }

        if (input.WasMouseReleased(MouseButtons.Middle))
            _isPanning = false;

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

    private void UpdatePainting(InputState input)
    {
        var isPainting = input.IsMouseDown(MouseButtons.Left);
        var isErasing = input.IsMouseDown(MouseButtons.Right);

        if (input.WasMousePressed(MouseButtons.Left) || input.WasMousePressed(MouseButtons.Right))
        {
            _session.BeginStroke();
            _hasLastPainted = false;
        }

        if ((isPainting || isErasing) && _session.IsStrokeActive && TryGetHoveredTile(out var x, out var y))
        {
            var kind = isErasing ? TileKind2D.Empty : SelectedKind;
            if (_hasLastPainted)
                _session.PaintLine(_lastPaintedX, _lastPaintedY, x, y, kind);
            else
                _session.Paint(x, y, kind);

            _lastPaintedX = x;
            _lastPaintedY = y;
            _hasLastPainted = true;
        }

        if (input.WasMouseReleased(MouseButtons.Left) || input.WasMouseReleased(MouseButtons.Right))
            EndStrokeIfActive();
    }

    private void EndStrokeIfActive()
    {
        if (!_session.IsStrokeActive)
            return;

        CommitChunks(_session.EndStroke());
        _hasLastPainted = false;
    }

    /// <summary>
    /// Called when editor mode is toggled off: ends any in-progress stroke and clears the
    /// pan state, so a middle-drag started before exiting never resumes with a stale
    /// anchor (and a stale camera jump) on the next entry into editor mode.
    /// </summary>
    private void EndEditingSession()
    {
        EndStrokeIfActive();
        _isPanning = false;
    }

    /// <summary>Writes a stroke's changed chunks in one transaction.</summary>
    private void CommitChunks(IReadOnlyCollection<TileChunk2D> chunks)
    {
        if (chunks.Count == 0)
            return;

        _database.SaveChunks(_map, chunks.ToArray());
    }

    public void Dispose() => _database.Dispose();
}
