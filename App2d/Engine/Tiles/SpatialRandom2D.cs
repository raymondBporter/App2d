namespace App2d.Engine.Tiles;

// Stateless coordinate random. A cell always receives the same value, so chunks
// can be generated, discarded, and regenerated in any order without seams.
public sealed class SpatialRandom2D(ulong seed)
{
    public ulong Seed { get; } = seed;

    public ulong Sample(int x, int y, int channel = 0)
    {
        var value = Seed;
        value ^= unchecked((ulong)(uint)x) * 0x9E3779B185EBCA87UL;
        value ^= unchecked((ulong)(uint)y) * 0xC2B2AE3D27D4EB4FUL;
        value ^= unchecked((ulong)(uint)channel) * 0x165667B19E3779F9UL;

        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ value >> 31;
    }

    public float Unit(int x, int y, int channel = 0) =>
        (Sample(x, y, channel) >> 40) * (1f / (1 << 24));

    public int Range(int x, int y, int minimum, int maximumExclusive, int channel = 0)
    {
        if (maximumExclusive <= minimum)
            throw new ArgumentOutOfRangeException(nameof(maximumExclusive));

        var range = (uint)(maximumExclusive - minimum);
        return minimum + (int)(Sample(x, y, channel) % range);
    }
}
