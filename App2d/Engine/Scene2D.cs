using System.Collections;

namespace App2d.Engine;

public sealed class Scene2D : IEnumerable<WorldObject2D>
{
    private readonly List<WorldObject2D> _objects = [];
    private List<WorldObject2D>? _drawOrder;

    public void Add(WorldObject2D worldObject)
    {
        if (!_objects.Contains(worldObject))
            worldObject.ZIndexChanged += InvalidateDrawOrder;

        _objects.Add(worldObject);
        InvalidateDrawOrder();
    }

    public bool Remove(WorldObject2D worldObject)
    {
        if (!_objects.Remove(worldObject))
            return false;

        if (!_objects.Contains(worldObject))
            worldObject.ZIndexChanged -= InvalidateDrawOrder;

        InvalidateDrawOrder();
        return true;
    }

    internal IReadOnlyList<WorldObject2D> GetDrawOrder() => _drawOrder ??= [.. _objects.OrderBy(worldObject => worldObject.ZIndex)];

    public List<WorldObject2D>.Enumerator GetEnumerator() => _objects.GetEnumerator();
    IEnumerator<WorldObject2D> IEnumerable<WorldObject2D>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void InvalidateDrawOrder() => _drawOrder = null;
}
