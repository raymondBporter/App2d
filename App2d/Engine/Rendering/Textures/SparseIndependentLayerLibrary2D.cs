using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using SkiaSharp;

namespace App2d.Engine.Rendering.Textures;

/// <summary>
/// Loads one shared character layer and one active equipment layer.  Both are
/// sampled independently against the same clip clock and composed by the
/// existing sparse depth shader.
/// </summary>
public sealed class SparseIndependentLayerLibrary2D : IDisposable
{
    private const string SupportedLibraryFormat = "sparse-independent-layer-library-v1";
    private const string SupportedPackageFormat = "sparse-rooted-layer-package-v1";
    private const string SupportedTimelineFormat = "sparse-rooted-timeline-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _libraryRoot;
    private readonly LibraryManifest _manifest;
    private readonly LayerPackage _character;
    private readonly Dictionary<LayeredFrameKey, SparseLayeredAnimationFrame2D> _frames = [];
    private LayerPackage? _equipment;
    private string? _equipmentId;
    private bool _disposed;

    private SparseIndependentLayerLibrary2D(
        string libraryPath,
        LibraryManifest manifest,
        LayerPackage character)
    {
        LibraryPath = libraryPath;
        _libraryRoot = StateGuard.RequireNotNull(
            Path.GetDirectoryName(libraryPath),
            "A sparse layer library must have a parent directory.");
        _manifest = manifest;
        _character = character;
    }

    public string LibraryPath { get; }
    public string Id => _manifest.Id;
    public string? ActiveEquipmentId => _equipmentId;
    public SKSizeI CanvasSize => new(_manifest.Canvas[0], _manifest.Canvas[1]);
    public long ResidentAtlasByteCount =>
        _character.ResidentAtlasByteCount + (_equipment?.ResidentAtlasByteCount ?? 0L);
    public long CharacterResidentAtlasByteCount => _character.ResidentAtlasByteCount;
    public long ActiveEquipmentResidentAtlasByteCount =>
        _equipment?.ResidentAtlasByteCount ?? 0L;

    public static SparseIndependentLayerLibrary2D Load(string libraryPath)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(libraryPath);
        var fullPath = Path.GetFullPath(libraryPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Sparse layer library was not found.", fullPath);

        var root = StateGuard.RequireNotNull(
            Path.GetDirectoryName(fullPath),
            "A sparse layer library must have a parent directory.");
        var manifest = Deserialize<LibraryManifest>(fullPath, "sparse layer library");
        if (!string.Equals(manifest.Format, SupportedLibraryFormat, StringComparison.Ordinal))
            throw new NotSupportedException($"Unsupported sparse layer library format '{manifest.Format}': {fullPath}");
        if (string.IsNullOrWhiteSpace(manifest.Id))
            throw new InvalidDataException($"Sparse layer library ID is missing: {fullPath}");
        if (manifest.Canvas.Length != 2 || manifest.Canvas[0] <= 0 || manifest.Canvas[1] <= 0)
            throw new InvalidDataException($"Sparse layer library canvas is invalid: {fullPath}");
        if (manifest.Equipment.Count == 0)
            throw new InvalidDataException($"Sparse layer library has no equipment: {fullPath}");

        var character = LayerPackage.Load(
            ResolveLibraryPath(root, manifest.Character.Package),
            ResolveLibraryPath(root, manifest.Character.Timeline),
            "character",
            manifest.Character.Id,
            manifest.Id,
            manifest.Canvas);
        return new SparseIndependentLayerLibrary2D(fullPath, manifest, character);
    }

    public bool ContainsEquipment(string equipmentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIfNullOrWhiteSpace(equipmentId);
        return _manifest.Equipment.ContainsKey(equipmentId);
    }

    public bool ActivateEquipment(string equipmentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIfNullOrWhiteSpace(equipmentId);
        if (string.Equals(_equipmentId, equipmentId, StringComparison.Ordinal))
            return true;
        if (!_manifest.Equipment.TryGetValue(equipmentId, out var descriptor))
            return false;

        var replacement = LayerPackage.Load(
            ResolveLibraryPath(_libraryRoot, descriptor.Package),
            ResolveLibraryPath(_libraryRoot, descriptor.Timeline),
            "equipment",
            equipmentId,
            _manifest.Id,
            _manifest.Canvas);
        ValidateCompatibleClocks(_character, replacement);
        _equipment?.Dispose();
        _equipment = replacement;
        _equipmentId = equipmentId;
        _frames.Clear();
        return true;
    }

    public bool TryGetLayeredFrameAtTime(
        string animationId,
        string facingId,
        float elapsedSeconds,
        out SparseLayeredAnimationFrame2D? frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIfNegativeOrNotFinite(elapsedSeconds);
        frame = null;
        if (_equipment is not { } equipment ||
            !_character.Contains(animationId, facingId) ||
            !equipment.Contains(animationId, facingId))
        {
            return false;
        }

        var character = _character.Resolve(animationId, facingId, elapsedSeconds, 0);
        var equipped = equipment.Resolve(animationId, facingId, elapsedSeconds, 1);
        var key = new LayeredFrameKey(
            animationId,
            facingId,
            character.SampleIndex,
            equipped.SampleIndex);
        if (_frames.TryGetValue(key, out frame))
            return true;

        var start = MathF.Max(character.TimeSeconds, equipped.TimeSeconds);
        var end = MathF.Min(
            character.TimeSeconds + character.DurationSeconds,
            equipped.TimeSeconds + equipped.DurationSeconds);
        frame = new SparseLayeredAnimationFrame2D(
            character.Layer,
            equipped.Layer,
            start,
            MathF.Max(float.Epsilon, end - start));
        _frames.Add(key, frame);
        return true;
    }

    public SKPointI GetRoot(string animationId, string facingId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _character.GetRoot(animationId, facingId);
    }

    public SparseIndependentSourceFrames2D GetSourceFramesAtTime(
        string animationId,
        string facingId,
        float elapsedSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIfNegativeOrNotFinite(elapsedSeconds);
        var equipment = StateGuard.RequireNotNull(
            _equipment,
            "Activate equipment before resolving independent source frames.");
        var character = _character.Resolve(animationId, facingId, elapsedSeconds, 0);
        var equipped = equipment.Resolve(animationId, facingId, elapsedSeconds, 1);
        return new SparseIndependentSourceFrames2D(
            character.SourceFrame,
            equipped.SourceFrame);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _frames.Clear();
        _equipment?.Dispose();
        _character.Dispose();
        _equipment = null;
        _equipmentId = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static void ValidateCompatibleClocks(LayerPackage character, LayerPackage equipment)
    {
        foreach (var animationId in character.AnimationIds)
        {
            if (!equipment.TryGetClock(animationId, out var equipmentClock))
                throw new InvalidDataException($"Equipment timeline is missing animation '{animationId}'.");
            var characterClock = character.GetClock(animationId);
            if (characterClock.Loop != equipmentClock.Loop ||
                MathF.Abs(characterClock.DurationSeconds - equipmentClock.DurationSeconds) > 0.0001f)
            {
                throw new InvalidDataException(
                    $"Character and equipment clocks differ for animation '{animationId}'.");
            }
        }
    }

    private static T Deserialize<T>(string path, string description)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) ??
                throw new InvalidDataException($"The {description} is empty: {path}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The {description} contains invalid JSON: {path}", exception);
        }
    }

    private static string ResolveLibraryPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidDataException($"Sparse library path must be relative: {relativePath}");
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(
                prefix,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Sparse library path leaves its root: {relativePath}");
        }
        return fullPath;
    }

    private sealed class LayerPackage : IDisposable
    {
        private readonly string _packageRoot;
        private readonly PackageManifest _package;
        private readonly TimelineManifest _timeline;
        private readonly SparseAtlasPageLease2D?[] _pages;
        private bool _disposed;

        private LayerPackage(
            string packagePath,
            PackageManifest package,
            TimelineManifest timeline)
        {
            PackagePath = packagePath;
            _packageRoot = StateGuard.RequireNotNull(Path.GetDirectoryName(packagePath), "A layer package must have a parent directory.");
            _package = package;
            _timeline = timeline;
            _pages = new SparseAtlasPageLease2D?[package.Atlases.Length];
        }

        public string PackagePath { get; }
        public IEnumerable<string> AnimationIds => _timeline.Animations.Keys;
        public long ResidentAtlasByteCount => _pages.Sum(
            page => page is null ? 0L : (long)page.Color.Width * page.Color.Height * 6L);

        public static LayerPackage Load(
            string packagePath,
            string timelinePath,
            string expectedKind,
            string expectedPackageId,
            string expectedTimelineId,
            int[] expectedCanvas)
        {
            if (!File.Exists(packagePath))
                throw new FileNotFoundException("Sparse layer package was not found.", packagePath);
            if (!File.Exists(timelinePath))
                throw new FileNotFoundException("Sparse layer timeline was not found.", timelinePath);
            var package = Deserialize<PackageManifest>(packagePath, "sparse layer package");
            var timeline = Deserialize<TimelineManifest>(timelinePath, "sparse layer timeline");
            if (!string.Equals(package.Format, SupportedPackageFormat, StringComparison.Ordinal))
                throw new NotSupportedException($"Unsupported sparse layer package format '{package.Format}': {packagePath}");
            if (!string.Equals(timeline.Format, SupportedTimelineFormat, StringComparison.Ordinal))
                throw new NotSupportedException($"Unsupported sparse timeline format '{timeline.Format}': {timelinePath}");
            if (!string.Equals(package.Kind, expectedKind, StringComparison.Ordinal) ||
                !string.Equals(package.Id, expectedPackageId, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Sparse layer identity mismatch: {packagePath}");
            }
            if (!string.Equals(timeline.Id, expectedTimelineId, StringComparison.Ordinal) ||
                !timeline.Canvas.SequenceEqual(expectedCanvas))
            {
                throw new InvalidDataException($"Sparse layer timeline does not match the library: {timelinePath}");
            }
            if (package.Atlases.Length == 0 || package.Animations.Count == 0 || timeline.Animations.Count == 0)
                throw new InvalidDataException($"Sparse layer package or timeline is empty: {packagePath}");

            var root = StateGuard.RequireNotNull(Path.GetDirectoryName(packagePath), "A layer package must have a parent directory.");
            for (var pageIndex = 0; pageIndex < package.Atlases.Length; pageIndex++)
            {
                var atlas = package.Atlases[pageIndex];
                if (atlas.Width <= 0 || atlas.Height <= 0 ||
                    !string.Equals(atlas.DepthFormat, "r16-unorm", StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Sparse atlas page {pageIndex} is invalid: {packagePath}");
                }
                if (!File.Exists(ResolvePackagePath(root, atlas.Color)) ||
                    !File.Exists(ResolvePackagePath(root, atlas.Depth)))
                {
                    throw new FileNotFoundException($"Sparse atlas page {pageIndex} is incomplete: {packagePath}");
                }
            }
            ValidateSamples(package, timeline, packagePath);
            return new LayerPackage(packagePath, package, timeline);
        }

        public bool Contains(string animationId, string facingId) =>
            _timeline.Animations.TryGetValue(animationId, out var animation) &&
            animation.Facings.ContainsKey(facingId) &&
            _package.Animations.TryGetValue(animationId, out var packageAnimation) &&
            packageAnimation.Facings.ContainsKey(facingId);

        public AnimationClock GetClock(string animationId)
        {
            if (!_timeline.Animations.TryGetValue(animationId, out var animation))
                throw new InvalidDataException($"Animation '{animationId}' is missing from {PackagePath}.");
            return new AnimationClock(animation.Loop, animation.DurationSeconds);
        }

        public bool TryGetClock(string animationId, out AnimationClock clock)
        {
            if (_timeline.Animations.TryGetValue(animationId, out var animation))
            {
                clock = new AnimationClock(animation.Loop, animation.DurationSeconds);
                return true;
            }
            clock = default;
            return false;
        }

        public SKPointI GetRoot(string animationId, string facingId)
        {
            if (!Contains(animationId, facingId))
                throw new InvalidDataException($"Animation '{animationId}' facing '{facingId}' is missing from {PackagePath}.");
            var root = _timeline.Animations[animationId].Facings[facingId].Root;
            return new SKPointI(root[0], root[1]);
        }

        public ResolvedLayer Resolve(
            string animationId,
            string facingId,
            float elapsedSeconds,
            int sortRank)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!Contains(animationId, facingId))
                throw new InvalidDataException($"Animation '{animationId}' facing '{facingId}' is missing from {PackagePath}.");
            var animation = _timeline.Animations[animationId];
            var samples = animation.Samples;
            if (animation.Loop && elapsedSeconds >= animation.DurationSeconds)
                elapsedSeconds %= animation.DurationSeconds;
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
            var timeSample = samples[sampleIndex];
            var frameSample = _package.Animations[animationId].Facings[facingId].Samples[sampleIndex];
            var atlasFrame = frameSample.Frame;
            var page = GetPage(atlasFrame.Page);
            return new ResolvedLayer(
                new SparseAnimationLayer2D(
                    _package.Kind,
                    page.Color,
                    page.Depth,
                    new SKRectI(
                        atlasFrame.Rect[0],
                        atlasFrame.Rect[1],
                        checked(atlasFrame.Rect[0] + atlasFrame.Rect[2]),
                        checked(atlasFrame.Rect[1] + atlasFrame.Rect[3])),
                    new SKPointI(atlasFrame.Origin[0], atlasFrame.Origin[1]),
                    sortRank),
                sampleIndex,
                timeSample.SourceFrame,
                timeSample.TimeSeconds,
                timeSample.DurationSeconds);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            foreach (var page in _pages)
                page?.Dispose();
            _disposed = true;
        }

        private SparseAtlasPageLease2D GetPage(int pageIndex)
        {
            if ((uint)pageIndex >= (uint)_pages.Length)
                throw new InvalidDataException($"Invalid atlas page {pageIndex}: {PackagePath}");
            if (_pages[pageIndex] is { } page)
                return page;
            var atlas = _package.Atlases[pageIndex];
            page = SparseAtlasPagePool2D.Acquire(
                ResolvePackagePath(_packageRoot, atlas.Color),
                ResolvePackagePath(_packageRoot, atlas.Depth),
                atlas.Width,
                atlas.Height);
            _pages[pageIndex] = page;
            return page;
        }

        private static void ValidateSamples(
            PackageManifest package,
            TimelineManifest timeline,
            string packagePath)
        {
            foreach (var (animationId, animation) in timeline.Animations)
            {
                if (!float.IsFinite(animation.DurationSeconds) || animation.DurationSeconds <= 0f ||
                    animation.Samples.Length == 0 ||
                    MathF.Abs(animation.Samples[0].TimeSeconds) > 0.0001f)
                {
                    throw new InvalidDataException($"Sparse timeline animation '{animationId}' is invalid: {packagePath}");
                }
                if (!package.Animations.TryGetValue(animationId, out var packageAnimation))
                    throw new InvalidDataException($"Sparse package is missing animation '{animationId}': {packagePath}");
                foreach (var (facingId, facing) in animation.Facings)
                {
                    if (facing.Root.Length != 2 ||
                        !packageAnimation.Facings.TryGetValue(facingId, out var packageFacing) ||
                        packageFacing.Samples.Length != animation.Samples.Length)
                    {
                        throw new InvalidDataException($"Sparse layer samples differ for '{animationId}/{facingId}': {packagePath}");
                    }
                    for (var index = 0; index < animation.Samples.Length; index++)
                    {
                        var time = animation.Samples[index];
                        var frame = packageFacing.Samples[index];
                        if (time.SourceFrame != frame.SourceFrame ||
                            !float.IsFinite(time.TimeSeconds) || time.TimeSeconds < 0f ||
                            !float.IsFinite(time.DurationSeconds) || time.DurationSeconds <= 0f ||
                            frame.Frame.Rect.Length != 4 || frame.Frame.Origin.Length != 2 ||
                            frame.Frame.Rect[2] <= 0 || frame.Frame.Rect[3] <= 0 ||
                            (uint)frame.Frame.Page >= (uint)package.Atlases.Length)
                        {
                            throw new InvalidDataException($"Sparse layer sample {animationId}/{facingId}/{index} is invalid: {packagePath}");
                        }
                        if (index > 0 && time.TimeSeconds <= animation.Samples[index - 1].TimeSeconds)
                            throw new InvalidDataException($"Sparse layer times are not increasing: {packagePath}");
                    }
                    var final = animation.Samples[^1];
                    if (MathF.Abs(final.TimeSeconds + final.DurationSeconds - animation.DurationSeconds) > 0.0001f)
                        throw new InvalidDataException($"Sparse layer timeline does not fill '{animationId}': {packagePath}");
                }
            }
        }

        private static string ResolvePackagePath(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new InvalidDataException($"Sparse package path must be relative: {relativePath}");
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException($"Sparse package path leaves its root: {relativePath}");
            return fullPath;
        }
    }

    private readonly record struct AnimationClock(bool Loop, float DurationSeconds);
    private readonly record struct ResolvedLayer(
        SparseAnimationLayer2D Layer,
        int SampleIndex,
        int SourceFrame,
        float TimeSeconds,
        float DurationSeconds);
    private readonly record struct LayeredFrameKey(
        string AnimationId,
        string FacingId,
        int CharacterSampleIndex,
        int EquipmentSampleIndex);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class LibraryManifest
    {
        public string Format { get; init; } = string.Empty;
        public string Id { get; init; } = string.Empty;
        public int[] Canvas { get; init; } = [];
        public LayerDescriptor Character { get; init; } = new();
        public Dictionary<string, LayerDescriptor> Equipment { get; init; } = [];
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class LayerDescriptor
    {
        public string Id { get; init; } = string.Empty;
        public string Package { get; init; } = string.Empty;
        public string Timeline { get; init; } = string.Empty;
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class PackageManifest
    {
        public string Format { get; init; } = string.Empty;
        public string Id { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public AtlasManifest[] Atlases { get; init; } = [];
        public Dictionary<string, PackageAnimationManifest> Animations { get; init; } = [];
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class AtlasManifest
    {
        public string Color { get; init; } = string.Empty;
        public string Depth { get; init; } = string.Empty;
        public int Width { get; init; }
        public int Height { get; init; }
        public string DepthFormat { get; init; } = string.Empty;
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class PackageAnimationManifest
    {
        public Dictionary<string, PackageFacingManifest> Facings { get; init; } = [];
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class PackageFacingManifest
    {
        public PackageSampleManifest[] Samples { get; init; } = [];
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class PackageSampleManifest
    {
        public int SourceFrame { get; init; }
        public AtlasFrameManifest Frame { get; init; } = new();
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class AtlasFrameManifest
    {
        public int Page { get; init; }
        public int[] Rect { get; init; } = [];
        public int[] Origin { get; init; } = [];
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class TimelineManifest
    {
        public string Format { get; init; } = string.Empty;
        public string Id { get; init; } = string.Empty;
        public int[] Canvas { get; init; } = [];
        public Dictionary<string, TimelineAnimationManifest> Animations { get; init; } = [];
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class TimelineAnimationManifest
    {
        public bool Loop { get; init; }
        public float DurationSeconds { get; init; }
        public TimelineSampleManifest[] Samples { get; init; } = [];
        public Dictionary<string, TimelineFacingManifest> Facings { get; init; } = [];
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class TimelineFacingManifest
    {
        public int[] Root { get; init; } = [];
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class TimelineSampleManifest
    {
        public int SourceFrame { get; init; }
        public float TimeSeconds { get; init; }
        public float DurationSeconds { get; init; }
    }
}

public readonly record struct SparseIndependentSourceFrames2D(
    int CharacterSourceFrame,
    int EquipmentSourceFrame);
