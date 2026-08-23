using App2d.Engine.Tiles;

namespace App2d.Gameplay;

public sealed class JumpableWorldGenerator2D
{
    private const int TerrainSectionWidth = 12;
    private const int VerticalRegionWidth = 64;
    private const int SidePlatformSectionWidth = 8;
    private const int FirstPlatformRow = 3;
    private const int PlatformRowSpacing = 2;

    private readonly SpatialRandom2D _random;

    public JumpableWorldGenerator2D(ulong seed, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        _random = new SpatialRandom2D(seed);
    }

    public int Width { get; }
    public int Height { get; }

    public bool IsSolid(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return false;

        // Closed world edges are generated just like every other chunk rather
        // than becoming two world-height objects kept alive forever.
        if (x == 0 || x == Width - 1)
            return true;

        if (y < TerrainHeight(x) && !IsJumpablePit(x))
            return true;

        return IsVerticalPlatform(x, y);
    }

    public int TerrainHeight(int x)
    {
        x = Math.Clamp(x, 0, Width - 1);
        var section = Math.DivRem(x, TerrainSectionWidth, out var localX);
        var first = _random.Range(section, 0, 2, 5, channel: 1);
        var second = _random.Range(section + 1, 0, 2, 5, channel: 1);
        var blend = localX / (float)TerrainSectionWidth;
        blend = blend * blend * (3f - 2f * blend);
        return (int)MathF.Round(float.Lerp(first, second, blend));
    }

    private bool IsJumpablePit(int x)
    {
        // At most three missing columns: comfortably inside the measured running
        // jump, with the first/last sections kept safe for spawn and goal.
        if (x < TerrainSectionWidth || x >= Width - TerrainSectionWidth)
            return false;

        var section = Math.DivRem(x, TerrainSectionWidth, out var localX);
        if (_random.Unit(section, 0, channel: 2) >= 0.32f)
            return false;

        var pitWidth = _random.Range(section, 0, 1, 4, channel: 3);
        var pitStart = _random.Range(
            section,
            0,
            3,
            TerrainSectionWidth - pitWidth - 2,
            channel: 4);
        return localX >= pitStart && localX < pitStart + pitWidth;
    }

    private bool IsVerticalPlatform(int x, int y)
    {
        if (y < FirstPlatformRow || (y - FirstPlatformRow) % PlatformRowSpacing != 0)
            return false;

        var platformRow = (y - FirstPlatformRow) / PlatformRowSpacing;
        var region = Math.DivRem(x, VerticalRegionWidth, out var localX);
        var maximumRows = Math.Max(1, (Height - FirstPlatformRow + 1) / PlatformRowSpacing);
        var towerRows = _random.Range(
            region,
            0,
            Math.Min(8, maximumRows),
            maximumRows + 1,
            channel: 20);
        if (platformRow >= towerRows)
            return false;

        // Every vertical region has one continuous climb. Its center moves by at
        // most one tile per tier and its ledges overlap, so the generated route is
        // always jumpable even though its silhouette and phase are seed-driven.
        var spineCenter = GetSpineCenter(region, platformRow);
        var spineWidth = _random.Range(region, platformRow, 5, 8, channel: 21);
        var spineStart = spineCenter - spineWidth / 2;
        if (localX >= spineStart && localX < spineStart + spineWidth)
            return true;

        // Optional side ledges make the shape genuinely two-dimensional. They
        // are sparse, stay near the guaranteed spine, and never replace it.
        var section = localX / SidePlatformSectionWidth;
        var globalSection = region * (VerticalRegionWidth / SidePlatformSectionWidth) + section;
        if (_random.Unit(globalSection, platformRow, channel: 22) >= 0.34f)
            return false;

        var sectionCenter = section * SidePlatformSectionWidth + SidePlatformSectionWidth / 2;
        if (Math.Abs(sectionCenter - spineCenter) > SidePlatformSectionWidth * 2)
            return false;

        var inset = _random.Range(globalSection, platformRow, 0, 3, channel: 23);
        var width = _random.Range(globalSection, platformRow, 3, 7, channel: 24);
        var start = section * SidePlatformSectionWidth + inset;
        return localX >= start &&
            localX < Math.Min((section + 1) * SidePlatformSectionWidth, start + width);
    }

    private int GetSpineCenter(int region, int platformRow)
    {
        var center = _random.Range(region, 0, 22, 43, channel: 25);
        var amplitude = _random.Range(region, 0, 5, 11, channel: 26);
        var period = amplitude * 4;
        var phase = _random.Range(region, 0, 0, period, channel: 27);
        var position = (platformRow + phase) % period;
        int offset;
        if (position < amplitude)
            offset = position;
        else if (position < amplitude * 3)
            offset = amplitude * 2 - position;
        else
            offset = position - amplitude * 4;
        return center + offset;
    }
}
