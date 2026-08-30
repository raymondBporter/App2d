namespace App2d.Engine.Collision.BroadPhase;

public readonly record struct BroadPhasePair2D<T>(T First, T Second)
    where T : class;
