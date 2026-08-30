using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Assets;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using SkiaSharp;
using System.Numerics;
using System.Text.Json;

namespace App2d.Gameplay.World;

internal enum OneWayTilePart2D
{
    Standalone,
    Left,
    Middle,
    Right
}

internal enum SpikeTilePart2D
{
    Standalone,
    Left,
    Middle,
    Right
}

internal sealed class SideScrollerTerrainTileset2D
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IShader2D _fillShader;
    private readonly IShader2D _topShader;
    private readonly IShader2D _rightShader;
    private readonly IShader2D _bottomShader;
    private readonly IShader2D _leftShader;
    private readonly IShader2D _outerCornerShader;
    private readonly IShader2D _innerCornerShader;
    private readonly IShader2D _oneWayStandaloneShader;
    private readonly IShader2D _oneWayLeftShader;
    private readonly IShader2D _oneWayMiddleShader;
    private readonly IShader2D _oneWayRightShader;
    private readonly IShader2D _spikeStandaloneShader;
    private readonly IShader2D _spikeLeftShader;
    private readonly IShader2D _spikeMiddleShader;
    private readonly IShader2D _spikeRightShader;
    private readonly IShader2D _grippableShader =
        new SolidColorShader(new SKColor(76, 231, 120));
    private readonly float _surfaceThickness;
    private readonly float _outerCornerSize;
    private readonly float _innerCornerSize;
    private readonly float _oneWayVisualHeight;
    private readonly float _spikeVisualHeight;

    private SideScrollerTerrainTileset2D(
        IShader2D fillShader,
        IShader2D topShader,
        IShader2D rightShader,
        IShader2D bottomShader,
        IShader2D leftShader,
        IShader2D outerCornerShader,
        IShader2D innerCornerShader,
        IShader2D oneWayStandaloneShader,
        IShader2D oneWayLeftShader,
        IShader2D oneWayMiddleShader,
        IShader2D oneWayRightShader,
        IShader2D spikeStandaloneShader,
        IShader2D spikeLeftShader,
        IShader2D spikeMiddleShader,
        IShader2D spikeRightShader,
        float surfaceThickness,
        float outerCornerSize,
        float innerCornerSize,
        float oneWayVisualHeight,
        float spikeVisualHeight)
    {
        _fillShader = ArgGuard.RequireNotNull(fillShader);
        _topShader = ArgGuard.RequireNotNull(topShader);
        _rightShader = ArgGuard.RequireNotNull(rightShader);
        _bottomShader = ArgGuard.RequireNotNull(bottomShader);
        _leftShader = ArgGuard.RequireNotNull(leftShader);
        _outerCornerShader = ArgGuard.RequireNotNull(outerCornerShader);
        _innerCornerShader = ArgGuard.RequireNotNull(innerCornerShader);
        _oneWayStandaloneShader = ArgGuard.RequireNotNull(oneWayStandaloneShader);
        _oneWayLeftShader = ArgGuard.RequireNotNull(oneWayLeftShader);
        _oneWayMiddleShader = ArgGuard.RequireNotNull(oneWayMiddleShader);
        _oneWayRightShader = ArgGuard.RequireNotNull(oneWayRightShader);
        _spikeStandaloneShader = ArgGuard.RequireNotNull(spikeStandaloneShader);
        _spikeLeftShader = ArgGuard.RequireNotNull(spikeLeftShader);
        _spikeMiddleShader = ArgGuard.RequireNotNull(spikeMiddleShader);
        _spikeRightShader = ArgGuard.RequireNotNull(spikeRightShader);
        ArgGuard.ThrowIfNotPositive(surfaceThickness);
        ArgGuard.ThrowIfNotPositive(outerCornerSize);
        ArgGuard.ThrowIfNotPositive(innerCornerSize);
        ArgGuard.ThrowIfNotPositive(oneWayVisualHeight);
        ArgGuard.ThrowIfNotPositive(spikeVisualHeight);
        _surfaceThickness = surfaceThickness;
        _outerCornerSize = outerCornerSize;
        _innerCornerSize = innerCornerSize;
        _oneWayVisualHeight = oneWayVisualHeight;
        _spikeVisualHeight = spikeVisualHeight;
    }

    public static SideScrollerTerrainTileset2D Load(TextureCache2D textures, string tilesetId, float tileSize)
    {
        ArgGuard.ThrowIfNull(textures);
        AssetId2D.Validate(tilesetId);
        ArgGuard.ThrowIfNotPositive(tileSize);
        var relativeRoot = Path.Combine("environments", "tilesets", tilesetId);
        var manifestPath = Path.Combine(textures.ContentRoot, relativeRoot, "tileset.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Tileset manifest was not found.", manifestPath);

        var manifest = JsonSerializer.Deserialize<TilesetManifest>(File.ReadAllText(manifestPath), JsonOptions) ??
            throw new InvalidDataException($"Tileset manifest is empty: {manifestPath}");
        if (!string.Equals(manifest.Id, tilesetId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Tileset manifest ID '{manifest.Id}' must match its folder '{tilesetId}': {manifestPath}");
        }
        if (MathF.Abs(manifest.TileSize - tileSize) > 0.001f)
        {
            throw new InvalidDataException($"Tileset '{tilesetId}' uses tile size {manifest.TileSize}, but the level uses {tileSize}.");
        }

        var leftSurface = ResolveSurfacePath(textures, relativeRoot, "left.png", "side.png");
        var rightSurface = ResolveSurfacePath(textures, relativeRoot, "right.png", "side.png");
        var spikeRoot = ResolveSpikeRoot(textures, relativeRoot);
        var spikeVisualHeight = manifest.SpikeVisualHeight > 0f
            ? manifest.SpikeVisualHeight
            : manifest.OneWayVisualHeight;
        return new SideScrollerTerrainTileset2D(
            new TextureShader2D(textures.Load(Path.Combine(relativeRoot, "fill.png")),
            new Vector2(tileSize)),
            CreateTerrainShader(textures, relativeRoot, Path.Combine("surfaces", "top.png"), new Vector2(tileSize, manifest.SurfaceThickness)),
            CreateTerrainShader(textures, relativeRoot, rightSurface, new Vector2(manifest.SurfaceThickness, tileSize)),
            CreateTerrainShader(textures, relativeRoot, Path.Combine("surfaces", "bottom.png"), new Vector2(tileSize, manifest.SurfaceThickness)),
            CreateTerrainShader(textures, relativeRoot, leftSurface, new Vector2(manifest.SurfaceThickness, tileSize)),
            CreateTerrainShader(textures, relativeRoot, Path.Combine("corners", "outer.png"), new Vector2(manifest.OuterCornerSize)),
            CreateTerrainShader(textures, relativeRoot, Path.Combine("corners", "inner.png"), new Vector2(manifest.InnerCornerSize)),
            CreateOneWayShader(textures, relativeRoot, "standalone"),
            CreateOneWayShader(textures, relativeRoot, "left"),
            CreateOneWayShader(textures, relativeRoot, "middle"),
            CreateOneWayShader(textures, relativeRoot, "right"),
            CreateStripShader(textures, relativeRoot, spikeRoot, "standalone"),
            CreateStripShader(textures, relativeRoot, spikeRoot, "left"),
            CreateStripShader(textures, relativeRoot, spikeRoot, "middle"),
            CreateStripShader(textures, relativeRoot, spikeRoot, "right"),
            manifest.SurfaceThickness,
            manifest.OuterCornerSize,
            manifest.InnerCornerSize,
            manifest.OneWayVisualHeight,
            spikeVisualHeight);
    }

    public static SideScrollerTerrainTileset2D CreateCollisionTest()
    {
        const float surfaceThickness = 8f;
        const float outerCornerSize = 12f;
        const float innerCornerSize = 10f;
        var topShader = new SolidColorShader(new SKColor(44, 229, 255));
        var sideShader = new SolidColorShader(new SKColor(67, 126, 255));
        var oneWayShader = new SolidColorShader(new SKColor(255, 207, 72));
        return new SideScrollerTerrainTileset2D(
            new SolidColorShader(new SKColor(24, 29, 40)),
            topShader,
            sideShader,
            new SolidColorShader(new SKColor(145, 92, 255)),
            sideShader,
            new SolidColorShader(new SKColor(242, 246, 255)),
            new SolidColorShader(new SKColor(255, 91, 176)),
            oneWayShader,
            oneWayShader,
            oneWayShader,
            oneWayShader,
            oneWayShader,
            oneWayShader,
            oneWayShader,
            oneWayShader,
            surfaceThickness,
            outerCornerSize,
            innerCornerSize,
            surfaceThickness,
            surfaceThickness);
    }

    public WorldObject2D CreateSolidFill(Bounds2D bounds) =>
        CreateVisual(bounds.Size, bounds.Center, _fillShader);

    public WorldObject2D CreateGrippable(Bounds2D tileBounds) =>
        CreateVisual(tileBounds.Size, tileBounds.Center, _grippableShader);

    public WorldObject2D CreateSurface(Bounds2D tileBounds, TileSurface2D surface) =>
        surface switch
        {
            TileSurface2D.Top => CreateVisual(new Vector2(tileBounds.Size.X, _surfaceThickness), new Vector2(tileBounds.Center.X, tileBounds.Max.Y - _surfaceThickness / 2f), _topShader),
            TileSurface2D.Right => CreateVisual(new Vector2(_surfaceThickness, tileBounds.Size.Y), new Vector2(tileBounds.Max.X - _surfaceThickness / 2f, tileBounds.Center.Y), _rightShader),
            TileSurface2D.Bottom => CreateVisual(new Vector2(tileBounds.Size.X, _surfaceThickness), new Vector2(tileBounds.Center.X, tileBounds.Min.Y + _surfaceThickness / 2f), _bottomShader),
            TileSurface2D.Left => CreateVisual(new Vector2(_surfaceThickness, tileBounds.Size.Y), new Vector2(tileBounds.Min.X + _surfaceThickness / 2f, tileBounds.Center.Y), _leftShader),
            _ => throw ArgGuard.CreateInvalid("Create one surface visual at a time.", nameof(surface))
        };

    public WorldObject2D CreateCorner(Bounds2D tileBounds, TileCorner2D corner)
    {
        var position = corner switch
        {
            TileCorner2D.OuterTopRight => tileBounds.Max - new Vector2(_outerCornerSize / 2f),
            TileCorner2D.OuterBottomRight => new Vector2(tileBounds.Max.X - _outerCornerSize / 2f, tileBounds.Min.Y + _outerCornerSize / 2f),
            TileCorner2D.OuterBottomLeft => tileBounds.Min + new Vector2(_outerCornerSize / 2f),
            TileCorner2D.OuterTopLeft => new Vector2(tileBounds.Min.X + _outerCornerSize / 2f, tileBounds.Max.Y - _outerCornerSize / 2f),
            TileCorner2D.InnerTopRight => tileBounds.Max,
            TileCorner2D.InnerBottomRight => new Vector2(tileBounds.Max.X, tileBounds.Min.Y),
            TileCorner2D.InnerBottomLeft => tileBounds.Min,
            TileCorner2D.InnerTopLeft => new Vector2(tileBounds.Min.X, tileBounds.Max.Y),
            _ => throw ArgGuard.CreateInvalid("Create one corner visual at a time.", nameof(corner))
        };
        var isOuter = corner <= TileCorner2D.OuterTopLeft;
        return CreateVisual(new Vector2(isOuter ? _outerCornerSize : _innerCornerSize), position, isOuter ? _outerCornerShader : _innerCornerShader);
    }

    public WorldObject2D CreateOneWay(Bounds2D tileBounds, OneWayTilePart2D part)
    {
        var shader = part switch
        {
            OneWayTilePart2D.Standalone => _oneWayStandaloneShader,
            OneWayTilePart2D.Left => _oneWayLeftShader,
            OneWayTilePart2D.Middle => _oneWayMiddleShader,
            OneWayTilePart2D.Right => _oneWayRightShader,
            _ => throw ArgGuard.CreateInvalid("Unknown one-way tile part.", nameof(part))
        };
        return CreateVisual(new Vector2(tileBounds.Size.X, _oneWayVisualHeight), new Vector2(tileBounds.Center.X, tileBounds.Max.Y - _oneWayVisualHeight / 2f), shader);
    }

    public WorldObject2D CreateSpikes(Bounds2D tileBounds, SpikeTilePart2D part)
    {
        var shader = part switch
        {
            SpikeTilePart2D.Standalone => _spikeStandaloneShader,
            SpikeTilePart2D.Left => _spikeLeftShader,
            SpikeTilePart2D.Middle => _spikeMiddleShader,
            SpikeTilePart2D.Right => _spikeRightShader,
            _ => throw ArgGuard.CreateInvalid("Unknown spike tile part.", nameof(part))
        };
        return CreateVisual(
            new Vector2(tileBounds.Size.X, _spikeVisualHeight),
            new Vector2(tileBounds.Center.X, tileBounds.Min.Y + _spikeVisualHeight / 2f),
            shader);
    }

    private static WorldObject2D CreateVisual(Vector2 size, Vector2 position, IShader2D shader)
    {
        var visual = new WorldObject2D(AxisAlignedRectangle2D.FromSize(size), shader);
        visual.Transform.Position = position;
        return visual;
    }

    private static TextureShader2D CreateTerrainShader(TextureCache2D textures, string relativeRoot, string fileName, Vector2 logicalSize) =>
        new(textures.Load(Path.Combine(relativeRoot, fileName)), logicalSize, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);

    private static string ResolveSurfacePath(
        TextureCache2D textures,
        string relativeRoot,
        string directionalFileName,
        string fallbackFileName)
    {
        var directionalPath = Path.Combine("surfaces", directionalFileName);
        return File.Exists(Path.Combine(textures.ContentRoot, relativeRoot, directionalPath))
            ? directionalPath
            : Path.Combine("surfaces", fallbackFileName);
    }

    private static string ResolveSpikeRoot(TextureCache2D textures, string relativeRoot)
    {
        var spikeRoot = Path.Combine("hazards", "spikes");
        return File.Exists(Path.Combine(textures.ContentRoot, relativeRoot, spikeRoot, "standalone.png"))
            ? spikeRoot
            : "one-way";
    }

    private static SpriteShader2D CreateOneWayShader(
        TextureCache2D textures,
        string relativeRoot,
        string part) =>
        CreateStripShader(textures, relativeRoot, "one-way", part);

    private static SpriteShader2D CreateStripShader(
        TextureCache2D textures,
        string relativeRoot,
        string stripRoot,
        string part) =>
        new(textures.Load(Path.Combine(relativeRoot, stripRoot, $"{part}.png")));

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class TilesetManifest
    {
        public string Id { get; init; } = string.Empty;
        public float TileSize { get; init; }
        public float SurfaceThickness { get; init; }
        public float OuterCornerSize { get; init; }
        public float InnerCornerSize { get; init; }
        public float OneWayVisualHeight { get; init; }
        public float SpikeVisualHeight { get; init; }
    }
}

internal sealed class SideScrollerTerrainTilesetResolver2D(Func<int, int, SideScrollerTerrainTileset2D> resolve)
{
    private readonly Func<int, int, SideScrollerTerrainTileset2D> _resolve = ArgGuard.RequireNotNull(resolve);

    public SideScrollerTerrainTileset2D GetTileset(int tileX, int tileY)
    {
        return StateGuard.RequireNotNull(_resolve(tileX, tileY), "The terrain tileset resolver returned no tileset.");
    }

    public bool UsesSameTileset(int firstTileX, int firstTileY, int secondTileX, int secondTileY)
    {
        return ReferenceEquals(GetTileset(firstTileX, firstTileY), GetTileset(secondTileX, secondTileY));
    }
}
