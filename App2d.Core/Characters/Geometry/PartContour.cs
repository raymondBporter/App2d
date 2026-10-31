using App2d.Core.Mathematics.Transforms;
using System.Collections;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>A drawable contour with real spatial positions and a separate drawing-order attribute.</summary>
public sealed class PartContour : IReadOnlyList<Vector3>
{
    private readonly IReadOnlyList<Vector3> _positions;
    private readonly IReadOnlyList<float>? _layers;
    private readonly float _layer;

    public PartContour(IReadOnlyList<Vector3> positions, float layer = 0, IReadOnlyList<float>? layers = null)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (layers is not null && layers.Count != positions.Count)
            throw new ArgumentException("Provide one layer per contour position.", nameof(layers));
        _positions = positions;
        _layer = layer;
        _layers = layers;
    }

    public int Count => _positions.Count;
    public Vector3 this[int index] => _positions[index];
    public float LayerAt(int index) => _layer + (_layers?[index] ?? 0);

    /// <summary>Transforms physical positions. Drawing layers are carried unchanged.</summary>
    public PartContour TransformedBy(Frame3D frame, float physicalZ = 0) => new(
        [.. _positions.Select(p => frame.TransformPoint(p + Vector3.UnitZ * physicalZ))], _layer, _layers);

    public IEnumerator<Vector3> GetEnumerator() => _positions.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
