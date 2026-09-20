using App2d.Core.Characters;
using App2d.Gameplay.Entities;
using System.Numerics;
using Xunit;

namespace App2d.Tests;

public sealed class EntityAuthoringTests
{
    private static string Assets
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "Assets", "Characters");
                if (File.Exists(Path.Combine(path, "catalog.json"))) return path;
            }
            throw new DirectoryNotFoundException("Character authoring assets not found.");
        }
    }
    [Fact]
    public void ContactMappingUsesActionTimeAndHonorsTrimmedClip()
    {
        var library = PointLibrary.Load(Path.Combine(Assets, "person", "library.json")); var clip = library.Clips["sword_attack"];
        var action = new EntityAction { Clip = clip.Id, Duration = 2, Contact = .7f, ClipStart = .1f, ClipContact = .3f, ClipEnd = .9f, AttackKind = "melee", ActiveStart = .6f, ActiveEnd = .8f };
        Assert.Equal(clip.Duration * .1, action.ClipTime(clip, 0), 5);
        Assert.Equal(clip.Duration * .3, action.ClipTime(clip, 1.4), 5);
        Assert.Equal(clip.Duration * .9, action.ClipTime(clip, 2), 5);
        Assert.False(action.Active(1)); Assert.True(action.Active(1.4)); Assert.False(action.Active(1.7)); Assert.False(action.Active(2));
    }
    [Fact]
    public void StarterTypesValidateAndShareSupportedAnatomies()
    {
        var types = Directory.GetFiles(Path.Combine(Assets, "entities"), "*.json").Select(EntityTypeDefinition.Load).ToArray();
        Assert.Equal(5, types.Length);
        foreach (var type in types)
        {
            var library = PointLibrary.Load(Path.Combine(Assets, type.Library, "library.json")); type.Validate(library);
            var copy = type.Copy(); copy.Appearance.Head = .5f; copy.Actions["attack"].Damage++;
            Assert.NotEqual(type.Appearance.Head, copy.Appearance.Head); Assert.NotEqual(type.Actions["attack"].Damage, copy.Actions["attack"].Damage);
        }
    }
    [Fact]
    public void MovementBodyIsStableWhileHeadFollowsAppearanceAndFacing()
    {
        var type = EntityTypeDefinition.Load(Path.Combine(Assets, "entities", "needle.json"));
        var library = PointLibrary.Load(Path.Combine(Assets, type.Library, "library.json")); var pose = new EntityPose(library); var action = type.Actions["walk"];
        pose.Evaluate(type, action, .1, false); var movement = pose.Movement.Points.ToArray(); var head = pose.Hurt.Single(r => r.Id == "head").Points.ToArray();
        pose.Evaluate(type, action, .1, true); var mirrored = pose.Hurt.Single(r => r.Id == "head").Points;
        Assert.Equal(head.Min(p => p.X), -mirrored.Max(p => p.X), 4); Assert.Equal(head.Max(p => p.Y), mirrored.Max(p => p.Y), 4);
        type.Appearance.Head *= 1.5f; pose.Evaluate(type, action, .1, false); var larger = pose.Hurt.Single(r => r.Id == "head").Points;
        Assert.True(larger.Max(p => p.X) - larger.Min(p => p.X) > head.Max(p => p.X) - head.Min(p => p.X));
        pose.Evaluate(type, action, .6, false); Assert.Equal(movement, pose.Movement.Points); Assert.DoesNotContain(pose.Hurt, r => r.Id == "arms");
    }
    [Fact]
    public void ConvexOverlapHandlesTranslationAndDisjointShapes()
    {
        var a = EntityRegion.Box("a", Vector2.Zero, Vector2.One); var b = EntityRegion.Box("b", Vector2.Zero, Vector2.One);
        Assert.True(a.Overlaps(b, Vector2.Zero, new(.9f, 0))); Assert.False(a.Overlaps(b, Vector2.Zero, new(1.1f, 0)));
    }
    [Fact]
    public void OneAttackCannotDamageSameTargetRepeatedlyDuringActiveWindow()
    {
        var type = EntityTypeDefinition.Load(Path.Combine(Assets, "entities", "player.json")); var target = type.Copy(); target.Id = "dummy"; target.Behavior = "passive"; target.Health = 20;
        type.Actions["attack"] = type.Actions["attack"] with { Duration = 1, ActiveStart = .2f, ActiveEnd = .8f, Damage = 2, HitX = 2, HitY = 1, HitWidth = 8, HitHeight = 4 };
        var library = PointLibrary.Load(Path.Combine(Assets, "person", "library.json")); var arena = new EntityPlaytest([(type, library), (target, library)]);
        arena.Step(new(0, false, true, false)); for (var i = 0; i < 119; i++) arena.Step(default);
        Assert.Equal(18, arena.Actors[1].Health);
    }
    [Fact]
    public void IdenticalFixedStepCommandsProduceIdenticalCombatAndCueHistory()
    {
        var library = PointLibrary.Load(Path.Combine(Assets, "person", "library.json"));
        var player = EntityTypeDefinition.Load(Path.Combine(Assets, "entities", "player.json")); var enemy = EntityTypeDefinition.Load(Path.Combine(Assets, "entities", "cinder.json"));
        var a = new EntityPlaytest([(player, library), (enemy, library)]); var b = new EntityPlaytest([(player, library), (enemy, library)]);
        for (var i = 0; i < 900; i++)
        {
            var input = new EntityInput(i < 240 ? 1 : i < 480 ? -1 : 0, i % 180 == 0, i % 200 < 10, i % 120 < 10);
            a.Step(input); b.Step(input);
            Assert.Equal(a.Cues, b.Cues); Assert.Equal(a.Bolts.Count, b.Bolts.Count);
            for (var j = 0; j < a.Actors.Count; j++) { Assert.Equal(a.Actors[j].Position, b.Actors[j].Position); Assert.Equal(a.Actors[j].Health, b.Actors[j].Health); Assert.Equal(a.Actors[j].ActionTime, b.Actors[j].ActionTime); }
        }
        Assert.Equal(900, a.Tick);
    }
}
