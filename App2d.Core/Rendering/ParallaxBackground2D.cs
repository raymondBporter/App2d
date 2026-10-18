using App2d.Core.Validation;
using System.Collections;

namespace App2d.Core.Rendering;

/// <summary>Any number of background layers, drawn from greatest depth to smallest depth.</summary>
public sealed class ParallaxBackground2D : IEnumerable<ParallaxLayer2D>
{
    private readonly List<ParallaxLayer2D> _layers = [];

    public void Add(ParallaxLayer2D layer) => _layers.Add(ArgGuard.RequireNotNull(layer));
    public bool Remove(ParallaxLayer2D layer) => _layers.Remove(layer);

    internal IOrderedEnumerable<ParallaxLayer2D> GetDrawOrder() => _layers.OrderByDescending(layer => layer.Depth);

    public List<ParallaxLayer2D>.Enumerator GetEnumerator() => _layers.GetEnumerator();
    IEnumerator<ParallaxLayer2D> IEnumerable<ParallaxLayer2D>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
