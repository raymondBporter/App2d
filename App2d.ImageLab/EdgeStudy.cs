using App2d.Core.Json;
using System.Text.Json;

namespace App2d.ImageLab;

internal static class EdgeStudy
{
    public static void Run(string sourcePath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var source = RasterFrame.Load(sourcePath);
        var measurements = new List<object>();
        foreach (var channel in new[] { ChannelSource.Luminance, ChannelSource.OklabLightness, ChannelSource.HsvValue })
        {
            foreach (var sigma in new[] { 1.2f, 2f })
            {
                var transform = new RasterTransform(TransformKind.Canny, channel, WeakEdge: .006f, StrongEdge: .025f, Smoothing: sigma);
                var frame = transform.Apply(source);
                var name = $"{channel}-blur{sigma:0.0}.png";
                frame.SavePng(Path.Combine(outputDirectory, name));
                measurements.Add(new { image = name, weak = transform.WeakEdge, strong = transform.StrongEdge, sigma,
                    edgePixels = frame.Mask!.AsSpan().ToArray().Count(static pixel => pixel) });
            }
        }
        File.WriteAllText(Path.Combine(outputDirectory, "settings.json"), JsonSerializer.Serialize(measurements, JsonProfiles.Indented));
        Console.WriteLine($"Edge study saved to {Path.GetFullPath(outputDirectory)}");
    }
}
