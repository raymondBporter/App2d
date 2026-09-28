using App2d.Core;
using App2d.Core.Geometry;
using App2d.Core.Validation;
using App2d.Game.Presentation.Assets;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
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

    private readonly Texture2D _fill;
    private readonly float _fillPeriod;
    private readonly IShader2D _topShader;
    private readonly IShader2D _rightShader;
    private readonly IShader2D _bottomShader;
    private readonly IShader2D _leftShader;
    private readonly IShader2D _rightGripShader;
    private readonly IShader2D _leftGripShader;
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
    private readonly IShader2D _ladderTopShader;
    private readonly IShader2D _ladderMiddleShader;
    private readonly float _surfaceThickness;
    private readonly float _outerCornerSize;
    private readonly float _innerCornerSize;
    private readonly float _oneWayVisualHeight;
    private readonly float _spikeVisualHeight;

    private SideScrollerTerrainTileset2D(TextureCache2D textures, string relativeRoot, string tilesetId, float tileSize, TilesetManifest manifest)
    {
        _surfaceThickness = manifest.SurfaceThickness;
        _outerCornerSize = manifest.OuterCornerSize;
        _innerCornerSize = manifest.InnerCornerSize;
        _oneWayVisualHeight = manifest.OneWayVisualHeight;
        _spikeVisualHeight = manifest.SpikeVisualHeight > 0f ? manifest.SpikeVisualHeight : manifest.OneWayVisualHeight;
        _fillPeriod = manifest.FillPeriod > 0f ? manifest.FillPeriod : tileSize;
        ArgGuard.ThrowIfNotFiniteOrNotPositive(_surfaceThickness);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(_outerCornerSize);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(_innerCornerSize);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(_oneWayVisualHeight);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(_spikeVisualHeight);

        _fill = textures.Load(Path.Combine(relativeRoot, "fill.png"));
        var horizontal = new Vector2(tileSize, _surfaceThickness);
        var vertical = new Vector2(_surfaceThickness, tileSize);
        var left = ResolveOptional(textures, relativeRoot, Path.Combine("surfaces", "left.png"), Path.Combine("surfaces", "side.png"));
        var right = ResolveOptional(textures, relativeRoot, Path.Combine("surfaces", "right.png"), Path.Combine("surfaces", "side.png"));
        _topShader = Strip(textures, relativeRoot, Path.Combine("surfaces", "top.png"), horizontal);
        _bottomShader = Strip(textures, relativeRoot, Path.Combine("surfaces", "bottom.png"), horizontal);
        _leftShader = Strip(textures, relativeRoot, left, vertical);
        _rightShader = Strip(textures, relativeRoot, right, vertical);
        _leftGripShader = Strip(textures, relativeRoot, ResolveOptional(textures, relativeRoot, Path.Combine("surfaces", "left-grip.png"), left), vertical);
        _rightGripShader = Strip(textures, relativeRoot, ResolveOptional(textures, relativeRoot, Path.Combine("surfaces", "right-grip.png"), right), vertical);
        _outerCornerShader = Strip(textures, relativeRoot, Path.Combine("corners", "outer.png"), new Vector2(_outerCornerSize));
        _innerCornerShader = Strip(textures, relativeRoot, Path.Combine("corners", "inner.png"), new Vector2(_innerCornerSize));
        var spikeRoot = ResolveOptional(textures, relativeRoot, Path.Combine("hazards", "spikes", "standalone.png"), Path.Combine("one-way", "standalone.png")) ==
            Path.Combine("hazards", "spikes", "standalone.png") ? Path.Combine("hazards", "spikes") : "one-way";
        _oneWayStandaloneShader = Sprite(textures, relativeRoot, "one-way", "standalone");
        _oneWayLeftShader = Sprite(textures, relativeRoot, "one-way", "left");
        _oneWayMiddleShader = Sprite(textures, relativeRoot, "one-way", "middle");
        _oneWayRightShader = Sprite(textures, relativeRoot, "one-way", "right");
        _spikeStandaloneShader = Sprite(textures, relativeRoot, spikeRoot, "standalone");
        _spikeLeftShader = Sprite(textures, relativeRoot, spikeRoot, "left");
        _spikeMiddleShader = Sprite(textures, relativeRoot, spikeRoot, "middle");
        _spikeRightShader = Sprite(textures, relativeRoot, spikeRoot, "right");
        _ladderTopShader = new SpriteShader2D(textures.Load(LadderAssets2D.ResolvePath(textures, tilesetId, isTop: true)));
        _ladderMiddleShader = new SpriteShader2D(textures.Load(LadderAssets2D.ResolvePath(textures, tilesetId, isTop: false)));
    }

    public static SideScrollerTerrainTileset2D Load(TextureCache2D textures, string tilesetId, float tileSize)
    {
        ArgGuard.ThrowIfNull(textures);
        AssetId2D.Validate(tilesetId);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(tileSize);
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
        return new SideScrollerTerrainTileset2D(textures, relativeRoot, tilesetId, tileSize, manifest);
    }

    /// <summary>
    /// Fill repeats on a world-anchored grid, so separate fill rectangles line up with each other
    /// whatever their size. The world origin sits on the grid because level origins are tile aligned.
    /// </summary>
    public WorldObject2D CreateSolidFill(Bounds2D bounds) =>
        CreateVisual(bounds.Size, bounds.Center,
            new TextureShader2D(_fill, new Vector2(_fillPeriod), imageOrigin: -bounds.Center));

    public WorldObject2D CreateLadder(Bounds2D tileBounds, bool isTop) =>
        CreateVisual(tileBounds.Size, tileBounds.Center, isTop ? _ladderTopShader : _ladderMiddleShader);

    /// <summary>Grippable walls keep ordinary terrain art; only their side faces show handholds.</summary>
    public WorldObject2D CreateSurface(Bounds2D tileBounds, TileSurface2D surface, bool grippable = false) =>
        surface switch
        {
            TileSurface2D.Top => CreateVisual(new Vector2(tileBounds.Size.X, _surfaceThickness), new Vector2(tileBounds.Center.X, tileBounds.Max.Y - _surfaceThickness / 2f), _topShader),
            TileSurface2D.Right => CreateVisual(new Vector2(_surfaceThickness, tileBounds.Size.Y), new Vector2(tileBounds.Max.X - _surfaceThickness / 2f, tileBounds.Center.Y), grippable ? _rightGripShader : _rightShader),
            TileSurface2D.Bottom => CreateVisual(new Vector2(tileBounds.Size.X, _surfaceThickness), new Vector2(tileBounds.Center.X, tileBounds.Min.Y + _surfaceThickness / 2f), _bottomShader),
            TileSurface2D.Left => CreateVisual(new Vector2(_surfaceThickness, tileBounds.Size.Y), new Vector2(tileBounds.Min.X + _surfaceThickness / 2f, tileBounds.Center.Y), grippable ? _leftGripShader : _leftShader),
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

    /// <summary>A clamped strip whose image fills its visual exactly, upright.</summary>
    private static TextureShader2D Strip(TextureCache2D textures, string relativeRoot, string fileName, Vector2 logicalSize) =>
        new(textures.Load(Path.Combine(relativeRoot, fileName)), logicalSize,
            Microsoft.Xna.Framework.Graphics.TextureAddressMode.Clamp, Microsoft.Xna.Framework.Graphics.TextureAddressMode.Clamp,
            imageOrigin: new Vector2(-logicalSize.X / 2f, logicalSize.Y / 2f));

    private static string ResolveOptional(TextureCache2D textures, string relativeRoot, string preferred, string fallback) =>
        File.Exists(Path.Combine(textures.ContentRoot, relativeRoot, preferred)) ? preferred : fallback;

    private static SpriteShader2D Sprite(TextureCache2D textures, string relativeRoot, string stripRoot, string part) =>
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
        /// <summary>World size of one repeat of fill.png; defaults to one tile.</summary>
        public float FillPeriod { get; init; }
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
