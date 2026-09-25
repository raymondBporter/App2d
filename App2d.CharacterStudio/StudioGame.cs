using App2d.Core.Characters;
using App2d.Rendering.Characters;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.CharacterStudio;

internal sealed partial class StudioGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _assetRoot;
    private readonly string? _smokePath;
    private readonly LibraryEntry[] _libraries;
    private readonly Dictionary<string, StudioDocument> _documents = [];
    private readonly Dictionary<string, Texture2D> _faces = [];
    private StudioDocument _document = null!;
    private ImGuiHost _gui = null!;
    private PointCharacterRenderer _renderer = null!;
    private RenderTarget2D? _preview, _capture;
    private nint _previewId;
    private string _search = "", _filter = "All", _status = "", _error = "", _mode = "filled";
    private bool _joints, _guides = true, _trails = true, _rest, _atEnd, _repeatSequence;
    private float _speed = 1, _zoom = 1;
    private NVector2 _center = new(0, 1.2f), _pan;
    private float _fitSpan = 3.3f;
    private readonly string[] _sequence = new string[3];
    private int _smokeIndex, _smokeFrame;
    private string _bindingRole = "Idle";
    private bool _disposed;
    private Form? _nativeForm;
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "App2d", "CharacterStudio", "settings.json");
    private float Scale => _gui.UiScale;

    public StudioGame(string assetRoot, string? smokePath, bool wolfSmokeOnly = false, bool workshop = false, bool workshopSmoke = false, bool motionSmoke = false)
    {
        _assetRoot = assetRoot; _smokePath = smokePath;
        var catalogPath = Path.Combine(assetRoot, "catalog.json");
        _libraries = [];
        if (File.Exists(catalogPath))
        {
            using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
            _libraries = JsonSerializer.Deserialize<LibraryEntry[]>(catalog.RootElement.GetProperty("libraries"), AuthoredJson.Tolerant)!;
        }
        _workshopActive = workshop || workshopSmoke || motionSmoke || _libraries.Length == 0; _workshopSmoke = workshopSmoke; _motionSmoke = motionSmoke;
        if (wolfSmokeOnly) _smokeIndex = _libraries.Length + SmokeScenarios.Length;
        _graphics = new(this) { PreferredBackBufferWidth = 1480, PreferredBackBufferHeight = 930, GraphicsProfile = GraphicsProfile.HiDef,
            PreferredDepthStencilFormat = DepthFormat.Depth24, PreferMultiSampling = true, SynchronizeWithVerticalRetrace = true };
        Window.Title = "Character Studio | App2d"; Window.AllowUserResizing = true; IsMouseVisible = true; IsFixedTimeStep = false;
        var control = System.Windows.Forms.Control.FromHandle(Window.Handle);
        var dpi = control?.DeviceDpi / 96f ?? 1;
        var workArea = System.Windows.Forms.Screen.FromHandle(Window.Handle).WorkingArea;
        _graphics.PreferredBackBufferWidth = (int)Math.Min(1440 * dpi, workArea.Width * .9f);
        _graphics.PreferredBackBufferHeight = (int)Math.Min(900 * dpi, workArea.Height * .9f);
    }
    protected override void LoadContent()
    {
        var userScale = 1f;
        if (_smokePath is null && File.Exists(_settingsPath)) Attempt(() =>
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(_settingsPath));
            if (settings.RootElement.TryGetProperty("uiScale", out var value) && value.TryGetSingle(out var scale) && float.IsFinite(scale)) userScale = Math.Clamp(scale, .75f, 2);
        });
        _gui = new(this, userScale); _renderer = new(GraphicsDevice);
        if (_workshopActive) EnterWorkshop(); else LoadImportedStudio();
        if (_smokePath is null)
        {
            _nativeForm = Control.FromHandle(Window.Handle)?.FindForm();
            if (_nativeForm is not null) _nativeForm.FormClosing += ConfirmClose;
        }
        if (_smokePath is not null) Directory.CreateDirectory(_smokePath);
    }
    private void LoadImportedStudio()
    {
        if (_document is not null) return;
        if (_libraries.Length == 0) throw new InvalidOperationException("No imported libraries are installed.");
        foreach (var id in new[] { "happy", "grumpy" }) { using var stream = File.OpenRead(Path.Combine(_assetRoot, "faces", id + ".png")); _faces[id] = Texture2D.FromStream(GraphicsDevice, stream); }
        ChooseLibrary(_libraries[0]); LoadEntityTypes();
        if (_smokePath is null && _entityDocuments.Count > 0) ActivateEntity(_entityDocuments.Values.FirstOrDefault(d => d.Entity!.Behavior == "player") ?? _entityDocuments.Values.First());
    }
    private void ChooseLibrary(LibraryEntry entry)
    {
        // Keep only unsaved documents. Clean libraries can be reloaded without retaining all packed data.
        foreach (var id in _documents.Where(p => !p.Value.Dirty && p.Key != entry.Id).Select(p => p.Key).ToArray()) _documents.Remove(id);
        if (!_documents.TryGetValue(entry.Id, out var document)) { document = new(SharedLibrary(entry.Id)); _documents.Add(entry.Id, document); }
        _document = document; _filter = "All"; _search = ""; _mode = "filled"; _rest = false;
        ReleaseUnusedLibraries();
        for (var i = 0; i < 3; i++) _sequence[i] = _document.Playback.ClipId;
        FitMotion();
    }
    private void FitMotion()
    {
        if (_document.Entity is not null) { FitEntityMotion(); return; }
        var clip = _document.Playback.Clip; var geometry = _document.Geometry;
        var min = new NVector3(float.PositiveInfinity); var max = new NVector3(float.NegativeInfinity);
        // Whole-action fit is explicit, never recalculated during playback. Scan source poses without decoding a clip cache.
        for (var i = 0; i < clip.SampleCount; i += Math.Max(1, clip.SampleCount / 40))
        {
            geometry.Build(clip, clip.Times[i], _document.Appearance, new(false, false, false, _mode, _rest), true);
            min = NVector3.Min(min, geometry.Body.Min); max = NVector3.Max(max, geometry.Body.Max);
        }
        geometry.Build(clip, clip.Duration, _document.Appearance, new(false, false, false, _mode, _rest), true);
        min = NVector3.Min(min, geometry.Body.Min); max = NVector3.Max(max, geometry.Body.Max);
        _center = new((min.X + max.X) / 2, (min.Y + max.Y) / 2); _fitSpan = MathF.Max(1.5f, MathF.Max(max.Y - min.Y + .55f, (max.X - min.X + .55f) / 1.2f));
        _zoom = 1; _pan = NVector2.Zero;
    }
    protected override void Update(GameTime time)
    {
        if (_gui is not null && _smokePath is null && IsActive)
        {
            if (!_pauseFaces) _faceClock += Math.Min(.1, time.ElapsedGameTime.TotalSeconds);
            var seconds = Math.Min(.1, time.ElapsedGameTime.TotalSeconds) * _speed;
            if (_workshopActive)
            {
                if (_workshopPlaying && !_workshopRest)
                {
                    AdvanceWorkshop((float)Math.Min(.1, time.ElapsedGameTime.TotalSeconds));
                }
            }
            else if (_document.Entity is null) _document.Playback.Advance(seconds);
            else if (_actionPlaying)
            {
                var action = CurrentAction; var previous = _actionTime; _actionTime += seconds;
                if (_previewCues && previous <= action.CueTime * action.Duration && _actionTime > action.CueTime * action.Duration) PlayCue(action.Cue);
                if (_actionTime >= action.Duration) { if (action.Loop) _actionTime %= action.Duration; else { _actionTime = action.Duration; _actionPlaying = false; } }
            }
        }
        base.Update(time);
    }
    protected override void Draw(GameTime time)
    {
        if (GraphicsDevice.PresentationParameters.BackBufferWidth < 100 || GraphicsDevice.PresentationParameters.BackBufferHeight < 100) return;
        if (_smokePath is not null && _smokeFrame++ % 3 == 0)
        {
            if (!(_motionSmoke ? PrepareMotionProof() : _workshopSmoke ? PrepareWorkshopSmoke() : PrepareSmokeFrame())) { Exit(); return; }
        }
        _gui.Begin((float)time.ElapsedGameTime.TotalSeconds);
        DrawInterface();
        GraphicsDevice.SetRenderTarget(null);
        if (_smokePath is not null)
        {
            var size = GraphicsDevice.PresentationParameters;
            if (_capture is null || _capture.Width != size.BackBufferWidth || _capture.Height != size.BackBufferHeight)
            {
                _capture?.Dispose();
                _capture = new(GraphicsDevice, size.BackBufferWidth, size.BackBufferHeight, false, SurfaceFormat.Color, DepthFormat.None);
            }
            GraphicsDevice.SetRenderTarget(_capture);
        }
        GraphicsDevice.Clear(new Color(19, 23, 29)); _gui.Render();
        if (_smokePath is not null && _smokeFrame % 3 == 0)
        {
            GraphicsDevice.SetRenderTarget(null);
            if (_workshopSmoke) CaptureWorkshopSmoke(); else if (!_motionSmoke) CaptureSmokeFrame();
            _smokeIndex++;
        }
        GraphicsDevice.SetRenderTarget(null);
        base.Draw(time);
    }
    private void Preview(NVector2 available)
    {
        var width = Math.Max(1, (int)available.X); var height = Math.Max(1, (int)available.Y);
        if (_preview is null || _preview.Width != width || _preview.Height != height)
        {
            if (_preview is not null) { _gui.Unregister(_previewId); _preview.Dispose(); }
            _preview = new(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
            _previewId = _gui.Register(_preview);
        }
        var geometry = _document.Geometry; var playback = _document.Playback;
        var look = _document.Appearance; var transform = Matrix.Identity;
        if (_document.Entity is { } entity)
        {
            var pose = _document.EntityPose!; pose.Evaluate(entity, CurrentAction, _actionTime, look.Flip); look = pose.Look;
            geometry.Build(_document.Library.Clips[CurrentAction.Clip], pose.ClipTime, look, new(_joints, false, _trails, Face: PreviewFace()), true);
            transform = Matrix.CreateTranslation(pose.Offset.X, pose.Offset.Y, 0);
        }
        else geometry.Build(playback.Clip, playback.Time, look, new(_joints, _guides, _trails, _mode, _rest, PreviewFace()), playback.Finished);
        GraphicsDevice.SetRenderTarget(_preview); GraphicsDevice.Clear(new Color(230, 234, 227));
        var ppu = MathF.Min(height / _fitSpan, width / (_fitSpan * 1.2f)) * _zoom;
        var anchor = new NVector2(width / 2f - _center.X * ppu, height / 2f + _center.Y * ppu) + _pan;
        var projection = PointCharacterRenderer.Projection(width, height, anchor, ppu);
        _renderer.Draw(geometry.Backdrop, projection, Matrix.Identity, writeDepth: false);
        _renderer.Draw(geometry.Body, projection, transform);
        if (_faces.TryGetValue(look.CustomHead?.Face ?? look.Face, out var texture)) _renderer.Draw(geometry.Face, projection, transform, texture, false);
        if (_document.Entity is not null && _showCollision)
        {
            BuildCollisionOverlay(_document.EntityPose!, CurrentAction.Active(_actionTime), CurrentAction);
            _renderer.Draw(_collisionOverlay, projection, Matrix.Identity, writeDepth: false);
        }
        GraphicsDevice.SetRenderTarget(null);
        if (_document.Entity is not null)
        {
            var start = ImGui.GetCursorScreenPos(); ImGui.InvisibleButton("Entity preview", new(width, height));
            ImGui.GetWindowDrawList().AddImage(_previewId, start, start + new NVector2(width, height));
            EditPreviewGeometry(ppu, anchor, start);
        }
        else ImGui.Image(_previewId, new(width, height));
        if (ImGui.IsItemHovered())
        {
            _zoom = Math.Clamp(_zoom * MathF.Pow(1.1f, ImGui.GetIO().MouseWheel), .1f, 8);
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle)) _pan += ImGui.GetIO().MouseDelta;
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) FitMotion();
        }
    }
    private void Attempt(Action action)
    {
        try { action(); _error = ""; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        { _error = ex.Message; }
    }
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing) { _disposed = true; if (_nativeForm is not null) _nativeForm.FormClosing -= ConfirmClose; _preview?.Dispose(); _capture?.Dispose(); _headPreview?.Dispose(); _arenaTarget?.Dispose(); _puppetTarget?.Dispose(); _proofTarget?.Dispose(); foreach (var sound in _cueSounds.Values) sound.Dispose(); foreach (var t in _faces.Values) t.Dispose(); _renderer?.Dispose(); _gui?.Dispose(); }
        base.Dispose(disposing);
    }
}
