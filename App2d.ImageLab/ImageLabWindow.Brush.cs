using App2d.Core.Imaging;
using App2d.EditorUi;
using ImGuiNET;
using System.Numerics;

namespace App2d.ImageLab;

internal sealed partial class ImageLabWindow
{
    private CanvasTool _tool;
    private int _brushRadius = 6;
    private IRasterStroke? _stroke;
    private bool _overlayOriginal;
    private bool _imageDirty;
    private Image2D<Color4b>? _originalPixels;

    internal void SetCanvasTool(CanvasTool tool)
    {
        FinishBrushStroke();
        if (_tool == tool)
        {
            return;
        }
        if (tool == CanvasTool.Eyedropper)
        {
            _returnFromDropper = _tool == CanvasTool.Line ? CanvasTool.Line : CanvasTool.Paint;
        }
        _tool = tool;
        if (tool != CanvasTool.Inspect)
        {
            _showOriginal = false;
            _showPreview = false;
            _overlayOriginal = _history.Current?.Mask is not null;
            _imageDirty = true;
        }
        _preview.Change(tool == CanvasTool.Inspect ? _history.Current : null, _transform);
    }

    private void DrawBrushToolbar()
    {
        ImGui.SetNextItemWidth(125 * Ui.Scale);
        if (ImGui.BeginCombo("##canvas-tool", ToolName(_tool)))
        {
            foreach (var tool in Enum.GetValues<CanvasTool>())
            {
                if (ImGui.Selectable(ToolName(tool), tool == _tool))
                {
                    SetCanvasTool(tool);
                }
            }
            ImGui.EndCombo();
        }
        if (_tool == CanvasTool.Erase)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100 * Ui.Scale);
            ImGui.SliderInt("##radius", ref _brushRadius, 1, 50, "%d px radius", ImGuiSliderFlags.AlwaysClamp);
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(DisplayFrame()?.Mask is null || _showOriginal);
        if (ImGui.Checkbox("Overlay original", ref _overlayOriginal))
        {
            _imageDirty = true;
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip("Display mask pixels in mint over the original image.\nThis guide is not included in PNG exports.\nWheel: zoom | Arrows / middle drag: pan | Shift: faster");
        }
    }

    private void HandleBrush(Vector2 imagePosition, float zoom, RasterFrame displayed)
    {
        if (_tool == CanvasTool.Inspect || _history.Current is null || _showOriginal || _showPreview
            || (_tool == CanvasTool.Erase && _history.Current.Mask is null))
        {
            return;
        }
        var point = (ImGui.GetIO().MousePos - imagePosition) / zoom;
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)
            || MathF.Abs(point.X) > 1e20f || MathF.Abs(point.Y) > 1e20f)
        {
            // ImGui uses -float.MaxValue for a pointer that has left the application.
            FinishBrushStroke();
            return;
        }
        var inside = point.X >= 0 && point.Y >= 0 && point.X < displayed.Width && point.Y < displayed.Height;
        if (_tool == CanvasTool.Eyedropper)
        {
            if (inside && ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                PickInk((int)point.X, (int)point.Y);
            }
            return;
        }
        if (_stroke is not null && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            // Include the released pointer position even when it moved after the previous render.
            MoveBrushStroke(point);
            FinishBrushStroke();
            return;
        }
        if (_stroke is null && ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && inside)
        {
            BeginBrushStroke(point);
        }
        if (_stroke is not null && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            MoveBrushStroke(point);
        }
    }

    internal void BeginBrushStroke(Vector2 point)
    {
        if (_tool is CanvasTool.Inspect or CanvasTool.Eyedropper || _history.Current is not { } source)
        {
            return;
        }
        if (_tool == CanvasTool.Erase)
        {
            if (source.Mask is null)
            {
                return;
            }
            _stroke = new RasterBrushStroke(source.Mask, _brushRadius, foreground: false);
        }
        else
        {
            _stroke = new RasterPixelStroke(source, _ink, line: _tool == CanvasTool.Line);
        }
        MoveBrushStroke(point);
    }

    internal void MoveBrushStroke(Vector2 point)
    {
        if (_stroke is not null)
        {
            _imageDirty |= _stroke.MoveTo(point);
        }
    }

    internal void BrushStudy()
    {
        var mask = _history.Current!.Mask!;
        var index = mask.AsSpan().IndexOf(true);
        if (index < 0)
        {
            throw new InvalidOperationException("Brush smoke needs a detected edge.");
        }
        var before = _history.Position;
        var point = new Vector2(index % mask.Width + .5f, index / mask.Width + .5f);
        BeginBrushStroke(point);
        MoveBrushStroke(point + new Vector2(10, 3));
        FinishBrushStroke();
        if (_history.Position != before + 1 || _history.Current!.Mask!.AsSpan()[index] || !mask.AsSpan()[index])
        {
            throw new InvalidOperationException("Brush stroke must commit once and preserve its input.");
        }
        _history.Undo();
        HistoryChanged();
        if (!_history.Current!.Mask!.AsSpan()[index])
        {
            throw new InvalidOperationException("Undo did not restore the erased edge.");
        }
        _history.Redo();
        HistoryChanged();
    }

    internal void FinishBrushStroke()
    {
        if (_stroke is not { } stroke)
        {
            return;
        }
        _stroke = null;
        if (stroke.Changed)
        {
            _history.Apply(stroke.Description, stroke.Frame);
            _status = $"{stroke.Description}. Ctrl+Z undoes the whole stroke.";
            HistoryChanged();
        }
    }

    private Color4b[] PrepareDisplayPixels(RasterFrame frame)
    {
        if (frame.Mask is null || _showOriginal)
        {
            return frame.DisplayPixels().AsSpan().ToArray();
        }
        var result = new Color4b[frame.Width * frame.Height];
        var mask = frame.Mask.AsSpan();
        var regions = _inspectRegions ? RegionMapForDisplay() : null;
        ReadOnlySpan<int> labels = regions is null ? default : regions.Labels.AsSpan();
        ReadOnlySpan<Color4b> original = _originalPixels is null ? default : _originalPixels.AsSpan();
        for (var i = 0; i < result.Length; i++)
        {
            var color = mask[i] ? Color4b.White : Color4b.Black;
            if (_overlayOriginal && !original.IsEmpty)
            {
                var rgb = RasterFrame.CompositeOnWhite(original[i]) * .7f;
                color = mask[i] ? new(75, 245, 199)
                    : new(RasterFrame.Byte(rgb.X), RasterFrame.Byte(rgb.Y), RasterFrame.Byte(rgb.Z));
            }
            if (regions is not null && _hoverRegion > 0 && mask[i])
            {
                color = labels[i] == _hoverRegion ? new Color4b(255, 208, 65) : new Color4b(65, 80, 85);
            }
            result[i] = color;
        }
        return result;
    }
}
