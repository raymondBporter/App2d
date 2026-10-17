using App2d.Levels;

namespace App2d.Tests.Levels;

public sealed class CombatLevelGeneratorTests
{
    private static readonly string[] Tilesets = ["ground", "wall", "prehistoric"];

    [Fact]
    public void GeneratedDraftRunsWithActualAuthoredEnemiesWithoutGraphics()
    {
        var assets = Core.Assets.AssetLocations.ForGame(AppContext.BaseDirectory, true);
        var catalog = Core.Characters.Authored.AuthoredCatalog.Load(assets.AuthoredCharacters);
        Assert.Empty(catalog.Errors);
        var draft = CombatLevelGenerator2D.Generate(new()
        {
            Difficulty = 1,
            Exploration = 1
        }, Tilesets);
        var things = draft.Placements.Select((p, i) => new App2d.Gameplay.World.WorldThingSpec2D(i + 1,
            Enum.Parse<App2d.Gameplay.World.WorldThingKind2D>(p.TypeKey.Replace("-", ""), true),
            p.Name, true, new(p.X, p.Y))).ToArray();
        using var simulation = App2d.Gameplay.Simulation.SideScrollerSimulation2D.Create(new(
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), draft.Map, [], things)
        {
            AuthoredCharacters = catalog
        });
        Assert.Equal(things.Count(t => t.Kind is not (App2d.Gameplay.World.WorldThingKind2D.PlayerSpawn or
            App2d.Gameplay.World.WorldThingKind2D.SavePoint or App2d.Gameplay.World.WorldThingKind2D.Goal)),
            simulation.Level.EnemySystem.Combatants.Count);
        for (var tick = 0; tick < 240; tick++)
        {
            simulation.Session.Advance();
        }

        Assert.True(simulation.Player.IsAlive);
        Assert.True(float.IsFinite(simulation.Player.Position.Y));
        Assert.NotEmpty(simulation.Session.CaptureContent().Terrain);
    }

    [Fact]
    public void SeedReproducesDraftAndChangingSeedChangesIt()
    {
        var settings = new CombatLevelSettings2D { Exploration = .5f };
        var first = CombatLevelGenerator2D.Generate(settings, Tilesets);
        var repeated = CombatLevelGenerator2D.Generate(settings, Tilesets);
        var rerolled = CombatLevelGenerator2D.Generate(settings with
        {
            Seed = 9876
        }, Tilesets);
        Assert.Equal(first.Beats, repeated.Beats);
        Assert.Equal(first.Placements, repeated.Placements);
        for (var x = 0; x < first.Map.Width; x++)
        {
            for (var y = 0; y < first.Map.Height; y++)
            {
                Assert.Equal(first.Map.GetTile(x, y), repeated.Map.GetTile(x, y));
            }
        }

        Assert.False(first.Placements.SequenceEqual(rerolled.Placements));
        foreach (var beat in first.Beats.Where(b => b.Recipe == "combined-fight"))
        {
            var label = $"{beat.Index + 1}: combined-fight";
            Assert.Contains(first.Placements, p => p.Name == label && p.TypeKey == "rock-thrower");
            Assert.Contains(first.Placements, p => p.Name == label && p.TypeKey != "rock-thrower");
        }
    }

    [Fact]
    public void ExplorationAddsOptionalDetoursWithoutChangingMainRoute()
    {
        var settings = new CombatLevelSettings2D { Exploration = 0 };
        var linear = CombatLevelGenerator2D.Generate(settings, Tilesets);
        var exploring = CombatLevelGenerator2D.Generate(settings with
        {
            Exploration = 1
        }, Tilesets);
        Assert.All(linear.Beats, b => Assert.False(b.HasOverlook));
        Assert.Equal(settings.Beats - 3, exploring.Beats.Count(b => b.HasOverlook));
        Assert.Equal(linear.Beats.Select(b => b.Recipe), exploring.Beats.Select(b => b.Recipe));
        Assert.Equal(linear.Placements, exploring.Placements.Where(p => !p.Name.StartsWith("Overlook", StringComparison.Ordinal) &&
            !p.Name.StartsWith("Optional overlook", StringComparison.Ordinal)));
        Assert.Contains(exploring.Placements, p => p.Name.StartsWith("Optional overlook", StringComparison.Ordinal));
        foreach (var draft in new[] { linear, exploring })
        {
            Assert.Equal("arrival", draft.Beats[0].Recipe);
            Assert.Equal("climax", draft.Beats[^2].Recipe);
            Assert.Equal("exit", draft.Beats[^1].Recipe);
            for (var x = 0; x < draft.Map.Width; x++)
            {
                Assert.True(draft.Map.IsSolid(x, 11));
            }
        }
    }

    [Fact]
    public void EveryPlacementIsWithinMapAndAboveSolidTerrainAcrossSeeds()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var draft = CombatLevelGenerator2D.Generate(new()
            {
                Seed = seed,
                Difficulty = 1,
                Exploration = 1,
                JumpRiseTiles = 4
            }, Tilesets);
            foreach (var placement in draft.Placements)
            {
                var x = (int)((placement.X - draft.Map.Origin.X) / draft.Map.TileSize);
                var y = (int)((placement.Y - draft.Map.Origin.Y) / draft.Map.TileSize);
                Assert.InRange(x, 0, draft.Map.Width - 1);
                Assert.InRange(y, 0, draft.Map.Height - 1);
                Assert.False(draft.Map.IsSolid(x, y));
            }
        }
    }

    [Fact]
    public void DraftRoundTripsThroughExistingWorldAndZoneFormatsAndRefusesOverwrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "app2d-generator-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new CombatLevelSettings2D();
            var manifest = CombatLevelGenerator2D.Create(root, settings, Tilesets);
            using var world = WorldFile2D.Load(manifest);
            Assert.Single(world.Maps);
            Assert.Contains(world.PositionThings, t => t.TypeKey == "player-spawn");
            Assert.Contains(world.PositionThings, t => t.TypeKey == "goal");
            Assert.Equal(settings.Beats, WorldZoneFile2D.Load(Path.Combine(root, "zones.json")).Length);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var bytes = File.ReadAllBytes(Path.Combine(root, "level.db"));
            Assert.Throws<IOException>(() => CombatLevelGenerator2D.Create(root, settings, Tilesets));
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(root, "level.db")));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            // The randomly created test directory is the only deletion target.
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void RejectsInvalidSettingsBeforeCreatingOutput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatLevelGenerator2D.Generate(new() { Difficulty = float.NaN }, Tilesets));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatLevelGenerator2D.Generate(new() { JumpRiseTiles = 5 }, Tilesets));
        Assert.Throws<ArgumentException>(() => CombatLevelGenerator2D.Generate(new() { MeleeEnemies = ["goal"] }, Tilesets));
    }
}
