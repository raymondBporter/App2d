namespace App2d.Core.Mathematics;

/// <summary>A normalized scalar easing. Its derivative supports velocity along a curve.</summary>
public sealed class Easing : IProgressMap
{
    private readonly Func<double, double> _easingFunction;
    private readonly Func<double, double>? _derivativeFunction;

    private Easing(Func<double, double> easingFunction, Func<double, double>? derivativeFunction = null)
    {
        _easingFunction = easingFunction;
        _derivativeFunction = derivativeFunction;
    }

    public double Sample(double time) => _easingFunction(time);
    public double Derivative(double time)
    {
        if (_derivativeFunction is { } derivative)
            return derivative(time);

        // Custom and legacy easings use a bounded numerical derivative.
        const double step = 0.00001d;
        var lower = Math.Max(0d, time - step);
        var upper = Math.Min(1d, time + step);
        return upper == lower ? 0d : (Sample(upper) - Sample(lower)) / (upper - lower);
    }
    public double SampleOverTime(double currentTime, double maxTime) => Sample(currentTime / maxTime);
    public static implicit operator Easing(Func<double, double> easingFunction) => new(easingFunction);
    public static implicit operator Func<double, double>(Easing easingFunction) => easingFunction._easingFunction;
    public static readonly Easing Linear = new(x => x, _ => 1d);
    public static readonly Easing Smooth = new(x => x * x * (3d - 2d * x), x => 6d * x * (1d - x));
    public static readonly Easing EaseInQuad = new(x => x * x);
    public static readonly Easing EaseOutQuad = new(x => 1 - (1 - x) * (1 - x));
    public static readonly Easing EaseInOutQuad = new(x => x < 0.5 ? 2 * x * x : 1 - Math.Pow(-2 * x + 2, 2) / 2);
    public static readonly Easing EaseInCubic = new(x => x * x * x);
    public static readonly Easing EaseOutCubic = new(x => 1 - Math.Pow(1 - x, 3));
    public static readonly Easing EaseInOutCubic = new(x => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2);
    public static readonly Easing EaseInQuart = new(x => x * x * x * x);
    public static readonly Easing EaseOutQuart = new(x => 1 - Math.Pow(1 - x, 4));
    public static readonly Easing EaseInOutQuart = new(x => x < 0.5 ? 8 * x * x * x * x : 1 - Math.Pow(-2 * x + 2, 4) / 2);
    public static readonly Easing EaseInQuint = new(x => x * x * x * x * x);
    public static readonly Easing EaseOutQuint = new(x => 1 - Math.Pow(1 - x, 5));
    public static readonly Easing EaseInOutQuint = new(x => x < 0.5 ? 16 * x * x * x * x * x : 1 - Math.Pow(-2 * x + 2, 5) / 2);
    public static readonly Easing EaseInSine = new(x => 1 - Math.Cos((x * Math.PI) / 2));
    public static readonly Easing EaseOutSine = new(x => Math.Sin((x * Math.PI) / 2));
    public static readonly Easing EaseInOutSine = new(
        x => -(Math.Cos(Math.PI * x) - 1) / 2,
        x => Math.PI * Math.Sin(Math.PI * x) / 2);
    public static readonly Easing EaseInExpo = new(x => x == 0 ? 0 : Math.Pow(2, 10 * x - 10));
    public static readonly Easing EaseOutExpo = new(x => x == 1 ? 1 : 1 - Math.Pow(2, -10 * x));
    public static readonly Easing EaseInOutExpo = new(x => x == 0 ? 0 : x == 1 ? 1 : x < 0.5 ? Math.Pow(2, 20 * x - 10) / 2 : (2 - Math.Pow(2, -20 * x + 10)) / 2);
    public static readonly Easing EaseInCirc = new(x => 1 - Math.Sqrt(1 - Math.Pow(x, 2)));
    public static readonly Easing EaseOutCirc = new(x => Math.Sqrt(1 - Math.Pow(x - 1, 2)));
    public static readonly Easing EaseInOutCirc = new(x => x < 0.5 ? (1 - Math.Sqrt(1 - Math.Pow(2 * x, 2))) / 2 : (Math.Sqrt(1 - Math.Pow(-2 * x + 2, 2)) + 1) / 2);

    private const double c1 = 1.70158;
    private const double c2 = c1 * 1.525;
    private const double c3 = c1 + 1;
    private const double c4 = (2 * Math.PI) / 3;
    private const double c5 = (2 * Math.PI) / 4.5;

    public static readonly Easing EaseInBack = new(x => c3 * x * x * x - c1 * x * x);
    public static readonly Easing EaseOutBack = new(x => 1 + c3 * Math.Pow(x - 1, 3) + c1 * Math.Pow(x - 1, 2));
    public static readonly Easing EaseInOutBack = new(x => x < 0.5 ? (Math.Pow(2 * x, 2) * ((c2 + 1) * 2 * x - c2)) / 2 : (Math.Pow(2 * x - 2, 2) * ((c2 + 1) * (x * 2 - 2) + c2) + 2) / 2);
    public static readonly Easing EaseInElastic = new(x => x == 0 ? 0 : x == 1 ? 1 : -Math.Pow(2, 10 * x - 10) * Math.Sin((x * 10 - 10.75) * c4));
    public static readonly Easing EaseOutElastic = new(x => x == 0 ? 0 : x == 1 ? 1 : Math.Pow(2, -10 * x) * Math.Sin((x * 10 - 0.75) * c4) + 1);
    public static readonly Easing EaseInOutElastic = new(x => x == 0 ? 0 : x == 1 ? 1 : x < 0.5 ? -(Math.Pow(2, 20 * x - 10) * Math.Sin((20 * x - 11.125) * c5)) / 2 : (Math.Pow(2, -20 * x + 10) * Math.Sin((20 * x - 11.125) * c5)) / 2 + 1);

    private static double BounceOut(double x)
    {
        const double n1 = 7.5625;
        const double d1 = 2.75;

        if (x < 1 / d1)
        {
            return n1 * x * x;
        }
        else if (x < 2 / d1)
        {
            x -= 1.5 / d1;
            return n1 * x * x + 0.75;
        }
        else if (x < 2.5 / d1)
        {
            x -= 2.25 / d1;
            return n1 * x * x + 0.9375;
        }
        else
        {
            x -= 2.625 / d1;
            return n1 * x * x + 0.984375;
        }
    }

    public static readonly Easing EaseInBounce = new(x => 1 - BounceOut(1 - x));
    public static readonly Easing EaseOutBounce = new(x => BounceOut(x));
    public static readonly Easing EaseInOutBounce = new(x => x < 0.5 ? (1 - BounceOut(1 - 2 * x)) / 2 : (1 + BounceOut(2 * x - 1)) / 2);
}
