using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json;

namespace App2d.CharacterStudio.Importing;

/// <summary>Shared motion loaded from editable JSON. Sampling interpolates stored poses without allocating.</summary>
public sealed class PointLibrary
{
    public string Id { get; }
    public string Label { get; }
    public string Anatomy { get; }
    public IReadOnlyList<string> PointNames { get; }
    public IReadOnlyDictionary<string, PointClip> Clips { get; }
    public JsonElement Drawing { get; }
    public static PointLibrary Load(string manifestPath) => new(
        File.ReadAllText(manifestPath), id => File.ReadAllText(Path.Combine(Path.GetDirectoryName(manifestPath)!, "points", id + ".json")));

    public PointLibrary(string json, Func<string, string> readPoints)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 2 || root.GetProperty("format").GetString() != "app2d-point-library")
        {
            throw new InvalidDataException("Unsupported point library format.");
        }

        Id = root.GetProperty("id").GetString()!;
        Label = root.GetProperty("label").GetString()!;
        Anatomy = root.GetProperty("anatomy").GetString()!;
        PointNames = Array.AsReadOnly(root.GetProperty("pointNames").EnumerateArray().Select(p => p.GetString()!).ToArray());
        if (PointNames.Count is < 1 or > 4096 || PointNames.Any(string.IsNullOrWhiteSpace) || PointNames.Distinct().Count() != PointNames.Count)
        {
            throw new InvalidDataException("Invalid point names.");
        }

        Drawing = root.GetProperty("drawing").Clone();
        var clips = new Dictionary<string, PointClip>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("clips").EnumerateObject())
        {
            if (property.Name is "." or ".." || property.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidDataException($"Invalid clip name: {property.Name}.");
            }

            using var points = JsonDocument.Parse(readPoints(property.Name));
            clips.Add(property.Name, new PointClip(property.Name, property.Value, PointNames, points.RootElement));
        }

        if (clips.Count == 0)
        {
            throw new InvalidDataException("Library has no clips.");
        }

        Clips = new System.Collections.ObjectModel.ReadOnlyDictionary<string, PointClip>(clips);
    }
}

public sealed class PointClip
{
    private readonly Vector3[] _points;
    private readonly double[] _times;
    public string Id { get; }
    public string Label { get; }
    public string Category { get; }
    public string Source { get; }
    public bool Loop { get; }
    public bool Placeholder { get; }
    public double Duration { get; }
    public int PointCount { get; }
    public int SampleCount => _times.Length;
    public IReadOnlyList<double> Times { get; }
    public IReadOnlyList<string> Warnings { get; }
    public JsonElement Metadata { get; }

    internal PointClip(string id, JsonElement spec, IReadOnlyList<string> pointNames, JsonElement samples)
    {
        Id = id;
        Metadata = spec.Clone();
        PointCount = pointNames.Count;
        Label = spec.TryGetProperty("label", out var label) ? label.GetString()! : id;
        Category = spec.TryGetProperty("category", out var category) ? category.GetString()! : "Motions";
        Source = spec.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String ? source.GetString()! : "";
        Placeholder = spec.TryGetProperty("placeholder", out var placeholder) && placeholder.GetBoolean();
        Loop = spec.GetProperty("loop").GetBoolean();
        Duration = spec.GetProperty("duration").GetDouble();
        _times = [.. spec.GetProperty("times").EnumerateArray().Select(t => t.GetDouble())];
        Times = Array.AsReadOnly(_times);
        Warnings = Array.AsReadOnly(spec.TryGetProperty("warnings", out var warnings)
            ? warnings.EnumerateArray().Select(w => w.GetString()!).ToArray() : []);
        if (!double.IsFinite(Duration) || Duration <= 0 || _times.Length < 2 ||
            _times[0] != 0 ||
            Math.Abs(_times[^1] - Duration) > 1e-8)
        {
            throw new InvalidDataException($"Invalid timing in {id}.");
        }

        for (var i = 1; i < _times.Length; i++)
        {
            if (!double.IsFinite(_times[i]) || _times[i] <= _times[i - 1])
            {
                throw new InvalidDataException($"Invalid timestamps in {id}.");
            }
        }

        if (samples.ValueKind != JsonValueKind.Object || samples.EnumerateObject().Count() != PointCount)
        {
            throw new InvalidDataException($"Invalid point names in {id}.");
        }

        _points = new Vector3[checked(SampleCount * PointCount)];
        for (var point = 0; point < PointCount; point++)
        {
            var name = pointNames[point];
            if (!samples.TryGetProperty(name, out var positions) || positions.ValueKind != JsonValueKind.Array || positions.GetArrayLength() != SampleCount)
            {
                throw new InvalidDataException($"Invalid samples for {id}/{name}.");
            }

            var sample = 0;
            foreach (var position in positions.EnumerateArray())
            {
                if (position.ValueKind != JsonValueKind.Array || position.GetArrayLength() != 3 ||
                    position.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.Number || !v.TryGetSingle(out var coordinate) || !float.IsFinite(coordinate)))
                {
                    throw new InvalidDataException($"Invalid XYZ position for {id}/{name} at sample {sample}.");
                }

                _points[sample++ * PointCount + point] = new(position[0].GetSingle(), position[1].GetSingle(), position[2].GetSingle());
            }
        }
    }

    public void Sample(double seconds, Span<Vector3> output, bool holdEnd = false)
    {
        ArgGuard.Finite(seconds);
        ArgGuard.ThrowIf(output.Length != PointCount, "Wrong pose size.", nameof(output));
        var t = Loop && !holdEnd ? ((seconds % Duration) + Duration) % Duration : Math.Clamp(seconds, 0, Duration);
        if (t == 0 || ((!Loop || holdEnd) && seconds >= Duration))
        {
            var sample = t == 0 ? 0 : _times.Length - 1;
            for (var i = 0; i < PointCount; i++)
            {
                output[i] = Read(sample, i);
            }

            return;
        }
        var lo = 0;
        var hi = _times.Length - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (_times[mid] <= t)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }
        var mix = (float)((t - _times[lo]) / (_times[hi] - _times[lo]));
        for (var i = 0; i < PointCount; i++)
        {
            output[i] = Vector3.Lerp(Read(lo, i), Read(hi, i), mix);
        }
    }

    public Vector3 Read(int sample, int point)
    {
        ArgGuard.Index(sample, SampleCount);
        ArgGuard.Index(point, PointCount);
        return _points[sample * PointCount + point];
    }
}
