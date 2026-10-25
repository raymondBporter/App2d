using App2d.Core.Imaging;
using System.Diagnostics;
using System.Text.Json;

namespace App2d.ImageLab;

internal static class EdgeBenchmark
{
    public static void Run(string sourcePath, string outputPath)
    {
        var source = RasterFrame.Load(sourcePath);
        var transform = new RasterTransform(TransformKind.Canny, WeakEdge: .006f, StrongEdge: .025f, Smoothing: 2);
        var channel = source.GetChannel();
        var blurred = channel.GaussianBlur(transform.Smoothing);
        var strengths = channel.CannyEdgeStrengths(transform.Smoothing);
        var processor = new RasterTransformProcessor();
        var coldStart = Stopwatch.GetTimestamp();
        _ = processor.Apply(source, transform);
        var coldPreviewMs = Stopwatch.GetElapsedTime(coldStart).TotalMilliseconds;
        var measurements = new Dictionary<string, double>
        {
            ["channelMs"] = Measure(() => source.GetChannel()),
            ["blurMs"] = Measure(() => channel.GaussianBlur(transform.Smoothing)),
            ["sobelMs"] = Measure(() => blurred.Sobel()),
            ["fullCannyMs"] = Measure(() => transform.Apply(source)),
            ["preparedStrengthsMs"] = Measure(() => channel.CannyEdgeStrengths(transform.Smoothing)),
            ["linkEdgesMs"] = Measure(() => strengths.HysteresisThreshold(transform.WeakEdge, transform.StrongEdge)),
            ["firstPreviewMs"] = Math.Round(coldPreviewMs, 2),
            ["cachedCutoffMs"] = Measure(() => processor.Apply(source, transform))
        };
        var report = JsonSerializer.Serialize(new { source.Width, source.Height, configuration =
#if DEBUG
            "Debug",
#else
            "Release",
#endif
            medianOf = 5, vectorLanes = System.Numerics.Vector<float>.Count,
            vectorAccelerated = System.Numerics.Vector.IsHardwareAccelerated,
            cacheBytes = processor.CachedPixelBytes, preparations = processor.CannyPreparationCount,
            measurements }, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, report);
        Console.WriteLine(report);
    }

    private static double Measure<T>(Func<T> action)
    {
        GC.KeepAlive(action()); // Warm this exact stage before sampling.
        var samples = new double[5];
        for (var i = 0; i < samples.Length; i++)
        {
            var start = Stopwatch.GetTimestamp();
            var result = action();
            samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            GC.KeepAlive(result);
        }
        Array.Sort(samples);
        return Math.Round(samples[samples.Length / 2], 2);
    }
}
