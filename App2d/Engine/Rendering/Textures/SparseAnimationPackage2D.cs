using System.Text.Json;
using App2d.Engine.Animation;
using SkiaSharp;

namespace App2d.Engine.Rendering.Textures;

/// <summary>
/// Loads, validates, composes, and bounds the resident composite memory for a
/// sparse-rooted-layers-v1 animation package.
/// </summary>
public sealed class SparseAnimationPackage2D : IDisposable
{
    private SparseAnimationPackage2D(string manifestPath, PackageManifest manifest, long cacheBudgetBytes, SparseCompositionOptions2D compositionOptions)
    {
        ManifestPath = manifestPath;
        _packageRoot = StateGuard.RequireNotNull(Path.GetDirectoryName(manifestPath), "A sparse package manifest must have a parent directory.");
        _manifest = manifest;
        _atlasPages = new SparseAtlasPageLease2D?[manifest.Atlases.Length];
        _cacheBudgetBytes = cacheBudgetBytes;
        _compositionOptions = compositionOptions;
    }

    public static SparseAnimationPackage2D Load(string manifestPath, long cacheBudgetBytes = 96L * 1024L * 1024L, SparseCompositionOptions2D? compositionOptions = null)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgGuard.ThrowIfNotPositive(cacheBudgetBytes);

        var fullManifestPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(fullManifestPath))
        {
            throw new FileNotFoundException("Sparse animation package manifest was not found.", fullManifestPath);
        }
        var packageRoot = StateGuard.RequireNotNull(Path.GetDirectoryName(fullManifestPath), "A sparse package manifest must have a parent directory.");
        PackageManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(fullManifestPath), JsonOptions) ??
                throw new InvalidDataException($"Sparse animation package is empty: {fullManifestPath}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Sparse animation package contains invalid JSON: {fullManifestPath}", exception);
        }

        ValidateManifest(manifest, fullManifestPath);
        for (var page = 0; page < manifest.Atlases.Length; page++)
        {
            var atlas = manifest.Atlases[page];
            var usage = FindFirstPageUse(manifest, page);
            try
            {
                var colorPath = ResolvePackagePath(packageRoot, atlas.Color);
                var depthPath = ResolvePackagePath(packageRoot, atlas.Depth);
                if (!File.Exists(colorPath))
                    throw new FileNotFoundException("Color atlas was not found.", colorPath);
                if (!File.Exists(depthPath))
                    throw new FileNotFoundException("Depth atlas was not found.", depthPath);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException)
            {
                throw PackageFailure(usage, $"atlas page {page} is invalid", exception);
            }
        }

        return new SparseAnimationPackage2D(
            fullManifestPath,
            manifest,
            cacheBudgetBytes,
            compositionOptions ?? new SparseCompositionOptions2D
            {
                AlphaTrim = true,
                AlphaTrimPadding = manifest.CropPadding
            });
    }

    private const string SupportedFormat = "sparse-rooted-layers-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _packageRoot;
    private readonly Lock _sync = new();
    private readonly PackageManifest _manifest;
    private readonly SparseAtlasPageLease2D?[] _atlasPages;
    private readonly Dictionary<CompositeKey, CachedFrame> _cache = [];
    private readonly Dictionary<SparseAnimationFrame2D, CachedFrame> _ownedFrames = [];
    private readonly LinkedList<CompositeKey> _recency = [];
    private readonly long _cacheBudgetBytes;
    private readonly SparseCompositionOptions2D _compositionOptions;
    private long _cacheBytes;
    private long _retainedEvictedBytes;
    private bool _sourceAtlasesReleased;
    private bool _disposed;
    public string ManifestPath { get; }
    public string Id => _manifest.Id;
    public string EquipmentId => _manifest.Equipment;
    public IReadOnlyCollection<string> AnimationIds => _manifest.Animations.Keys;
    public SKSizeI CanvasSize => new(_manifest.Canvas[0], _manifest.Canvas[1]);
    public long CachedByteCount => _cacheBytes;
    public long RetainedEvictedByteCount => _retainedEvictedBytes;
    public int CachedFrameCount => _cache.Count;
    public long ResidentAtlasByteCount => _atlasPages
        .Where(page => page is not null)
        .Sum(page => checked((long)page!.Color.Width * page.Color.Height * 6L));
    public long TotalResidentByteCount =>
        CachedByteCount + RetainedEvictedByteCount + ResidentAtlasByteCount;
    public bool SourceAtlasesReleased => _sourceAtlasesReleased;

    public int GetSampleCount(string animationId, string facingId) =>
        GetFacing(animationId, facingId).Samples.Length;

    public float GetTargetFramesPerSecond(string animationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return GetAnimation(animationId).TargetFramesPerSecond ??
            _manifest.TargetFramesPerSecond;
    }

    public bool IsLooping(string animationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return GetAnimation(animationId).Loop;
    }

    public bool ContainsAnimationFacing(string animationId, string facingId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _manifest.Animations.TryGetValue(animationId, out var animation) &&
            animation.Facings.ContainsKey(facingId);
    }

    public SKPointI GetRoot(string animationId, string facingId)
    {
        var facing = GetFacing(animationId, facingId);
        return new SKPointI(facing.Root[0], facing.Root[1]);
    }

    public AnimationClip2D<SparseAnimationSampleReference2D> CreateClip(
        string animationId,
        string facingId)
    {
        var animation = GetAnimation(animationId);
        var facing = GetFacing(animationId, facingId);
        var samples = facing.Samples
            .Select((sample, index) => new SparseAnimationSampleReference2D(
                animationId,
                facingId,
                index,
                sample.SourceFrame,
                sample.TimeSeconds,
                sample.DurationSeconds))
            .ToArray();
        return new AnimationClip2D<SparseAnimationSampleReference2D>(
            samples,
            samples.Select(sample => sample.DurationSeconds),
            animation.Loop);
    }

    public bool TryGetFrameForSourceFrame(
        string animationId,
        string facingId,
        int sourceFrame,
        out SparseAnimationFrame2D? frame,
        IReadOnlyCollection<string>? participatingLayerIds = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIfNotPositive(sourceFrame);
        frame = null;
        if (!ContainsAnimationFacing(animationId, facingId))
            return false;

        var samples = GetFacing(animationId, facingId).Samples;
        var sampleIndex = 0;
        for (var index = 1; index < samples.Length; index++)
        {
            if (samples[index].SourceFrame > sourceFrame)
                break;
            sampleIndex = index;
        }
        frame = GetFrame(animationId, facingId, sampleIndex, participatingLayerIds);
        return true;
    }

    public bool TryGetFrameAtTime(
        string animationId,
        string facingId,
        float elapsedSeconds,
        out SparseAnimationFrame2D? frame,
        IReadOnlyCollection<string>? participatingLayerIds = null)
        => TryResolveFrameAtTime(
            animationId,
            facingId,
            elapsedSeconds,
            retain: false,
            out frame,
            participatingLayerIds);

    /// <summary>
    /// Gets and retains a frame so LRU eviction cannot dispose its texture while
    /// a renderer still references it. Every successful call must be paired with
    /// <see cref="ReleaseRetainedFrame"/>.
    /// </summary>
    public bool TryRetainFrameAtTime(
        string animationId,
        string facingId,
        float elapsedSeconds,
        out SparseAnimationFrame2D? frame,
        IReadOnlyCollection<string>? participatingLayerIds = null)
        => TryResolveFrameAtTime(
            animationId,
            facingId,
            elapsedSeconds,
            retain: true,
            out frame,
            participatingLayerIds);

    public void ReleaseRetainedFrame(SparseAnimationFrame2D frame)
    {
        ArgGuard.ThrowIfNull(frame);
        lock (_sync)
        {
            if (!_ownedFrames.TryGetValue(frame, out var cached) ||
                cached.RetentionCount <= 0)
            {
                throw new InvalidOperationException(
                    "The sparse animation frame is not retained by this package.");
            }

            cached.RetentionCount--;
            if (cached.RetentionCount == 0 && cached.IsEvicted)
                DisposeEvictedFrame(cached);
        }
    }

    private bool TryResolveFrameAtTime(
        string animationId,
        string facingId,
        float elapsedSeconds,
        bool retain,
        out SparseAnimationFrame2D? frame,
        IReadOnlyCollection<string>? participatingLayerIds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIfNegativeOrNotFinite(elapsedSeconds);
        frame = null;
        if (!ContainsAnimationFacing(animationId, facingId))
            return false;

        var animation = GetAnimation(animationId);
        var samples = GetFacing(animationId, facingId).Samples;
        var duration = samples[^1].TimeSeconds + samples[^1].DurationSeconds;
        if (animation.Loop && elapsedSeconds >= duration)
            elapsedSeconds %= duration;
        var low = 0;
        var high = samples.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (samples[middle].TimeSeconds <= elapsedSeconds)
                low = middle + 1;
            else
                high = middle;
        }
        var sampleIndex = Math.Max(0, low - 1);
        lock (_sync)
        {
            frame = GetFrameCore(
                animationId,
                facingId,
                sampleIndex,
                participatingLayerIds);
            if (retain)
                _ownedFrames[frame].RetentionCount++;
        }
        return true;
    }

    /// <summary>
    /// Composes every declared default-layer sample. Source atlases are released
    /// only when the cache budget retained every result.
    /// </summary>
    public bool PrecomposeAll(
        bool releaseSourceAtlases = true,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var expectedKeys = new List<CompositeKey>();
        foreach (var (animationId, animation) in _manifest.Animations)
            PrecomposeAnimation(animationId, animation, expectedKeys, cancellationToken);

        lock (_sync)
        {
            var retainedAll = expectedKeys.All(_cache.ContainsKey);
            if (retainedAll && releaseSourceAtlases)
                ReleaseSourceAtlasesCore();
            return retainedAll;
        }
    }

    public void PrecomposeAnimations(
        IEnumerable<string> animationIds,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIfNull(animationIds);
        foreach (var animationId in animationIds.Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var animation = GetAnimation(animationId);
            PrecomposeAnimation(animationId, animation, null, cancellationToken);
        }
    }

    private void PrecomposeAnimation(
        string animationId,
        AnimationManifest animation,
        List<CompositeKey>? expectedKeys,
        CancellationToken cancellationToken)
    {
        foreach (var (facingId, facing) in animation.Facings)
        {
            for (var sampleIndex = 0; sampleIndex < facing.Samples.Length; sampleIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sample = facing.Samples[sampleIndex];
                _ = GetFrame(animationId, facingId, sampleIndex);
                expectedKeys?.Add(new CompositeKey(
                    animationId,
                    facingId,
                    sampleIndex,
                    sample.CanonicalLayerSet));
            }
        }
    }

    public SparseAnimationFrame2D GetFrame(
        string animationId,
        string facingId,
        int sampleIndex,
        IReadOnlyCollection<string>? participatingLayerIds = null)
    {
        lock (_sync)
            return GetFrameCore(animationId, facingId, sampleIndex, participatingLayerIds);
    }

    private SparseAnimationFrame2D GetFrameCore(
        string animationId,
        string facingId,
        int sampleIndex,
        IReadOnlyCollection<string>? participatingLayerIds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var facing = GetFacing(animationId, facingId);
        if ((uint)sampleIndex >= (uint)facing.Samples.Length)
            throw new ArgumentOutOfRangeException(nameof(sampleIndex));
        var sample = facing.Samples[sampleIndex];
        var layerIds = participatingLayerIds is null
            ? sample.CanonicalLayerIds
            : participatingLayerIds.Order(StringComparer.Ordinal).ToArray();
        if (layerIds.Length == 0)
        {
            throw PackageFailure(
                new PackageLocation(animationId, facingId, sampleIndex, null),
                "no participating layers were requested");
        }
        if (participatingLayerIds is not null && HasAdjacentDuplicate(layerIds))
        {
            throw PackageFailure(
                new PackageLocation(animationId, facingId, sampleIndex, null),
                "participating layer IDs contain duplicates");
        }

        var layerSet = participatingLayerIds is null
            ? sample.CanonicalLayerSet
            : string.Join('\u001f', layerIds);
        var key = new CompositeKey(
            animationId,
            facingId,
            sampleIndex,
            layerSet);
        if (_cache.TryGetValue(key, out var cached))
        {
            _recency.Remove(cached.RecencyNode);
            _recency.AddLast(cached.RecencyNode);
            return cached.Frame;
        }
        if (_sourceAtlasesReleased)
        {
            throw PackageFailure(
                new PackageLocation(animationId, facingId, sampleIndex, null),
                "source atlases were released and this composite is not cached");
        }

        var layers = new SparseDepthTextureLayer2D[layerIds.Length];
        for (var index = 0; index < layerIds.Length; index++)
        {
            var layerId = layerIds[index];
            if (!sample.Layers.TryGetValue(layerId, out var layer))
            {
                throw PackageFailure(
                    new PackageLocation(animationId, facingId, sampleIndex, layerId),
                    "animation sample does not contain the requested layer");
            }
            var page = GetAtlasPage(layer.Page);
            layers[index] = new SparseDepthTextureLayer2D(
                layerId,
                page.Color,
                page.Depth,
                ToRectangle(layer.Rect),
                new SKPointI(layer.Origin[0], layer.Origin[1]));
        }

        var composed = DepthCompositeTexture2D.CreateSparse(
            layers,
            new SparseCompositionContext2D(animationId, facingId, sampleIndex),
            _compositionOptions);
        var frame = new SparseAnimationFrame2D(
            composed.Texture,
            composed.Origin,
            sample.TimeSeconds,
            sample.DurationSeconds);
        AddToCache(key, frame, layerIds);
        return frame;
    }

    /// <summary>
    /// Invalidates only composites that used the specified layer.
    /// </summary>
    public void InvalidateLayer(string layerId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIfNullOrWhiteSpace(layerId);
        lock (_sync)
        {
            var keys = _cache
                .Where(pair => pair.Value.ParticipatingLayers.Contains(layerId))
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var key in keys)
                RemoveCached(key);
        }
    }

    /// <summary>
    /// Releases color and depth pages after all future frames needed by the caller
    /// have been composed. Cached composites remain usable until evicted.
    /// </summary>
    public void ReleaseSourceAtlases()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sync)
            ReleaseSourceAtlasesCore();
    }

    private void ReleaseSourceAtlasesCore()
    {
        if (_sourceAtlasesReleased)
            return;
        for (var index = 0; index < _atlasPages.Length; index++)
        {
            _atlasPages[index]?.Dispose();
            _atlasPages[index] = null;
        }
        _sourceAtlasesReleased = true;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            ClearCache();
            if (!_sourceAtlasesReleased)
                ReleaseSourceAtlasesCore();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private AnimationManifest GetAnimation(string animationId)
    {
        AssetNameGuard(animationId, nameof(animationId));
        if (!_manifest.Animations.TryGetValue(animationId, out var animation))
            throw new InvalidDataException($"Animation '{animationId}' is missing from {ManifestPath}.");
        return animation;
    }

    private FacingManifest GetFacing(string animationId, string facingId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssetNameGuard(facingId, nameof(facingId));
        var animation = GetAnimation(animationId);
        if (!animation.Facings.TryGetValue(facingId, out var facing))
        {
            throw new InvalidDataException(
                $"Animation '{animationId}' is missing facing '{facingId}' in {ManifestPath}.");
        }
        return facing;
    }

    private SparseAtlasPageLease2D GetAtlasPage(int pageIndex)
    {
        if (_atlasPages[pageIndex] is { } loaded)
            return loaded;
        var atlas = _manifest.Atlases[pageIndex];
        var usage = FindFirstPageUse(_manifest, pageIndex);
        try
        {
            loaded = SparseAtlasPagePool2D.Acquire(
                ResolvePackagePath(_packageRoot, atlas.Color),
                ResolvePackagePath(_packageRoot, atlas.Depth),
                atlas.Width,
                atlas.Height);
            _atlasPages[pageIndex] = loaded;
            return loaded;
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or NotSupportedException)
        {
            throw PackageFailure(usage, $"atlas page {pageIndex} is invalid", exception);
        }
    }

    private void AddToCache(
        CompositeKey key,
        SparseAnimationFrame2D frame,
        IReadOnlyCollection<string> layerIds)
    {
        var bytes = checked((long)frame.Texture.Width * frame.Texture.Height * sizeof(uint));
        var compositeBudget = Math.Max(0L, _cacheBudgetBytes - ResidentAtlasByteCount);
        while (_cacheBytes + bytes > compositeBudget && _recency.First is { } oldest)
            RemoveCached(oldest.Value);
        var node = _recency.AddLast(key);
        var participants = layerIds.ToHashSet(StringComparer.Ordinal);
        var cached = new CachedFrame(frame, bytes, node, participants);
        _cache.Add(key, cached);
        _ownedFrames.Add(frame, cached);
        _cacheBytes += bytes;
    }

    private void RemoveCached(CompositeKey key)
    {
        if (!_cache.Remove(key, out var cached))
            return;
        _recency.Remove(cached.RecencyNode);
        _cacheBytes -= cached.ByteCount;
        cached.IsEvicted = true;
        if (cached.RetentionCount == 0)
        {
            DisposeEvictedFrame(cached);
        }
        else
        {
            cached.IsCountedAsRetainedEvicted = true;
            _retainedEvictedBytes += cached.ByteCount;
        }
    }

    private void ClearCache()
    {
        foreach (var cached in _cache.Values)
        {
            cached.IsEvicted = true;
            if (cached.RetentionCount == 0)
            {
                DisposeEvictedFrame(cached);
            }
            else
            {
                cached.IsCountedAsRetainedEvicted = true;
                _retainedEvictedBytes += cached.ByteCount;
            }
        }
        _cache.Clear();
        _recency.Clear();
        _cacheBytes = 0;
    }

    private void DisposeEvictedFrame(CachedFrame cached)
    {
        if (!_ownedFrames.Remove(cached.Frame))
            return;
        if (cached.RetentionCount != 0 || !cached.IsEvicted)
            StateGuard.Throw("Only unretained evicted sparse frames can be disposed.");
        if (cached.IsCountedAsRetainedEvicted)
        {
            _retainedEvictedBytes -= cached.ByteCount;
            cached.IsCountedAsRetainedEvicted = false;
        }
        cached.Frame.Texture.Dispose();
    }

    private static void ValidateManifest(PackageManifest manifest, string manifestPath)
    {
        if (!string.Equals(manifest.Format, SupportedFormat, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Unsupported sparse package format '{manifest.Format}': {manifestPath}");
        }
        if (string.IsNullOrWhiteSpace(manifest.Id))
            throw new InvalidDataException($"Sparse package ID is missing: {manifestPath}");
        if (string.IsNullOrWhiteSpace(manifest.Equipment))
            throw new InvalidDataException($"Sparse package equipment ID is missing: {manifestPath}");
        if (manifest.CropPadding < 0)
            throw new InvalidDataException($"Sparse package crop padding is invalid: {manifestPath}");
        if (manifest.AtlasGutter < 0)
            throw new InvalidDataException($"Sparse package atlas gutter is invalid: {manifestPath}");
        if (manifest.Canvas.Length != 2 || manifest.Canvas[0] <= 0 || manifest.Canvas[1] <= 0)
            throw new InvalidDataException($"Sparse package canvas is invalid: {manifestPath}");
        if (!float.IsFinite(manifest.TargetFramesPerSecond) ||
            manifest.TargetFramesPerSecond <= 0f)
        {
            throw new InvalidDataException(
                $"Sparse package default target frame rate is invalid: {manifestPath}");
        }
        if (manifest.Atlases.Length == 0)
            throw new InvalidDataException($"Sparse package has no atlas pages: {manifestPath}");
        for (var page = 0; page < manifest.Atlases.Length; page++)
        {
            var atlas = manifest.Atlases[page];
            if (string.IsNullOrWhiteSpace(atlas.Color) || string.IsNullOrWhiteSpace(atlas.Depth))
                throw new InvalidDataException($"Sparse atlas page {page} has missing data: {manifestPath}");
            if (!string.Equals(atlas.DepthFormat, "r16-unorm", StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    $"Unsupported depth format '{atlas.DepthFormat}' on page {page}: {manifestPath}");
            }
            if (atlas.Width <= 0 || atlas.Height <= 0)
                throw new InvalidDataException($"Sparse atlas page {page} has invalid dimensions: {manifestPath}");
        }
        if (manifest.Animations.Count == 0)
            throw new InvalidDataException($"Sparse package has no animations: {manifestPath}");

        foreach (var (animationId, animation) in manifest.Animations)
        {
            if (animation.TargetFramesPerSecond is { } targetFramesPerSecond &&
                (!float.IsFinite(targetFramesPerSecond) || targetFramesPerSecond <= 0f))
            {
                throw PackageFailure(
                    new(animationId, "<all>", 0, null),
                    "target frame rate is nonpositive or invalid");
            }
            if (animation.Facings.Count == 0)
                throw PackageFailure(new(animationId, "<missing>", 0, null), "missing facings");
            int? expectedSampleCount = null;
            string[]? expectedLayerIds = null;
            SampleManifest[]? expectedTimeline = null;
            foreach (var (facingId, facing) in animation.Facings)
            {
                if (facing.Root.Length != 2)
                    throw PackageFailure(new(animationId, facingId, 0, null), "shared root is invalid");
                if (facing.Samples.Length == 0)
                    throw PackageFailure(new(animationId, facingId, 0, null), "missing animation samples");
                if (expectedSampleCount is { } count && count != facing.Samples.Length)
                {
                    throw PackageFailure(
                        new(animationId, facingId, 0, null),
                        $"character/equipment sample-count mismatch ({count} and {facing.Samples.Length})");
                }
                expectedSampleCount = facing.Samples.Length;
                if (MathF.Abs(facing.Samples[0].TimeSeconds) > 0.0001f)
                {
                    throw PackageFailure(
                        new(animationId, facingId, 0, null),
                        "the shared timeline does not begin at zero");
                }

                for (var sampleIndex = 0; sampleIndex < facing.Samples.Length; sampleIndex++)
                {
                    var sample = facing.Samples[sampleIndex];
                    if (!float.IsFinite(sample.TimeSeconds) || sample.TimeSeconds < 0f)
                        throw PackageFailure(new(animationId, facingId, sampleIndex, null), "source time is invalid");
                    if (!float.IsFinite(sample.DurationSeconds) || sample.DurationSeconds <= 0f)
                        throw PackageFailure(new(animationId, facingId, sampleIndex, null), "sample duration is nonpositive or invalid");
                    if (sampleIndex > 0 &&
                        sample.TimeSeconds <= facing.Samples[sampleIndex - 1].TimeSeconds)
                    {
                        throw PackageFailure(new(animationId, facingId, sampleIndex, null), "source times are not increasing");
                    }
                    if (sampleIndex + 1 < facing.Samples.Length &&
                        MathF.Abs(
                            sample.TimeSeconds + sample.DurationSeconds -
                            facing.Samples[sampleIndex + 1].TimeSeconds) > 0.0001f)
                    {
                        throw PackageFailure(
                            new(animationId, facingId, sampleIndex, null),
                            "sample duration does not reach the next source time");
                    }
                    if (sample.Layers.Count == 0)
                        throw PackageFailure(new(animationId, facingId, sampleIndex, null), "sample has no layers");

                    var layerIds = sample.Layers.Keys.Order(StringComparer.Ordinal).ToArray();
                    sample.CanonicalLayerIds = layerIds;
                    sample.CanonicalLayerSet = string.Join('\u001f', layerIds);
                    expectedLayerIds ??= layerIds;
                    if (!expectedLayerIds.SequenceEqual(layerIds, StringComparer.Ordinal))
                    {
                        throw PackageFailure(
                            new(animationId, facingId, sampleIndex, null),
                            "character/equipment sample-count mismatch; participating layers differ");
                    }
                    foreach (var (layerId, layer) in sample.Layers)
                    {
                        var location = new PackageLocation(animationId, facingId, sampleIndex, layerId);
                        if ((uint)layer.Page >= (uint)manifest.Atlases.Length)
                            throw PackageFailure(location, $"invalid atlas page reference {layer.Page}");
                        if (layer.Rect.Length != 4 || layer.Origin.Length != 2)
                            throw PackageFailure(location, "atlas rectangle or root-relative origin is invalid");
                        var rectangle = ToRectangle(layer.Rect);
                        var atlas = manifest.Atlases[layer.Page];
                        if (rectangle.Width <= 0 || rectangle.Height <= 0 ||
                            rectangle.Left < 0 || rectangle.Top < 0 ||
                            rectangle.Right > atlas.Width || rectangle.Bottom > atlas.Height)
                        {
                            throw PackageFailure(location, "atlas rectangle is outside its atlas");
                        }
                    }
                }
                if (animation.DurationSeconds is { } durationSeconds)
                {
                    if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
                    {
                        throw PackageFailure(
                            new(animationId, facingId, 0, null),
                            "animation duration is nonpositive or invalid");
                    }
                    var finalSample = facing.Samples[^1];
                    if (MathF.Abs(
                            finalSample.TimeSeconds + finalSample.DurationSeconds -
                            durationSeconds) > 0.0001f)
                    {
                        throw PackageFailure(
                            new(animationId, facingId, facing.Samples.Length - 1, null),
                            "final sample does not end at the animation duration");
                    }
                }
                if (expectedTimeline is null)
                {
                    expectedTimeline = facing.Samples;
                }
                else
                {
                    for (var sampleIndex = 0; sampleIndex < facing.Samples.Length; sampleIndex++)
                    {
                        var expected = expectedTimeline[sampleIndex];
                        var actual = facing.Samples[sampleIndex];
                        if (expected.SourceFrame != actual.SourceFrame ||
                            MathF.Abs(expected.TimeSeconds - actual.TimeSeconds) > 0.0001f ||
                            MathF.Abs(expected.DurationSeconds - actual.DurationSeconds) > 0.0001f)
                        {
                            throw PackageFailure(
                                new(animationId, facingId, sampleIndex, null),
                                "facing does not use the shared animation timeline");
                        }
                    }
                }
            }
        }
    }

    private static PackageLocation FindFirstPageUse(PackageManifest manifest, int page)
    {
        foreach (var (animationId, animation) in manifest.Animations)
        {
            foreach (var (facingId, facing) in animation.Facings)
            {
                for (var sampleIndex = 0; sampleIndex < facing.Samples.Length; sampleIndex++)
                {
                    foreach (var (layerId, layer) in facing.Samples[sampleIndex].Layers)
                    {
                        if (layer.Page == page)
                            return new PackageLocation(animationId, facingId, sampleIndex, layerId);
                    }
                }
            }
        }

        return new PackageLocation("<unknown>", "<unknown>", 0, $"page-{page}");
    }

    private static SKRectI ToRectangle(int[] values) =>
        new(values[0], values[1], checked(values[0] + values[2]), checked(values[1] + values[3]));

    private static bool HasAdjacentDuplicate(ReadOnlySpan<string> sortedValues)
    {
        for (var index = 1; index < sortedValues.Length; index++)
        {
            if (string.Equals(
                    sortedValues[index - 1],
                    sortedValues[index],
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string ResolvePackagePath(string packageRoot, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new InvalidDataException($"Atlas path must be relative: {relativePath}");
        var fullPath = Path.GetFullPath(Path.Combine(packageRoot, relativePath));
        var rootPrefix = packageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(
                rootPrefix,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Atlas path leaves its package: {relativePath}");
        }
        return fullPath;
    }

    private static void AssetNameGuard(string value, string parameterName)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Contains(Path.DirectorySeparatorChar) ||
            (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar &&
             value.Contains(Path.AltDirectorySeparatorChar)))
        {
            throw new ArgumentException("Asset name cannot contain path separators.", parameterName);
        }
    }

    private static InvalidDataException PackageFailure(
        PackageLocation location,
        string message,
        Exception? innerException = null)
    {
        var layer = location.LayerId is null ? string.Empty : $", layer '{location.LayerId}'";
        return new InvalidDataException(
            $"Sparse package failure for animation '{location.AnimationId}', " +
            $"facing '{location.FacingId}', sample {location.SampleIndex}{layer}: {message}.",
            innerException);
    }

    private sealed class CachedFrame(
        SparseAnimationFrame2D frame,
        long byteCount,
        LinkedListNode<CompositeKey> recencyNode,
        HashSet<string> participatingLayers)
    {
        public SparseAnimationFrame2D Frame { get; } = frame;
        public long ByteCount { get; } = byteCount;
        public LinkedListNode<CompositeKey> RecencyNode { get; } = recencyNode;
        public HashSet<string> ParticipatingLayers { get; } = participatingLayers;
        public int RetentionCount { get; set; }
        public bool IsEvicted { get; set; }
        public bool IsCountedAsRetainedEvicted { get; set; }
    }

    private readonly record struct CompositeKey(
        string AnimationId,
        string FacingId,
        int SampleIndex,
        string LayerSet);

    private readonly record struct PackageLocation(
        string AnimationId,
        string FacingId,
        int SampleIndex,
        string? LayerId);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class PackageManifest
    {
        public string Format { get; init; } = string.Empty;
        public string Id { get; init; } = string.Empty;
        public string Equipment { get; init; } = string.Empty;
        public int CropPadding { get; init; }
        public int AtlasGutter { get; init; }
        public float TargetFramesPerSecond { get; init; }
        public int[] Canvas { get; init; } = [];
        public AtlasManifest[] Atlases { get; init; } = [];
        public Dictionary<string, AnimationManifest> Animations { get; init; } = [];
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class AtlasManifest
    {
        public string Color { get; init; } = string.Empty;
        public string Depth { get; init; } = string.Empty;
        public int Width { get; init; }
        public int Height { get; init; }
        public string DepthFormat { get; init; } = string.Empty;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class AnimationManifest
    {
        public bool Loop { get; init; }
        public float? DurationSeconds { get; init; }
        public float? TargetFramesPerSecond { get; init; }
        public Dictionary<string, FacingManifest> Facings { get; init; } = [];
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class FacingManifest
    {
        public int[] Root { get; init; } = [];
        public SampleManifest[] Samples { get; init; } = [];
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class SampleManifest
    {
        public int SourceFrame { get; init; }
        public float TimeSeconds { get; init; }
        public float DurationSeconds { get; init; }
        public Dictionary<string, LayerManifest> Layers { get; init; } = [];
        [System.Text.Json.Serialization.JsonIgnore]
        public string[] CanonicalLayerIds { get; set; } = [];
        [System.Text.Json.Serialization.JsonIgnore]
        public string CanonicalLayerSet { get; set; } = string.Empty;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class LayerManifest
    {
        public int Page { get; init; }
        public int[] Rect { get; init; } = [];
        public int[] Origin { get; init; } = [];
    }
}

public sealed class SparseAnimationFrame2D(
    Texture2D texture,
    SKPointI origin,
    float sourceTimeSeconds,
    float durationSeconds)
{
    public Texture2D Texture { get; } = ArgGuard.RequireNotNull(texture);
    public SKPointI Origin { get; } = origin;
    public int Width => Texture.Width;
    public int Height => Texture.Height;
    public float SourceTimeSeconds { get; } = sourceTimeSeconds;
    public float DurationSeconds { get; } = durationSeconds;
}

public readonly record struct SparseAnimationSampleReference2D(
    string AnimationId,
    string FacingId,
    int SampleIndex,
    int SourceFrame,
    float SourceTimeSeconds,
    float DurationSeconds);
