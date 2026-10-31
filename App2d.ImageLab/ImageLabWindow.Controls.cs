using App2d.EditorUi;
using ImGuiNET;
using System.Numerics;
using GpuTexture = Microsoft.Xna.Framework.Graphics.Texture2D;

namespace App2d.ImageLab;

internal sealed partial class ImageLabWindow
{
    private Vector2 _lastImagePosition;
    private float _lastImageZoom;
    private bool _texturePointSample;
    private void DrawToolbar()
    {
        Ui.Row(
            () => ImGui.TextColored(UiTheme.Accent, "IMAGE LAB"),
            () => Ui.Button("Load image", () => _afterFrame = OpenImage),
            () => Ui.Button("Save applied PNG", () => _afterFrame = SaveImage, _history.Current is not null),
            () => Ui.Button("Undo", () => { _history.Undo(); HistoryChanged(); }, _history.CanUndo),
            () => Ui.Button("Redo", () => { _history.Redo(); HistoryChanged(); }, _history.CanRedo));
        if (_problem is not null)
        {
            Ui.Problem(_problem);
        }
        else
        {
            Ui.Help(_status);
        }
    }

    private void DrawCanvasToolbar()
    {
        Ui.Row(
            () => Ui.Button("Fit", () => { _fit = true; _pan = Vector2.Zero; }),
            () => Ui.Button("100%", () => { _fit = false; _zoom = 1; _pan = Vector2.Zero; }),
            () =>
            {
                if (ImGui.Checkbox("Original", ref _showOriginal))
                {
                    FinishBrushStroke();
                    SetCanvasTool(CanvasTool.Inspect);
                }
            },
            () =>
            {
                if (ImGui.Checkbox("Preview", ref _showPreview))
                {
                    FinishBrushStroke();
                    SetCanvasTool(CanvasTool.Inspect);
                }
            });
        DrawBrushToolbar();
        DrawRegionToolbar();
        var frame = DisplayFrame();
        if (frame is not null)
        {
            var label = _showOriginal ? "Original" : _showPreview && _preview.Frame is not null ? "Preview" : $"Step {_history.Position}";
            Ui.Help($"{label} · {frame.Width} × {frame.Height} · {frame.Kind} · Arrows pan");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Arrow keys move the image in screen directions; hold to keep moving.\nShift + arrows moves three times faster.\nWheel: zoom | Middle drag: pan");
            }
        }
    }

    private RasterFrame? DisplayFrame() => _history.Current is null ? null
        : _stroke is not null ? _stroke.Frame
        : _showOriginal ? _history.Steps[0].Frame
        : _showPreview && _preview.Frame is not null ? _preview.Frame : _history.Current;

    private void DrawCanvas()
    {
        if (DisplayFrame() is not { } frame)
        {
            Ui.Header("A small raster workspace");
            ImGui.TextWrapped("Load or drop an image, choose a transform, and adjust its controls. Preview it, then Apply to keep a step.");
            Ui.Help("Every applied step stays in the stack. Undo, redo, or click a step to return to it.");
            Ui.Help("Try: 1 channel > Threshold > Fill holes (optional) > Contours.");
            return;
        }
        var origin = ImGui.GetCursorScreenPos();
        var room = Vector2.Max(ImGui.GetContentRegionAvail(), Vector2.One);
        ImGui.InvisibleButton("raster-viewport", room);
        var hovered = ImGui.IsItemHovered();
        var fitZoom = MathF.Min(room.X / frame.Width, room.Y / frame.Height);
        if (_fit)
        {
            _zoom = fitZoom;
        }
        HandleArrowPan();
        var io = ImGui.GetIO();
        if (hovered)
        {
            if (io.MouseWheel != 0)
            {
                var previous = _zoom;
                _zoom = Math.Clamp(_zoom * MathF.Pow(1.15f, io.MouseWheel), .01f, 64);
                var relative = io.MousePos - origin - room / 2;
                _pan = relative - (relative - _pan) * (_zoom / previous);
                _fit = false;
            }
            if (ImGui.IsMouseDown(ImGuiMouseButton.Middle))
            {
                _pan += io.MouseDelta;
                _fit = false;
            }
        }
        var size = new Vector2(frame.Width, frame.Height) * _zoom;
        var position = origin + (room - size) / 2 + _pan;
        _lastImagePosition = position;
        _lastImageZoom = _zoom;
        HandleBrush(position, _zoom, frame);
        frame = DisplayFrame()!;
        UpdateRegionHover(position, _zoom, frame);
        if (!ReferenceEquals(frame, _uploaded))
        {
            ReleaseTexture();
            _texture = new GpuTexture(_device!, frame.Width, frame.Height);
            _texturePointSample = _zoom >= 1;
            _textureId = _gui!.Register(_texture, _texturePointSample);
            _uploaded = frame;
            _imageDirty = true;
        }
        else if (_texturePointSample != (_zoom >= 1))
        {
            _gui!.Unregister(_textureId);
            _texturePointSample = _zoom >= 1;
            _textureId = _gui.Register(_texture!, _texturePointSample);
        }
        if (_imageDirty)
        {
            _texture!.SetData(PrepareDisplayPixels(frame));
            _imageDirty = false;
        }
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + room, true);
        draw.AddRectFilled(origin, origin + room, Ui.Color(21, 25, 33));
        var min = Vector2.Max(origin, position);
        var max = Vector2.Min(origin + room, position + size);
        const int tile = 16;
        for (var y = min.Y; y < max.Y; y += tile)
        {
            for (var x = min.X; x < max.X; x += tile)
            {
                var alternate = ((int)((x - min.X) / tile) + (int)((y - min.Y) / tile)) % 2 == 0;
                draw.AddRectFilled(new(x, y), Vector2.Min(new(x + tile, y + tile), max), alternate ? Ui.Color(65, 69, 78) : Ui.Color(48, 52, 61));
            }
        }
        draw.AddImage(_textureId, position, position + size);
        draw.AddRect(position, position + size, Ui.Color(100, 110, 130));
        DrawEditCursor(draw, position, _zoom, frame, hovered);
        draw.PopClipRect();
    }

    private void DrawTools()
    {
        if (_tool == CanvasTool.Inspect)
        {
            DrawTransformTools();
        }
        else
        {
            DrawManualTools();
        }
        DrawHistory();
    }

    private void DrawTransformTools()
    {
        Ui.Header("Transform");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##operation", RasterTransform.Name(_transform.Kind)))
        {
            foreach (var kind in Enum.GetValues<TransformKind>())
            {
                if (ImGui.Selectable(RasterTransform.Name(kind), kind == _transform.Kind))
                {
                    SelectTransform(_transform with { Kind = kind });
                }
            }
            ImGui.EndCombo();
        }
        DrawParameters();
        if (_transform.Kind == TransformKind.Canny && _history.Current?.Mask is not null)
        {
            Ui.Help("To recover soft photo edges, return to the original step first.");
        }
        Ui.Help("About this transform (?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(_transform.Kind switch
            {
                TransformKind.SingleChannel => "Choose RGB, HSV, or Oklab components. Alpha extracts coverage.\nHue wraps and is unreliable near gray; it can make false scalar edges.\nOklab a/b use +0.5 to display signed values; a mask becomes 0/1.",
                TransformKind.Sobel => "Measures local change. Colors use Oklab; one channel uses its scalar gradient.\nApply Threshold next to make a mask.",
                TransformKind.Canny => "Smooths texture, thins gradients, then links weak edges to strong edges.\nLower Weak edge to retain faint continuations. Lower Strong edge to admit more seeds.\nHigher smoothing reduces texture but can lose small details. Start from the original image.",
                TransformKind.Threshold => "White pixels are foreground. For dark art on white, enable Dark foreground.\nFor edges or alpha, leave it off.",
                TransformKind.Palette => "Groups visible colors in Oklab and preserves alpha.\nMore colors and iterations take longer.",
                TransformKind.FillHoles => "Fills closed enclosures, including meaningful holes.\nOpen outlines can leak. Keep this step optional.",
                TransformKind.Contours => "Traces boundaries, including interior holes.\nDisplays the foreground side of each edge as raster pixels.",
                TransformKind.Skeleton => "Thins the mask to a raster centerline.",
                TransformKind.Invert => "Reverses color, scalar values, or foreground/background.\nPreserves alpha on color images.",
                _ => "Connected-region cleanup on the foreground mask."
            });
        }
        if (_preview.Pending)
        {
            Ui.Help("Updating preview...");
        }
        if (_preview.Error is { } error)
        {
            Ui.Problem(error);
        }
        if (DisplayFrame() is { Details.Length: > 0 } result)
        {
            Ui.Help(result.Details);
        }
        Ui.Button("Apply to stack", ApplyPreview, PreviewReady);
        Ui.Help("Adjust to preview. Apply keeps one step.");
    }

    private void DrawHistory()
    {
        ImGui.Separator();
        Ui.Header($"Stack · {Math.Max(0, _history.Position)} applied");
        Ui.Help("Click a step to return. (?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Applying from an earlier step replaces its redo branch.\nCtrl+Z: undo | Ctrl+Y / Ctrl+Shift+Z: redo\nSave exports the selected applied step.");
        }
        Ui.Panel("history", () =>
        {
            for (var i = 0; i < _history.Steps.Count; i++)
            {
                var step = _history.Steps[i];
                var suffix = i > _history.Position ? " (redo)" : "";
                if (ImGui.Selectable($"{i:00}  {step.Description}{suffix}##step{i}", i == _history.Position))
                {
                    _history.Select(i);
                    HistoryChanged();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"{step.Description}\n{step.Frame.Kind}");
                }
            }
        });
    }

    private void DrawParameters()
    {
        switch (_transform.Kind)
        {
            case TransformKind.SingleChannel:
                DrawChannelChoice();
                break;
            case TransformKind.Canny:
                DrawChannelChoice();
                var smoothing = _transform.Smoothing;
                var weak = _transform.WeakEdge;
                var strong = _transform.StrongEdge;
                var cannyChanged = Ui.Slider("Smoothing (pixels)", ref smoothing, 0, 5, "%.1f");
                ImGui.TextUnformatted("Weak / strong edge cutoff");
                var cutoffWidth = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2;
                ImGui.SetNextItemWidth(cutoffWidth);
                const ImGuiSliderFlags cutoffFlags = ImGuiSliderFlags.Logarithmic | ImGuiSliderFlags.AlwaysClamp;
                cannyChanged |= ImGui.SliderFloat("##weak", ref weak, .0001f, .25f, "%.4f", cutoffFlags);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Keep faint edges above this value when they connect to a strong edge.");
                }
                strong = MathF.Max(strong, weak);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(cutoffWidth);
                cannyChanged |= ImGui.SliderFloat("##strong", ref strong, weak, .5f, "%.4f", cutoffFlags);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Edges above this value seed the connected edge mask.");
                }
                if (cannyChanged)
                {
                    SelectTransform(_transform with { Smoothing = smoothing, WeakEdge = weak, StrongEdge = strong });
                }
                break;
            case TransformKind.Threshold:
                var threshold = _transform.Threshold;
                var dark = _transform.DarkForeground;
                var changed = Ui.Slider("Threshold", ref threshold, 0, 1);
                changed |= ImGui.Checkbox("Dark foreground", ref dark);
                if (changed)
                {
                    SelectTransform(_transform with { Threshold = threshold, DarkForeground = dark });
                }
                break;
            case TransformKind.Palette:
                var colors = _transform.Colors;
                var iterations = _transform.Iterations;
                ImGui.TextUnformatted("Colors");
                ImGui.SetNextItemWidth(-1);
                var paletteChanged = ImGui.SliderInt("##colors", ref colors, 2, 32, "%d", ImGuiSliderFlags.AlwaysClamp);
                ImGui.TextUnformatted("Iterations");
                ImGui.SetNextItemWidth(-1);
                paletteChanged |= ImGui.SliderInt("##iterations", ref iterations, 1, 64, "%d", ImGuiSliderFlags.AlwaysClamp);
                if (paletteChanged)
                {
                    SelectTransform(_transform with { Colors = colors, Iterations = iterations });
                }
                break;
            case TransformKind.RemoveSmallRegions:
            case TransformKind.LargestRegion:
                var pixels = _transform.MinimumPixels;
                var largestCount = _transform.LargestRegions;
                var connected = _transform.EightConnected;
                var regionChanged = false;
                if (_transform.Kind == TransformKind.RemoveSmallRegions)
                {
                    ImGui.TextUnformatted("Minimum region size (pixels)");
                    ImGui.SetNextItemWidth(-1);
                    regionChanged = ImGui.SliderInt("##minimum", ref pixels, 1, 10000, "%d", ImGuiSliderFlags.Logarithmic | ImGuiSliderFlags.AlwaysClamp);
                }
                else
                {
                    ImGui.TextUnformatted("Regions to keep (N)");
                    ImGui.SetNextItemWidth(-1);
                    regionChanged = ImGui.SliderInt("##largest-count", ref largestCount, 1, 100, "%d", ImGuiSliderFlags.AlwaysClamp);
                }
                regionChanged |= ImGui.Checkbox("Connect diagonals (8 neighbors)", ref connected);
                if (regionChanged)
                {
                    SelectTransform(_transform with { MinimumPixels = pixels, LargestRegions = largestCount, EightConnected = connected });
                }
                break;
        }
    }

    private void DrawChannelChoice()
    {
        if (_history.Current?.Colors is not null)
        {
            var channel = Ui.Combo("Channel", _transform.Channel.ToString(), Enum.GetNames<ChannelSource>(),
                static name => RasterTransform.ChannelName(Enum.Parse<ChannelSource>(name)));
            if (channel is not null)
            {
                SelectTransform(_transform with { Channel = Enum.Parse<ChannelSource>(channel) });
            }
            if (_transform.Channel == ChannelSource.HsvHue)
            {
                Ui.Help("Hue wraps at red and is unstable near gray.");
            }
        }
    }
}
