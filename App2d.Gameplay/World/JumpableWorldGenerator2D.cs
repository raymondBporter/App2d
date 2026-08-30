using App2d.Core;
using App2d.Tiles;

namespace App2d.Gameplay;

public sealed class JumpableWorldGenerator2D
{
    private const int TerrainSectionWidth = 20;
    private const int VerticalRegionWidth = 64;
    private const int SidePlatformSectionWidth = 8;
    private const int TopologyRegionWidth = 48;

    private readonly SpatialRandom2D _random;
    private readonly int _maximumPitWidth;
    private readonly int _platformRowSpacing;
    private readonly int _standingPassageTiles;

    public JumpableWorldGenerator2D(
        ulong seed,
        int width,
        int height,
        TraversalMetrics2D traversal)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        ArgGuard.ThrowIfNull(traversal);
        Width = width;
        Height = height;
        _random = new SpatialRandom2D(seed);
        _platformRowSpacing = traversal.ReliableJumpRiseTiles;
        _standingPassageTiles = traversal.StandingPassageTiles;
        var runningJumpTiles = traversal
            .MeasureJump(traversal.RunSpeed)
            .HorizontalDistance / traversal.TileSize;
        _maximumPitWidth = Math.Clamp((int)MathF.Floor(runningJumpTiles) - 2, 3, TerrainSectionWidth - 8);
    }

    public int Width { get; }
    public int Height { get; }

    public TileKind2D GetTileKind(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return TileKind2D.Empty;

        // Closed world edges are generated just like every other chunk rather
        // than becoming two world-height objects kept alive forever.
        if (x == 0 || x == Width - 1)
            return TileKind2D.Solid;

        if (y < TerrainHeight(x) && !IsJumpablePit(x))
            return TileKind2D.Solid;

        var topologyKind = GetTopologyKind(x, y);
        if (topologyKind != TileKind2D.Empty)
            return topologyKind;

        if (y == TerrainHeight(x) && IsSpikePatch(x))
            return TileKind2D.Spikes;

        return IsOneWayPlatform(x, y)
            ? TileKind2D.OneWay
            : TileKind2D.Empty;
    }

    public int TerrainHeight(int x)
    {
        x = Math.Clamp(x, 0, Width - 1);
        var section = Math.DivRem(x, TerrainSectionWidth, out var localX);
        var first = _random.Range(section, 0, 2, 8, channel: 1);
        var second = _random.Range(section + 1, 0, 2, 8, channel: 1);
        var blend = localX / (float)TerrainSectionWidth;
        blend = blend * blend * (3f - 2f * blend);
        return (int)MathF.Round(float.Lerp(first, second, blend));
    }

    private bool IsJumpablePit(int x)
    {
        // Pit width comes from measured running-jump reach with two tiles held
        // back for takeoff/landing error. First/last sections stay spawn-safe.
        if (x < TerrainSectionWidth || x >= Width - TerrainSectionWidth)
            return false;

        var section = Math.DivRem(x, TerrainSectionWidth, out var localX);
        if (_random.Unit(section, 0, channel: 2) >= 0.32f)
            return false;

        var pitWidth = _random.Range(
            section,
            0,
            2,
            _maximumPitWidth + 1,
            channel: 3);
        var pitStart = _random.Range(
            section,
            0,
            3,
            TerrainSectionWidth - pitWidth - 2,
            channel: 4);
        return localX >= pitStart && localX < pitStart + pitWidth;
    }

    private bool IsOneWayPlatform(int x, int y)
    {
        var region = Math.DivRem(x, VerticalRegionWidth, out var localX);
        var firstPlatformY = GetFirstPlatformY(region);
        if (y < firstPlatformY || (y - firstPlatformY) % _platformRowSpacing != 0)
            return false;

        var platformRow = (y - firstPlatformY) / _platformRowSpacing;
        var availableRows = Math.Max(
            1,
            (Height - firstPlatformY + _platformRowSpacing - 1) /
            _platformRowSpacing);
        var maximumRows = Math.Min(6, availableRows);
        var minimumRows = Math.Min(3, maximumRows);
        var towerRows = minimumRows == maximumRows
            ? maximumRows
            : _random.Range(
                region,
                0,
                minimumRows,
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

    private int GetFirstPlatformY(int region)
    {
        var spineX = Math.Clamp(
            region * VerticalRegionWidth + GetSpineCenter(region, 0),
            1,
            Width - 2);
        var groundSurfaceY = TerrainHeight(spineX);
        return groundSurfaceY + _platformRowSpacing - 1;
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

    private TileKind2D GetTopologyKind(int x, int y)
    {
        if (x < TerrainSectionWidth || x >= Width - TerrainSectionWidth)
            return TileKind2D.Empty;

        var region = Math.DivRem(x, TopologyRegionWidth, out var regionX);
        if (region > 0 && _random.Unit(region, 0, channel: 40) >= 0.72f)
            return TileKind2D.Empty;

        var startX = _random.Range(region, 0, 10, 19, channel: 41);
        var localX = regionX - startX;
        var style = _random.Range(region, 0, 0, 6, channel: 43);
        var anchorX = Math.Clamp(
            region * TopologyRegionWidth + startX,
            1,
            Width - 2);
        var groundSurfaceY = TerrainHeight(anchorX);
        var baseY = style is 3 or 5
            ? groundSurfaceY
            : groundSurfaceY + _standingPassageTiles;
        var localY = y - baseY;

        if (style == 5)
            return GetGripCourseKind(localX, localY);

        var isOccupied = style switch
        {
            // A two-cell-thick slab exercises all four outer corners and gives
            // the diagnostic tileset a continuous ceiling to draw.
            0 => localX >= 0 && localX < 9 && localY >= 0 && localY < 2,

            // A block with an open top notch adds two visible inner corners
            // without enclosing the player in generated terrain.
            1 => localX >= 0 && localX < 10 && localY >= 0 && localY < 4 &&
                !(localX >= 3 && localX < 7 && localY >= 2),

            // A hollow frame supplies outer and inner corners, walls, a floor,
            // and an underside in one compact visual sanity check.
            2 => localX >= 0 && localX < 10 && localY >= 0 && localY < 6 &&
                (localX == 0 || localX == 9 || localY == 0 || localY == 5),

            // Grounded two-wide steps produce repeated convex and concave
            // transitions while remaining useful as traversal geometry.
            3 => localX >= 0 && localX < 10 && localY >= 0 &&
                localY < 1 + localX / 2,

            // Three one-way balconies form a small rise-and-fall sequence. The
            // outer ledges sit at reliable jump height and the center is two
            // cells higher, creating variety without requiring a maximum jump.
            4 => localX >= 0 && localX < 12 &&
                ((localX < 4 || localX >= 8) && localY == 0 ||
                 localX >= 4 && localX < 8 && localY == 2),
            _ => false
        };

        if (!isOccupied)
            return TileKind2D.Empty;

        if (style == 4)
            return TileKind2D.OneWay;

        return style is 1 or 2 or 3
            ? TileKind2D.Solid | TileKind2D.Grippable
            : TileKind2D.Solid;
    }

    private bool IsSpikePatch(int x)
    {
        // Keep the opening and goal approaches safe. Elsewhere, short patches
        // are capped at three tiles so every generated hazard is jumpable.
        if (x < TerrainSectionWidth + 4 ||
            x >= Width - TerrainSectionWidth - 4 ||
            IsJumpablePit(x))
        {
            return false;
        }

        var section = Math.DivRem(x, TerrainSectionWidth, out var localX);
        if (_random.Unit(section, 0, channel: 60) >= 0.38f)
            return false;

        var width = _random.Range(section, 0, 1, 4, channel: 61);
        var start = _random.Range(
            section,
            0,
            4,
            TerrainSectionWidth - width - 3,
            channel: 62);
        return localX >= start && localX < start + width;
    }

    private static TileKind2D GetGripCourseKind(int x, int y)
    {
        var isPillar =
            y >= 0 && y < 9 &&
            (x >= 0 && x < 2 || x >= 5 && x < 7);
        if (isPillar)
            return TileKind2D.Solid | TileKind2D.Grippable;

        var isRestLedge =
            x >= 2 && x < 5 &&
            (y == 3 || y == 7);
        return isRestLedge
            ? TileKind2D.OneWay
            : TileKind2D.Empty;
    }
}
