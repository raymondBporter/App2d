using App2d.Core.Rendering;
using App2d.EditorUi;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using GpuTexture = Microsoft.Xna.Framework.Graphics.Texture2D;

namespace App2d.ImageLab;

internal sealed partial class ImageLabWindow : Form
{
    private readonly GraphicsSurface2D _surface = new() { Dock = DockStyle.Fill, TabStop = true, DepthStencilFormat = DepthFormat.None };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly RasterHistory _history = new();
    private readonly RasterPreview _preview = new();
    private RasterTransform _transform = new();
    private ImGuiHost? _gui;
    private GraphicsDevice? _device;
    private GpuTexture? _texture;
    private nint _textureId;
    private RasterFrame? _uploaded;
    private Action? _afterFrame;
    private bool _rendering;
    private bool _disposed;
    private bool _showPreview;
    private bool _showOriginal;
    private string? _sourcePath;
    private string? _problem;
    private string _status = "Load an image to begin.";
    private bool _fit = true;
    private float _zoom = 1;
    private Vector2 _pan;

    public ImageLabWindow()
    {
        Text = "App2d Image Lab";
        ClientSize = new(1380, 880);
        MinimumSize = new(1060, 700);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        Controls.Add(_surface);
        _surface.AllowDrop = true;
        _surface.RenderFrame += Render;
        _surface.DeviceDisposing += ReleaseDevice;
        _timer.Tick += Tick;
        Shown += (_, _) => { _surface.Focus(); _timer.Start(); };
        DragEnter += ImageDragEnter;
        _surface.DragEnter += ImageDragEnter;
        DragDrop += ImageDragDrop;
        _surface.DragDrop += ImageDragDrop;
        Deactivate += (_, _) => FinishBrushStroke();
    }

    private static void ImageDragEnter(object? sender, DragEventArgs e) =>
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;

    private void ImageDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
        {
            LoadImage(paths[0]);
        }
    }

    public void LoadImage(string path)
    {
        try
        {
            var frame = RasterFrame.Load(path);
            LoadFrame(frame, Path.GetFileName(path));
            _sourcePath = Path.GetFullPath(path);
            Text = $"{Path.GetFileName(path)} — App2d Image Lab";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Runtime.InteropServices.ExternalException)
        {
            _problem = error.Message;
        }
    }

    internal void LoadFrame(RasterFrame frame, string name)
    {
        _stroke = null;
        _ink = RasterInk.White;
        _inkKind = frame.Kind;
        _paletteFrame = null;
        _regionFrame = null;
        _viewRegions = null;
        _inspectRegions = false;
        _hoverRegion = 0;
        _tool = CanvasTool.Inspect;
        _overlayOriginal = false;
        _history.Load(frame, name);
        _originalPixels = _history.Steps[0].Frame.DisplayPixels();
        _sourcePath = null;
        _problem = null;
        _transform = new();
        _fit = true;
        _pan = Vector2.Zero;
        _showOriginal = false;
        _showPreview = false;
        _status = $"Loaded {name}. Choose a transform, preview, then Apply.";
        _preview.Change(_history.Current, _transform);
    }

    internal void SelectTransform(RasterTransform transform)
    {
        FinishBrushStroke();
        _tool = CanvasTool.Inspect;
        _transform = transform;
        _showPreview = true;
        _showOriginal = false;
        _problem = null;
        _preview.Change(_history.Current, transform);
    }

    internal bool PreviewReady => _preview.Frame is not null;

    internal void ApplyPreview()
    {
        if (_preview.Frame is not { } result)
        {
            return;
        }
        _history.Apply(_transform, result);
        _status = $"Applied {_transform.Description}.";
        HistoryChanged();
    }

    private void HistoryChanged()
    {
        _stroke = null;
        ResetInkForKind();
        _paletteFrame = null;
        _regionFrame = null;
        _viewRegions = null;
        _hoverRegion = 0;
        _imageDirty = true;
        _showPreview = false;
        _showOriginal = false;
        _problem = null;
        _preview.Change(_tool == CanvasTool.Inspect ? _history.Current : null, _transform);
    }

    private void Tick(object? sender, EventArgs e)
    {
        _surface.Refresh();
        var action = _afterFrame;
        _afterFrame = null;
        if (action is not null)
        {
            _timer.Stop();
            try
            {
                action();
            }
            finally
            {
                if (!IsDisposed)
                {
                    _timer.Start();
                }
            }
        }
    }

    private void Render(GraphicsDevice device, int width, int height)
    {
        if (_rendering)
        {
            return;
        }
        _rendering = true;
        try
        {
            _device = device;
            _gui ??= new(device, new WinFormsImGuiPlatform(_surface));
            _preview.Poll();
            device.Clear(new Color4b(17, 22, 33));
            _gui.Begin(1f / 60);
            Shortcuts();
            Ui.Root("Image Lab", () =>
            {
                DrawToolbar();
                var available = ImGui.GetContentRegionAvail();
                var sidebar = MathF.Min(350 * Ui.Scale, available.X * .38f);
                Ui.Row(
                    () => Ui.Panel("image", DrawCanvas, DrawCanvasToolbar, new(available.X - sidebar - ImGui.GetStyle().ItemSpacing.X, available.Y)),
                    () => Ui.Panel("tools", DrawTools, size: new(sidebar, available.Y)));
            });
            _gui.Render();
        }
        finally
        {
            _rendering = false;
        }
    }

    private void OpenImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Load an image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadImage(dialog.FileName);
        }
    }

    private void SaveImage()
    {
        FinishBrushStroke();
        if (_history.Current is not { } frame)
        {
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "Save the current applied step as PNG",
            Filter = "PNG image|*.png",
            DefaultExt = "png",
            AddExtension = true,
            FileName = (_sourcePath is null ? "image" : Path.GetFileNameWithoutExtension(_sourcePath)) + "-processed.png"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            frame.SavePng(dialog.FileName);
            _problem = null;
            _status = $"Saved step {_history.Position} to {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            _problem = error.Message;
        }
    }

    private void Shortcuts()
    {
        if (_gui!.WantTextInput)
        {
            return;
        }
        if (!ImGui.GetIO().KeyCtrl)
        {
            if (!ImGui.IsAnyItemActive() && !ImGui.GetIO().KeyAlt)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.E, false))
                {
                    SetCanvasTool(CanvasTool.Erase);
                }
                if (ImGui.IsKeyPressed(ImGuiKey.B, false))
                {
                    SetCanvasTool(CanvasTool.Paint);
                }
                if (ImGui.IsKeyPressed(ImGuiKey.L, false))
                {
                    SetCanvasTool(CanvasTool.Line);
                }
                if (ImGui.IsKeyPressed(ImGuiKey.I, false))
                {
                    SetCanvasTool(CanvasTool.Eyedropper);
                }
                if (ImGui.IsKeyPressed(ImGuiKey.V, false))
                {
                    SetCanvasTool(CanvasTool.Inspect);
                }
            }
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                _stroke = null;
                SetCanvasTool(CanvasTool.Inspect);
            }
            return;
        }
        if (ImGui.IsKeyPressed(ImGuiKey.O, false))
        {
            _afterFrame = OpenImage;
        }
        if (ImGui.IsKeyPressed(ImGuiKey.S, false))
        {
            _afterFrame = SaveImage;
        }
        if (ImGui.IsKeyPressed(ImGuiKey.Z, false))
        {
            FinishBrushStroke();
            if (ImGui.GetIO().KeyShift)
            {
                _history.Redo();
            }
            else
            {
                _history.Undo();
            }
            HistoryChanged();
        }
        if (ImGui.IsKeyPressed(ImGuiKey.Y, false))
        {
            _history.Redo();
            HistoryChanged();
        }
    }

    private void ReleaseTexture()
    {
        if (_texture is not null)
        {
            _gui?.Unregister(_textureId);
            _texture.Dispose();
            _texture = null;
        }
        _uploaded = null;
    }

    private void ReleaseDevice()
    {
        ReleaseTexture();
        _gui?.Dispose();
        _gui = null;
        _device = null;
    }

    internal void RenderStudy(GraphicsDevice device) => Render(device, device.PresentationParameters.BackBufferWidth, device.PresentationParameters.BackBufferHeight);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
            _surface.RenderFrame -= Render;
            _surface.DeviceDisposing -= ReleaseDevice;
            ReleaseDevice();
        }
        base.Dispose(disposing);
    }
}
