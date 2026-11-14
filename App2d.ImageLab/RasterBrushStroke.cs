using App2d.Core.Imaging;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.ImageLab;

internal enum CanvasTool { Inspect, Paint, Line, Eyedropper, Erase }

// A whole drag owns one scratch mask and becomes at most one history entry.
internal sealed class RasterBrushStroke : IRasterStroke
{
    private Vector2f? _previous;
    private readonly bool _foreground;
    public float Radius { get; }
    public RasterFrame Frame { get; }
    public bool Changed { get; private set; }
    public string Description => $"{(_foreground ? "Paint" : "Erase")} brush · {Radius:0} px radius";

    public RasterBrushStroke(Image2D<bool> source, float radius, bool foreground)
    {
        Radius = Ensure.Positive(radius);
        _foreground = foreground;
        Frame = new(source.Clone(), "Manually cleaned mask");
    }

    public bool MoveTo(Vector2f point)
    {
        Ensure.Finite(point);
        var from = _previous ?? point;
        _previous = point;
        var mask = Frame.Mask!;
        var min = Vector2f.Max(Vector2f.Zero, Vector2f.Min(from, point) - new Vector2f(Radius));
        var max = Vector2f.Min(new(mask.Width - 1, mask.Height - 1), Vector2f.Max(from, point) + new Vector2f(Radius));
        var delta = point - from;
        var lengthSquared = delta.LengthSquared();
        var changed = false;
        for (var y = (int)MathF.Ceiling(min.Y - .5f); y <= MathF.Floor(max.Y); y++)
        {
            for (var x = (int)MathF.Ceiling(min.X - .5f); x <= MathF.Floor(max.X); x++)
            {
                var center = new Vector2f(x + .5f, y + .5f);
                var t = lengthSquared == 0 ? 0 : Math.Clamp(Vector2f.Dot(center - from, delta) / lengthSquared, 0, 1);
                if (Vector2f.DistanceSquared(center, from + t * delta) <= Radius * Radius && mask[x, y] != _foreground)
                {
                    mask[x, y] = _foreground;
                    changed = true;
                }
            }
        }
        Changed |= changed;
        return changed;
    }
}
