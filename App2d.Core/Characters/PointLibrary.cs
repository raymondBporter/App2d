using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>Immutable shared motion storage. Sampling decodes only two poses, never a whole clip.</summary>
public sealed class PointLibrary
{
    private readonly byte[] _data;
    public string Id { get; }
    public string Label { get; }
    public string Anatomy { get; }
    public IReadOnlyList<string> PointNames { get; }
    public IReadOnlyDictionary<string, PointClip> Clips { get; }
    public JsonElement Drawing { get; }
    public int PackedBytes => _data.Length;

    public static PointLibrary Load(string manifestPath) => new(
        File.ReadAllText(manifestPath), File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(manifestPath)!, "points.bin")));

    public PointLibrary(string json, byte[] data)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1 || root.GetProperty("format").GetString() != "app2d-point-library")
            throw new InvalidDataException("Unsupported point library format.");
        if (!Convert.ToHexString(SHA256.HashData(data)).Equals(root.GetProperty("dataSha256").GetString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Point library data hash mismatch.");
        _data = (byte[])data.Clone();
        Id = root.GetProperty("id").GetString()!;
        Label = root.GetProperty("label").GetString()!;
        Anatomy = root.GetProperty("anatomy").GetString()!;
        PointNames = Array.AsReadOnly(root.GetProperty("pointNames").EnumerateArray().Select(p => p.GetString()!).ToArray());
        if (PointNames.Count is < 1 or > 4096 || PointNames.Distinct().Count() != PointNames.Count)
            throw new InvalidDataException("Invalid point names.");
        Drawing = root.GetProperty("drawing").Clone();
        var clips = new Dictionary<string, PointClip>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("clips").EnumerateObject())
            clips.Add(property.Name, new PointClip(property.Name, property.Value, PointNames.Count, _data));
        if (clips.Count == 0) throw new InvalidDataException("Library has no clips.");
        Clips = new System.Collections.ObjectModel.ReadOnlyDictionary<string, PointClip>(clips);
    }
}

public sealed class PointClip
{
    private readonly byte[] _data;
    private readonly int _offset, _coordinateBytes;
    private readonly Vector3 _origin;
    private readonly float _step;
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
    public int PackedBytes { get; }
    public IReadOnlyList<double> Times { get; }
    public IReadOnlyList<string> Warnings { get; }
    public JsonElement Metadata { get; }

    internal PointClip(string id, JsonElement spec, int pointCount, byte[] data)
    {
        Id = id; Metadata = spec.Clone(); PointCount = pointCount; _data = data;
        Label = spec.TryGetProperty("label", out var label) ? label.GetString()! : id;
        Category = spec.TryGetProperty("category", out var category) ? category.GetString()! : "Motions";
        Source = spec.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String ? source.GetString()! : "";
        Placeholder = spec.TryGetProperty("placeholder", out var placeholder) && placeholder.GetBoolean();
        Loop = spec.GetProperty("loop").GetBoolean(); Duration = spec.GetProperty("duration").GetDouble();
        _times = spec.GetProperty("times").EnumerateArray().Select(t => t.GetDouble()).ToArray();
        Times = Array.AsReadOnly(_times);
        Warnings = Array.AsReadOnly(spec.TryGetProperty("warnings", out var warnings)
            ? warnings.EnumerateArray().Select(w => w.GetString()!).ToArray() : []);
        if (!double.IsFinite(Duration) || Duration <= 0 || _times.Length < 2 ||
            _times.Length != spec.GetProperty("sampleCount").GetInt32() || _times[0] != 0 ||
            Math.Abs(_times[^1] - Duration) > 1e-8)
            throw new InvalidDataException($"Invalid timing in {id}.");
        for (var i = 1; i < _times.Length; i++)
            if (!double.IsFinite(_times[i]) || _times[i] <= _times[i - 1]) throw new InvalidDataException($"Invalid timestamps in {id}.");
        var encoding = spec.GetProperty("encoding");
        _coordinateBytes = encoding.GetProperty("type").GetString() switch
        {
            "uint16-le" => 2, "float32-le" => 4, _ => throw new InvalidDataException("Unsupported coordinate encoding.")
        };
        if (encoding.GetProperty("layout").GetString() != "sample,point,xyz") throw new InvalidDataException("Invalid coordinate layout.");
        var origin = encoding.GetProperty("origin").EnumerateArray().Select(v => v.GetSingle()).ToArray();
        if (origin.Length != 3 || origin.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Invalid coordinate origin.");
        _origin = new(origin[0], origin[1], origin[2]); _step = encoding.GetProperty("step").GetSingle();
        if (!float.IsFinite(_step) || _step <= 0) throw new InvalidDataException("Invalid quantization step.");
        _offset = spec.GetProperty("byteOffset").GetInt32(); PackedBytes = spec.GetProperty("byteLength").GetInt32();
        if (_offset < 0 || (long)SampleCount * pointCount * 3 * _coordinateBytes != PackedBytes || (long)_offset + PackedBytes > data.Length)
            throw new InvalidDataException($"Invalid packed range in {id}.");
        if (_coordinateBytes == 4)
            for (var i = _offset; i < _offset + PackedBytes; i += 4)
                if (!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(i, 4))))
                    throw new InvalidDataException($"Nonfinite point in {id}.");
    }

    public void Sample(double seconds, Span<Vector3> output, bool holdEnd = false)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (output.Length != PointCount) throw new ArgumentException("Wrong pose size.", nameof(output));
        var t = Loop && !holdEnd ? ((seconds % Duration) + Duration) % Duration : Math.Clamp(seconds, 0, Duration);
        if (t == 0 || ((!Loop || holdEnd) && seconds >= Duration))
        {
            var sample = t == 0 ? 0 : _times.Length - 1;
            for (var i = 0; i < PointCount; i++) output[i] = Read(sample, i);
            return;
        }
        var lo = 0; var hi = _times.Length - 1;
        while (hi - lo > 1) { var mid = (lo + hi) / 2; if (_times[mid] <= t) lo = mid; else hi = mid; }
        var mix = (float)((t - _times[lo]) / (_times[hi] - _times[lo]));
        for (var i = 0; i < PointCount; i++) output[i] = Vector3.Lerp(Read(lo, i), Read(hi, i), mix);
    }

    public Vector3 Read(int sample, int point)
    {
        if ((uint)sample >= SampleCount || (uint)point >= PointCount) throw new ArgumentOutOfRangeException(nameof(sample));
        var offset = _offset + (sample * PointCount + point) * 3 * _coordinateBytes;
        var span = _data.AsSpan(offset, 3 * _coordinateBytes);
        return _coordinateBytes == 4
            ? new(BinaryPrimitives.ReadSingleLittleEndian(span), BinaryPrimitives.ReadSingleLittleEndian(span[4..]), BinaryPrimitives.ReadSingleLittleEndian(span[8..]))
            : _origin + new Vector3(BinaryPrimitives.ReadUInt16LittleEndian(span), BinaryPrimitives.ReadUInt16LittleEndian(span[2..]), BinaryPrimitives.ReadUInt16LittleEndian(span[4..])) * _step;
    }
}
