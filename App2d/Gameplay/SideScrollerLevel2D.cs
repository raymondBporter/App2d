using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using App2d.Engine.Tiles;
using SkiaSharp;

namespace App2d.Gameplay;

public sealed class SideScrollerLevel2D
{
    private readonly float _tileSize;
    private bool _environmentCreated;
    private bool _enemiesCreated;

    public SideScrollerLevel2D(float tileSize)
    {
        ArgGuard.ThrowIfNotPositive(tileSize);

        _tileSize = tileSize;
        TileMap = CreateTileMap(tileSize);
        SpawnPoint = TileCenter(4f, 2f) + new Vector2(0f, 38f);
        GoalX = TileCenter(116f, 0f).X;
    }

    public TileMap2D TileMap { get; }
    public Vector2 SpawnPoint { get; }
    public float GoalX { get; }
    public List<WorldObject2D> Platforms { get; } = [];
    public List<PatrolEnemy2D> Enemies { get; } = [];

    public void CreateEnvironment(
        Scene2D scene,
        PhysicsWorld2D physics,
        IShader2D platformShader,
        uint worldLayer,
        uint playerLayer,
        uint enemyLayer)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(platformShader);
        StateGuard.ThrowIf(
            _environmentCreated,
            "The level environment has already been created.");

        _environmentCreated = true;
        var grassShader = new SolidColorShader(new SKColor(101, 205, 116));
        var groundTop = TileMap.Origin.Y + _tileSize * 2f;

        foreach (var bounds in TileMap.CollisionRectangles)
        {
            var platform = new WorldObject2D(
                AxisAlignedRectangle2D.FromSize(bounds.Size),
                platformShader);
            platform.Transform.Position = bounds.Center;
            scene.Add(platform);
            Platforms.Add(platform);

            var body = physics.AddBody(platform, BodyMotionType2D.Static);
            body.Restitution = 0f;
            body.CollisionLayer = worldLayer;
            body.CollisionMask = playerLayer | enemyLayer;
            body.IsOneWayPlatform =
                bounds.Size.Y <= _tileSize + 0.01f &&
                bounds.Min.Y >= groundTop + 0.01f;

            const float capHeight = 9f;
            var cap = new WorldObject2D(
                AxisAlignedRectangle2D.FromSize(new Vector2(bounds.Size.X, capHeight)),
                grassShader);
            cap.Transform.Position = new Vector2(
                bounds.Center.X,
                bounds.Max.Y - capHeight / 2f);
            scene.Add(cap);
        }

        CreateGoal(scene, groundTop);
    }

    public void CreateEnemies(
        Scene2D scene,
        PhysicsWorld2D physics,
        uint worldLayer,
        uint enemyLayer)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(physics);
        StateGuard.ThrowIf(
            !_environmentCreated,
            "Create the level environment before its enemies.");
        StateGuard.ThrowIf(
            _enemiesCreated,
            "The level enemies have already been created.");

        _enemiesCreated = true;
        var hitShader = new SolidColorShader(new SKColor(255, 245, 245));
        var coralShader = new LinearGradientShader(
            new SKColor(255, 101, 137),
            new SKColor(179, 48, 102));
        var violetShader = new LinearGradientShader(
            new SKColor(178, 125, 255),
            new SKColor(91, 61, 178));
        Span<EnemySpawn> spawns =
        [
            new(10f, 7f, 12f, 105f),
            new(22f, 18f, 31f, 120f),
            new(42f, 37f, 50f, 112f),
            new(57f, 52f, 67f, 135f),
            new(72f, 68f, 77f, 105f),
            new(87f, 84f, 91f, 125f),
            new(99f, 93f, 106f, 138f),
            new(114f, 111f, 118f, 115f)
        ];

        for (var i = 0; i < spawns.Length; i++)
        {
            var spawn = spawns[i];
            IShader2D normalShader = i % 2 == 0 ? coralShader : violetShader;
            var worldObject = new WorldObject2D(
                new Capsule2D(new Vector2(-19f, 0f), new Vector2(19f, 0f), 22f),
                normalShader);
            worldObject.Transform.Position = TileCenter(spawn.TileX, 2f);
            scene.Add(worldObject);

            var body = physics.AddBody(worldObject, BodyMotionType2D.Dynamic);
            body.Restitution = 0f;
            body.Mass = 1.25f;
            body.CollisionLayer = enemyLayer;
            body.CollisionMask = worldLayer;

            Enemies.Add(new PatrolEnemy2D(
                worldObject,
                body,
                TileCenter(spawn.MinTileX, 0f).X,
                TileCenter(spawn.MaxTileX, 0f).X,
                spawn.Speed,
                3,
                normalShader,
                hitShader));
        }
    }

    private static TileMap2D CreateTileMap(float tileSize)
    {
        var map = new TileMap2D(120, 18, tileSize, new Vector2(-512f, -640f));

        map.Fill(0, 0, 120, 2);
        map.Fill(14, 0, 3, 2, false);
        map.Fill(33, 0, 3, 2, false);
        map.Fill(79, 0, 4, 2, false);
        map.Fill(108, 0, 3, 2, false);

        map.Fill(0, 2, 1, 12);
        map.Fill(119, 2, 1, 12);
        map.Fill(7, 4, 6, 1);
        map.Fill(18, 6, 5, 1);
        map.Fill(26, 3, 6, 1);
        map.Fill(38, 7, 7, 1);
        map.Fill(49, 4, 6, 1);
        map.Fill(59, 9, 6, 1);
        map.Fill(69, 5, 9, 1);
        map.Fill(84, 3, 5, 1);
        map.Fill(92, 7, 7, 1);
        map.Fill(102, 4, 6, 1);
        map.Fill(112, 3, 4, 1);

        return map;
    }

    private void CreateGoal(Scene2D scene, float groundTop)
    {
        var pole = new WorldObject2D(
            new Capsule2D(Vector2.Zero, new Vector2(0f, 190f), 5f),
            new SolidColorShader(new SKColor(238, 242, 232)));
        pole.Transform.Position = new Vector2(GoalX, groundTop);
        scene.Add(pole);

        var flag = new WorldObject2D(
            new ConvexPolygon2D(
            [
                Vector2.Zero,
                new Vector2(92f, -30f),
                new Vector2(0f, -60f)
            ]),
            new SolidColorShader(new SKColor(255, 79, 120)));
        flag.Transform.Position = new Vector2(GoalX, groundTop + 185f);
        scene.Add(flag);
    }

    private Vector2 TileCenter(float x, float y) =>
        TileMap.Origin + new Vector2((x + 0.5f) * _tileSize, (y + 0.5f) * _tileSize);

    private readonly record struct EnemySpawn(
        float TileX,
        float MinTileX,
        float MaxTileX,
        float Speed);
}
