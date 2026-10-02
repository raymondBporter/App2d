using App2d.Contracts.Persons;
using App2d.Contracts.Simulation;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Gameplay.Tests.Enemies;

/// <summary>The game's player drawn and played from its authored hero entity.</summary>
public sealed class AuthoredEntityGameTests
{
    [Fact]
    public void TheSwordTakesItsTimingAndHitBoxFromTheAuthoredHero()
    {
        var authored = AuthoredCatalog.Load(Path.GetFullPath(Path.Combine(TestAssetPath.Root, "..", "Characters", "authored")));
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, [], [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 40))])
        { AuthoredCharacters = authored, PlayerMaximumHealth = 30 });
        var hero = new Gameplay.Persons.Actions.AuthoredHero2D(authored.Entities["hero"], game.Player.Body.WorldObject.LocalBounds.Size);
        var cut = hero.Attack.Clip;
        void Tick(int ticks) { for (var i = 0; i < ticks; i++) { var tick = game.Session.Tick + 1; game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick, new PersonCommand2D())); } }
        string? Swing() => game.Arsenal.CaptureActionState().Swing;
        game.Arsenal.UsePrimary(1);
        // The gameplay swing lasts exactly as long as the drawn side cut, and damages only between its strike and recover markers.
        Assert.Equal(cut.Duration, game.Arsenal.CaptureActionState().DurationSeconds, 4);
        Assert.Equal(cut.Markers.Single(m => m.Id == "strike").Time, hero.Attack.Hits[0].Start, 4);
        Assert.Equal(cut.Markers.Single(m => m.Id == "recover").Time, hero.Attack.Hits[0].Finish, 4);
        Assert.Equal(EntityControllers.Attack, Swing()); // the first swing draws from the back
        // The hit box is fixed to the player while it is live: ahead of the body, the entity's size, the same every tick.
        Vector2? Box() => game.Arsenal.GetActiveAttackHitboxes().Select(h => (Vector2?)(h.WorldBounds.Center - game.Player.Body.WorldObject.Transform.Position)).FirstOrDefault();
        Tick((int)MathF.Ceiling(hero.Attack.Hits[0].Start * 120) + 1);
        var first = Box() ?? throw new Xunit.Sdk.XunitException("no live hit box at the strike");
        Assert.True(first.X > 20, $"hit box ahead of the player: {first}");
        Tick(2);
        Assert.True(Box() is { } later && Vector2.Distance(first, later) < .01f, $"the box stays put on the player: {first} then {Box()}");
        Assert.True(Vector2.Distance(new Core.SpatialObject2D(hero.Shape()).LocalBounds.Size, game.Arsenal.GetActiveAttackHitboxes().First().WorldBounds.Size) < .01f, "the box is the entity's size");
        // Pressing again during a swing queues the next one in the combo; it starts as the last ends.
        game.Arsenal.UsePrimary(1);
        Tick((int)MathF.Ceiling(cut.Duration * 120));
        Assert.Equal(hero.Attack.Next, Swing());
        var backhand = hero.Swing(hero.Attack.Next);
        Tick((int)MathF.Ceiling(backhand.Clip.Duration * 120) + 12); // past the backhand, its put-away still holding the sword
        game.Arsenal.UsePrimary(1);
        Assert.Equal(backhand.Next, Swing());
        Tick(240);
        game.Arsenal.UsePrimary(1);
        Assert.Equal(EntityControllers.Attack, Swing()); // after the put-away the sword is drawn again
        var muzzle = hero.Muzzle(1, false);
        Assert.True(muzzle.X > 10 && MathF.Abs(muzzle.Y) < game.Player.Body.WorldObject.LocalBounds.Size.Y / 2, $"muzzle in front at chest height: {muzzle}");
    }
}
