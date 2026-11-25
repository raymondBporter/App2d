namespace App2d.Core.Geometry.Curves;

/// <summary>
/// A curve made of exact polynomial pieces. Distance and ray queries are written once against the pieces, so a
/// curve that implements this needs no case of its own in either.
/// </summary>
public interface IPolynomialCurve2D
{
    /// <summary>The curve's pieces in parameter order. One call per query; nothing is allocated.</summary>
    PolynomialSpans2D GetPolynomialSpans();
}

/// <summary>Where one piece sits in the curve's parameter, and how many Bézier controls it has.</summary>
public readonly record struct PolynomialSpanRange(double From, double To, int ControlCount);

/// <summary>
/// The Bézier pieces of a polynomial curve, in double precision. A single-piece curve carries its controls inline;
/// a polyline or a prepared curve refers to storage the curve already owns.
/// </summary>
public readonly struct PolynomialSpans2D
{
    private readonly PolynomialCurveSpan2D[]? _prepared;
    private readonly Vector2f[]? _polyline;
    private readonly Vector2d _first;
    private readonly Vector2d _second;
    private readonly Vector2d _third;
    private readonly Vector2d _fourth;

    internal PolynomialSpans2D(Vector2f first, Vector2f second) : this(1, first, second, default, default) { }
    internal PolynomialSpans2D(Vector2f first, Vector2f second, Vector2f third) : this(2, first, second, third, default) { }
    internal PolynomialSpans2D(Vector2f first, Vector2f second, Vector2f third, Vector2f fourth) : this(3, first, second, third, fourth) { }

    private PolynomialSpans2D(int degree, Vector2f first, Vector2f second, Vector2f third, Vector2f fourth)
    {
        Count = 1;
        MaximumDegree = degree;
        _first = first.ToVector2d();
        _second = second.ToVector2d();
        _third = third.ToVector2d();
        _fourth = fourth.ToVector2d();
    }

    /// <summary>Straight pieces between consecutive points, uniformly spaced in the parameter.</summary>
    internal PolynomialSpans2D(Vector2f[] polyline)
    {
        _polyline = polyline;
        Count = polyline.Length - 1;
        MaximumDegree = 1;
    }

    internal PolynomialSpans2D(PolynomialCurveSpan2D[] prepared, int maximumDegree)
    {
        _prepared = prepared;
        Count = prepared.Length;
        MaximumDegree = maximumDegree;
    }

    public int Count { get; }

    /// <summary>The highest degree of any piece; storage of this plus one controls fits every piece.</summary>
    public int MaximumDegree { get; }

    /// <summary>Copies one piece's Bézier controls into caller storage and returns its parameter interval.</summary>
    public PolynomialSpanRange Get(int index, Span<Vector2d> controls)
    {
        if (_prepared is { } prepared)
        {
            var span = prepared[index];
            span.Controls.CopyTo(controls);
            return new(span.From, span.To, span.Controls.Length);
        }
        if (_polyline is { } points)
        {
            controls[0] = points[index].ToVector2d();
            controls[1] = points[index + 1].ToVector2d();
            return new(index / (double)Count, (index + 1) / (double)Count, 2);
        }
        if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        controls[0] = _first;
        controls[1] = _second;
        if (MaximumDegree >= 2) controls[2] = _third;
        if (MaximumDegree >= 3) controls[3] = _fourth;
        return new(0, 1, MaximumDegree + 1);
    }
}
