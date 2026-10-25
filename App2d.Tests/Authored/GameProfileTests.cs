using App2d.Core.Characters.Authored;
using App2d.Presentation.Persons;

namespace App2d.Tests.Authored;

/// <summary>P9c: the game's character bindings are profile data, checked by name, read by the move set, the loadout and the spawner.</summary>
public sealed class GameProfileTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    [Fact]
    public void TheShippedProfileBindsThePlayerRolesGearAndSpawns()
    {
        Assert.Empty(Catalog.Errors);
        var profile = Catalog.Profiles[GameProfile.GameId];
        Assert.True(profile.HasPlayer);
        Assert.Equal(("hero", "person"), (profile.Entity, profile.Model));
        Assert.Equal(PersonRoles.All.Order(StringComparer.Ordinal), profile.Roles.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("player-gun-shot", profile.Roles[PersonRoles.GunShot]);
        Assert.Equal(PersonGearIds.Default, profile.Gear);
        Assert.Equal(9, profile.Spawns.Count);
        Assert.Equal("maul-brute", profile.Spawns["BoilerBrute"]);
        // The move set binds every role through the profile: the role constants are roles, the clips the profile's.
        var moves = PersonMoves.From(Catalog);
        Assert.Same(profile, moves.Profile);
        Assert.Equal("player-gun-shot", moves[PersonMoves.GunShot].Id);
        Assert.Equal("person-walk", moves[PersonMoves.Walk].Id);
        Assert.Equal("hero", moves.Hero.Id);
        Assert.Equal(PersonLoadout.Sword, moves.Gear.Sword);
        Assert.Equal(PersonRoles.GunShot, PersonMoves.GunShot);
        // The loadout places the profile's gear ids.
        var idle = Catalog.Animations[profile.Roles[PersonRoles.Idle]];
        var custom = new PersonGearIds { Sword = "spear", Pistol = "pistol" };
        Assert.Contains(("spear", Core.Characters.Humanoid.BackSocket), PersonLoadout.Worn(idle, 0, PersonGear.Sword, custom));
        Assert.Contains((PersonLoadout.Sword, Core.Characters.Humanoid.BackSocket), PersonLoadout.Worn(idle, 0, PersonGear.Sword));
    }

    [Fact]
    public void ReferencesAreCheckedByNameAndASpawnsOnlyProfileCarriesNoPlayer()
    {
        var root = Path.Combine(Path.GetTempPath(), $"profile-{Guid.NewGuid():N}");
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(root, Path.GetRelativePath(TestModels.AuthoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        try
        {
            var path = Path.Combine(root, "profiles", "game.json");
            string Broken(Action<GameProfile> change)
            {
                var profile = GameProfile.FromJson(File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "profiles", "game.json")));
                change(profile);
                File.WriteAllText(path, profile.ToJson());
                return string.Join(" | ", AuthoredCatalog.Load(root).Errors);
            }
            Assert.Contains("roles.gun-shot: no animation 'player-gun-blast'", Broken(p => p.Roles[PersonRoles.GunShot] = "player-gun-blast"));
            Assert.Contains("entity: 'champion' is missing", Broken(p => p.Entity = "champion"));
            Assert.Contains("gear.sword: no prop 'excalibur'", Broken(p => p.Gear.Sword = "excalibur"));
            Assert.Contains("spawns.BoilerBrute: entity 'ogre' is missing", Broken(p => p.Spawns["BoilerBrute"] = "ogre"));
            Assert.Contains("roles.idle: animation 'quadruped-walk' is for model 'quadruped'", Broken(p => p.Roles[PersonRoles.Idle] = "quadruped-walk"));
            // Missing roles are the move set's complaint, by name.
            Assert.Empty(Broken(p => p.Roles.Remove(PersonRoles.Dash)));
            Assert.Contains("role dash", Assert.Throws<InvalidDataException>(() => PersonMoves.From(AuthoredCatalog.Load(root))).Message);
            // A spawns-only profile is valid and binds no player.
            var spawnsOnly = new GameProfile { Spawns = new(StringComparer.Ordinal) { ["Pterosaur"] = "flying-pterosaur" } };
            File.WriteAllText(path, spawnsOnly.ToJson());
            var catalog = AuthoredCatalog.Load(root);
            Assert.Empty(catalog.Errors);
            Assert.False(catalog.Profiles[GameProfile.GameId].HasPlayer);
            Assert.Contains("bind a player entity", Assert.Throws<InvalidDataException>(() => PersonMoves.From(catalog)).Message);
            Assert.Contains("roles need a player", Broken(p => { p.Entity = ""; p.Model = ""; }));
            Assert.Contains("names both", Assert.Throws<InvalidDataException>(() => new GameProfile { Entity = "hero" }.Validate()).Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
