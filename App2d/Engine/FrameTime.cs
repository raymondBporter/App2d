namespace App2d.Engine;

public readonly record struct FrameTime(
    float DeltaSeconds,
    double TotalSeconds,
    long FrameNumber);
