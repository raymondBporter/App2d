using App2d.Core;
using App2d.Core.Hosting;
using App2d.Core.Rendering.Textures;
using App2d.EditorUi;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Editor;

/// <summary>Shared ImGui host for the map palette and thing inspector. The editor owns all editing commands.</summary>
internal sealed class MapEditorUi2D(TileEditor2D editor, TextureCache2D textures) : IGameToolUi2D
{
    private Control? _surface;
    private WinFormsImGuiPlatform? _platform;
    private ImGuiHost? _gui;
    private bool _shortcutCaptured;
    private readonly Dictionary<string, nint> _textureIds = [];
    private readonly List<Microsoft.Xna.Framework.Graphics.Texture2D> _previews = [];
    private GraphicsDevice? _device;
    public bool CaptureMouse => editor.IsActive && (editor.IsPointerOverUi || _gui?.WantCaptureMouse == true);
    public bool CaptureKeyboard => editor.IsActive && (_shortcutCaptured || _gui?.WantCaptureKeyboard == true);
    public bool IsEditingText => editor.IsActive && _gui?.WantTextInput == true;
    public ImGuiHost Gui => _gui ?? throw new InvalidOperationException("The map UI has not begun a frame.");
    public void Attach(Control surface)
    {
        _surface = surface;
        _platform = new(surface);
    }

    public void BeginFrame(GraphicsDevice device, FrameTime time)
    {
        _device = device;
        _gui ??= new(device, _platform ??= new(_surface ?? throw new InvalidOperationException("Attach the map UI first.")));
        _gui.Begin(time.DeltaSeconds);
        _shortcutCaptured = false;
        editor.IsPointerOverUi = false;
        if (!editor.IsActive)
        {
            return;
        }

        var width = Ui.Sidebar("Map editor", () =>
        {
            Ui.Header("Map editor");
            Ui.Button(editor.MapLabel + " [next map]", () => editor.CycleMap(1));
            Ui.Row(
                () => Ui.Button("Tiles", () => editor.SelectMode(LevelEditorMode2D.Tiles)),
                () => Ui.Button("Things", () => editor.SelectMode(LevelEditorMode2D.Things)),
                () => Ui.Button("Play [F1]", editor.RequestToggle));
            ImGui.Separator();
            if (editor.Mode == LevelEditorMode2D.Tiles)
            {
                TileEditorMenu2D.Draw(editor, textures, TextureId);
            }
            else
            {
                editor.InspectorView.Draw();
            }
        }, width: 360);
        var io = ImGui.GetIO();
        var pointer = io.MousePos;
        editor.IsPointerOverUi = _gui.WantCaptureMouse ||
            (pointer.X >= io.DisplaySize.X - width && pointer.X < io.DisplaySize.X && pointer.Y >= 0 && pointer.Y < io.DisplaySize.Y);
        if (ImGui.IsKeyPressed(ImGuiKey.F1, false))
        {
            editor.RequestToggle();
            _shortcutCaptured = true;
        }
        if (!io.WantTextInput && io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Z))
        {
            _shortcutCaptured = true;
            if (editor.Mode == LevelEditorMode2D.Tiles)
            {
                editor.UndoTiles();
            }
            else
            {
                editor.UndoThingEdit();
            }
        }
    }
    private nint TextureId(string path)
    {
        if (!_textureIds.TryGetValue(path, out var id))
        {
            var source = textures.Load(path);
            var preview = new Microsoft.Xna.Framework.Graphics.Texture2D(_device!, source.Width, source.Height);
            preview.SetData(source.CopyPixels());
            _previews.Add(preview);
            _textureIds[path] = id = Gui.Register(preview);
        }
        return id;
    }
    public void Render() => _gui?.Render();
    public void ReleaseDevice()
    {
        _gui?.Dispose();
        _gui = null;
        _platform?.Dispose();
        _platform = null;
        _textureIds.Clear();
        foreach (var preview in _previews)
        {
            preview.Dispose();
        }

        _previews.Clear();
        _device = null;
    }
    public void Dispose() => ReleaseDevice();
}
