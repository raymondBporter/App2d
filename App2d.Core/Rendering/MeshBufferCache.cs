using Microsoft.Xna.Framework.Graphics;

namespace App2d.Core.Rendering;

/// <summary>Renderer-owned buffers, bounded by resident bytes and evicted in least-recently-used order.</summary>
internal sealed class MeshBufferCache<TKey> : IDisposable where TKey : notnull
{
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _entries = [];
    private readonly LinkedList<Entry> _recent = [];
    public long Bytes { get; private set; }

    public bool TryGet(TKey key, out MeshBuffers buffers)
    {
        if (!_entries.TryGetValue(key, out var node))
        {
            buffers = null!;
            return false;
        }

        _recent.Remove(node);
        _recent.AddLast(node);
        buffers = node.Value.Buffers;
        return true;
    }

    public void Add(TKey key, MeshBuffers buffers)
    {
        if (_entries.Remove(key, out var previous))
        {
            Release(previous);
        }

        _entries.Add(key, _recent.AddLast(new Entry(key, buffers)));
        Bytes += buffers.Bytes;
    }

    // Call only after submission and unbinding, including for a mesh larger than the budget.
    public void Trim(long budget)
    {
        while (Bytes > budget && _recent.First is { } oldest)
        {
            _entries.Remove(oldest.Value.Key);
            Release(oldest);
        }
    }

    private void Release(LinkedListNode<Entry> node)
    {
        _recent.Remove(node);
        Bytes -= node.Value.Buffers.Bytes;
        node.Value.Buffers.Dispose();
    }

    public void Dispose() => Trim(0);

    private sealed record Entry(TKey Key, MeshBuffers Buffers);
}

internal sealed class MeshBuffers(VertexBuffer vertices, IndexBuffer? indices = null) : IDisposable
{
    public VertexBuffer Vertices { get; } = vertices;
    public IndexBuffer? Indices { get; } = indices;
    public long Version { get; set; }
    public long Bytes { get; } = (long)vertices.VertexCount * vertices.VertexDeclaration.VertexStride
        + (indices is null ? 0 : (long)indices.IndexCount * (indices.IndexElementSize == IndexElementSize.SixteenBits ? 2 : 4));

    public void Dispose()
    {
        Vertices.Dispose();
        Indices?.Dispose();
    }
}
