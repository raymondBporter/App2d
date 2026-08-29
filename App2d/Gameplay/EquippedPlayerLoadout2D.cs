using App2d.Engine.Rendering.Textures;
using SkiaSharp;

namespace App2d.Gameplay;

internal sealed class EquippedPlayerLoadout2D(
    TextureCache2D textures,
    long frameCacheBudgetBytes = TextureMemoryBudget2D.CompositeFrameCacheBytes,
    long sparsePackageCacheBudgetBytes = TextureMemoryBudget2D.SparsePackageCacheBytes) : IDisposable
{
    private static readonly string[] FacingIds = ["right", "left"];

    private readonly TextureCache2D _textures = ArgGuard.RequireNotNull(textures);
    private readonly Dictionary<EquippedFrameKey, CachedFrame> _frameCache = [];
    private readonly LinkedList<EquippedFrameKey> _frameRecency = [];
    private readonly Dictionary<string, CachedSparsePackage> _sparsePackages = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _sparsePackageRecency = [];
    private readonly long _frameCacheBudgetBytes = frameCacheBudgetBytes > 0
        ? frameCacheBudgetBytes
        : throw new ArgumentOutOfRangeException(nameof(frameCacheBudgetBytes));
    private readonly long _sparsePackageCacheBudgetBytes = sparsePackageCacheBudgetBytes > 0
        ? sparsePackageCacheBudgetBytes
        : throw new ArgumentOutOfRangeException(nameof(sparsePackageCacheBudgetBytes));
    private long _frameCacheBytes;
    private string? _equipmentId;
    private SparseAnimationPackage2D? _sparsePackage;
    private SparseAnimationPackage2D? _retainedFramePackage;
    private SparseAnimationFrame2D? _retainedFrame;
    private bool _disposed;

    public bool IsEquipped => _equipmentId is not null;
    public string? EquipmentId => _equipmentId;

    public void Equip(
        string equipmentId,
        IEnumerable<EquippedAnimationDefinition2D> animations)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssetId2D.Validate(equipmentId);
        ArgGuard.ThrowIfNull(animations);
        if (string.Equals(_equipmentId, equipmentId, StringComparison.Ordinal))
            return;

        var definitions = animations.ToArray();
        ArgGuard.ThrowIfTooShort(definitions.AsSpan(), 1, nameof(animations));
        foreach (var definition in definitions)
        {
            ValidateEquipmentAnimation(equipmentId, definition);
            foreach (var additionalEquipmentId in definition.AdditionalEquipmentIds)
                ValidateEquipmentAnimation(additionalEquipmentId, definition);
        }

        ReleaseRetainedSparseFrame();
        ClearFrameCache();
        _sparsePackage = GetOrLoadSparsePackage(equipmentId);
        _equipmentId = equipmentId;
        TrimSparsePackageCache();
    }

    public EquippedFrame2D GetFrame(EquippedAnimationDefinition2D animation, string facingId, int frameIndex, float elapsedSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var equipmentId = StateGuard.RequireNotNull(
            _equipmentId,
            "Equip a player loadout before requesting its frames.");
        if (animation.AdditionalEquipmentIds.Count == 0 &&
            _sparsePackage is { } layeredPackage &&
            layeredPackage.TryGetLayeredFrameAtTime(
                animation.Id,
                facingId,
                elapsedSeconds,
                out var layeredFrame))
        {
            ReleaseRetainedSparseFrame();
            var equippedFrame = new EquippedFrame2D(
                StateGuard.RequireNotNull(
                    layeredFrame,
                    "Layered sparse frame lookup succeeded without a frame."),
                layeredPackage.CanvasSize,
                layeredPackage.GetRoot(animation.Id, facingId));
            TrimSparsePackageCache();
            return equippedFrame;
        }

        if (animation.AdditionalEquipmentIds.Count == 0 &&
            _sparsePackage is { } sparsePackage &&
            sparsePackage.TryRetainFrameAtTime(
                animation.Id,
                facingId,
                elapsedSeconds,
                out var sparseFrame))
        {
            var retained = StateGuard.RequireNotNull(sparseFrame, "Sparse frame lookup succeeded without a frame.");
            RetainSparseFrame(sparsePackage, retained);
            var equippedFrame = new EquippedFrame2D(retained, sparsePackage.CanvasSize, sparsePackage.GetRoot(animation.Id, facingId));
            TrimSparsePackageCache();
            return equippedFrame;
        }

        ReleaseRetainedSparseFrame();

        var key = new EquippedFrameKey(animation.Id, facingId, frameIndex);
        if (_frameCache.TryGetValue(key, out var cached))
        {
            _frameRecency.Remove(cached.RecencyNode);
            _frameRecency.AddLast(cached.RecencyNode);
            return new EquippedFrame2D(cached.Texture);
        }

        var equipmentIds = new string[animation.AdditionalEquipmentIds.Count + 1];
        equipmentIds[0] = equipmentId;
        for (var index = 0; index < animation.AdditionalEquipmentIds.Count; index++)
            equipmentIds[index + 1] = animation.AdditionalEquipmentIds[index];
        var frame = EquippedLoadoutAnimationAssets2D.LoadFrame(_textures, "player", animation.Id, animation.FrameCount, facingId, frameIndex, equipmentIds);

        var frameBytes = checked((long)frame.Width * frame.Height * sizeof(uint));
        while (_frameCacheBytes + frameBytes > _frameCacheBudgetBytes && _frameRecency.First is { } leastRecent)
        {
            _frameRecency.RemoveFirst();
            if (_frameCache.Remove(leastRecent.Value, out var evicted))
            {
                _frameCacheBytes -= evicted.ByteCount;
                evicted.Texture.Dispose();
            }
        }
        var recencyNode = _frameRecency.AddLast(key);
        _frameCache.Add(key, new CachedFrame(frame, frameBytes, recencyNode));
        _frameCacheBytes += frameBytes;
        return new EquippedFrame2D(frame);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ReleaseRetainedSparseFrame();
        ClearFrameCache();
        foreach (var cached in _sparsePackages.Values)
            cached.Package.Dispose();
        _sparsePackages.Clear();
        _sparsePackageRecency.Clear();
        _sparsePackage = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ValidateEquipmentAnimation(
        string equipmentId,
        EquippedAnimationDefinition2D animation)
    {
        foreach (var facingId in FacingIds)
        {
            EquippedLoadoutAnimationAssets2D.ValidateEquipmentAnimation(_textures, equipmentId, animation.Id, animation.FrameCount, facingId);
        }
    }

    private SparseAnimationPackage2D? TryLoadSparsePackage(string equipmentId)
    {
        var manifestPath = Path.Combine(_textures.ContentRoot, "sparse-loadouts", equipmentId, "package.json");
        if (!File.Exists(manifestPath))
            return null;
        var package = SparseAnimationPackage2D.Load(manifestPath, _frameCacheBudgetBytes);
        if (!string.Equals(package.EquipmentId, equipmentId, StringComparison.Ordinal))
        {
            package.Dispose();
            throw new InvalidDataException($"Sparse package equipment '{package.EquipmentId}' does not match equipped item '{equipmentId}': {manifestPath}");
        }
        return package;
    }

    private SparseAnimationPackage2D? GetOrLoadSparsePackage(string equipmentId)
    {
        if (_sparsePackages.TryGetValue(equipmentId, out var cached))
        {
            _sparsePackageRecency.Remove(cached.RecencyNode);
            _sparsePackageRecency.AddLast(cached.RecencyNode);
            return cached.Package;
        }

        var package = TryLoadSparsePackage(equipmentId);
        if (package is null)
            return null;

        var node = _sparsePackageRecency.AddLast(equipmentId);
        _sparsePackages.Add(equipmentId, new CachedSparsePackage(package, node));
        return package;
    }

    private void TrimSparsePackageCache()
    {
        long residentBytes = 0;
        foreach (var cached in _sparsePackages.Values)
            residentBytes = checked(residentBytes + cached.Package.TotalResidentByteCount);

        var candidate = _sparsePackageRecency.First;
        while (residentBytes > _sparsePackageCacheBudgetBytes && candidate is not null)
        {
            var next = candidate.Next;
            if (_sparsePackages.TryGetValue(candidate.Value, out var cached) &&
                !ReferenceEquals(cached.Package, _sparsePackage))
            {
                _sparsePackageRecency.Remove(candidate);
                _sparsePackages.Remove(candidate.Value);
                residentBytes -= cached.Package.TotalResidentByteCount;
                cached.Package.Dispose();
            }
            candidate = next;
        }
    }

    private void RetainSparseFrame(
        SparseAnimationPackage2D package,
        SparseAnimationFrame2D frame)
    {
        if (ReferenceEquals(_retainedFramePackage, package) &&
            ReferenceEquals(_retainedFrame, frame))
        {
            package.ReleaseRetainedFrame(frame);
            return;
        }

        ReleaseRetainedSparseFrame();
        _retainedFramePackage = package;
        _retainedFrame = frame;
    }

    private void ReleaseRetainedSparseFrame()
    {
        if (_retainedFramePackage is { } package &&
            _retainedFrame is { } frame)
        {
            package.ReleaseRetainedFrame(frame);
        }
        _retainedFramePackage = null;
        _retainedFrame = null;
    }

    private void ClearFrameCache()
    {
        foreach (var frame in _frameCache.Values)
            frame.Texture.Dispose();
        _frameCache.Clear();
        _frameRecency.Clear();
        _frameCacheBytes = 0;
    }

    private sealed record CachedFrame(
        Texture2D Texture,
        long ByteCount,
        LinkedListNode<EquippedFrameKey> RecencyNode);

    private sealed record CachedSparsePackage(
        SparseAnimationPackage2D Package,
        LinkedListNode<string> RecencyNode);

    private readonly record struct EquippedFrameKey(
        string AnimationId,
        string FacingId,
        int FrameIndex);
}

internal sealed record EquippedAnimationDefinition2D(
    string Id,
    int FrameCount,
    IReadOnlyList<string> AdditionalEquipmentIds);

internal readonly record struct EquippedFrame2D
{
    public EquippedFrame2D(Texture2D texture)
    {
        Texture = ArgGuard.RequireNotNull(texture);
        SparseFrame = null;
        LayeredFrame = null;
        SourceCanvasSize = default;
        SourceRoot = default;
    }

    public EquippedFrame2D(
        SparseAnimationFrame2D sparseFrame,
        SKSizeI sourceCanvasSize,
        SKPointI sourceRoot)
    {
        SparseFrame = ArgGuard.RequireNotNull(sparseFrame);
        Texture = sparseFrame.Texture;
        LayeredFrame = null;
        SourceCanvasSize = sourceCanvasSize;
        SourceRoot = sourceRoot;
    }

    public EquippedFrame2D(
        SparseLayeredAnimationFrame2D layeredFrame,
        SKSizeI sourceCanvasSize,
        SKPointI sourceRoot)
    {
        LayeredFrame = ArgGuard.RequireNotNull(layeredFrame);
        Texture = null;
        SparseFrame = null;
        SourceCanvasSize = sourceCanvasSize;
        SourceRoot = sourceRoot;
    }

    public Texture2D? Texture { get; }
    public SparseAnimationFrame2D? SparseFrame { get; }
    public SparseLayeredAnimationFrame2D? LayeredFrame { get; }
    public SKSizeI SourceCanvasSize { get; }
    public SKPointI SourceRoot { get; }
}
