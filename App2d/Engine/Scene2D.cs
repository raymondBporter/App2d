using System.Collections;

namespace App2d.Engine;

public sealed class Scene2D : IEnumerable<WorldObject2D>
{
    private readonly List<WorldObject2D> _objects = [];

    public void Add(WorldObject2D worldObject) => _objects.Add(worldObject);
    public bool Remove(WorldObject2D worldObject) => _objects.Remove(worldObject);
    public List<WorldObject2D>.Enumerator GetEnumerator() => _objects.GetEnumerator();
    IEnumerator<WorldObject2D> IEnumerable<WorldObject2D>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
