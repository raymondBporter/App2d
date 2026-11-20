using App2d.Core.Geometry.Construction;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.RootFinding;
using App2d.EditorUi;
using ImGuiNET;
using System.Numerics;

namespace App2d.Demos;

internal sealed class ConicDemo : IDemo
{
    private static readonly Vector2f[] DefaultPoints = [new(-3, -2), new(3, -2), new(3, 2), new(-3, 2)];
    private readonly Vector2f[] _points = [.. DefaultPoints];
    private ConicPencil2D? _pencil = MakePencil(DefaultPoints);
    private float _ratio = .25f;
    private int _drag = -1;
    private Vector2f _grabOffset;

    public string Name => "Conic pencil";
    public void Draw() => DemoCanvas.Layout(DrawCanvas, DrawControls);
    public void Deactivate() => _drag = -1;

    private void DrawControls()
    {
        ImGui.TextWrapped("Drag any of the four points. Turn the ratio to change the curve through them.");
        ImGui.TextUnformatted("Ratio");
        ImGui.SetNextItemWidth(-1);
        ImGui.SliderFloat("##ratio", ref _ratio, 0f, 1f, "%.3f");
        if (ImGui.Button("AB × CD"))
        {
            _ratio = 0f;
        }
        ImGui.SameLine();
        if (ImGui.Button("BC × DA"))
        {
            _ratio = .5f;
        }

        if (_pencil is { } pencil)
        {
            var (sine, cosine) = double.SinCosPi(_ratio % 1d);
            var determinant = pencil.At(_ratio).QuadraticDeterminant;
            ImGui.TextUnformatted($"Weights  {cosine:+0.000;-0.000;0.000}, {sine:+0.000;-0.000;0.000}");
            ImGui.TextUnformatted(determinant switch
            {
                > 0d => "Elliptic",
                < 0d => "Hyperbolic",
                _ => "Parabolic or degenerate",
            });
        }
        else
        {
            ImGui.TextWrapped("Use four distinct points with no three on one line.");
        }

        if (ImGui.Button("Reset points"))
        {
            DefaultPoints.CopyTo(_points, 0);
            _pencil = MakePencil(_points);
            _ratio = .25f;
            _drag = -1;
        }
    }

    private void DrawCanvas()
    {
        var canvas = DemoCanvas.Begin("conic", Vector2f.Zero, new(12, 8));
        if (canvas.Activated)
        {
            var closest = 15f * Ui.Scale;
            _drag = -1;
            for (var i = 0; i < _points.Length; i++)
            {
                var distance = Vector2.Distance(canvas.ScreenAt(_points[i]), ImGui.GetMousePos());
                if (distance < closest)
                {
                    closest = distance;
                    _drag = i;
                }
            }
            if (_drag >= 0)
            {
                _grabOffset = _points[_drag] - canvas.Pointer();
            }
        }
        if (_drag >= 0 && ImGui.IsMousePosValid())
        {
            var point = canvas.Pointer() + _grabOffset;
            if (point != _points[_drag])
            {
                _points[_drag] = point;
                _pencil = MakePencil(_points);
            }
        }
        // A quick drag can deliver the last pointer position on the button-up frame.
        if (!canvas.Active)
        {
            _drag = -1;
        }

        var draw = canvas.Draw;
        draw.PushClipRect(canvas.Min, canvas.Max, true);
        try
        {
            canvas.DrawGrid();
            if (_pencil is { } pencil)
            {
                var conic = pencil.At(_ratio);
                DrawSlices(canvas, conic, columns: true);
                DrawSlices(canvas, conic, columns: false);
            }
            for (var i = 0; i < _points.Length; i++)
            {
                var point = canvas.ScreenAt(_points[i]);
                draw.AddCircleFilled(point, (i == _drag ? 8f : 6f) * Ui.Scale, Ui.Color(247, 182, 97));
                draw.AddText(point + new Vector2(10f, -20f) * Ui.Scale, Ui.Color(255, 225, 173), ((char)('A' + i)).ToString());
            }
        }
        finally
        {
            draw.PopClipRect();
        }
    }

    private static void DrawSlices(DemoCanvas canvas, Conic2D conic, bool columns)
    {
        // Solve the implicit equation on both horizontal and vertical cuts. Each cut draws the
        // branch whose tangent follows its scan direction, including line-pair cases at the basis weights.
        var min = (canvas.WorldMin - conic.Origin).ToVector2d();
        var max = (canvas.WorldMax - conic.Origin).ToVector2d();
        var low = columns ? min.X : min.Y;
        var high = columns ? max.X : max.Y;
        var rootLow = columns ? min.Y : min.X;
        var rootHigh = columns ? max.Y : max.X;
        var count = Math.Max(1, (int)Math.Ceiling((columns ? canvas.Size.X : canvas.Size.Y) / 2d));
        var step = (high - low) / count;
        Span<double> roots = stackalloc double[2];
        var fixedSquared = columns ? conic.XX : conic.YY;
        var fixedLinear = columns ? conic.Linear.X : conic.Linear.Y;
        var freeLinear = columns ? conic.Linear.Y : conic.Linear.X;
        var a = columns ? conic.YY : conic.XX;

        for (var slice = 0; slice <= count; slice++)
        {
            var fixedValue = low + slice * step;
            var c = (fixedSquared * fixedValue + fixedLinear) * fixedValue + conic.Constant;
            var b = conic.XY * fixedValue + freeLinear;
            if (c == 0d && b == 0d && a == 0d)
                continue;

            var rootCount = PolynomialRoots.SolveQuadratic(a, b, c, rootLow, rootHigh, roots);
            for (var i = 0; i < rootCount; i++)
            {
                var point = columns ? new Vector2d(fixedValue, roots[i]) : new Vector2d(roots[i], fixedValue);
                var tangent = new Vector2d(
                    -(conic.XY * point.X + 2d * conic.YY * point.Y + conic.Linear.Y),
                    2d * conic.XX * point.X + conic.XY * point.Y + conic.Linear.X);
                var along = Math.Abs(columns ? tangent.X : tangent.Y);
                var across = Math.Abs(columns ? tangent.Y : tangent.X);
                if (along == 0d || along < across)
                    continue;

                var halfStroke = (tangent * (.68d * step / along)).ToVector2f();
                var world = conic.Origin + point.ToVector2f();
                canvas.Draw.AddLine(canvas.ScreenAt(world - halfStroke), canvas.ScreenAt(world + halfStroke),
                    Ui.Color(108, 228, 202), 2f * Ui.Scale);
            }
        }
    }

    private static ConicPencil2D? MakePencil(Vector2f[] points)
    {
        try
        {
            return new(points[0], points[1], points[2], points[3]);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
