namespace App2d.ImageLab;

internal sealed record RasterStep(string Description, RasterFrame Frame);

internal sealed class RasterHistory
{
    private readonly List<RasterStep> _steps = [];
    public IReadOnlyList<RasterStep> Steps => _steps;
    public int Position { get; private set; } = -1;
    public RasterFrame? Current => Position < 0 ? null : _steps[Position].Frame;
    public bool CanUndo => Position > 0;
    public bool CanRedo => Position >= 0 && Position < _steps.Count - 1;

    public void Load(RasterFrame frame, string name)
    {
        var original = frame.Clone();
        _steps.Clear();
        _steps.Add(new($"Original · {name}", original));
        Position = 0;
    }

    public void Apply(RasterTransform transform, RasterFrame result) => Apply(transform.Description, result);

    public void Apply(string description, RasterFrame result)
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Load an image first.");
        }
        _steps.RemoveRange(Position + 1, _steps.Count - Position - 1);
        _steps.Add(new(description, result));
        Position++;
    }

    public void Select(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(position, _steps.Count);
        Position = position;
    }

    public void Undo()
    {
        if (CanUndo)
        {
            Position--;
        }
    }

    public void Redo()
    {
        if (CanRedo)
        {
            Position++;
        }
    }
}
