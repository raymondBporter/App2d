using App2d.Core.Geometry.Primitives;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Construction;

/// <summary>The one-parameter family of conics through four fixed points.</summary>
public sealed class ConicPencil2D
{
    private readonly Vector2d _a;
    private readonly Vector2d _b;
    private readonly Vector2d _c;
    private readonly Vector2d _d;

    /// <summary>
    /// Constructs the family from four distinct finite points, with no three collinear.
    /// The input order determines which lines form the two basis conics.
    /// </summary>
    public ConicPencil2D(Vector2f a, Vector2f b, Vector2f c, Vector2f d)
    {
        Ensure.Finite(a);
        Ensure.Finite(b);
        Ensure.Finite(c);
        Ensure.Finite(d);
        _a = a.ToVector2d();
        _b = b.ToVector2d();
        _c = c.ToVector2d();
        _d = d.ToVector2d();
        Ensure.ThrowIf(_a.Orientation(_b, _c) == 0d || _a.Orientation(_b, _d) == 0d
            || _a.Orientation(_c, _d) == 0d || _b.Orientation(_c, _d) == 0d,
            "The four points must be distinct, with no three collinear.");

        // Express the equations relative to A so large world translations stay out of the coefficients.
        var ab = LineThrough(_a, _b, _a);
        var bc = LineThrough(_b, _c, _a);
        var cd = LineThrough(_c, _d, _a);
        var da = LineThrough(_d, _a, _a);
        First = MultiplyLines(a, ab, cd);
        Second = MultiplyLines(a, bc, da);
    }

    /// <summary>The pair of lines AB and CD.</summary>
    public Conic2D First { get; }

    /// <summary>The pair of lines BC and DA.</summary>
    public Conic2D Second { get; }

    /// <summary>
    /// Sweeps the full family over one turn of the knob. Zero gives <see cref="First"/>,
    /// one half gives <see cref="Second"/>, and one wraps to zero.
    /// </summary>
    public Conic2D At(double knob)
    {
        Ensure.Finite(knob);
        var (secondWeight, firstWeight) = double.SinCosPi(knob % 1d);
        return AtWeights(firstWeight, secondWeight);
    }

    /// <summary>Chooses a member by the ratio of two finite weights, at least one nonzero.</summary>
    public Conic2D AtWeights(double firstWeight, double secondWeight)
    {
        Ensure.Finite(firstWeight);
        Ensure.Finite(secondWeight);
        var scale = Math.Max(Math.Abs(firstWeight), Math.Abs(secondWeight));
        Ensure.ThrowIf(scale == 0d, "At least one conic weight must be nonzero.");

        // The ratio determines the curve, so scaling keeps large weights from overflowing the coefficients.
        firstWeight /= scale;
        secondWeight /= scale;
        return new(First.Origin,
            firstWeight * First.XX + secondWeight * Second.XX,
            firstWeight * First.XY + secondWeight * Second.XY,
            firstWeight * First.YY + secondWeight * Second.YY,
            firstWeight * First.Linear + secondWeight * Second.Linear,
            firstWeight * First.Constant + secondWeight * Second.Constant);
    }

    /// <summary>Selects the member that also passes through a fifth distinct point.</summary>
    public Conic2D ThroughPoint(Vector2f point)
    {
        Ensure.Finite(point);
        var q = point.ToVector2d();
        // Use the factored equations to choose weights directly from the orientation tests.
        var first = _a.Orientation(_b, q) * _c.Orientation(_d, q);
        var second = _b.Orientation(_c, q) * _d.Orientation(_a, q);
        Ensure.ThrowIf(first == 0d && second == 0d, "The fifth point does not select a unique conic.", nameof(point));
        return AtWeights(second, -first);
    }

    private static (Vector2d Normal, double Constant) LineThrough(Vector2d start, Vector2d end, Vector2d origin)
    {
        var normal = (end - start).PerpCcw;
        return (normal, -Vector2d.Dot(normal, start - origin));
    }

    private static Conic2D MultiplyLines(Vector2f origin,
        (Vector2d Normal, double Constant) first, (Vector2d Normal, double Constant) second) =>
        new(origin,
            first.Normal.X * second.Normal.X,
            first.Normal.X * second.Normal.Y + first.Normal.Y * second.Normal.X,
            first.Normal.Y * second.Normal.Y,
            first.Normal * second.Constant + second.Normal * first.Constant,
            first.Constant * second.Constant);
}
