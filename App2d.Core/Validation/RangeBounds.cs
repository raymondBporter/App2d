namespace App2d.Core.Validation;

/// <summary>Which endpoints belong to a range. Comparisons are exact; no epsilon is added.</summary>
public enum RangeBounds
{
    Closed,     // [minimum, maximum]
    Open,       // (minimum, maximum)
    ClosedOpen, // [minimum, maximum)
    OpenClosed  // (minimum, maximum]
}
