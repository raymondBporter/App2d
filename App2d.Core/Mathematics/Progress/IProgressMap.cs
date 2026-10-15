namespace App2d.Core.Mathematics.Progress;

/// <summary>
/// Maps normalized phase to interpolation or path progress, with a derivative for velocity.
/// Implementations choose extrapolation and overshoot behavior; consumers enforce any required range.
/// Derivatives can be infinite at vertical tangents or NaN at discontinuities. Maps document corner conventions.
/// </summary>
public interface IProgressMap
{
    double Sample(double phase);
    double Derivative(double phase);
}
