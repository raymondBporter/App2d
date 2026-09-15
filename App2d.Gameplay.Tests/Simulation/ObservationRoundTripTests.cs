using App2d.Core;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Tiles;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace App2d.Gameplay.Tests.Simulation;

public sealed class ObservationRoundTripTests
{
    [Fact]
    public void CompleteObservationAndEveryOccurrenceCanCrossAValueOnlyBoundary()
    {
        var map = new EditableTileMap2D(8, 4, 16f, 4, new Vector2(-50f, 20f), ["stone", "grass"]);
        map.SetTile(3, 1, new TileCell2D(TileKind2D.Solid, 1));
        map.SetTile(4, 1, new TileCell2D(TileKind2D.Ladder, 1)); // Adjacent chunk's halo.
        var terrain = TerrainChunkState2D.Capture(map, new(0, 0), 7);
        var playerId = new EntityId2D(100);
        var enemyId = new EntityId2D(101);
        var position = new Vector2(24f, -3f);
        var person = new PersonState2D { Id = playerId, Position = position, Facing = -1f,
            HitPoints = 3, MaximumHitPoints = 5, IsGrounded = true, Action = new(PlayerAttackKind2D.Melee, 0.12f, 0.4f) };
        var player = new PlayerState2D(person, -1f, "sword", true, 0f, 42, false,
            new(false, 0f, position, [new(new EntityId2D(102), position, new(1250f, 0f), new(2f, 4f))]));
        var world = new WorldState2D([new(new EntityId2D(103), 40, position, new(90f, 12f), 0xffaabbcc)],
            [new(42, position, true)], [terrain], new(300f, 0f));
        ImmutableArray<EnemyState2D> enemies = [new(enemyId, EnemyKind2D.Rival, position, new(5f, 1f), 0.2f, -1f, true, true)
            { Person = person with { Id = enemyId }, IsAttacking = true, AttackElapsedSeconds = 0.12f, MoveX = -1f }];
        var snapshot = new SessionSnapshot2D(500, 620, player, world, enemies);
        var options = CreateOptions();
        var restored = RoundTrip(snapshot, options);
        Assert.Equal(player.Person, restored.Player.Person);
        Assert.Equal(player.Weapons.Projectiles.ToArray(), restored.Player.Weapons.Projectiles.ToArray());
        Assert.Equal(enemies.ToArray(), restored.Enemies.ToArray());
        var restoredTerrain = Assert.Single(restored.World.Terrain);
        Assert.NotSame(terrain, restoredTerrain);
        Assert.Equal(terrain.Collisions.ToArray(), restoredTerrain.Collisions.ToArray());
        for (var y = -1; y <= 4; y++)
            for (var x = -1; x <= 4; x++)
            {
                Assert.Equal(terrain.GetTileKind(x, y), restoredTerrain.GetTileKind(x, y));
                Assert.Equal(terrain.GetTilesetIndex(x, y), restoredTerrain.GetTilesetIndex(x, y));
            }
        map.SetTileKind(4, 1, TileKind2D.Empty);
        Assert.Equal(TileKind2D.Ladder, restoredTerrain.GetTileKind(4, 1));

        var stamp = new SessionEventStamp2D(501, 10, playerId);
        var events = new List<SessionEvent2D>
        {
            new JumpStarted2D(stamp), new Landed2D(stamp, 700f), new Footstep2D(stamp),
            new Damaged2D(stamp), new Died2D(stamp), new Respawned2D(stamp, position),
            new GoalReached2D(stamp), new CheckpointActivated2D(stamp, 42, 3, position),
            new EquipmentChanged2D(stamp, "gun"), new AttackStarted2D(stamp, PlayerAttackKind2D.Downward, 0.4f, true),
            new CombatDamageOccurred2D(stamp, new(enemyId, CombatFaction2D.Enemy, position, true))
        };
        EnemyEvent2D[] enemyEvents = [new HammerStarted2D(enemyId, position), new HammerStruck2D(enemyId, position),
            new RivalAttackStarted2D(enemyId, position, UnarmedAttackKind2D.Kick, 0.3f),
            new RivalDamaged2D(enemyId, position), new RivalDied2D(enemyId, position)];
        WeaponEvent2D[] weaponEvents = [new ChargeStarted2D(position), new ChargeCancelled2D(position, 0.5f),
            new GunFired2D(position), new ProjectileImpact2D(position, EntityId2D.None), new SwordImpact2D(position)];
        events.AddRange(enemyEvents.Select(e => new EnemyOccurred2D(stamp, e)));
        events.AddRange(weaponEvents.Select(e => new WeaponOccurred2D(stamp, e)));
        var frame = new SessionFrame2D(501, 621, player, events.ToImmutableArray()) { World = world, Enemies = enemies };
        Assert.Equal(events.ToArray(), RoundTrip(frame, options).Events.ToArray());
    }

    [Fact]
    public void TerrainConstructorRejectsIncompleteHaloAndUnresolvableTileset()
    {
        Assert.Throws<ArgumentException>(() => new TerrainChunkState2D(new(0, 0), 1, 4, 4, 4, 16f,
            Vector2.Zero, ["stone"], [], [0]));
        Assert.Throws<ArgumentException>(() => new TerrainChunkState2D(new(0, 0), 1, 4, 4, 4, 16f,
            Vector2.Zero, ["stone"], [], Enumerable.Repeat(new TileCell2D(TileKind2D.Solid, 1).Packed, 36).ToImmutableArray()));
    }

    private static T RoundTrip<T>(T value, JsonSerializerOptions options)
    {
        var json = JsonSerializer.Serialize(value, options);
        var restored = JsonSerializer.Deserialize<T>(json, options)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored, options));
        return restored;
    }

    // Test codec only: proves reconstructibility without choosing a production wire protocol.
    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(SessionEvent2D) && info.Type != typeof(EnemyEvent2D) && info.Type != typeof(WeaponEvent2D)) return;
            info.PolymorphismOptions = new JsonPolymorphismOptions();
            foreach (var type in info.Type.Assembly.GetTypes().Where(t => !t.IsAbstract && info.Type.IsAssignableFrom(t)))
                info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, type.Name));
        });
        var options = new JsonSerializerOptions { IncludeFields = true, TypeInfoResolver = resolver };
        options.Converters.Add(new EntityIdConverter());
        return options;
    }

    private sealed class EntityIdConverter : JsonConverter<EntityId2D>
    {
        public override EntityId2D Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.GetInt64() is var value && value == 0 ? EntityId2D.None : new EntityId2D(value);
        public override void Write(Utf8JsonWriter writer, EntityId2D value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Value);
    }
}
