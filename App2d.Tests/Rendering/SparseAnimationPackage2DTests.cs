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
    private static readonly string ContentRoot = Path.GetFullPath(Path.Combine(
        PackageRoot, "..", "..", "..", "Content"));
    private static readonly string ProductionLibraryPath = Path.Combine(
        ContentRoot, "sparse", "player-one-handed-v1", "library.json");

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
    public void ProductionLibraryLoadsPagesLazilyAndSamplesLayersIndependently()
    {
        using var library = SparseIndependentLayerLibrary2D.Load(ProductionLibraryPath);
        Assert.Equal(0, library.ResidentAtlasByteCount);
        Assert.True(library.ActivateEquipment("right-hand-sword-a"));
        Assert.Equal(0, library.ResidentAtlasByteCount);

        Assert.True(library.TryGetLayeredFrameAtTime(
            "idle", "right", 0f, out var first));
        Assert.NotNull(first);
        Assert.True(library.ResidentAtlasByteCount > 0);
        Assert.True(library.TryGetLayeredFrameAtTime(
            "idle", "right", 0.001f, out var repeated));
        Assert.Same(first, repeated);

        var independent = Enumerable.Range(0, 241)
            .Select(index => library.GetSourceFramesAtTime(
                "sword-attack", "right", index / 1000f))
            .FirstOrDefault(value =>
                value.CharacterSourceFrame != value.EquipmentSourceFrame);
        Assert.NotEqual(
            independent.CharacterSourceFrame,
            independent.EquipmentSourceFrame);
    }

    [Fact]
    public void ProductionLibraryUsesOneClockForIndependentLoopingLayers()
    {
        using var library = SparseIndependentLayerLibrary2D.Load(ProductionLibraryPath);
        Assert.True(library.ActivateEquipment("right-hand-sword-a"));
        using var timeline = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            ContentRoot,
            "sparse",
            "player-one-handed-v1",
            "timelines",
            "character.json")));
        var duration = timeline.RootElement
            .GetProperty("animations")
            .GetProperty("idle")
            .GetProperty("durationSeconds")
            .GetSingle();
        var first = library.GetSourceFramesAtTime("idle", "right", 0.137f);
        var wrapped = library.GetSourceFramesAtTime(
            "idle", "right", duration + 0.137f);
        Assert.Equal(first, wrapped);
    }

    [Fact]
    public void IndependentGpuLayersPreserveLegacyCanvasPlacement()
    {
        using var library = SparseIndependentLayerLibrary2D.Load(ProductionLibraryPath);
        Assert.True(library.ActivateEquipment("right-hand-sword-a"));
        Assert.True(library.TryGetLayeredFrameAtTime(
            "idle", "right", 0f, out var layeredFrame));
        using var sparseShader = new SparseDepthCompositeShader2D(
            layeredFrame!,
            library.CanvasSize,
            library.GetRoot("idle", "right"),
            SKFilterMode.Nearest);

        using var characterColor = Texture2D.Load(Path.Combine(
            ContentRoot, "characters", "player", "animations", "idle", "color", "right", "frame-0001.png"));
        using var characterDepth = Texture2D.Load(Path.Combine(
            ContentRoot, "characters", "player", "animations", "idle", "depth", "right", "frame-0001.png"));
        using var weaponColor = Texture2D.Load(Path.Combine(
            ContentRoot, "weapons", "right-hand-sword-a", "animations", "idle", "right", "color", "frame-0001.png"));
        using var weaponDepth = Texture2D.Load(Path.Combine(
            ContentRoot, "weapons", "right-hand-sword-a", "animations", "idle", "right", "depth", "frame-0001.png"));
        using var full = DepthCompositeTexture2D.Create(
            new DepthTextureLayer2D(characterColor, characterDepth, "character"),
            new DepthTextureLayer2D(weaponColor, weaponDepth, "equipment"));
        var fullShader = new SpriteShader2D(full, SKFilterMode.Nearest);

        AssertEquivalentPixels(RenderShader(fullShader), RenderShader(sparseShader));
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
    public void WarmGpuAnimationFrameSwitchDoesNotAllocateManagedMemory()
    {
        using var package = SparseAnimationPackage2D.Load(
            Path.Combine(PackageRoot, "package.json"));
        var clip = package.CreateClip("walk", "right");
        Assert.True(clip.FrameCount >= 2);
        Assert.True(package.TryGetLayeredFrameAtTime(
            "walk",
            "right",
            clip[0].SourceTimeSeconds,
            out var firstFrame));
        Assert.True(package.TryGetLayeredFrameAtTime(
            "walk",
            "right",
            clip[1].SourceTimeSeconds,
            out var secondFrame));
        using var shader = new SparseDepthCompositeShader2D(
            firstFrame!,
            package.CanvasSize,
            package.GetRoot("walk", "right"),
            cacheEntryLimit: 2);
        var context = new ShaderContext(
            Matrix3x2.Identity,
            new Bounds2D(new Vector2(-256f, -192f), new Vector2(256f, 192f)),
            default);

        using (shader.AcquireShader(context))
        {
        }
        shader.SetFrame(
            secondFrame!,
            package.CanvasSize,
            package.GetRoot("walk", "right"));
        using (shader.AcquireShader(context))
        {
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            shader.SetFrame(
                iteration % 2 == 0 ? firstFrame! : secondFrame!,
                package.CanvasSize,
                package.GetRoot("walk", "right"));
            using var lease = shader.AcquireShader(context);
            if (lease.Shader is null)
                throw new InvalidOperationException("Expected a cached GPU shader.");
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(2, shader.CachedShaderCount);
    }

    [Fact]
    public void GpuAnimationShaderCacheEvictsLeastRecentlyUsedEntry()
    {
        using var package = SparseAnimationPackage2D.Load(
            Path.Combine(PackageRoot, "package.json"));
        var clip = package.CreateClip("walk", "right");
        Assert.True(clip.FrameCount >= 3);
        var frames = new SparseLayeredAnimationFrame2D[3];
        for (var index = 0; index < frames.Length; index++)
        {
            Assert.True(package.TryGetLayeredFrameAtTime(
                "walk",
                "right",
                clip[index].SourceTimeSeconds,
                out var frame));
            frames[index] = frame!;
        }

        using var shader = new SparseDepthCompositeShader2D(
            frames[0],
            package.CanvasSize,
            package.GetRoot("walk", "right"),
            cacheEntryLimit: 2);
        var context = new ShaderContext(
            Matrix3x2.Identity,
            new Bounds2D(new Vector2(-256f, -192f), new Vector2(256f, 192f)),
            default);
        SKShader? evictedShader;
        using (var first = shader.AcquireShader(context))
            evictedShader = first.Shader;
        shader.SetFrame(frames[1], package.CanvasSize, package.GetRoot("walk", "right"));
        using (shader.AcquireShader(context))
        {
        }
        shader.SetFrame(frames[2], package.CanvasSize, package.GetRoot("walk", "right"));
        using (shader.AcquireShader(context))
        {
        }
        Assert.Equal(2, shader.CachedShaderCount);

        shader.SetFrame(frames[0], package.CanvasSize, package.GetRoot("walk", "right"));
        using var reloaded = shader.AcquireShader(context);
        Assert.NotSame(evictedShader, reloaded.Shader);
        Assert.Equal(2, shader.CachedShaderCount);
    }

    [Fact]
    public void ProductionLibraryKeepsOnlyCharacterAndActiveEquipmentResident()
    {
        using var library = SparseIndependentLayerLibrary2D.Load(ProductionLibraryPath);
        Assert.True(library.ActivateEquipment("right-hand-sword-e"));
        Assert.True(library.TryGetLayeredFrameAtTime(
            "sword-attack", "right", 0.1f, out _));
        Assert.True(library.ActivateEquipment("right-hand-wand-a"));
        Assert.Equal("right-hand-wand-a", library.ActiveEquipmentId);
        Assert.True(library.TryGetLayeredFrameAtTime(
            "magic-shot", "right", 0.2f, out _));

        Assert.True(library.ResidentAtlasByteCount > 0);
        Assert.True(library.ActiveEquipmentResidentAtlasByteCount > 0);
        Assert.True(library.ActiveEquipmentResidentAtlasByteCount <= 2048L * 2048L * 6L);
    }

    [Fact]
    public void EveryProductionEquipmentPackageActivatesAndRenders()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ProductionLibraryPath));
        var equipmentIds = document.RootElement
            .GetProperty("equipment")
            .EnumerateObject()
            .Select(entry => entry.Name)
            .ToArray();
        Assert.Equal(14, equipmentIds.Length);

        using var library = SparseIndependentLayerLibrary2D.Load(ProductionLibraryPath);
        foreach (var equipmentId in equipmentIds)
        {
            Assert.True(library.ActivateEquipment(equipmentId), equipmentId);
            Assert.True(library.TryGetLayeredFrameAtTime(
                "idle", "right", 0f, out var frame), equipmentId);
            Assert.NotNull(frame);
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
    public void ProductionLibraryDeclaresIndependentLayerPackagesWithoutLoadoutCopies()
    {
        using var library = JsonDocument.Parse(File.ReadAllText(ProductionLibraryPath));
        var libraryRoot = library.RootElement;
        Assert.Equal(
            "sparse-independent-layer-library-v1",
            libraryRoot.GetProperty("format").GetString());
        Assert.Equal(
            "character/package.json",
            libraryRoot.GetProperty("character").GetProperty("package").GetString());
        var equipment = libraryRoot.GetProperty("equipment").EnumerateObject().ToArray();
        Assert.Equal(14, equipment.Length);
        foreach (var entry in equipment)
        {
            var package = entry.Value.GetProperty("package").GetString();
            Assert.StartsWith("equipment/", package, StringComparison.Ordinal);
            Assert.DoesNotContain("character", package, StringComparison.Ordinal);
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
