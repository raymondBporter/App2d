using App2d.Core.Characters.Editing;
using App2d.Rendering.Characters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// Host for the character editor: window, GPU, ImGui and the frame loop. Everything the author sees comes from
/// <see cref="EditorShell"/> over one <see cref="EditorSession"/>; this class owns no editing state.
/// </summary>
internal sealed class EditorApp : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _authoredRoot;
    private readonly string? _smokePath;
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "App2d", "CharacterStudio", "settings.json");
    private ImGuiHost _gui = null!;
    private PointCharacterRenderer _renderer = null!;
    private EditorShell _shell = null!;
    private EditorSmoke? _smoke;
    private RenderTarget2D? _capture;
    private Form? _form;

    public EditorApp(string authoredRoot, string? smokePath)
    {
        _authoredRoot = authoredRoot; _smokePath = smokePath;
        _graphics = new(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef, PreferredDepthStencilFormat = DepthFormat.Depth24, PreferMultiSampling = true, SynchronizeWithVerticalRetrace = true,
        };
        Window.Title = "Character Editor | App2d"; Window.AllowUserResizing = true; IsMouseVisible = true; IsFixedTimeStep = false;
        var dpi = System.Windows.Forms.Control.FromHandle(Window.Handle)?.DeviceDpi / 96f ?? 1;
        var area = System.Windows.Forms.Screen.FromHandle(Window.Handle).WorkingArea;
        // Smoke frames use a fixed logical size so captures compare across machines; the UI still scales with DPI.
        _graphics.PreferredBackBufferWidth = (int)(smokePath is null ? Math.Min(1500 * dpi, area.Width * .92f) : 1600 * dpi);
        _graphics.PreferredBackBufferHeight = (int)(smokePath is null ? Math.Min(940 * dpi, area.Height * .92f) : 940 * dpi);
    }

    protected override void LoadContent()
    {
        var scale = 1f;
        if (_smokePath is null && File.Exists(_settingsPath))
            try
            {
                using var settings = JsonDocument.Parse(File.ReadAllText(_settingsPath));
                if (settings.RootElement.TryGetProperty("uiScale", out var value) && value.TryGetSingle(out var s) && float.IsFinite(s)) scale = Math.Clamp(s, .75f, 2);
            }
            catch (Exception ex) when (ex is IOException or JsonException) { }
        _gui = new(this, scale); _renderer = new(GraphicsDevice);
        var root = _authoredRoot;
        if (_smokePath is not null) { _smoke = new(_smokePath); root = _smoke.PrepareWorkspace(_authoredRoot); }
        var assets = AuthoringWorkspace.Open(root);
        // Sources come from the real characters folder, also during a smoke run on a scratch copy of the authored assets.
        var sources = App2d.Core.Characters.SourceLibraries.Open(Path.GetDirectoryName(Path.GetFullPath(_authoredRoot))!);
        _shell = new(new EditorSession(assets, sources), new Viewport(GraphicsDevice, _gui, _renderer), new ArenaTest(assets, GraphicsDevice, _gui, _renderer), _gui);
        if (_smokePath is null)
        {
            _form = Control.FromHandle(Window.Handle)?.FindForm();
            if (_form is not null) _form.FormClosing += ConfirmClose;
        }
    }

    protected override void Update(GameTime time)
    {
        var seconds = (float)Math.Min(.1, time.ElapsedGameTime.TotalSeconds);
        if (_smoke is not null) { if (!_smoke.Step(_shell)) Exit(); }
        else if (IsActive) _shell.Session.Tick(seconds);
        base.Update(time);
    }

    protected override void Draw(GameTime time)
    {
        var size = GraphicsDevice.PresentationParameters;
        if (size.BackBufferWidth < 100 || size.BackBufferHeight < 100) return;
        _gui.Begin((float)time.ElapsedGameTime.TotalSeconds);
        _shell.Draw();
        GraphicsDevice.SetRenderTarget(null);
        if (_smoke is not null)
        {
            if (_capture is null || _capture.Width != size.BackBufferWidth || _capture.Height != size.BackBufferHeight)
            { _capture?.Dispose(); _capture = new(GraphicsDevice, size.BackBufferWidth, size.BackBufferHeight, false, SurfaceFormat.Color, DepthFormat.None); }
            GraphicsDevice.SetRenderTarget(_capture);
        }
        GraphicsDevice.Clear(new Color(19, 23, 29)); _gui.Render();
        if (_smoke is not null) { GraphicsDevice.SetRenderTarget(null); _smoke.Capture(_capture!); }
        base.Draw(time);
    }

    private void ConfirmClose(object? sender, FormClosingEventArgs e)
    {
        var dirty = _shell.Session.Assets.DirtyDocuments.ToArray();
        if (dirty.Length == 0) return;
        var answer = MessageBox.Show($"Save changes to {string.Join(", ", dirty.Select(d => d.Name))}?", "Character editor", MessageBoxButtons.YesNoCancel);
        if (answer == DialogResult.Cancel) { e.Cancel = true; return; }
        if (answer == DialogResult.Yes)
        {
            _shell.Session.SaveAll();
            if (_shell.Session.Assets.DirtyDocuments.Any()) { e.Cancel = true; MessageBox.Show(_shell.Session.Message, "Character editor"); }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_form is not null) _form.FormClosing -= ConfirmClose;
            _shell?.Dispose(); _capture?.Dispose(); _renderer?.Dispose(); _gui?.Dispose();
        }
        base.Dispose(disposing);
    }
}
