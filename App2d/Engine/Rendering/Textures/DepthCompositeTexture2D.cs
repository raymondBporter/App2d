using SkiaSharp;

namespace App2d.Engine.Rendering.Textures;

/// <summary>
/// Depth-composites independently rendered layers once, producing an ordinary
/// texture that is safe to draw on a raster-backed canvas.
/// </summary>
public static class DepthCompositeTexture2D
{
    private const int MaxStackLayers = 64;

    public static Texture2D Create(params DepthTextureLayer2D[] layers)
    {
        ArgGuard.ThrowIfNull(layers);
        ArgGuard.ThrowIfTooShort(layers.AsSpan(), 1);
        ValidateFullCanvasLayers(layers);

        Span<int> sortRanks = layers.Length <= MaxStackLayers
            ? stackalloc int[layers.Length]
            : new int[layers.Length];
        for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
        {
            var layerId = GetLayerId(layers[layerIndex]);
            var rank = 0;
            for (var candidateIndex = 0; candidateIndex < layers.Length; candidateIndex++)
            {
                if (StringComparer.Ordinal.Compare(
                        GetLayerId(layers[candidateIndex]),
                        layerId) < 0)
                {
                    rank++;
                }
            }
            sortRanks[layerIndex] = rank;
        }

        var result = Texture2D.CreateUninitializedGenerated(
            $"depth-composite:{layers.Length}:{layers[0].Color.SourcePath}",
            layers[0].Color.Width,
            layers[0].Color.Height);
        try
        {
            Span<LayerPixel> ordered = layers.Length <= MaxStackLayers
                ? stackalloc LayerPixel[layers.Length]
                : new LayerPixel[layers.Length];
            var output = result.WritablePixelSpan;
            for (var pixelIndex = 0; pixelIndex < output.Length; pixelIndex++)
            {
                var activeCount = 0;
                for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                {
                    var color = layers[layerIndex].Color.GetPixelUnchecked(pixelIndex);
                    if (color.Alpha == 0)
                        continue;

                    InsertBackToFront(
                        ordered,
                        ref activeCount,
                        new LayerPixel(
                            DecodeDepth(layers[layerIndex].Depth.GetPixelUnchecked(pixelIndex)),
                            sortRanks[layerIndex],
                            color));
                }
                output[pixelIndex] = Compose(ordered, activeCount);
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    public static RootedTexture2D CreateSparse(
        IReadOnlyList<SparseDepthTextureLayer2D> layers,
        SparseCompositionContext2D context,
        SparseCompositionOptions2D? options = null)
    {
        ArgGuard.ThrowIfNull(layers);
        options ??= new SparseCompositionOptions2D();
        ValidateSparseLayers(layers, context, options);

        var firstLayer = layers[0];
        var left = (long)firstLayer.Origin.X;
        var top = (long)firstLayer.Origin.Y;
        var right = left + firstLayer.AtlasRectangle.Width;
        var bottom = top + firstLayer.AtlasRectangle.Height;
        for (var layerIndex = 1; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            left = Math.Min(left, layer.Origin.X);
            top = Math.Min(top, layer.Origin.Y);
            right = Math.Max(right, (long)layer.Origin.X + layer.AtlasRectangle.Width);
            bottom = Math.Max(bottom, (long)layer.Origin.Y + layer.AtlasRectangle.Height);
        }
        var width64 = right - left;
        var height64 = bottom - top;
        if (width64 <= 0 || height64 <= 0 ||
            width64 > options.MaxWidth || height64 > options.MaxHeight ||
            width64 * height64 > options.MaxPixelCount)
        {
            throw Failure(
                context,
                null,
                $"composition output {width64}x{height64} exceeds the configured " +
                $"limit {options.MaxWidth}x{options.MaxHeight} and " +
                $"{options.MaxPixelCount} pixels");
        }

        var width = (int)width64;
        var height = (int)height64;
        Span<int> sortRanks = layers.Count <= MaxStackLayers
            ? stackalloc int[layers.Count]
            : new int[layers.Count];
        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var rank = 0;
            for (var candidateIndex = 0; candidateIndex < layers.Count; candidateIndex++)
            {
                if (StringComparer.Ordinal.Compare(
                        layers[candidateIndex].LayerId,
                        layers[layerIndex].LayerId) < 0)
                {
                    rank++;
                }
            }
            sortRanks[layerIndex] = rank;
        }

        var sourceName =
            $"sparse-depth-composite:{context.AnimationId}:{context.FacingId}:" +
            $"{context.SampleIndex}:{layers.Count}";
        var trim = options.AlphaTrim
            ? FindSparseAlphaBounds(
                layers,
                left,
                top,
                width,
                height,
                options.AlphaTrimPadding)
            : new SKRectI(0, 0, width, height);
        var texture = Texture2D.CreateUninitializedGenerated(
            sourceName,
            trim.Width,
            trim.Height);
        try
        {
            Span<LayerPixel> ordered = layers.Count <= MaxStackLayers
                ? stackalloc LayerPixel[layers.Count]
                : new LayerPixel[layers.Count];
            var output = texture.WritablePixelSpan;
            for (var outputY = 0; outputY < trim.Height; outputY++)
            {
                var rootY = top + trim.Top + outputY;
                for (var outputX = 0; outputX < trim.Width; outputX++)
                {
                    var rootX = left + trim.Left + outputX;
                    var activeCount = 0;
                    for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
                    {
                        var layer = layers[layerIndex];
                        var localX = rootX - layer.Origin.X;
                        var localY = rootY - layer.Origin.Y;
                        if ((ulong)localX >= (ulong)layer.AtlasRectangle.Width ||
                            (ulong)localY >= (ulong)layer.AtlasRectangle.Height)
                        {
                            continue;
                        }

                        var atlasX = layer.AtlasRectangle.Left + (int)localX;
                        var atlasY = layer.AtlasRectangle.Top + (int)localY;
                        var color = layer.ColorAtlas.GetPixelUnchecked(
                            atlasY * layer.ColorAtlas.Width + atlasX);
                        if (color.Alpha == 0)
                            continue;

                        var depth = layer.DepthAtlas.Pixels[
                            atlasY * layer.DepthAtlas.Width + atlasX];
                        InsertBackToFront(
                            ordered,
                            ref activeCount,
                            new LayerPixel(depth, sortRanks[layerIndex], color));
                    }

                    output[outputY * trim.Width + outputX] =
                        Compose(ordered, activeCount);
                }
            }

            var origin = new SKPointI(
                checked((int)left + trim.Left),
                checked((int)top + trim.Top));
            return new RootedTexture2D(texture, origin);
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    private static SKRectI FindSparseAlphaBounds(
        IReadOnlyList<SparseDepthTextureLayer2D> layers,
        long compositionLeft,
        long compositionTop,
        int width,
        int height,
        int padding)
    {
        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;
        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            var rectangle = layer.AtlasRectangle;
            for (var localY = 0; localY < rectangle.Height; localY++)
            {
                var atlasRow = (rectangle.Top + localY) * layer.ColorAtlas.Width;
                for (var localX = 0; localX < rectangle.Width; localX++)
                {
                    if (layer.ColorAtlas.GetPixelUnchecked(
                            atlasRow + rectangle.Left + localX).Alpha == 0)
                    {
                        continue;
                    }

                    var outputX = checked((int)(layer.Origin.X - compositionLeft + localX));
                    var outputY = checked((int)(layer.Origin.Y - compositionTop + localY));
                    left = Math.Min(left, outputX);
                    top = Math.Min(top, outputY);
                    right = Math.Max(right, outputX);
                    bottom = Math.Max(bottom, outputY);
                }
            }
        }

        if (right < left)
            return new SKRectI(0, 0, 1, 1);
        return new SKRectI(
            Math.Max(0, left - padding),
            Math.Max(0, top - padding),
            Math.Min(width, right + padding + 1),
            Math.Min(height, bottom + padding + 1));
    }

    private static void InsertBackToFront(
        Span<LayerPixel> ordered,
        ref int activeCount,
        LayerPixel value)
    {
        var insertionIndex = activeCount;
        while (insertionIndex > 0 && Compare(ordered[insertionIndex - 1], value) > 0)
        {
            ordered[insertionIndex] = ordered[insertionIndex - 1];
            insertionIndex--;
        }
        ordered[insertionIndex] = value;
        activeCount++;
    }

    private static int Compare(LayerPixel first, LayerPixel second)
    {
        var depthComparison = first.Depth.CompareTo(second.Depth);
        return depthComparison != 0
            ? depthComparison
            : first.SortRank.CompareTo(second.SortRank);
    }

    private static SKColor Compose(ReadOnlySpan<LayerPixel> ordered, int activeCount)
    {
        if (activeCount == 0)
            return new SKColor(0, 0, 0, 0);
        if (activeCount == 1 || ordered[activeCount - 1].Color.Alpha == byte.MaxValue)
            return ordered[activeCount - 1].Color;

        var outputRed = 0d;
        var outputGreen = 0d;
        var outputBlue = 0d;
        var outputAlpha = 0d;
        for (var layerIndex = 0; layerIndex < activeCount; layerIndex++)
        {
            var front = ordered[layerIndex].Color;
            var frontAlpha = front.Alpha / 255d;
            var inverseFrontAlpha = 1d - frontAlpha;
            var nextAlpha = frontAlpha + outputAlpha * inverseFrontAlpha;
            if (nextAlpha <= 0d)
                continue;

            outputRed =
                (front.Red / 255d * frontAlpha +
                 outputRed * outputAlpha * inverseFrontAlpha) /
                nextAlpha;
            outputGreen =
                (front.Green / 255d * frontAlpha +
                 outputGreen * outputAlpha * inverseFrontAlpha) /
                nextAlpha;
            outputBlue =
                (front.Blue / 255d * frontAlpha +
                 outputBlue * outputAlpha * inverseFrontAlpha) /
                nextAlpha;
            outputAlpha = nextAlpha;
        }

        return new SKColor(
            ToByte(outputRed),
            ToByte(outputGreen),
            ToByte(outputBlue),
            ToByte(outputAlpha));
    }

    private static int DecodeDepth(SKColor color) =>
        color.Red << 8 | color.Green;

    private static string GetLayerId(DepthTextureLayer2D layer) =>
        layer.LayerId ?? layer.Color.SourcePath;

    private static byte ToByte(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0d, 1d) * 255d, MidpointRounding.ToEven);

    private static void ValidateFullCanvasLayers(DepthTextureLayer2D[] layers)
    {
        ArgGuard.ThrowIfNull(layers[0].Color);
        ArgGuard.ThrowIfNull(layers[0].Depth);
        var width = layers[0].Color.Width;
        var height = layers[0].Color.Height;
        foreach (var layer in layers)
        {
            ArgGuard.ThrowIfNull(layer.Color);
            ArgGuard.ThrowIfNull(layer.Depth);
            StateGuard.ThrowIf(
                layer.Color.Width != width || layer.Color.Height != height ||
                layer.Depth.Width != width || layer.Depth.Height != height,
                "Every full-canvas depth-composite layer must have identical pixel dimensions.");
        }
    }

    private static void ValidateSparseLayers(
        IReadOnlyList<SparseDepthTextureLayer2D> layers,
        SparseCompositionContext2D context,
        SparseCompositionOptions2D options)
    {
        if (layers.Count == 0)
            throw Failure(context, null, "missing animation layers");
        if (options.MaxWidth <= 0 || options.MaxHeight <= 0 ||
            options.MaxPixelCount <= 0 || options.AlphaTrimPadding < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Composition limits must be positive and trim padding cannot be negative.");
        }

        for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            var layer = layers[layerIndex];
            if (string.IsNullOrWhiteSpace(layer.LayerId))
                throw Failure(context, "<unknown>", "layer ID is missing");
            for (var priorIndex = 0; priorIndex < layerIndex; priorIndex++)
            {
                if (string.Equals(
                        layers[priorIndex].LayerId,
                        layer.LayerId,
                        StringComparison.Ordinal))
                {
                    throw Failure(context, layer.LayerId, "layer ID is duplicated");
                }
            }
            if (layer.ColorAtlas is null)
                throw Failure(context, layer.LayerId, "color atlas data is missing");
            if (layer.DepthAtlas is null)
                throw Failure(context, layer.LayerId, "depth atlas data is missing");
            if (layer.ColorAtlas.Width != layer.DepthAtlas.Width ||
                layer.ColorAtlas.Height != layer.DepthAtlas.Height)
            {
                throw Failure(context, layer.LayerId, "color/depth atlas dimensions do not match");
            }
            var rectangle = layer.AtlasRectangle;
            if (rectangle.Width <= 0 || rectangle.Height <= 0 ||
                rectangle.Left < 0 || rectangle.Top < 0 ||
                rectangle.Right > layer.ColorAtlas.Width ||
                rectangle.Bottom > layer.ColorAtlas.Height)
            {
                throw Failure(
                    context,
                    layer.LayerId,
                    $"atlas rectangle [{rectangle.Left},{rectangle.Top}," +
                    $"{rectangle.Width},{rectangle.Height}] is outside its atlas");
            }
        }
    }

    private static InvalidDataException Failure(
        SparseCompositionContext2D context,
        string? layerId,
        string message)
    {
        var layer = layerId is null ? string.Empty : $", layer '{layerId}'";
        return new InvalidDataException(
            $"Sparse composition failed for animation '{context.AnimationId}', " +
            $"facing '{context.FacingId}', sample {context.SampleIndex}{layer}: {message}.");
    }

    private readonly record struct LayerPixel(int Depth, int SortRank, SKColor Color);
}

public readonly record struct DepthTextureLayer2D(
    Texture2D Color,
    Texture2D Depth,
    string? LayerId = null);

public readonly record struct SparseDepthTextureLayer2D(
    string LayerId,
    Texture2D ColorAtlas,
    DepthAtlas2D DepthAtlas,
    SKRectI AtlasRectangle,
    SKPointI Origin);

public readonly record struct RootedTexture2D(Texture2D Texture, SKPointI Origin);

public readonly record struct SparseCompositionContext2D(
    string AnimationId,
    string FacingId,
    int SampleIndex);

public sealed record SparseCompositionOptions2D
{
    public int MaxWidth { get; init; } = 4096;
    public int MaxHeight { get; init; } = 4096;
    public long MaxPixelCount { get; init; } = 16L * 1024L * 1024L;
    public bool AlphaTrim { get; init; } = true;
    public int AlphaTrimPadding { get; init; }
}
