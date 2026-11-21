using App2d.Core.Validation;
using System.Collections;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A finite arithmetic sequence: First + index * Step. Indexing is constant-time and enumeration computes values
/// on demand; no value array is stored. Floating-point rounding may make neighboring values equal.
/// </summary>
public sealed class UniformSequence<TReal> : IReadOnlyList<TReal> where TReal : IFloatingPointIeee754<TReal>
{
    public UniformSequence(TReal first, TReal step, int count)
    {
        Ensure.Finite(first);
        Ensure.Finite(step);
        Ensure.NonNegative(count);
        First = first;
        Step = step;
        Count = count;
        if (count > 0) Ensure.Finite(this[count - 1], nameof(count));
    }

    public TReal First { get; }
    public TReal Step { get; }
    public int Count { get; }

    public TReal this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            return TReal.FusedMultiplyAdd(TReal.CreateChecked(index), Step, First);
        }
    }

    public IEnumerator<TReal> GetEnumerator()
    {
        for (var index = 0; index < Count; index++) yield return this[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
