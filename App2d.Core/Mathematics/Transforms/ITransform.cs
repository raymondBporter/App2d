namespace App2d.Core.Mathematics.Transforms;

/// <summary>
/// Maps points and directions of one vector type. Directions ignore translation. Generic code takes the
/// concrete transform as a type argument, so calls are resolved per struct with no boxing.
/// </summary>
public interface ITransform<TPoint>
{
    TPoint TransformPoint(TPoint local);
    TPoint TransformDirection(TPoint local);
}
