using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>A local-coordinate canvas, independent of the character viewport and its current pose or socket.</summary>
internal sealed class GeometryCanvas(EditorSession session)
{
    private string? _id;
    private Vector2 _center;
    private float _span = 1.5f;
    private int _drag = -1;
    private bool _grid = true, _snap, _triangles;
    private float _gridStep = .1f;
    public int Selected { get; set; }

    public void Draw(GeometryAsset2D asset, Action<GeometryAsset2D>? change, PartGeometry? preview = null)
    {
        var entry = preview ?? asset.Entry(new() { Fill = "#8faecb", Outline = RenderOutlineDefinition2D.Ink, StrokeWidth = .025f });
        var contour = PartContours2D.Contour(entry, _ => Vector3.Zero, _ => LayeredFrame2D.Identity).Select(p => new Vector2(p.X, p.Y)).ToArray();
        void Fit()
        {
            if (contour.Length == 0) return;
            var min = contour.Aggregate(Vector2.Zero, Vector2.Min);
            var max = contour.Aggregate(Vector2.Zero, Vector2.Max);
            _center = (min + max) / 2;
            _span = MathF.Max(.2f, MathF.Max(max.X - min.X, max.Y - min.Y) * 1.5f);
        }
        if (_id != asset.Id)
        {
            if (_drag >= 0) session.EndDrag();
            _id = asset.Id;
            _drag = -1;
            Selected = 0;
            Fit();
        }
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        void Continue(string label)
        {
            var needed = ImGui.GetFrameHeight() + ImGui.CalcTextSize(label).X + ImGui.GetStyle().ItemSpacing.X * 2;
            if (ImGui.GetItemRectMax().X + needed < right) ImGui.SameLine();
        }
        if (ImGui.SmallButton("Fit geometry")) Fit();
        Continue("Grid"); ImGui.Checkbox("Grid", ref _grid);
        Continue("Snap"); ImGui.Checkbox("Snap", ref _snap);
        Continue("Triangles"); ImGui.Checkbox("Triangles", ref _triangles);
        ImGui.SetNextItemWidth(90 * Ui.Scale);
        ImGui.DragFloat("Grid units", ref _gridStep, .005f, .001f, 100, "%.3f", ImGuiSliderFlags.AlwaysClamp);
        _gridStep = Math.Clamp(_gridStep, .001f, 100);
        var size = Vector2.Max(ImGui.GetContentRegionAvail(), new(1, 1));
        var origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("local-geometry-canvas", size, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonMiddle);
        var hovered = ImGui.IsItemHovered();
        var ppu = MathF.Min(size.X, size.Y) / _span;
        var anchor = origin + size / 2;
        Vector2 Screen(Vector2 p) => anchor + new Vector2(p.X - _center.X, _center.Y - p.Y) * ppu;
        Vector2 World(Vector2 p) => _center + new Vector2(p.X - anchor.X, anchor.Y - p.Y) / ppu;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + size, 0xff211e1b);
        draw.PushClipRect(origin, origin + size, true);
        var low = World(origin + new Vector2(0, size.Y));
        var high = World(origin + new Vector2(size.X, 0));
        if (_grid)
        {
            var step = _gridStep;
            while (step * ppu < 18) step *= 10;
            for (var i = 0; i < 512; i++)
            {
                var x = (MathF.Ceiling(low.X / step) + i) * step;
                if (x > high.X) break;
                draw.AddLine(Screen(new(x, low.Y)), Screen(new(x, high.Y)), 0xff3c3732);
            }
            for (var i = 0; i < 512; i++)
            {
                var y = (MathF.Ceiling(low.Y / step) + i) * step;
                if (y > high.Y) break;
                draw.AddLine(Screen(new(low.X, y)), Screen(new(high.X, y)), 0xff3c3732);
            }
            draw.AddText(origin + new Vector2(10, size.Y - 24 * Ui.Scale), 0xffb8b2a9, $"Grid {step:0.###} units");
        }
        var material = entry.RenderMaterial;
        uint Color(string hex, float alpha = 1)
        {
            var color = ColorExtensions.FromHexRgb(hex).ToNumericsVector4();
            color.W = alpha;
            if (material.Tint is { } tint) color *= ColorExtensions.FromHexRgba(tint).ToNumericsVector4();
            return ImGui.GetColorU32(color);
        }
        var closed = entry.Geometry is not ICurve2D && entry.SpatialCurve is null;
        if (entry.Mesh is { } mesh && mesh.Outline is null)
        {
            for (var i = 0; i < mesh.Triangles.Count; i += 3)
            {
                var a = Screen(contour[mesh.Triangles[i]]); var b = Screen(contour[mesh.Triangles[i + 1]]); var c = Screen(contour[mesh.Triangles[i + 2]]);
                if (material.Fill is { } fill) draw.AddTriangleFilled(a, b, c, Color(fill, material.Alpha ?? 1));
                if (_triangles) draw.AddTriangle(a, b, c, 0x88777777);
            }
        }
        else if (closed && contour.Length >= 3)
        {
            var triangles = PolygonTriangulation2D.Triangulate(contour);
            foreach (var (a, b, c) in triangles.Triangles)
            {
                if (material.Fill is { } fill) draw.AddTriangleFilled(Screen(a), Screen(b), Screen(c), Color(fill, material.Alpha ?? 1));
                if (_triangles) draw.AddTriangle(Screen(a), Screen(b), Screen(c), 0x88777777);
            }
        }
        var outlineColor = material.Outline?.Color;
        if (!closed && material.Fill is not null || closed && outlineColor is not null)
        {
            var width = closed ? material.Outline?.Width ?? .01f : material.StrokeWidth ?? .025f;
            var color = Color(closed ? outlineColor ?? "#222b32" : material.Fill ?? "#222b32", material.Alpha ?? 1);
            for (var i = 0; i < contour.Length - (closed ? 0 : 1); i++)
                draw.AddLine(Screen(contour[i]), Screen(contour[(i + 1) % contour.Length]), color, MathF.Max(1, width * ppu));
        }
        draw.AddLine(Screen(new(low.X, 0)), Screen(new(high.X, 0)), 0x99666699, 1.5f);
        draw.AddLine(Screen(new(0, low.Y)), Screen(new(0, high.Y)), 0x99669966, 1.5f);
        draw.AddCircle(Screen(Vector2.Zero), 5 * Ui.Scale, 0xffdddddd);
        draw.AddText(Screen(Vector2.Zero) + new Vector2(7, 5), 0xffdddddd, "0,0");
        var points = change is null ? [] : GeometryAssetEditing.Points(asset);
        var localFrame = PartAttachment2D.FrameOf(entry, _ => Vector3.Zero, _ => LayeredFrame2D.Identity).Frame;
        Vector2 PointScreen(Vector2 point) => Screen(localFrame.TransformPoint(point));
        if (asset.Geometry is QuadraticBezier2D or CubicBezier2D or BSpline2D)
        {
            for (var i = 1; i < points.Count; i++)
                draw.AddLine(PointScreen(points[i - 1]), PointScreen(points[i]), 0xffa89261, 1);
        }
        var nearest = -1;
        var distance = 11 * Ui.Scale;
        for (var i = 0; i < points.Count; i++)
        {
            var p = PointScreen(points[i]);
            var d = Vector2.Distance(p, ImGui.GetMousePos());
            if (d < distance) { nearest = i; distance = d; }
            draw.AddCircleFilled(p, (i == Selected ? 6 : 4) * Ui.Scale, i == Selected ? 0xff6bdcff : 0xffeeeeee);
            draw.AddText(p + new Vector2(7, -16), 0xffeeeeee, (i + 1).ToString());
        }
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && nearest >= 0)
        {
            Selected = _drag = nearest;
            session.BeginDrag();
        }
        if (_drag >= 0)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) { _drag = -1; session.EndDrag(); }
            else if (_drag < points.Count && ImGui.GetIO().MouseDelta != Vector2.Zero && change is not null)
            {
                if (!localFrame.TryToLocal(World(ImGui.GetMousePos()), out var value)) { draw.PopClipRect(); return; }
                if (_snap) value = new(MathF.Round(value.X / _gridStep) * _gridStep, MathF.Round(value.Y / _gridStep) * _gridStep);
                var edited = points.ToArray();
                edited[_drag] = value;
                change(GeometryAssetEditing.WithPoints(asset, edited));
            }
        }
        if (hovered && ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
            _center += new Vector2(-ImGui.GetIO().MouseDelta.X, ImGui.GetIO().MouseDelta.Y) / ppu;
        if (hovered && ImGui.GetIO().MouseWheel != 0)
        {
            var mouse = World(ImGui.GetMousePos());
            var ratio = MathF.Pow(1.15f, -ImGui.GetIO().MouseWheel);
            var nextSpan = Math.Clamp(_span * ratio, .005f, 10000);
            _center = mouse + (_center - mouse) * (nextSpan / _span);
            _span = nextSpan;
        }
        draw.PopClipRect();
    }
}
