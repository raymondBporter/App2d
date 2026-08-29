using App2d.Engine.Rendering.Textures;
using App2d.Engine.Rendering;
using App2d.Engine.Geometry;
using System.Numerics;
using System.Text.Json;
using SkiaSharp;

namespace App2d.Tests.Rendering;

public sealed class SparseAnimationPackage2DTests
{
    private static readonly string PackageRoot = TestAssets.GetPath(
        "Work",
        "art-pipeline",
        "SparseLayerSwordAProof");

    [Fact]
    public void EverySparseCompositeMatchesValidatedFullCanvasProof()
    {
        using var package = SparseAnimationPackage2D.Load(
            Path.Combine(PackageRoot, "package.json"));

        foreach (var animationId in new[] { "idle", "walk" })
        foreach (var facingId in new[] { "right", "left" })
        {
            var clip = package.CreateClip(animationId, facingId);
            var declaredDuration = 0f;
            for (var sampleIndex = 0; sampleIndex < clip.FrameCount; sampleIndex++)
            {
                var actual = package.GetFrame(animationId, facingId, sampleIndex);
                using var expected = Texture2D.Load(Path.Combine(
                    PackageRoot,
                    "proof-composites",
                    animationId,
                    facingId,
                    $"frame-{sampleIndex + 1:0000}.png"));

                Assert.Equal(expected.Width, actual.Width);
                Assert.Equal(expected.Height, actual.Height);
                Assert.Equal(expected.CopyPixels(), actual.Texture.CopyPixels());
                Assert.Equal(clip[sampleIndex].DurationSeconds, actual.DurationSeconds);
                declaredDuration += actual.DurationSeconds;
            }
            Assert.Equal(declaredDuration, clip.Duration, 5);
        }
    }

    [Fact]
    public void LayerSelectionOrderDoesNotChangeComposite()
    {
        using var package = SparseAnimationPackage2D.Load(
            Path.Combine(PackageRoot, "package.json"));

        var first = package.GetFrame(
            "idle",
            "right",
            0,
            ["character", "equipment"]);
        var second = package.GetFrame(
            "idle",
            "right",
            0,
            ["equipment", "character"]);

        Assert.Same(first, second);
        Assert.Equal(first.Texture.CopyPixels(), second.Texture.CopyPixels());
    }

    [Fact]
    public void ThreeSparseLayersAreOrderIndependent()
    {
        using var color0 = Texture2D.Load(Path.Combine(PackageRoot, "atlases", "color-00.png"));
        using var color1 = Texture2D.Load(Path.Combine(PackageRoot, "atlases", "color-01.png"));
        using var depth0 = DepthAtlas2D.LoadR16Unorm(Path.Combine(PackageRoot, "atlases", "depth-00.png"));
        using var depth1 = DepthAtlas2D.LoadR16Unorm(Path.Combine(PackageRoot, "atlases", "depth-01.png"));
        var character = new SparseDepthTextureLayer2D(
            "character",
            color0,
            depth0,
            new SKRectI(353, 243, 468, 480),
            new SKPointI(-54, -234));
        var weapon = new SparseDepthTextureLayer2D(
            "weapon",
            color1,
            depth1,
            new SKRectI(770, 780, 960, 853),
            new SKPointI(-33, -93));
        var secondEquipment = weapon with { LayerId = "shield" };
        var context = new SparseCompositionContext2D("idle", "right", 0);

        using var first = DepthCompositeTexture2D
            .CreateSparse([character, weapon, secondEquipment], context)
            .Texture;
        using var second = DepthCompositeTexture2D
            .CreateSparse([secondEquipment, character, weapon], context)
            .Texture;

        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
        Assert.Equal(first.CopyPixels(), second.CopyPixels());
    }

    [Fact]
    public void SparseProofAlsoMatchesLegacyFullCanvasComposition()
    {
        var content = Path.GetFullPath(Path.Combine(PackageRoot, "..", "..", "..", "Content"));
        using var characterColor = Texture2D.Load(Path.Combine(
            content, "characters", "player", "animations", "idle", "color", "right", "frame-0001.png"));
        using var characterDepth = Texture2D.Load(Path.Combine(
            content, "characters", "player", "animations", "idle", "depth", "right", "frame-0001.png"));
        using var weaponColor = Texture2D.Load(Path.Combine(
            content, "weapons", "right-hand-sword-a", "animations", "idle", "right", "color", "frame-0001.png"));
        using var weaponDepth = Texture2D.Load(Path.Combine(
            content, "weapons", "right-hand-sword-a", "animations", "idle", "right", "depth", "frame-0001.png"));
        using var full = DepthCompositeTexture2D.Create(
            new DepthTextureLayer2D(characterColor, characterDepth, "character"),
            new DepthTextureLayer2D(weaponColor, weaponDepth, "equipment"));
        using var proof = Texture2D.Load(Path.Combine(
            PackageRoot, "proof-composites", "idle", "right", "frame-0001.png"));
        var fullPixels = full.CopyPixels();
        var proofPixels = proof.CopyPixels();
        const int proofLeft = 256 - 54;
        const int proofTop = 324 - 234;

        for (var y = 0; y < full.Height; y++)
        for (var x = 0; x < full.Width; x++)
        {
            var expected = x >= proofLeft && x < proofLeft + proof.Width &&
                y >= proofTop && y < proofTop + proof.Height
                    ? proofPixels[(y - proofTop) * proof.Width + x - proofLeft]
                    : SKColors.Transparent;
            var actual = fullPixels[y * full.Width + x];
            if (expected.Alpha == 0 && actual.Alpha == 0)
                continue;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void CacheCanReleaseSourcesAndInvalidateOnlyParticipatingLayers()
    {
        using var package = SparseAnimationPackage2D.Load(
            Path.Combine(PackageRoot, "package.json"));
        var character = package.GetFrame("idle", "right", 0, ["character"]);
        var combined = package.GetFrame("idle", "right", 0);

        package.InvalidateLayer("equipment");

        Assert.False(character.Texture.IsDisposed);
        Assert.True(combined.Texture.IsDisposed);
        package.ReleaseSourceAtlases();
        Assert.Same(character, package.GetFrame("idle", "right", 0, ["character"]));
        Assert.Throws<InvalidDataException>(() => package.GetFrame("idle", "right", 1));
    }

    [Fact]
    public void ProductionPackageLoadsPagesLazilyAndMapsSourceFrames()
    {
        var productionManifest = Path.GetFullPath(Path.Combine(
            PackageRoot,
            "..",
            "..",
            "..",
            "Content",
            "sparse-loadouts",
            "right-hand-sword-a",
            "package.json"));
        using var package = SparseAnimationPackage2D.Load(productionManifest);
        Assert.Equal(0, package.ResidentAtlasByteCount);

        Assert.True(package.TryGetFrameForSourceFrame("idle", "right", 1, out var first));
        Assert.NotNull(first);
        Assert.True(package.ResidentAtlasByteCount > 0);
        Assert.True(package.TryGetFrameForSourceFrame("idle", "right", 2, out var repeated));
        Assert.Same(first, repeated);
        var clip = package.CreateClip("idle", "right");
        Assert.True(package.TryGetFrameForSourceFrame(
            "idle", "right", clip[1].SourceFrame, out var next));
        Assert.NotSame(first, next);

        using var proof = Texture2D.Load(Path.Combine(
            PackageRoot, "proof-composites", "idle", "right", "frame-0001.png"));
        Assert.Equal(proof.CopyPixels(), first!.Texture.CopyPixels());
    }

    [Fact]
    public void ProductionPackageMapsFramesByDeclaredAnimationTime()
    {
        var productionManifest = Path.GetFullPath(Path.Combine(
            PackageRoot,
            "..",
            "..",
            "..",
            "Content",
            "sparse-loadouts",
            "right-hand-sword-a",
            "package.json"));
        using var package = SparseAnimationPackage2D.Load(productionManifest);
        var clip = package.CreateClip("idle", "right");
        var transition = clip[1].SourceTimeSeconds;
        var followingTransition = clip[2].SourceTimeSeconds;
        const float epsilon = 0.0001f;

        Assert.True(package.TryGetFrameAtTime(
            "idle", "right", transition - epsilon, out var first));
        Assert.True(package.TryGetFrameAtTime("idle", "right", transition, out var second));
        Assert.True(package.TryGetFrameAtTime(
            "idle", "right", followingTransition - epsilon, out var held));
        var loopDuration = clip.Duration;
        Assert.True(package.TryGetFrameAtTime(
            "idle",
            "right",
            loopDuration + transition - epsilon,
            out var wrapped));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Same(second, held);
        Assert.Same(first, wrapped);
        Assert.Equal(0f, first!.SourceTimeSeconds, 5);
        Assert.Equal(transition, second!.SourceTimeSeconds, 5);
    }

    [Fact]
    public void SparseCanvasShaderPreservesLegacyCanvasPlacement()
    {
        var productionManifest = Path.GetFullPath(Path.Combine(
            PackageRoot, "..", "..", "..", "Content", "sparse-loadouts",
            "right-hand-sword-a", "package.json"));
        using var package = SparseAnimationPackage2D.Load(productionManifest);
        Assert.True(package.TryGetFrameForSourceFrame("idle", "right", 1, out var sparseFrame));
        var sparseShader = new SparseCanvasSpriteShader2D(
            sparseFrame!,
            package.CanvasSize,
            package.GetRoot("idle", "right"),
            SKFilterMode.Nearest);

        var content = Path.GetFullPath(Path.Combine(PackageRoot, "..", "..", "..", "Content"));
        using var characterColor = Texture2D.Load(Path.Combine(
            content, "characters", "player", "animations", "idle", "color", "right", "frame-0001.png"));
        using var characterDepth = Texture2D.Load(Path.Combine(
            content, "characters", "player", "animations", "idle", "depth", "right", "frame-0001.png"));
        using var weaponColor = Texture2D.Load(Path.Combine(
            content, "weapons", "right-hand-sword-a", "animations", "idle", "right", "color", "frame-0001.png"));
        using var weaponDepth = Texture2D.Load(Path.Combine(
            content, "weapons", "right-hand-sword-a", "animations", "idle", "right", "depth", "frame-0001.png"));
        using var full = DepthCompositeTexture2D.Create(
            new DepthTextureLayer2D(characterColor, characterDepth, "character"),
            new DepthTextureLayer2D(weaponColor, weaponDepth, "equipment"));
        var fullShader = new SpriteShader2D(full, SKFilterMode.Nearest);

        AssertEquivalentPixels(RenderShader(fullShader), RenderShader(sparseShader));
    }

    [Fact]
    public void GpuDepthCompositeShaderMatchesCpuSparseComposite()
    {
        var productionManifest = Path.GetFullPath(Path.Combine(
            PackageRoot, "..", "..", "..", "Content", "sparse-loadouts",
            "right-hand-sword-a", "package.json"));
        using var package = SparseAnimationPackage2D.Load(productionManifest);
        Assert.True(package.TryGetLayeredFrameAtTime(
            "idle",
            "right",
            0f,
            out var layeredFrame));
        var cpuFrame = package.GetFrame("idle", "right", 0);
        var cpuShader = new SparseCanvasSpriteShader2D(
            cpuFrame,
            package.CanvasSize,
            package.GetRoot("idle", "right"),
            SKFilterMode.Nearest);
        using var gpuShader = new SparseDepthCompositeShader2D(
            layeredFrame!,
            package.CanvasSize,
            package.GetRoot("idle", "right"),
            SKFilterMode.Nearest);

        AssertEquivalentPixels(RenderShader(cpuShader), RenderShader(gpuShader));
    }

    [Fact]
    public void WarmLayeredFrameLookupDoesNotAllocateManagedMemory()
    {
        using var package = SparseAnimationPackage2D.Load(
            Path.Combine(PackageRoot, "package.json"));
        Assert.True(package.TryGetLayeredFrameAtTime(
            "idle",
            "right",
            0f,
            out var expected));

        for (var iteration = 0; iteration < 100; iteration++)
        {
            if (!package.TryGetLayeredFrameAtTime(
                    "idle",
                    "right",
                    0f,
                    out _))
            {
                throw new InvalidOperationException("Expected a layered sparse frame.");
            }
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        SparseLayeredAnimationFrame2D? actual = null;
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            if (!package.TryGetLayeredFrameAtTime(
                    "idle",
                    "right",
                    0f,
                    out actual))
            {
                throw new InvalidOperationException("Expected a layered sparse frame.");
            }
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Same(expected, actual);
        Assert.Equal(0, allocated);
        Assert.Equal(1, package.CachedLayeredFrameCount);
        Assert.Equal(0, package.CachedFrameCount);
    }

    [Fact]
    public void AtlasResidencyDoesNotConsumeCompositeFrameBudget()
    {
        var productionManifest = Path.GetFullPath(Path.Combine(
            PackageRoot, "..", "..", "..", "Content", "sparse-loadouts",
            "right-hand-sword-a", "package.json"));
        using var package = SparseAnimationPackage2D.Load(
            productionManifest,
            cacheBudgetBytes: 16L * 1024L * 1024L);

        package.PrecomposeAnimations(["idle"]);

        Assert.True(package.ResidentAtlasByteCount > 16L * 1024L * 1024L);
        Assert.True(package.CachedFrameCount > 1);
        Assert.True(package.CachedByteCount <= 16L * 1024L * 1024L);
    }

    [Fact]
    public void WarmAtlasCompositionDoesNotCopyWholeColorPages()
    {
        var productionManifest = Path.GetFullPath(Path.Combine(
            PackageRoot, "..", "..", "..", "Content", "sparse-loadouts",
            "right-hand-sword-a", "package.json"));
        using var package = SparseAnimationPackage2D.Load(productionManifest);
        package.PrecomposeAnimations(["idle"]);
        package.InvalidateLayer("equipment");

        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = package.GetFrame("idle", "right", 1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(
            allocated < 8L * 1024L,
            $"Warm sparse composition allocated {allocated:N0} managed bytes.");
    }

    [Fact]
    public void CompletePrecompositionReleasesAtlasesWhenBudgetFits()
    {
        var productionManifest = Path.GetFullPath(Path.Combine(
            PackageRoot, "..", "..", "..", "Content", "sparse-loadouts",
            "right-hand-sword-a", "package.json"));
        using var package = SparseAnimationPackage2D.Load(
            productionManifest,
            cacheBudgetBytes: 384L * 1024L * 1024L);

        Assert.True(package.PrecomposeAll());
        Assert.True(package.SourceAtlasesReleased);
        Assert.Equal(0, package.ResidentAtlasByteCount);
        Assert.True(package.TotalResidentByteCount < 192L * 1024L * 1024L);
        var expectedFrameCount = package.AnimationIds.Sum(animationId =>
            package.GetSampleCount(animationId, "right") +
            package.GetSampleCount(animationId, "left"));
        Assert.Equal(expectedFrameCount, package.CachedFrameCount);
        Assert.All(package.AnimationIds, animationId =>
            Assert.True(package.GetTargetFramesPerSecond(animationId) > 0f));
        Assert.Equal(0.24f, package.CreateClip("sword-attack", "right").Duration, 5);
        Assert.Equal(0.4f, package.CreateClip("magic-shot", "right").Duration, 5);
        Assert.NotNull(package.GetFrame(
            "shield-block",
            "left",
            package.GetSampleCount("shield-block", "left") - 1));
    }

    [Fact]
    public void EveryProductionLoadoutPrecomposesWithinStagingAndRuntimeBudgets()
    {
        var productionRoot = Path.GetFullPath(Path.Combine(
            PackageRoot, "..", "..", "..", "Content", "sparse-loadouts"));
        var manifests = Directory
            .EnumerateFiles(productionRoot, "package.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(14, manifests.Length);

        foreach (var manifest in manifests)
        {
            using var package = SparseAnimationPackage2D.Load(
                manifest,
                cacheBudgetBytes: 384L * 1024L * 1024L);
            Assert.True(package.PrecomposeAll(), package.EquipmentId);
            var expectedFrameCount = package.AnimationIds.Sum(animationId =>
                package.GetSampleCount(animationId, "right") +
                package.GetSampleCount(animationId, "left"));
            Assert.Equal(expectedFrameCount, package.CachedFrameCount);
            Assert.True(package.SourceAtlasesReleased);
            Assert.True(package.TotalResidentByteCount < 192L * 1024L * 1024L);
        }
    }

    [Fact]
    public void RetainedFrameSurvivesBackgroundStyleEvictionUntilReleased()
    {
        using var package = SparseAnimationPackage2D.Load(
            Path.Combine(PackageRoot, "package.json"),
            cacheBudgetBytes: 1);

        Assert.True(package.TryRetainFrameAtTime(
            "idle",
            "right",
            0f,
            out var retained));
        _ = package.GetFrame("walk", "left", 0);

        Assert.NotNull(retained);
        Assert.False(retained!.Texture.IsDisposed);
        Assert.True(package.RetainedEvictedByteCount > 0);

        package.ReleaseRetainedFrame(retained);

        Assert.True(retained.Texture.IsDisposed);
        Assert.Equal(0, package.RetainedEvictedByteCount);
    }

    [Fact]
    public void WarmRetainedFrameLookupDoesNotAllocateManagedMemory()
    {
        using var package = SparseAnimationPackage2D.Load(
            Path.Combine(PackageRoot, "package.json"));
        Assert.True(package.TryRetainFrameAtTime(
            "idle",
            "right",
            0f,
            out var displayed));

        try
        {
            for (var iteration = 0; iteration < 100; iteration++)
            {
                if (!package.TryRetainFrameAtTime(
                        "idle",
                        "right",
                        0f,
                        out var frame))
                {
                    throw new InvalidOperationException("Expected a retained sparse frame.");
                }
                package.ReleaseRetainedFrame(frame!);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < 1_000; iteration++)
            {
                if (!package.TryRetainFrameAtTime(
                        "idle",
                        "right",
                        0f,
                        out var frame))
                {
                    throw new InvalidOperationException("Expected a retained sparse frame.");
                }
                package.ReleaseRetainedFrame(frame!);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(0, allocated);
        }
        finally
        {
            package.ReleaseRetainedFrame(displayed!);
        }
    }

    [Fact]
    public void EveryProductionLoadoutMatchesLibrarySamplingContract()
    {
        var productionRoot = Path.GetFullPath(Path.Combine(
            PackageRoot, "..", "..", "..", "Content", "sparse-loadouts"));
        using var library = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(productionRoot, "library.json")));
        var libraryRoot = library.RootElement;
        Assert.Equal(
            "sparse-loadout-library-v1",
            libraryRoot.GetProperty("format").GetString());

        var expectedAnimations = libraryRoot
            .GetProperty("animationTargetFramesPerSecond")
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetSingle(),
                StringComparer.Ordinal);
        Assert.NotEmpty(expectedAnimations);

        var equipment = libraryRoot.GetProperty("equipment").EnumerateObject().ToArray();
        Assert.Equal(14, equipment.Length);
        foreach (var entry in equipment)
        {
            var relativeManifest = Assert.IsType<string>(entry.Value.GetString());
            using var package = SparseAnimationPackage2D.Load(
                Path.Combine(productionRoot, relativeManifest));

            Assert.Equal(entry.Name, package.EquipmentId);
            Assert.Equal(
                expectedAnimations.Keys.Order(StringComparer.Ordinal),
                package.AnimationIds.Order(StringComparer.Ordinal));
            foreach (var (animationId, expectedFramesPerSecond) in expectedAnimations)
            {
                Assert.Equal(
                    expectedFramesPerSecond,
                    package.GetTargetFramesPerSecond(animationId),
                    5);
            }
        }
    }

    private static SKColor[] RenderShader(IShader2D shader)
    {
        var bounds = new Bounds2D(new Vector2(-256f, -192f), new Vector2(256f, 192f));
        using var bitmap = new SKBitmap(new SKImageInfo(
            512, 384, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(0, 0, 0, 0));
        using var imageShader = shader.AcquireShader(new ShaderContext(
            Matrix3x2.Identity,
            bounds,
            default));
        using var paint = new SKPaint { Shader = imageShader.Shader, Color = SKColors.White };
        canvas.Translate(256f, 192f);
        canvas.Scale(1f, -1f);
        canvas.DrawRect(-256f, -192f, 256f, 192f, paint);
        return bitmap.Pixels;
    }

    private static void AssertEquivalentPixels(SKColor[] expected, SKColor[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            if (expected[index].Alpha == 0 && actual[index].Alpha == 0)
                continue;
            Assert.Equal(expected[index], actual[index]);
        }
    }
}
