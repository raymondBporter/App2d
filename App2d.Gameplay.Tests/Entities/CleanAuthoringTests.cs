using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;
using Xunit;

namespace App2d.Gameplay.Tests.Entities;

/// <summary>
/// The replacement's final acceptance: from an empty authored folder and no imported libraries, create a model, animate it,
/// make a variant and an entity through the editor session, save, reopen with the game's loader and play it in the game.
/// </summary>
public sealed class CleanAuthoringTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("app2d-clean-authoring-").FullName;
    private string Root => Path.Combine(_folder, "authored");

    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public void ACreatureAuthoredFromNothingPlaysInTheGame()
    {
        Directory.CreateDirectory(Root);
        var s = new EditorSession(AuthoringWorkspace.Open(Root), SourceLibraries.Open(_folder));
        Assert.Empty(s.Sources.Entries);

        // Model: a body on two IK legs, from Empty.
        Assert.True(s.NewModel("beetle", "Beetle", "empty"), s.Message);
        var model = s.SubjectModel!; var shellPart = "";
        Assert.True(s.Edit(model, () =>
        {
            var m = model.Asset;
            ModelAuthoring.AddControl(m, null, new(0, .55f, 0), "body");
            ModelAuthoring.AddControl(m, "body", new(.35f, .6f, 0), "jaw");
            foreach (var (leg, x, z) in new[] { ("front", .22f, -.1f), ("back", -.22f, .1f) })
            {
                ModelAuthoring.AddControl(m, "body", new(x, .5f, z), $"{leg}-hip");
                ModelAuthoring.AddControl(m, $"{leg}-hip", new(x + .1f, .26f, z), $"{leg}-knee");
                ModelAuthoring.AddControl(m, $"{leg}-knee", new(x, 0, z), $"{leg}-foot");
                ModelAuthoring.AddChain(m, $"{leg}-foot");
                ModelAuthoring.AddPart(m, "stroke", $"{leg}-hip", $"{leg}-knee"); ModelAuthoring.AddPart(m, "stroke", $"{leg}-knee", $"{leg}-foot");
            }
            ModelAuthoring.AddMeasure(m, "leg", ["front-hip", "front-knee", "front-foot"]);
            foreach (var chain in m.Chains) chain.Scale = "leg";
            var shell = ModelAuthoring.AddPart(m, "ellipse", "body"); shell.Width = .8f; shell.Height = .4f; shell.Fill = "#9ec27a"; shellPart = shell.Id;
            m.Sockets.Add(new() { Id = "mouth", Control = "jaw" });
            m.HurtLayouts.Add(new() { Id = "body", Regions = [new() { Id = "shell", Controls = ["body", "jaw", "front-hip", "back-hip"], Pad = .12f }] });
        }), s.Message);
        Assert.True(s.Save(model), s.Message);

        // Animate: idle, a planted walk with travel, and a bite with strike and recover markers.
        Assert.True(s.NewClip("beetle-idle", "Beetle idle", 1), s.Message);
        Pose(s, 0, "body", 0); Pose(s, .5f, "body", -.03f); Pose(s, 1, "body", 0);
        Assert.True(s.NewClip("beetle-walk", "Beetle walk", .8f), s.Message);
        var walk = s.ClipDocument!;
        Assert.True(s.Edit(walk, () => walk.Asset.Travel.Keys = [new() { Time = 0 }, new() { Time = .8f, X = .5f }]), s.Message);
        foreach (var (time, lift) in new[] { (0f, 0f), (.2f, .08f), (.4f, 0f), (.6f, .08f), (.8f, 0f) })
        {
            s.Seek(time); var pose = s.Evaluate("beetle")!.Pose;
            s.DragControl(time is .2f ? "back-foot" : "front-foot", pose.World(time is .2f ? "back-foot" : "front-foot") + new Vector3(0, lift, 0)); s.EndDrag();
        }
        s.Seek(0);
        var subject = s.Evaluate("beetle")!;
        Assert.True(s.Edit(walk, () => { ClipAuthoring.Plant(subject.Model, walk.Asset, subject.Pose, "front-foot-chain", 0, .4f); ClipAuthoring.Plant(subject.Model, walk.Asset, subject.Pose, "back-foot-chain", .4f, .4f); }), s.Message);
        Assert.True(s.NewClip("beetle-bite", "Beetle bite", .6f, loop: false), s.Message);
        Pose(s, 0, "jaw", 0); Pose(s, .25f, "jaw", 0, .25f); Pose(s, .6f, "jaw", 0);
        var bite = s.ClipDocument!;
        Assert.True(s.Edit(bite, () => { ClipAuthoring.SetMarker(bite.Asset, "strike", .2f); ClipAuthoring.SetMarker(bite.Asset, "recover", .35f); }), s.Message);
        Assert.True(s.NewMotionSet("beetle", "standard", "Standard", null), s.Message);
        foreach (var (role, clip) in new[] { ("idle", "beetle-idle"), ("walk", "beetle-walk"), ("attack", "beetle-bite") })
            Assert.True(s.AssignRole("beetle", "standard", role, clip), s.Message);

        // Variant and entity. The GreenDinosaur placement spawns whatever entity is called stalker-pest.
        Assert.True(s.NewVariant("red-beetle", "Red beetle", "beetle"), s.Message);
        var variant = s.SubjectVariant!;
        Assert.True(s.Edit(variant, () => variant.Asset.Parts[shellPart] = new() { Fill = "#c9563c" }), s.Message);
        Assert.True(s.NewEntity("stalker-pest", "Red beetle pest", "red-beetle", EntityAuthoring.Guard), s.Message);
        var entity = s.EntityDocument!;
        Assert.True(s.Edit(entity, () =>
        {
            entity.Asset.Controller.Range = .5f;
            entity.Asset.Actions.Single().Hits.Add(new() { Id = "bite", Socket = "mouth", Width = .4f, Height = .3f, Start = new() { Marker = "strike" }, Finish = new() { Marker = "recover" } });
        }), s.Message);
        s.SaveAll();
        Assert.False(s.Assets.DirtyDocuments.Any(), s.Message);
        Assert.All(s.Assets.Documents, d => Assert.Empty(s.Assets.Problems(d)));

        // Reopen with the loader the game uses, then play it.
        var catalog = AuthoredCatalog.Load(Root);
        Assert.Empty(catalog.Errors);
        Assert.Equal(new[] { "beetle" }, catalog.Models.Keys);
        Assert.Equal(new[] { "red-beetle" }, catalog.Variants.Keys);
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin, ["dark-cave"]);
        for (var x = 0; x < 640; x++) map.SetTileKind(x, 19, TileKind2D.Solid);
        using var game = SideScrollerSimulation2D.Create(new(TraversalMetricsLoader2D.Load(TestAssetPath.Root), map, [],
            [new(1, WorldThingKind2D.PlayerSpawn, null, true, new(-368, 40)), new(2, WorldThingKind2D.GreenDinosaur, null, true, new(-120, 42))])
        { AuthoredCharacters = catalog, PlayerMaximumHealth = 30 });
        var beetle = Assert.IsType<AuthoredEntityEnemy2D>(Assert.Single(game.Level.EnemySystem.Combatants));
        Assert.Equal("stalker-pest", Assert.Single(game.Session.CaptureEnemies()).TypeId);
        var start = game.Session.CaptureEnemies()[0].Position.X;
        var attacked = false; var health = game.Player.Health.Current;
        for (var i = 0; i < 1200 && !attacked; i++) attacked = game.Session.Advance().Enemies[0].ActionId == EntityControllers.Attack;
        Assert.True(game.Session.CaptureEnemies()[0].Position.X < start - 20, "the beetle walks toward the player");
        Assert.True(attacked, "the beetle bites once in range");
        for (var i = 0; i < 120; i++) game.Session.Advance();
        Assert.True(game.Player.Health.Current < health, "the bite lands on the player");
        Assert.Same(beetle.Pose, game.Session.CaptureEnemies()[0].AuthoredPose);
    }

    /// <summary>Keys a control moved from its current pose at a time, as a drag would.</summary>
    private static void Pose(EditorSession s, float time, string control, float lift, float reach = 0)
    {
        s.Seek(time); var pose = s.Evaluate(s.SubjectId!)!.Pose;
        s.BeginDrag(); s.DragControl(control, pose.World(control) + new Vector3(reach, lift, 0)); s.EndDrag();
    }
}
