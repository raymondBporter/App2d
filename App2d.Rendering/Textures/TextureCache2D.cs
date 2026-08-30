using App2d.Core;

namespace App2d.Rendering.Textures;

public sealed class TextureCache2D : IDisposable
{
    private readonly Dictionary<string, Texture2D> _textures =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public TextureCache2D(string contentRoot)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(contentRoot);
        ContentRoot = Path.GetFullPath(contentRoot);
    }

    public string ContentRoot { get; }
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

    private string ResolvePath(string relativePath)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
            ArgGuard.ThrowInvalid(relativePath, "Texture cache paths must be relative to the content root.");

        var fullPath = Path.GetFullPath(Path.Combine(ContentRoot, relativePath));
        var relativeToRoot = Path.GetRelativePath(ContentRoot, fullPath);
        if (relativeToRoot == ".." ||
            relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativeToRoot))
        {
            ArgGuard.ThrowInvalid(relativePath, "Texture path must stay inside the content root.");
        }

        return fullPath;
    }

    private void ReleaseAll()
    {
        foreach (var texture in _textures.Values)
            texture.Dispose();

        _textures.Clear();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
