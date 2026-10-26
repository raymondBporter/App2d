using System.Diagnostics;

namespace App2d.ImageLab;

// One worker at a time; slider changes replace the pending request. Stale results never become applicable.
internal sealed class RasterPreview
{
    private sealed record Request(int Version, RasterFrame Source, RasterTransform Transform);
    private sealed record Result(RasterFrame? Frame, string? Error);
    private Request? _requested;
    private Request? _running;
    private Task<Result>? _task;
    private readonly RasterTransformProcessor _processor = new();
    private long _changedAt;
    private int _debounceMilliseconds = 160;
    private int _version;
    private int _completedVersion;

    public RasterFrame? Frame { get; private set; }
    public string? Error { get; private set; }
    public bool Pending => _requested is not null && _completedVersion != _requested.Version;

    public void Change(RasterFrame? source, RasterTransform transform)
    {
        var previous = _requested;
        var sameRegions = previous is not null
            && previous.Transform.Kind is TransformKind.LargestRegion or TransformKind.RemoveSmallRegions
            && transform.Kind is TransformKind.LargestRegion or TransformKind.RemoveSmallRegions
            && previous.Transform.EightConnected == transform.EightConnected;
        _debounceMilliseconds = previous is not null && ReferenceEquals(previous.Source, source)
            && (sameRegions || (transform.Kind == TransformKind.Canny && previous.Transform.Kind == TransformKind.Canny
            && transform.Channel == previous.Transform.Channel && transform.Smoothing == previous.Transform.Smoothing)) ? 35 : 160;
        _version++;
        Frame = null;
        Error = source is null ? null : transform.UnavailableReason(source);
        _requested = source is null || Error is not null ? null : new(_version, source, transform);
        _changedAt = Stopwatch.GetTimestamp();
        if (_task is null)
        {
            _processor.KeepSource(source);
        }
    }

    public void Poll()
    {
        if (_task is { IsCompleted: true })
        {
            var result = _task.GetAwaiter().GetResult();
            if (_running!.Version == _requested?.Version)
            {
                Frame = result.Frame;
                Error = result.Error;
                _completedVersion = _running.Version;
            }
            _task = null;
            _running = null;
            _processor.KeepSource(_requested?.Source);
        }
        if (_task is not null || !Pending || Stopwatch.GetElapsedTime(_changedAt).TotalMilliseconds < _debounceMilliseconds)
        {
            return;
        }
        _running = _requested!;
        var request = _running;
        _task = Task.Run(() =>
        {
            try
            {
                return new Result(_processor.Apply(request.Source, request.Transform), null);
            }
            catch (Exception error)
            {
                return new Result(null, error.Message);
            }
        });
    }
}
