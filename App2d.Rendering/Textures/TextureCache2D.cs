using App2d.Core.Validation;
using App2d.Core.IO;

namespace App2d.Rendering.Textures;

public sealed class TextureCache2D(string contentRoot) : IDisposable
{
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public string ContentRoot { get; } = Path.GetFullPath(ArgGuard.RequireNotNullOrWhiteSpace(contentRoot));
    public int Count => _textures.Count;

    public Texture2D Load(string relativePath)
    {
        ThrowIfDisposed();
        var fullPath = ResolvePath(relativePath);
        if (_textures.TryGetValue(fullPath, out var cached))
            return cached;

        var texture = Texture2D.Load(fullPath);
        _textures.Add(fullPath, texture);
        return texture;
    }

    public bool Unload(string relativePath)
    {
        ThrowIfDisposed();
        var fullPath = ResolvePath(relativePath);
        if (!_textures.Remove(fullPath, out var texture))
            return false;

        texture.Dispose();
        return true;
    }

    public void Clear()
    {
        ThrowIfDisposed();
        ReleaseAll();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ReleaseAll();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private string ResolvePath(string relativePath) => FilePaths.ResolveUnderRoot(ContentRoot, relativePath);

    private void ReleaseAll()
    {
        foreach (var texture in _textures.Values)
            texture.Dispose();

        _textures.Clear();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
