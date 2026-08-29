using App2d.Engine.Rendering.Textures;
using SkiaSharp;

namespace App2d.Gameplay;

internal sealed class EquippedPlayerLoadout2D(
    TextureCache2D textures,
    long frameCacheBudgetBytes = 96L * 1024L * 1024L) : IDisposable
{
    private static readonly string[] FacingIds = ["right", "left"];

    private readonly TextureCache2D _textures = ArgGuard.RequireNotNull(textures);
    private readonly Dictionary<EquippedFrameKey, CachedFrame> _frameCache = [];
    private readonly LinkedList<EquippedFrameKey> _frameRecency = [];
    private readonly long _frameCacheBudgetBytes = frameCacheBudgetBytes > 0
        ? frameCacheBudgetBytes
        : throw new ArgumentOutOfRangeException(nameof(frameCacheBudgetBytes));
    private long _frameCacheBytes;
    private string? _equipmentId;
    private SparseAnimationPackage2D? _sparsePackage;
    private CancellationTokenSource? _precomposeCancellation;
    private Task? _precomposeTask;
    private bool _disposed;

    public bool IsEquipped => _equipmentId is not null;

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

        StopPrecomposition();
        ClearFrameCache();
        _sparsePackage?.Dispose();
        _sparsePackage = TryLoadSparsePackage(equipmentId);
        StartPrecomposition(_sparsePackage);
        _equipmentId = equipmentId;
    }

    public EquippedFrame2D GetFrame(
        EquippedAnimationDefinition2D animation,
        string facingId,
        int frameIndex,
        float elapsedSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfPrecompositionFailed();
        var equipmentId = StateGuard.RequireNotNull(
            _equipmentId,
            "Equip a player loadout before requesting its frames.");
        if (animation.AdditionalEquipmentIds.Count == 0 &&
            _sparsePackage is { } sparsePackage &&
            sparsePackage.TryGetFrameAtTime(
                animation.Id,
                facingId,
                elapsedSeconds,
                out var sparseFrame))
        {
            return new EquippedFrame2D(
                StateGuard.RequireNotNull(sparseFrame, "Sparse frame lookup succeeded without a frame."),
                sparsePackage.CanvasSize,
                sparsePackage.GetRoot(animation.Id, facingId));
        }

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
        var frame = EquippedLoadoutAnimationAssets2D.LoadFrame(
            _textures,
            "player",
            animation.Id,
            animation.FrameCount,
            facingId,
            frameIndex,
            equipmentIds);

        var frameBytes = checked((long)frame.Width * frame.Height * sizeof(uint));
        while (_frameCacheBytes + frameBytes > _frameCacheBudgetBytes &&
            _frameRecency.First is { } leastRecent)
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

        StopPrecomposition();
        ClearFrameCache();
        _sparsePackage?.Dispose();
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
            EquippedLoadoutAnimationAssets2D.ValidateEquipmentAnimation(
                _textures,
                equipmentId,
                animation.Id,
                animation.FrameCount,
                facingId);
        }
    }

    private SparseAnimationPackage2D? TryLoadSparsePackage(string equipmentId)
    {
        var manifestPath = Path.Combine(
            _textures.ContentRoot,
            "sparse-loadouts",
            equipmentId,
            "package.json");
        if (!File.Exists(manifestPath))
            return null;
        var package = SparseAnimationPackage2D.Load(
            manifestPath,
            _frameCacheBudgetBytes);
        if (!string.Equals(package.EquipmentId, equipmentId, StringComparison.Ordinal))
        {
            package.Dispose();
            throw new InvalidDataException(
                $"Sparse package equipment '{package.EquipmentId}' does not match " +
                $"equipped item '{equipmentId}': {manifestPath}");
        }
        return package;
    }

    private void StartPrecomposition(SparseAnimationPackage2D? package)
    {
        if (package is null)
            return;
        _precomposeCancellation = new CancellationTokenSource();
        var cancellationToken = _precomposeCancellation.Token;
        _precomposeTask = Task.Run(
            () => package.PrecomposeAnimations(
                ["idle", "walk"],
                cancellationToken),
            cancellationToken);
    }

    private void StopPrecomposition()
    {
        _precomposeCancellation?.Cancel();
        try
        {
            _precomposeTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _precomposeCancellation?.Dispose();
            _precomposeCancellation = null;
            _precomposeTask = null;
        }
    }

    private void ThrowIfPrecompositionFailed()
    {
        if (_precomposeTask is { IsFaulted: true } task)
            task.GetAwaiter().GetResult();
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
        SourceCanvasSize = sourceCanvasSize;
        SourceRoot = sourceRoot;
    }

    public Texture2D Texture { get; }
    public SparseAnimationFrame2D? SparseFrame { get; }
    public SKSizeI SourceCanvasSize { get; }
    public SKPointI SourceRoot { get; }
}
