using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;
using System.Text.Json;
using Xunit;

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
        var hero = new App2d.Gameplay.Persons.Actions.AuthoredHero2D(authored.Entities["hero"], game.Player.Body.WorldObject.Shape.LocalBounds.Size);
        var slash = authored.Animations["player-sword-draw-slash"];
        game.Arsenal.UsePrimary(1);
        // The gameplay swing lasts exactly as long as the drawn slash, and damages only between its strike and recover markers.
        Assert.Equal(slash.Duration, game.Arsenal.CaptureActionState().DurationSeconds, 4);
        Assert.Equal(slash.Markers.Single(m => m.Id == "strike").Time, hero.Attack.Hits[0].Start, 4);
        Assert.Equal(slash.Markers.Single(m => m.Id == "recover").Time, hero.Attack.Hits[0].Finish, 4);
        // The hit box sits ahead of the player at the sword tip, mirrored by facing.
        var strike = hero.Attack.Hits[0].Start + .01f;
        var right = hero.Offset(strike, 1); var left = hero.Offset(strike, -1);
        Assert.True(right.X > 20, $"hit box ahead of the player: {right}");
        Assert.Equal(-right.X, left.X, 3); Assert.Equal(right.Y, left.Y, 3);
        Assert.False(game.Arsenal.CaptureActionState().FollowUp); // the first swing draws from the sheath
        Assert.Equal(hero.Attack.Hits[0].Start, strike - .01f, 4);
        void Tick(int ticks) { for (var i = 0; i < ticks; i++) { var tick = game.Session.Tick + 1; game.Session.Advance(new PlayerInput2D(game.Player.Id, tick, tick, new PersonCommand2D())); } }
        Tick(60); // past the swing, inside the follow-up window
        game.Arsenal.UsePrimary(1);
        Assert.True(game.Arsenal.CaptureActionState().FollowUp, "a swing soon after the last keeps the blade out");
        Assert.NotEqual(hero.Offset(strike, 1, followUp: true), hero.Offset(strike, 1)); // and its hit box follows the other clip
        Tick(200);
        game.Arsenal.UsePrimary(1);
        Assert.False(game.Arsenal.CaptureActionState().FollowUp, "after a pause the sword is drawn again");
        var muzzle = hero.Muzzle(1, false);
        Assert.True(muzzle.X > 10 && MathF.Abs(muzzle.Y) < game.Player.Body.WorldObject.Shape.LocalBounds.Size.Y / 2, $"muzzle in front at chest height: {muzzle}");
    }
}
