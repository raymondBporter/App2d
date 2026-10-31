using App2d.EditorUi;
using ImGuiNET;
using System.Numerics;

namespace App2d.ImageLab;

internal sealed partial class ImageLabWindow
{
    private RasterInk _ink = RasterInk.White;
    private CanvasTool _returnFromDropper = CanvasTool.Paint;
    private RasterFrame? _paletteFrame;
    private RasterInk[] _palette = [];
    private string? _inkKind;

    private static string ToolName(CanvasTool tool) => tool switch
    {
        CanvasTool.Inspect => "View / tools",
        CanvasTool.Paint => "Pixel brush",
        CanvasTool.Erase => "Erase mask",
        CanvasTool.Line => "Line",
        _ => "Eyedropper"
    };

    private void ResetInkForKind()
    {
        var kind = _history.Current?.Kind;
        if (_inkKind != kind)
        {
            _ink = RasterInk.White;
            _inkKind = kind;
        }
    }

    internal void PickInk(int x, int y)
    {
        if (_history.Current is not { } frame)
        {
            return;
        }
        _ink = RasterInk.Sample(frame, x, y);
        var sampled = _ink.Description(frame);
        SetCanvasTool(_returnFromDropper);
        _status = $"Picked {sampled} at ({x}, {y}).";
    }

    private void DrawManualTools()
    {
        Ui.Header(ToolName(_tool));
        Ui.Help(_tool switch
        {
            CanvasTool.Paint => "1 px brush. Click or drag; one stroke is one undo step.",
            CanvasTool.Line => "Drag a 1 px line. Release to keep one undo step; Esc cancels.",
            CanvasTool.Eyedropper => "Click an image pixel to pick its actual value, then return to brush/line. Display overlays are ignored.",
            _ => "Drag to erase mask pixels. Each stroke is one undo step."
        });
        if (_tool == CanvasTool.Line && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("One-pixel diagonal lines connect with 8-neighbor.\n4-neighbor requires pixels to share an edge.");
        }
        if (_history.Current is not { } frame)
        {
            Ui.Help("Load an image first.");
            return;
        }
        if (_tool == CanvasTool.Erase && frame.Mask is null)
        {
            Ui.Problem("Apply Threshold or Canny first to create a mask for the eraser.");
        }
        if (_tool != CanvasTool.Erase)
        {
            if (!ReferenceEquals(frame, _paletteFrame))
            {
                _palette = RasterInk.Palette(frame);
                _paletteFrame = frame;
            }
            ImGui.ColorButton("##brush-ink", Swatch(_ink), ImGuiColorEditFlags.NoTooltip, new(24 * Ui.Scale));
            ImGui.SameLine();
            ImGui.TextUnformatted(_ink.Description(frame));
            Ui.Header(frame.Mask is not null ? "Mask values" : "Image palette");
            if (frame.Mask is null && ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Up to 24 common displayed colors. Use Eyedropper to pick any other image pixel.");
            }
            var size = 23 * Ui.Scale;
            var columns = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X + ImGui.GetStyle().ItemSpacing.X) / (size + ImGui.GetStyle().ItemSpacing.X)));
            for (var i = 0; i < _palette.Length; i++)
            {
                if (i % columns != 0)
                {
                    ImGui.SameLine();
                }
                var ink = _palette[i];
                if (ImGui.ColorButton($"##ink{i}", Swatch(ink), ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.AlphaPreviewHalf, new(size)))
                {
                    _ink = ink;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(ink.Description(frame));
                }
            }
        }
        Ui.Row(
            () => Ui.Button("Eyedropper (I)", () => SetCanvasTool(CanvasTool.Eyedropper), _tool != CanvasTool.Eyedropper),
            () => Ui.Button("Tools (V)", () => SetCanvasTool(CanvasTool.Inspect)));
        Ui.Help("B brush · L line · I pick\nCtrl+Z undo · Esc cancel");
    }

    private static Vector4 Swatch(RasterInk ink) => new(ink.Color.R / 255f, ink.Color.G / 255f, ink.Color.B / 255f, ink.Color.A / 255f);

    internal void PixelRepairStudy()
    {
        var mask = new Core.Imaging.Image2D<bool>(64, 56);
        for (var y = 6; y <= 22; y++)
        {
            mask[30, y] = true;
        }
        for (var y = 26; y <= 47; y++)
        {
            mask[30, y] = true;
        }
        mask[12, 35] = mask[12, 36] = true;
        LoadFrame(new(mask), "One-pixel line repair");
        SetCanvasTool(CanvasTool.Line);
        SetCanvasTool(CanvasTool.Eyedropper);
        PickInk(30, 15);
        if (_tool != CanvasTool.Line || _history.Position != 0 || _ink.Scalar != 1)
        {
            throw new InvalidOperationException("Eyedropper must return to Line without adding a step.");
        }
        BeginBrushStroke(new(30.5f, 22.5f));
        MoveBrushStroke(new(30.5f, 26.5f));
        if (_history.Current!.Mask![30, 24])
        {
            throw new InvalidOperationException("Line preview changed the committed mask.");
        }
        FinishBrushStroke();
        if (_history.Position != 1 || !_history.Current!.Mask![30, 24]
            || new RasterRegions(_history.Current.Mask, true).Count != 2)
        {
            throw new InvalidOperationException("One line must join two components as one step.");
        }
        _history.Undo();
        HistoryChanged();
        if (_history.Current!.Mask![30, 24])
        {
            throw new InvalidOperationException("Line undo did not restore the gap.");
        }
        _history.Redo();
        HistoryChanged();
        _overlayOriginal = false;
        _inspectRegions = true;
    }

    internal void LineReleaseStudy(Action draw)
    {
        draw();
        draw();
        SetCanvasTool(CanvasTool.Line);
        var before = _history.Position;
        var io = ImGui.GetIO();
        io.ConfigInputTrickleEventQueue = false;
        PointAtPixelForStudy(new(40.5f, 40.5f));
        io.AddMouseButtonEvent(0, true);
        draw();
        draw();
        PointAtPixelForStudy(new(45.5f, 40.5f));
        io.AddMouseButtonEvent(0, false);
        draw();
        draw();
        if (_history.Position != before + 1 || !_history.Current!.Mask![45, 40])
        {
            throw new InvalidOperationException("A released line must include its final pointer position and commit once.");
        }
    }

    private void DrawEditCursor(ImDrawListPtr draw, Vector2 imagePosition, float zoom, RasterFrame frame, bool hovered)
    {
        if (_tool == CanvasTool.Inspect || !hovered)
        {
            return;
        }
        var mouse = ImGui.GetIO().MousePos;
        if (_tool == CanvasTool.Erase)
        {
            if (frame.Mask is not null)
            {
                draw.AddCircle(mouse, _brushRadius * zoom, Ui.Color(255, 255, 255), 48, 1.5f);
            }
        }
        else
        {
            var relative = (mouse - imagePosition) / zoom;
            var pixel = new Vector2(MathF.Floor(relative.X), MathF.Floor(relative.Y));
            if (pixel.X >= 0 && pixel.Y >= 0 && pixel.X < frame.Width && pixel.Y < frame.Height)
            {
                var min = imagePosition + pixel * zoom;
                draw.AddRect(min, min + new Vector2(zoom), Ui.Color(255, 208, 65));
            }
        }
    }
}
