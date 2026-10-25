using App2d.CharacterSeeds;
using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P9a: typed behavior configurations with their own defaults and validation; geometry and timing carry no combat fields.</summary>
public sealed class EntityBehaviorTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    private static ResolvedEntity Compile(EntityAsset asset) =>
        ResolvedEntity.Compile(asset, Catalog.Resolve, Catalog.Animations.GetValueOrDefault, Catalog.Props.GetValueOrDefault);

    private static string Error(EntityAsset asset)
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
    public void LegacyAndTypedEntitiesResolveToTheSameTypedBehaviors()
    {
        // The shipped maul brute is still version 2: its window damage, sound, health and mass read into the typed values.
        var brute = Catalog.Entities["maul-brute"];
        Assert.Null(brute.Asset.Behaviors);
        var slam = brute.Actions["attack"].Hits[0];
        Assert.Equal(new StrikeDef { Damage = 5, Sound = "heavy" }, slam.Strike);
        Assert.Equal("hammer-head", slam.Group);
        Assert.Equal(new ReceiveDamageDef { Health = 22, Mass = 3 }, brute.Damage);
        Assert.Null(brute.GuardBehavior);
        Assert.Empty(brute.Properties);
        // The shipped shield defender is version 4: the same values come from behaviors, the guard has its typed defaults.
        var defender = Catalog.Entities["shield-defender"];
        Assert.NotNull(defender.Asset.Behaviors);
        Assert.Equal(EntityAsset.CurrentVersion, defender.Asset.Version);
        Assert.Equal(new StrikeDef { Damage = 2, Sound = "hit" }, defender.Actions["attack"].Hits[0].Strike);
        Assert.Equal(new ReceiveDamageDef { Health = 8, Mass = 1.5f }, defender.Damage);
        Assert.Equal(new GuardBehaviorDef(), defender.GuardBehavior);
        Assert.Equal(new HitWindow().Damage, defender.Actions["attack"].Hits[0].Window.Damage);
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
        Assert.Equal(EntityAsset.CurrentVersion, guard.Version);
        Assert.NotNull(guard.Behaviors);
        var compiled = Compile(guard);
        Assert.Equal(3, compiled.Damage.Health);
        Assert.Equal(1, compiled.Actions["attack"].Hits[0].Strike.Damage);
        string Refused(Action<EntityAsset> change)
        {
            var broken = StarterContent.SpearGuardEntity();
            change(broken);
            return Error(broken);
        }
        Assert.Contains("no hit window or projectile action of that name", Refused(e => e.Behaviors!.Strikes["spear-butt"] = new()));
        var unlisted = StarterContent.SpearGuardEntity();
        unlisted.Behaviors!.Strikes.Remove("spear-tip");
        Assert.Equal(new StrikeDef(), Compile(unlisted).Actions["attack"].Hits[0].Strike); // a group without an entry strikes with the typed defaults
        Assert.Contains("damage and sound belong to behaviors.strikes.spear-tip", Refused(e => e.Actions[0].Hits[0].Damage = 3));
        Assert.Contains("health and mass belong to behaviors.damage", Refused(e => e.Health = 9));
        Assert.Contains("behaviors need entity version 4", Refused(e => e.Version = EntityAsset.ColliderVersion));
        Assert.Contains("declares its behaviors", Refused(e => e.Behaviors = null));
        Assert.Contains("behaviors.damage health", Refused(e => e.Behaviors!.Damage.Health = 0));
        Assert.Contains("behaviors.strikes.spear-tip damage", Refused(e => e.Behaviors!.Strikes["spear-tip"].Damage = -1));
        Assert.Contains("behaviors.strikes.spear-tip impact", Refused(e => e.Behaviors!.Strikes["spear-tip"].Impact = "laser"));
        Assert.Contains("behaviors.properties key", Refused(e => e.Behaviors!.Properties["Not An Id"] = "x"));
        Assert.Contains("behaviors.guard openEvent", Refused(e => e.Behaviors!.Guard = new() { OpenEvent = "" }));
        // Custom properties ride along untouched and reach the resolved entity by name.
        var custom = StarterContent.SpearGuardEntity();
        custom.Behaviors!.Properties["patrol-note"] = "stays near the gate";
        Assert.Equal("stays near the gate", Compile(custom).Properties["patrol-note"]);
        var json = custom.ToJson();
        Assert.Contains("\"patrol-note\"", json);
        Assert.DoesNotContain("\"health\"", json.Split("\"behaviors\"")[0]); // no legacy health outside the behaviors
        Assert.Equal(custom.ToJson(), EntityAsset.FromJson(json).ToJson());
    }

    [Fact]
    public void TheConversionMovesLegacyCombatFieldsIntoBehaviorsAndIsRerunnable()
    {
        var brute = Catalog.Entities["maul-brute"].Asset;
        var (converted, report) = BehaviorConversion.Convert(brute);
        Assert.Equal(EntityAsset.CurrentVersion, converted.Version);
        Assert.Equal(new ReceiveDamageDef { Health = 22, Mass = 3 }, converted.Behaviors!.Damage);
        Assert.Equal(new StrikeDef { Damage = 5, Sound = "heavy" }, converted.Behaviors.Strikes["hammer-head"]);
        Assert.Equal(new EntityAsset().Health, converted.Health);
        Assert.Equal(new HitWindow().Damage, converted.Actions[0].Hits[0].Damage);
        Assert.Null(converted.Actions[0].Hits[0].Sound);
        Assert.Contains(report, line => line.Contains("behaviors.strikes.hammer-head"));
        var resolved = Compile(converted);
        Assert.Equal(Catalog.Entities["maul-brute"].Actions["attack"].Hits[0].Strike, resolved.Actions["attack"].Hits[0].Strike);
        var (again, note) = BehaviorConversion.Convert(converted);
        Assert.Equal(converted.ToJson(), again.ToJson());
        Assert.Equal("nothing to convert: the entity already declares its behaviors", Assert.Single(note));
        // The hero's three windows share the hit group 'blade' with one strike; a projectile strike takes its action's name.
        var hero = Catalog.Entities["hero"].Asset;
        var (heroConverted, _) = BehaviorConversion.Convert(hero);
        Assert.Equal(["blade", "shoot", "wall-shot"], heroConverted.Behaviors!.Strikes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(2, heroConverted.Behaviors.Strikes["shoot"].Damage);
        // A hit group with two different damages cannot become one strike.
        var conflicting = EntityAsset.FromJson(hero.ToJson());
        conflicting.Actions[1].Hits[0].Damage = 3;
        Assert.Contains("a hit group has one strike", Assert.Throws<InvalidDataException>(() => BehaviorConversion.Convert(conflicting)).Message);
        // On a scratch tree: written once, unchanged on a rerun, the source untouched.
        var output = Path.Combine(Path.GetTempPath(), "app2d-behavior-conversion", Guid.NewGuid().ToString("N"));
        try
        {
            var first = BehaviorConversion.ConvertFile(TestModels.AuthoredRoot, "club-caveman", output);
            Assert.Equal("written", first.Status);
            Assert.Contains(first.Report, line => line.StartsWith("verified", StringComparison.Ordinal));
            Assert.True(File.Exists(Path.Combine(output, "behavior-conversion-club-caveman.md")));
            Assert.Equal(EntityAsset.CurrentVersion, EntityAsset.FromJson(File.ReadAllText(first.Output!)).Version);
            Assert.StartsWith("unchanged", BehaviorConversion.ConvertFile(TestModels.AuthoredRoot, "club-caveman", output).Status);
            Assert.Equal(EntityAsset.LegacyVersion, Catalog.Entities["club-caveman"].Asset.Version);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    [Fact]
    public void TheArenaAppliesTheTypedStrikeAndReceivingDamage()
    {
        // The guard's thrust strike and the player's hurt window come from the typed behaviors; the fight is the reference one.
        var player = StarterContent.PlayerEntity();
        player.Behaviors!.Strikes["spear-tip"].Damage = 2;
        player.Behaviors.Damage.HurtSeconds = .1f;
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
