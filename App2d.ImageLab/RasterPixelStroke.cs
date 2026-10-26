using System.Numerics;

namespace App2d.ImageLab;

internal interface IRasterStroke
{
    RasterFrame Frame { get; }
    bool Changed { get; }
    string Description { get; }
    bool MoveTo(Vector2 point);
}

// Pixel brush and drag-to-preview line. A line restores only its old pixels as its endpoint moves.
internal sealed class RasterPixelStroke : IRasterStroke
{
    private readonly RasterFrame _source;
    private readonly RasterInk _ink;
    private readonly bool _line;
    private readonly List<int> _linePixels = [];
    private Point? _start;
    private Point? _previous;
    public RasterFrame Frame { get; }
    public bool Changed { get; private set; }
    public string Description => _line ? $"Line · ({_start?.X}, {_start?.Y}) to ({_previous?.X}, {_previous?.Y}) · {_ink.Description(Frame)}"
        : $"Pixel brush · {_ink.Description(Frame)}";

    public RasterPixelStroke(RasterFrame source, RasterInk ink, bool line = false)
    {
        if (source.Width == 0 || source.Height == 0)
        {
            throw new ArgumentException("Cannot edit an empty image.", nameof(source));
        }
        _source = source;
        _ink = ink;
        _line = line;
        Frame = source.Clone("Manual pixel repair");
    }

    public bool MoveTo(Vector2 point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }
        if (!_line && (point.X < 0 || point.Y < 0 || point.X >= Frame.Width || point.Y >= Frame.Height))
        {
            _previous = null;
            return false;
        }
        var pixel = new Point((int)Math.Clamp(MathF.Floor(point.X), 0, Frame.Width - 1),
            (int)Math.Clamp(MathF.Floor(point.Y), 0, Frame.Height - 1));
        if (_previous == pixel)
        {
            return false;
        }
        var dirty = false;
        if (_line)
        {
            foreach (var index in _linePixels)
            {
                dirty |= CopyOriginal(index);
            }
            _linePixels.Clear();
            Changed = false;
        }
        _start ??= pixel;
        var from = _line ? _start.Value : _previous ?? pixel;
        _previous = pixel;
        // Bresenham: endpoints included, one pixel wide, eight-connected for all octants.
        var x = from.X;
        var y = from.Y;
        var dx = Math.Abs(pixel.X - x);
        var dy = -Math.Abs(pixel.Y - y);
        var sx = x < pixel.X ? 1 : -1;
        var sy = y < pixel.Y ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            var index = y * Frame.Width + x;
            if (_line)
            {
                _linePixels.Add(index);
            }
            var changed = Write(index, _ink);
            dirty |= changed;
            Changed |= changed;
            if (x == pixel.X && y == pixel.Y)
            {
                break;
            }
            var twice = 2 * error;
            if (twice >= dy)
            {
                error += dy;
                x += sx;
            }
            if (twice <= dx)
            {
                error += dx;
                y += sy;
            }
        }
        return dirty;
    }

    private bool CopyOriginal(int index) => Write(index, RasterInk.SampleIndex(_source, index));

    private bool Write(int index, RasterInk ink)
    {
        if (Frame.Colors is not null)
        {
            ref var value = ref Frame.Colors.AsSpan()[index];
            var changed = value != ink.Color;
            value = ink.Color;
            return changed;
        }
        if (Frame.Channel is not null)
        {
            ref var value = ref Frame.Channel.AsSpan()[index];
            var changed = value != ink.Scalar;
            value = ink.Scalar;
            return changed;
        }
        ref var foreground = ref Frame.Mask!.AsSpan()[index];
        var next = ink.Scalar >= .5f;
        var maskChanged = foreground != next;
        foreground = next;
        return maskChanged;
    }
}
