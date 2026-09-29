using System.Collections.Immutable;

namespace App2d.Core.Assets;

/// <summary>A read-only description of a registered resource and its current consumers.</summary>
public sealed record ResourceInfo2D(
    string Key,
    Type Type,
    string? Source,
    bool IsLoaded,
    ImmutableArray<string> Owners);

/// <summary>
/// A small, single-threaded registry for shared resources. Keys identify definitions;
/// loading is lazy and each definition is loaded at most once until removed.
/// Owners are labels for inspection, not reference counts or automatic eviction.
/// </summary>
public sealed class ResourceManager2D : IDisposable
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private bool _disposed;

    public int Count => _entries.Count;

    /// <param name="ownsResource">False when another object owns and disposes the loaded value.</param>
    public void Register<T>(string key, Func<T> loader, string? source = null, bool ownsResource = true)
        where T : class
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(loader);
        if (!_entries.TryAdd(key, new Entry(typeof(T), () => loader(), source, ownsResource)))
            throw new ArgumentException($"Resource '{key}' is already registered.", nameof(key));
    }

    /// <summary>Preload a registered resource without retrieving it.</summary>
    public void Load<T>(string key, string? owner = null) where T : class =>
        _ = GetLoad<T>(key, owner);

    /// <summary>Return a registered resource, loading it on the first call.</summary>
    public T GetLoad<T>(string key, string? owner = null) where T : class
    {
        var entry = Require<T>(key);
        ValidateOwner(owner);
        if (entry.Value is null)
        {
            if (entry.IsLoading)
                throw new InvalidOperationException($"Resource '{key}' is already loading.");
            entry.IsLoading = true;
            try
            {
                entry.Value = entry.Loader() ??
                    throw new InvalidOperationException($"Resource '{key}' loader returned null.");
            }
            finally
            {
                entry.IsLoading = false;
            }
        }
        if (owner is not null) entry.Owners.Add(owner);
        return (T)entry.Value;
    }

    /// <summary>Return a loaded resource; never runs its loader.</summary>
    public T Get<T>(string key, string? owner = null) where T : class
    {
        var entry = Require<T>(key);
        ValidateOwner(owner);
        if (entry.Value is null)
            throw new InvalidOperationException($"Resource '{key}' has not been loaded.");
        if (owner is not null) entry.Owners.Add(owner);
        return (T)entry.Value;
    }

    /// <summary>Forget a consumer label when its world, scene, or chunk is gone.</summary>
    public void ForgetOwner(string owner)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        foreach (var entry in _entries.Values) entry.Owners.Remove(owner);
    }

    /// <summary>Remove a definition and release its loaded value if this manager owns it.</summary>
    public bool Remove(string key)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_entries.Remove(key, out var entry)) return false;
        Release(entry);
        return true;
    }

    /// <summary>Remove every definition and release owned values in reverse registration order.</summary>
    public void Clear()
    {
        ThrowIfDisposed();
        foreach (var entry in _entries.Values.Reverse()) Release(entry);
        _entries.Clear();
    }

    public IReadOnlyList<ResourceInfo2D> Snapshot()
    {
        ThrowIfDisposed();
        return [.. _entries.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
            new ResourceInfo2D(pair.Key, pair.Value.Type, pair.Value.Source,
                pair.Value.Value is not null,
                [.. pair.Value.Owners.Order(StringComparer.Ordinal)]))];
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private Entry Require<T>(string key) where T : class
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_entries.TryGetValue(key, out var entry))
            throw new KeyNotFoundException($"Resource '{key}' is not registered.");
        if (entry.Type != typeof(T))
            throw new InvalidOperationException(
                $"Resource '{key}' is {entry.Type.Name}, not {typeof(T).Name}.");
        return entry;
    }

    private static void ValidateOwner(string? owner)
    {
        if (owner is not null) ArgumentException.ThrowIfNullOrWhiteSpace(owner);
    }

    private static void Release(Entry entry)
    {
        if (entry.OwnsResource && entry.Value is IDisposable disposable)
            disposable.Dispose();
        entry.Value = null;
        entry.Owners.Clear();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class Entry(Type type, Func<object?> loader, string? source, bool ownsResource)
    {
        public Type Type { get; } = type;
        public Func<object?> Loader { get; } = loader;
        public string? Source { get; } = source;
        public bool OwnsResource { get; } = ownsResource;
        public HashSet<string> Owners { get; } = new(StringComparer.Ordinal);
        public object? Value { get; set; }
        public bool IsLoading { get; set; }
    }
}
