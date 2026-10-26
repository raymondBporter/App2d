using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
using App2d.Rendering;
using System.Numerics;

namespace App2d.Beamwright;

public sealed partial class BeamwrightGame2D
{
    private static readonly Color4b Amber = new(255, 194, 105);
    private static readonly Color4b Ink = new(221, 228, 235);
    private static readonly Color4b Dim = new(143, 158, 172);
    private static readonly Color4b Green = new(100, 230, 167);

    public override void Render(Renderer2D renderer)
    {
        Layout();
        var width = Camera.ViewportSize.X;
        var height = Camera.ViewportSize.Y;
        renderer.DrawScreenRoundedRectangle(new(_sidebarWidth + 14, 107, width - 14, height - 100), 14, new(29, 35, 42));
        renderer.DrawScreenRoundedRectangle(new(_left - 12, _top - 12, _left + Session.Level.W * _cell + 12, _top + Session.Level.H * _cell + 12), 8, new(33, 40, 47));
        DrawBoard(renderer);
        DrawLight(renderer);
        foreach (var placement in Session.Level.Fixed) DrawPart(renderer, placement, fixedPart: true);
        for (var i = 0; i < Session.Placed.Count; i++) DrawPart(renderer, Session.Placed[i], selected: i == _selected);
        foreach (var source in Session.Level.Sources) DrawSource(renderer, source);
        for (var i = 0; i < Session.Level.Targets.Length; i++) DrawTarget(renderer, i);
        DrawGhosts(renderer);
    }

    private Vector2 World(Vector2 point) => Vector2.Transform(point, _benchToWorld);
    private void Line(Renderer2D renderer, Vector2 start, Vector2 end, Color4b color, float stroke = 1) =>
        renderer.DrawWorldSegment(World(start), World(end), color, stroke);

    private void DrawBoard(Renderer2D renderer)
    {
        var level = Session.Level;
        for (var x = 0; x <= level.W; x++)
        {
            Line(renderer, new(x, 0), new(x, level.H), new(47, 55, 64));
        }
        for (var y = 0; y <= level.H; y++)
        {
            Line(renderer, new(0, y), new(level.W, y), new(47, 55, 64));
        }
        for (var x = 0; x < level.W; x++)
        {
            for (var y = 0; y < level.H; y++)
            {
                renderer.DrawWorldCircle(World(new(x + .5f, y + .5f)), Math.Max(1.5f, _cell * .027f), new(17, 23, 29), 3);
            }
        }
        for (var x = 0; x < level.W; x++)
        {
            Text(renderer, (x + 1).ToString(), _left + (x + .5f) * _cell - 5, _top - 20, Dim);
        }
        for (var y = 0; y < level.H; y++)
        {
            Text(renderer, ((char)('A' + y)).ToString(), _left - 27, _top + (y + .5f) * _cell + 5, Dim);
        }
        foreach (var wall in level.Walls)
        {
            var x = _left + wall[0] * _cell + 3;
            var y = _top + wall[1] * _cell + 3;
            renderer.DrawScreenRoundedRectangle(new(x, y, x + _cell - 6, y + _cell - 6), 4, new(67, 76, 85));
            Line(renderer, new(wall[0] + .18f, wall[1] + .2f), new(wall[0] + .82f, wall[1] + .8f), new(91, 100, 109), 2);
        }
        foreach (var gate in level.Gates)
        {
            var x = gate.X + .5f;
            Line(renderer, new(x, 0), new(x, gate.Y + .5f - gate.Gap), new(107, 117, 127), Math.Max(3, _cell * .08f));
            Line(renderer, new(x, gate.Y + .5f + gate.Gap), new(x, level.H), new(107, 117, 127), Math.Max(3, _cell * .08f));
        }
        foreach (var zone in level.Mirage)
        {
            var origin = new Vector2(_left, _top) + zone.Cell.ToVector2() * _cell;
            var size = new Size2D(zone.W * _cell, zone.H * _cell);
            renderer.DrawScreenRoundedRectangle(ScreenRectangle2D.FromTopLeftAndSize(origin, size), 2, Fade(new(255, 135, 74), .07f));
            var bottom = zone.Cell.ToVector2() + new Vector2(0, zone.H - .06f);
            Line(renderer, bottom, bottom + new Vector2(zone.W, 0), new(186, 98, 64), _cell * .12f);
            Text(renderer, "HOT AIR", origin.X + 8, origin.Y + 21, new(187, 125, 90));
        }
    }

    private void DrawLight(Renderer2D renderer)
    {
        foreach (var segment in Session.Result.Segments)
        {
            var color = Spectrum(segment.Color);
            var power = Math.Clamp((float)Math.Sqrt(segment.Power * 5), .09f, 1);
            Line(renderer, segment.Start, segment.End, Fade(color, .035f * power), 15);
            Line(renderer, segment.Start, segment.End, Fade(color, .12f * power), 6);
            Line(renderer, segment.Start, segment.End, Fade(color, .76f * power), 1.8f);
            var pulse = (_time * 3.5 - segment.Distance) % 16;
            var length = segment.EndDistance - segment.Distance;
            if (pulse >= 0 && pulse < length && length > .001)
            {
                var position = Vector2.Lerp(segment.Start, segment.End, (float)(pulse / length));
                renderer.DrawWorldCircle(World(position), 1.2f, Fade(color, .8f * power), 2);
            }
        }
    }

    private void DrawPart(Renderer2D renderer, PartPlacement placement, bool selected = false, bool fixedPart = false, float opacity = 1)
    {
        var definition = Session.Content.Part(placement.Type);
        var frame = Frame2D.FromRotationDegrees(placement.Angle, placement.Center);
        var matrix = frame.Matrix * _benchToWorld;
        var color = placement.Type switch
        {
            "mirror" or "cmirror" or "xmirror" => new Color4b(210, 223, 236),
            "prism" or "diamond" => new Color4b(188, 159, 240),
            "fiber" or "bend" => new Color4b(114, 220, 177),
            "droplet" => new Color4b(111, 180, 243),
            _ => new Color4b(121, 199, 230)
        };
        if (selected) renderer.DrawWorldCircle(World(placement.Center), _cell * .47f, Amber, 2);
        if (definition.Geometry is IShape2D geometry)
        {
            renderer.DrawShape(geometry, matrix, Fade(color, .18f * opacity), Fade(color, opacity), 2);
            if (definition.Mirror)
            {
                var back = frame.TransformPoint(new(definition.Radius, 0));
                renderer.DrawWorldCircle(World(back), 3, Fade(Amber, opacity), 3);
            }
        }
        else if (definition.Geometry is LineSegmentCurve2D surface)
        {
            var start = frame.TransformPoint(surface.Start);
            var end = frame.TransformPoint(surface.End);
            Line(renderer, start, end, Fade(color, .13f * opacity), 10);
            Line(renderer, start, end, Fade(color, opacity), placement.Type == "splitter" ? 2 : 4);
            var normal = frame.YAxis;
            if (definition.Act == "cmirror")
            {
                var center = placement.Center;
                Line(renderer, center, center + normal * .19f, Fade(Amber, opacity), 2);
            }
            if (definition.Act == "lens")
            {
                var tangent = frame.XAxis;
                var sign = definition.FocalLength > 0 ? 1 : -1;
                foreach (var point in new[] { start, end })
                {
                    var inward = point == start ? tangent : -tangent;
                    Line(renderer, point, point + inward * .12f * sign + normal * .08f, Fade(color, opacity), 2);
                    Line(renderer, point, point + inward * .12f * sign - normal * .08f, Fade(color, opacity), 2);
                }
            }
        }
        renderer.DrawWorldCircle(World(placement.Center), 2.2f, Fade(fixedPart ? Dim : Amber, opacity), 3);
        if (fixedPart) Text(renderer, "FIX", _left + placement.Center.X * _cell - 12, _top + (placement.Center.Y + .44f) * _cell, Dim);
    }

    private void DrawSource(Renderer2D renderer, LightSource source)
    {
        var center = source.Center;
        var circle = Circle2D.Builder.Create(.29f);
        var matrix = Matrix3x2.CreateTranslation(center) * _benchToWorld;
        renderer.DrawShape(circle, matrix, new(40, 43, 45), Spectrum(source.Color), 3);
        Line(renderer, center, center + Vector2.FromAngleDegrees(source.Angle) * .33f, Spectrum(source.Color), 5);
    }

    private void DrawTarget(Renderer2D renderer, int index)
    {
        var target = Session.Level.Targets[index];
        var lit = Session.Result.Lit[index];
        var color = lit ? Green : target.Color == "any" ? Amber : Spectrum(target.Color);
        if (target.Profile)
        {
            var tangent = Vector2.FromAngleDegrees(target.Angle + 90);
            for (var bin = 0; bin < target.Bins; bin++)
            {
                var a = -target.Half + bin * (2 * target.Half / target.Bins);
                var b = a + 2 * target.Half / target.Bins - .015f;
                var filled = Session.Result.Profiles[index]!.Bins[bin] >= target.BinNeed - 1e-6;
                Line(renderer, target.Center + tangent * a, target.Center + tangent * b, filled ? Green : Fade(color, .35f), 7);
            }
        }
        else
        {
            renderer.DrawShape(Circle2D.Builder.Create(target.Radius), Matrix3x2.CreateTranslation(target.Center) * _benchToWorld,
                Fade(color, lit ? .24f : .07f), color, 2);
            renderer.DrawWorldCircle(World(target.Center), Math.Max(2, _cell * target.Radius * .55f), Fade(color, lit ? 1 : .35f), 2);
        }
        var text = $"{Session.Result.Lumens[index]:0.#}/{target.Need:0.##}";
        Text(renderer, text, _left + target.Center.X * _cell - text.Length * 3.5f,
            _top + (target.Center.Y + Math.Max(target.Radius, .3f)) * _cell + 16, color);
    }

    private void DrawGhosts(Renderer2D renderer)
    {
        foreach (var placement in Session.Level.Solution.Take(Session.HintCount))
            if (!Session.Placed.Contains(placement)) DrawPart(renderer, placement, opacity: .32f);
        var cell = PointerCell();
        if (_showLevels || cell.X < 0 || cell.X >= Session.Level.W || cell.Y < 0 || cell.Y >= Session.Level.H) return;
        if (_dragging && _dragIndex >= 0 && _dragIndex < Session.Placed.Count)
        {
            if (Session.CanOccupy(cell.X, cell.Y, _dragIndex))
                DrawPart(renderer, Session.Placed[_dragIndex] with { X = cell.X, Y = cell.Y }, opacity: .45f);
        }
        else if (_armed is { } type && Session.CanOccupy(cell.X, cell.Y))
        {
            DrawPart(renderer, new() { Type = type, X = cell.X, Y = cell.Y, Angle = Session.Content.Part(type).DefaultAngle }, opacity: .4f);
        }
    }

    public override void RenderUI(Renderer2D renderer, App2d.Core.FrameTime time)
    {
        var width = Camera.ViewportSize.X;
        var height = Camera.ViewportSize.Y;
        renderer.DrawScreenRoundedRectangle(new(0, 0, _sidebarWidth, height), 0, new(23, 28, 34));
        Text(renderer, "B E A M W R I G H T", 22, 40, Amber);
        Text(renderer, "OPTICAL BENCH", 22, 65, Dim);
        Text(renderer, Session.Level.Sandbox ? "LAB / UNLIMITED PARTS" : $"BENCH {Session.LevelIndex + 1:00} / CHAPTER {Session.Level.Chapter}", _sidebarWidth + 30, 37, Dim);
        Text(renderer, Session.Level.Name, _sidebarWidth + 30, 69, Ink, .85f);
        Text(renderer, $"{Session.Placed.Count} placed" + (Session.Level.Par > 0 ? $"  /  par {Session.Level.Par}" : ""), _sidebarWidth + 30, 91, Amber);
        Text(renderer, "PARTS  /  CLICK TO EQUIP", 20, 157, Dim);
        var parts = RackParts().ToArray();
        if (parts.Length > VisibleRackRows)
            Text(renderer, $"Wheel to scroll  {_rackOffset + 1}-{Math.Min(parts.Length, _rackOffset + VisibleRackRows)}/{parts.Length}", 20, _rackBottom + 5, Dim);
        if (_showLevels)
        {
            renderer.DrawScreenRoundedRectangle(new(0, 0, width, height), 0, Fade(new(5, 8, 12), .9f));
            var left = Math.Max(20, (width - 1120) / 2);
            var top = Math.Max(20, (height - 630) / 2);
            renderer.DrawScreenRoundedRectangle(new(left, top, width - left, height - top), 14, new(28, 35, 43));
            Text(renderer, "CHOOSE A BENCH", left + 24, top + 38, Amber);
            Text(renderer, $"{Enumerable.Range(0, Session.Content.Levels.Length).Count(Session.Completed)} / {Session.Content.Levels.Length} completed", left + 24, top + 62, Dim);
        }
        foreach (var button in _buttons) DrawButton(renderer, button);
        if (_showLevels) return;
        for (var row = 0; row < VisibleRackRows && row + _rackOffset < parts.Length; row++)
        {
            var part = parts[row + _rackOffset];
            var count = Session.Level.Sandbox ? "inf" : Session.Available(part.Type).ToString();
            Text(renderer, count, _sidebarWidth - 48, 202 + row * 49, Session.Available(part.Type) > 0 ? Amber : Dim);
        }
        var footerY = height - 76;
        var status = Session.Result.Win ? "ALL PHOTOCELLS LIT" : $"{Session.Result.Lit.Count(lit => lit)} / {Session.Level.Targets.Length} PHOTOCELLS LIT";
        Text(renderer, status, _sidebarWidth + 30, footerY, Session.Result.Win ? Green : Amber);
        Text(renderer, "Drag: move | Q/E or wheel: rotate | Right-click: return | Esc: exit", _sidebarWidth + 30, height - 24, Dim);
        var detail = _selected >= 0 && _selected < Session.Placed.Count
            ? $"{Session.Content.Part(Session.Placed[_selected].Type).Name}  /  {Session.Placed[_selected].Angle:0} degrees  /  Delete: return part"
            : _armed is { } armed ? Session.Content.Part(armed).Spec : Session.Level.Tip;
        renderer.DrawScreenTextWrapped(detail, new(_sidebarWidth + 30, height - 61),
            width - _sidebarWidth - (Session.Result.Win ? 260 : 55), Dim, .55f, maxLines: 2);
        if (Session.HasSaveError) Text(renderer, "Progress could not be saved", 20, height - 10, Amber);
    }

    private void DrawButton(Renderer2D renderer, BenchButton button)
    {
        var hover = button.Enabled && Contains(button.Bounds, _pointer);
        var fill = button.Active ? new Color4b(78, 61, 39) : hover ? new Color4b(54, 64, 75) : new Color4b(36, 44, 53);
        renderer.DrawScreenRoundedRectangle(button.Bounds, 5, fill);
        if (button.Active) renderer.DrawScreenRoundedRectangle(button.Bounds, 5, Amber, 1);
        var label = button.Label;
        var availableWidth = button.Bounds.Width - 25;
        if (!_showLevels && button.Bounds.Left < _sidebarWidth && button.Bounds.Top >= 176 && button.Bounds.Top < _rackBottom)
            availableWidth -= 30;
        if (renderer.MeasureScreenText(label, .55f) > availableWidth)
        {
            while (label.Length > 1 && renderer.MeasureScreenText(label + "..", .55f) > availableWidth) label = label[..^1];
            label += "..";
        }
        Text(renderer, label, button.Bounds.Left + 11, button.Bounds.MidY + 5, button.Enabled ? button.Active ? Amber : Ink : new(91, 104, 117));
        if (_showLevels && button.Label.Length > 2 && int.TryParse(button.Label.AsSpan(0, 2), out var number) && Session.Completed(number - 1))
            Text(renderer, "+", button.Bounds.Right - 22, button.Bounds.MidY + 5, Green);
    }

    private static void Text(Renderer2D renderer, string text, float x, float y, Color4b color, float scale = .55f)
    {
        if (!string.IsNullOrWhiteSpace(text)) renderer.DrawScreenText(text, new(x, y), color, scale);
    }

    private static Color4b Fade(Color4b color, float opacity) => new(color.R, color.G, color.B, (byte)(Math.Clamp(opacity, 0, 1) * 255));

    private static Color4b Spectrum(string color) => color switch
    {
        "R" => new(255, 64, 52),
        "O" => new(255, 140, 36),
        "Y" => new(255, 230, 60),
        "G" => new(60, 235, 110),
        "B" => new(60, 130, 255),
        "V" => new(170, 80, 255),
        _ => new(255, 212, 120)
    };
}
