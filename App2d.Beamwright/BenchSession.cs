using App2d.Core.Json;
using App2d.Beamwright.Optics;
using System.Text.Json;

namespace App2d.Beamwright;

/// <summary>Desktop-independent puzzle state, inventory, undo, hints and local campaign progress.</summary>
public sealed class BenchSession
{
    private readonly List<PartPlacement> _placed = [];
    private readonly Stack<PartPlacement[]> _undo = [];
    private readonly BeamTracer _tracer;
    private readonly string? _savePath;
    private readonly ProgressData _progress;

    public BenchSession(BeamwrightContent? content = null, string? savePath = null)
    {
        Content = content ?? BeamwrightContent.Default;
        _tracer = new(Content);
        _savePath = savePath;
        _progress = ReadProgress(savePath);
        Placed = _placed.AsReadOnly();
        LoadLevel(Math.Clamp(_progress.LastLevel, 0, Content.Levels.Length - 1));
    }

    public BeamwrightContent Content { get; }
    public BenchLevel Level { get; private set; } = null!;
    public int LevelIndex { get; private set; }
    public IReadOnlyList<PartPlacement> Placed { get; }
    public TraceResult Result { get; private set; } = null!;
    public int HintCount { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool HasSaveError { get; private set; }
    public bool Completed(int index) => _progress.BestParts.ContainsKey(index);
    public int? BestParts(int index) => _progress.BestParts.TryGetValue(index, out var best) ? best : null;

    public void LoadLevel(int index)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Content.Levels.Length);
        LevelIndex = index;
        Level = index == -1 ? BeamwrightContent.FreeBench : Content.Levels[index];
        _placed.Clear();
        _undo.Clear();
        HintCount = 0;
        if (index >= 0) _progress.LastLevel = index;
        Retrace();
        SaveProgress();
    }

    public int Available(string type) => Level.Sandbox ? int.MaxValue :
        Level.Inv.GetValueOrDefault(type) - _placed.Count(p => p.Type == type);

    public bool CanOccupy(int x, int y, int ignore = -1)
    {
        if (x < 0 || x >= Level.W || y < 0 || y >= Level.H) return false;
        return !Level.Walls.Any(w => w[0] == x && w[1] == y)
            && !Level.Sources.Any(s => s.X == x && s.Y == y)
            && !Level.Targets.Any(t => t.X == x && t.Y == y)
            && !Level.Fixed.Any(p => p.X == x && p.Y == y)
            && !Level.Gates.Any(g => g.X == x)
            && !Level.Mirage.Any(z => x >= z.X && x < z.X + z.W && y >= z.Y && y < z.Y + z.H)
            && !_placed.Where((_, i) => i != ignore).Any(p => p.X == x && p.Y == y);
    }

    public int At(int x, int y) => _placed.FindIndex(p => p.X == x && p.Y == y);

    public bool TryPlace(string type, int x, int y, float? angle = null)
    {
        if (Available(type) <= 0 || !CanOccupy(x, y)) return false;
        var part = Content.Part(type);
        Snapshot();
        _placed.Add(new() { Type = type, X = x, Y = y, Angle = angle ?? part.DefaultAngle });
        Retrace();
        return true;
    }

    public bool TryMove(int index, int x, int y)
    {
        if (index < 0 || index >= _placed.Count || !CanOccupy(x, y, index)) return false;
        if (_placed[index].X == x && _placed[index].Y == y) return true;
        Snapshot();
        _placed[index] = _placed[index] with { X = x, Y = y };
        Retrace();
        return true;
    }

    public void Rotate(int index, float degrees)
    {
        if (index < 0 || index >= _placed.Count) return;
        Snapshot();
        var angle = ((_placed[index].Angle + degrees) % 360 + 360) % 360;
        _placed[index] = _placed[index] with { Angle = angle };
        Retrace();
    }

    public void Remove(int index)
    {
        if (index < 0 || index >= _placed.Count) return;
        Snapshot();
        _placed.RemoveAt(index);
        Retrace();
    }

    public void Reset()
    {
        if (_placed.Count > 0) Snapshot();
        _placed.Clear();
        HintCount = 0;
        Retrace();
    }

    public void Undo()
    {
        if (!_undo.TryPop(out var previous)) return;
        _placed.Clear();
        _placed.AddRange(previous);
        Retrace();
    }

    public void RevealHint() => HintCount = Math.Min(HintCount + 1, Level.Solution.Length);

    private void Snapshot() => _undo.Push([.. _placed]);

    private void Retrace()
    {
        Result = _tracer.Trace(Level, _placed);
        if (Result.Win && LevelIndex >= 0 && (!_progress.BestParts.TryGetValue(LevelIndex, out var best) || _placed.Count < best))
        {
            _progress.BestParts[LevelIndex] = _placed.Count;
            SaveProgress();
        }
    }

    private void SaveProgress()
    {
        if (_savePath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_savePath))!);
            var temporary = _savePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_progress, JsonProfiles.Indented));
            File.Move(temporary, _savePath, true);
            HasSaveError = false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { HasSaveError = true; }
    }

    private static ProgressData ReadProgress(string? path)
    {
        if (path is null) return new();
        try
        {
            var progress = JsonSerializer.Deserialize<ProgressData>(File.ReadAllText(path), JsonProfiles.Indented) ?? new();
            progress.BestParts ??= [];
            return progress;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    private sealed class ProgressData
    {
        public int LastLevel { get; set; }
        public Dictionary<int, int> BestParts { get; set; } = [];
    }
}
