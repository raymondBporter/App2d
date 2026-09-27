namespace App2d.Core.Collision.BroadPhase;

public readonly record struct BroadPhasePair2D<T>(T First, T Second)
    where T : class;
