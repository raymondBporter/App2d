using App2d.EditorUi;
using ImGuiNET;
using System.Numerics;

namespace App2d.ImageLab;

internal sealed partial class ImageLabWindow
{
    private bool _inspectRegions;
    private RasterFrame? _regionFrame;
    private RasterRegions? _viewRegions;
    private bool _regionEightConnected;
    private int _hoverRegion;

    internal Vector2 RegionInspectionStudy()
    {
        _inspectRegions = true;
        var frame = _history.Current!;
        var regions = RegionMapForDisplay()!;
        var id = regions.LargestFirst[0];
        var index = regions.Labels.AsSpan().IndexOf(id);
        var original = frame.Mask!.AsSpan().ToArray();
        _hoverRegion = id;
        var display = PrepareDisplayPixels(frame);
        if (display[index] != new Color4b(255, 208, 65) || !original.AsSpan().SequenceEqual(frame.Mask.AsSpan()))
        {
            throw new InvalidOperationException("Region hover must highlight without changing pixels.");
        }
        _imageDirty = true;
        return new(index % frame.Width + .5f, index / frame.Width + .5f);
    }

    internal void PointAtPixelForStudy(Vector2 pixel)
    {
        var io = ImGui.GetIO();
        io.AddFocusEvent(true);
        var position = _lastImagePosition + pixel * _lastImageZoom;
        io.AddMousePosEvent(position.X, position.Y);
    }

    private RasterRegions? GetRegionMap(RasterFrame? frame)
    {
        if (frame?.Mask is null)
        {
            return null;
        }
        if (!ReferenceEquals(frame, _regionFrame) || _regionEightConnected != _transform.EightConnected)
        {
            _viewRegions = new(frame.Mask, _transform.EightConnected);
            _regionFrame = frame;
            _regionEightConnected = _transform.EightConnected;
            _hoverRegion = 0;
            _imageDirty = true;
        }
        return _viewRegions;
    }

    // Keep labels stable while a line/stroke is being previewed; refresh once on commit, rather than per pixel.
    private RasterRegions? RegionMapForDisplay() => GetRegionMap(_stroke is null ? DisplayFrame() : _history.Current);

    private void DrawRegionToolbar()
    {
        ImGui.BeginDisabled(DisplayFrame()?.Mask is null || _showOriginal);
        if (ImGui.Checkbox("Inspect regions", ref _inspectRegions))
        {
            _hoverRegion = 0;
            _imageDirty = true;
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip("Hover a mask pixel to highlight its whole connected region.\nColors are a view only; the stored mask and PNG remain unchanged.");
        }
        if (_inspectRegions)
        {
            ImGui.SameLine();
            var connected = _transform.EightConnected;
            if (ImGui.Checkbox("8-neighbor", ref connected))
            {
                _transform = _transform with { EightConnected = connected };
                _imageDirty = true;
                if (_tool == CanvasTool.Inspect && _transform.Kind is TransformKind.LargestRegion or TransformKind.RemoveSmallRegions)
                {
                    _preview.Change(_history.Current, _transform);
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("8-neighbor includes diagonal contacts. Uncheck for 4-neighbor (edge contacts only).\nThis is the same setting used by the region cleanup transforms.");
            }
        }
        ImGui.EndDisabled();
        if (_inspectRegions && DisplayFrame()?.Mask is not null && !_showOriginal)
        {
            var regions = RegionMapForDisplay();
            ImGui.SameLine();
            ImGui.TextUnformatted($"{regions?.Count ?? 0} regions");
        }
    }

    private void UpdateRegionHover(Vector2 imagePosition, float zoom, RasterFrame displayed)
    {
        var next = 0;
        if (_inspectRegions && displayed.Mask is not null && !_showOriginal && ImGui.IsItemHovered())
        {
            var point = (ImGui.GetIO().MousePos - imagePosition) / zoom;
            if (point.X >= 0 && point.Y >= 0 && point.X < displayed.Width && point.Y < displayed.Height)
            {
                var regions = RegionMapForDisplay();
                var tolerance = Math.Clamp((int)MathF.Ceiling(2 * Ui.Scale / zoom), 1, 12);
                next = regions?.PickRegion(point, tolerance, displayed.Mask) ?? 0;
                if (next > 0 && regions is not null)
                {
                    ImGui.SetTooltip($"Region #{next} · {regions.PixelCounts[next]} pixels · {(_transform.EightConnected ? 8 : 4)}-neighbor"
                        + (_stroke is null ? "" : "\nConnections refresh when the stroke is committed."));
                }
            }
        }
        if (next != _hoverRegion)
        {
            _hoverRegion = next;
            _imageDirty = true;
        }
    }
}
