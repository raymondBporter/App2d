namespace App2d.Core;

public readonly record struct FrameTime(float DeltaSeconds, double TotalSeconds, long FrameNumber);
