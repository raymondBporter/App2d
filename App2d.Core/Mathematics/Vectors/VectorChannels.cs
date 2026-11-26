using App2d.Core.Mathematics.Vectors;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Reusable component channels for any vector dimension. Indices must be within the vector's dimension.</summary>
public sealed class VectorChannels<TVector, TReal>
    where TVector : unmanaged, IVector<TVector, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public static VectorChannels<TVector, TReal> Instance { get; } = new();
    private readonly InterpolationChannel<TVector, TReal>[] _components;

    private VectorChannels()
    {
        _components = new InterpolationChannel<TVector, TReal>[TVector.Dimension];
        for (var axis = 0; axis < _components.Length; axis++)
        {
            var component = axis;
            _components[axis] = new(value => value[component], (value, replacement) =>
            {
                var count = TVector.Dimension;
                Span<TReal> coordinates = count <= 16 ? stackalloc TReal[count] : new TReal[count];
                for (var index = 0; index < count; index++) coordinates[index] = index == component ? replacement : value[index];
                return TVector.FromCoordinates(coordinates);
            });
        }
    }

    public InterpolationChannel<TVector, TReal> this[int axis] => _components[axis];
    public InterpolationChannel<TVector, TReal> X => this[0];
    public InterpolationChannel<TVector, TReal> Y => this[1];
    public InterpolationChannel<TVector, TReal> Z => this[2];
    public InterpolationChannel<TVector, TReal> W => this[3];
    public InterpolationChannel<TVector, TVector> V => InterpolationChannel<TVector, TVector>.Whole;
}
