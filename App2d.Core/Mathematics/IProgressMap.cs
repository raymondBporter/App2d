namespace App2d.Core.Mathematics;

/// <summary>Maps normalized cycle phase to normalized path progress.</summary>
public interface IProgressMap
{
    double Sample(double phase);
    double Derivative(double phase);
}
