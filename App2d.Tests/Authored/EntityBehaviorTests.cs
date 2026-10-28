using App2d.CharacterSeeds;
using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P9a: typed behavior configurations with their own defaults and validation; geometry and timing carry no combat fields.</summary>
public sealed class EntityBehaviorTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    private static ResolvedEntity Compile(Part asset) =>
        ResolvedEntity.Compile(asset, Catalog.Resolve, Catalog.Animations.GetValueOrDefault, Catalog.Parts.GetValueOrDefault);

    private static string Error(Part asset)
    {
        try
        {
            asset.Validate();
        }
        catch (InvalidDataException ex)
        {
            return ex.Message;
        }

        return Assert.Throws<InvalidDataException>(() => Compile(asset)).Message;
    }

    [Fact]
    public void TypedEntitiesResolveTheirBehaviors()
    {
        // The maul brute: its strike, hit group and receiving damage are the typed behaviors (P11a converted the shipped file; P11c retired the loose fields).
        var brute = Catalog.Entities["maul-brute"];
        var slam = brute.Actions["attack"].Hits[0];
        Assert.Equal(new StrikeDef { Damage = 5, Sound = "heavy" }, slam.Strike);
        Assert.Equal("hammer-head", slam.Group);
        Assert.Equal(new ReceiveDamageDef { Health = 22, KnockbackResistance = 3 }, brute.Damage);
        Assert.Null(brute.GuardBehavior);
        Assert.Empty(brute.Properties);
        // The shield defender: the guard has its typed defaults.
        var defender = Catalog.Entities["shield-defender"];
        Assert.Equal(Part.CurrentVersion, defender.Asset.Version);
        Assert.Equal(new StrikeDef { Damage = 2, Sound = "hit" }, defender.Actions["attack"].Hits[0].Strike);
        Assert.Equal(new ReceiveDamageDef { Health = 8, KnockbackResistance = 1.5f }, defender.Damage);
        Assert.Equal(new GuardBehaviorDef { Collider = defender.Asset.Behavior!.Guard!.Collider }, defender.GuardBehavior);
        // A projectile's strike is keyed by its action.
        var gunner = Catalog.Entities["cinder-gunner"];
        Assert.Equal(2, gunner.Actions["attack"].ProjectileStrike!.Damage);
        var hero = Catalog.Entities["hero"];
        Assert.Equal(2, hero.Actions["shoot"].ProjectileStrike!.Damage);
        Assert.Null(hero.Actions["attack"].ProjectileStrike);
        Assert.Equal(2, hero.Actions["forehand"].Hits[0].Strike.Damage);
    }

    [Fact]
    public void TypedBehaviorsValidateAndUnknownStrikesAreReportedAtResolution()
    {
        var guard = StarterContent.SpearGuardEntity();
        Assert.Equal(Part.CurrentVersion, guard.Version);
        Assert.NotNull(guard.Behavior);
        var compiled = Compile(guard);
        Assert.Equal(3, compiled.Damage.Health);
        Assert.Equal(1, compiled.Actions["attack"].Hits[0].Strike.Damage);
        string Refused(Action<Part> change)
        {
            var broken = StarterContent.SpearGuardEntity();
            change(broken);
            return Error(broken);
        }
        Assert.Contains("no hit window or projectile action of that name", Refused(e => e.Behavior!.Strikes["spear-butt"] = new()));
        var unlisted = StarterContent.SpearGuardEntity();
        unlisted.Behavior!.Strikes.Remove("spear-tip");
        Assert.Equal(new StrikeDef(), Compile(unlisted).Actions["attack"].Hits[0].Strike); // a group without an entry strikes with the typed defaults
        Assert.Contains("unsupported format/version", Refused(e => e.Version = Part.CurrentVersion - 1));
        Assert.Contains("needs Behavior", Refused(e => e.Behavior = null));
        Assert.Contains("behaviors.damage health", Refused(e => e.Behavior!.Damage.Health = 0));
        Assert.Contains("behaviors.strikes.spear-tip damage", Refused(e => e.Behavior!.Strikes["spear-tip"].Damage = -1));
        Assert.Contains("behaviors.strikes.spear-tip impact", Refused(e => e.Behavior!.Strikes["spear-tip"].Impact = "laser"));
        Assert.Contains("behaviors.properties key", Refused(e => e.Behavior!.Properties["Not An Id"] = "x"));
        Assert.Contains("behaviors.guard openEvent", Refused(e => e.Behavior!.Guard = new() { OpenEvent = "" }));
        // Custom properties ride along untouched and reach the resolved entity by name.
        var custom = StarterContent.SpearGuardEntity();
        custom.Behavior!.Properties["patrol-note"] = "stays near the gate";
        Assert.Equal("stays near the gate", Compile(custom).Properties["patrol-note"]);
        var json = custom.ToJson();
        Assert.Contains("\"patrol-note\"", json);
        Assert.DoesNotContain("\"health\"", json.Split("\"behaviors\"")[0]); // no legacy health outside the behaviors
        Assert.Equal(custom.ToJson(), Part.FromJson(json).ToJson());
    }

    [Fact]
    public void TheArenaAppliesTheTypedStrikeAndReceivingDamage()
    {
        // The guard's thrust strike and the player's hurt window come from the typed behaviors; the fight is the reference one.
        var player = StarterContent.PlayerEntity();
        player.Behavior!.Strikes["spear-tip"].Damage = 2;
        player.Behavior!.Damage.HurtSeconds = .1f;
        var target = Compile(player);
        var arena = new AuthoredArena([target, Catalog.Entities["spear-guard"]]);
        arena.Teleport(0, new(0, 0));
        arena.Teleport(1, new(2.05f, 0));
        var hurtSteps = 0;
        for (var i = 0; i < 360; i++)
        {
            arena.Step(default);
            if (arena.Player.HurtTime > 0)
            {
                hurtSteps++;
            }
        }
        Assert.Equal([48L, 289L], arena.Hits.Select(h => h.Tick));
        Assert.All(arena.Hits, h => Assert.Equal(1, h.Damage));
        Assert.Equal(target.Damage.Health - 2, arena.Player.Health);
        Assert.InRange(hurtSteps, 20, 26); // two hits, .1 s each at 120 Hz
        // The player's own strike (damage 2) lands on the guard when it attacks.
        var duel = new AuthoredArena([target, Catalog.Entities["spear-guard"]]);
        duel.Teleport(0, new(0, 0));
        duel.Teleport(1, new(1.9f, 0));
        for (var i = 0; i < 120 && !duel.Hits.Any(h => h.Attacker == 0); i++)
        {
            duel.Step(new(Attack: i == 0));
        }
        var strike = Assert.Single(duel.Hits.Where(h => h.Attacker == 0));
        Assert.Equal(2, strike.Damage);
        Assert.Equal(Catalog.Entities["spear-guard"].Damage.Health - 2, duel.Actors[1].Health);
    }
}
