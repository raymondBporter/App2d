using System.Diagnostics;
using System.Numerics;
using App2d.Core.Characters;
using App2d.Core.Characters.Editing;

namespace App2d.Tests.Authored;

/// <summary>The phase-four gates: repeated authoring of people, motion sets independent of size, one shared walk, and a masked upper action over locomotion.</summary>
public sealed class RepeatedAuthoringTests : IDisposable
{
    private const float Dt = 1 / 120f;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"repeat-{Guid.NewGuid():N}");

    public RepeatedAuthoringTests() => PersonTemplate.WriteStudies(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private EditorSession Session()
    {
        var workspace = AuthoringWorkspace.Open(_root);
        Assert.Empty(workspace.LoadErrors);
        return new(workspace);
    }

    private static void Ok(EditorSession session, bool result) => Assert.True(result, session.Message);

    private static ResolvedEntity Compile(AuthoredCatalog catalog, EntityAsset asset) =>
        ResolvedEntity.Compile(asset, catalog.Resolve, catalog.Animations.GetValueOrDefault, catalog.Props.GetValueOrDefault);

    /// <summary>The spear guard thrusting from the upper body over its walk.</summary>
    private static EntityAsset SkirmishingGuard()
    {
        var guard = StarterContent.SpearGuardEntity(); guard.Id = "skirmisher";
        guard.Actions[0].Mask = StarterContent.Upper; guard.Actions[0].BlendIn = .08f; guard.Actions[0].BlendOut = .12f;
        return guard;
    }

    // ---- Masked layer --------------------------------------------------------------------------------------------

    [Fact]
    public void AMaskedLayerBlendsFromTheBaseToTheOverlayAndLeavesTheLegsAlone()
    {
        var catalog = AuthoredCatalog.Load(_root); var person = catalog.Resolve("person");
        var walk = catalog.Animations["person-walk"]; var thrust = catalog.Animations["person-thrust"];
        var upper = person.Base.Groups.Single(g => g.Id == StarterContent.Upper).Targets.ToHashSet();
        Assert.Equal(PersonLoadout.UpperBody.Order(), upper.Order());
        EvaluatedPose At(float weight) => PoseEvaluator.Sample(person, walk, .45, input: new() { Overlay = new(thrust, .5, upper, weight) });
        var plain = PoseEvaluator.Sample(person, walk, .45); var none = At(0); var half = At(.5f); var full = At(1);
        foreach (var (id, point) in plain.Points) TestModels.Near(point, none.Points[id], 1e-6f, $"weight 0 leaves {id} on the base");
        Assert.True(Vector3.Distance(plain.World("right-hand"), full.World("right-hand")) > .2f, "the thrust moves the spear hand");
        Assert.True(MathF.Abs(full.Angles["chest"] - plain.Angles["chest"]) > .05f, "the thrust turns the chest");
        // Channels blend before the hierarchy is built: the chest's rotation is halfway at half weight.
        Assert.Equal((plain.Angles["chest"] + full.Angles["chest"]) / 2, half.Angles["chest"], 4);
        foreach (var control in new[] { "hips", "left-knee", "left-foot", "right-knee", "right-foot" })
        {
            TestModels.Near(plain.World(control), half.World(control), 1e-5f, $"{control} ignores the upper layer");
            TestModels.Near(plain.World(control), full.World(control), 1e-5f, $"{control} ignores the upper layer");
        }
        Assert.Equal(plain.Contacts.Select(c => c.Chain), full.Contacts.Select(c => c.Chain));
        Assert.All(half.Chains, c => Assert.True(c.Reached || c.Residual < 1e-3f, c.Chain));
    }

    [Fact]
    public void MaskedActionsAreCheckedAgainstTheModel()
    {
        var catalog = AuthoredCatalog.Load(_root);
        var blendWithoutMask = StarterContent.SpearGuardEntity(); blendWithoutMask.Actions[0].BlendIn = .1f;
        Assert.Contains("blending needs a mask", Assert.Throws<InvalidDataException>(blendWithoutMask.Validate).Message);
        var unknown = SkirmishingGuard(); unknown.Actions[0].Mask = "tail";
        Assert.Contains("mask: 'person' has no control group 'tail'", Assert.Throws<InvalidDataException>(() => Compile(catalog, unknown)).Message);
        var tooLong = SkirmishingGuard(); tooLong.Actions[0].BlendIn = 2;
        Assert.Contains("exceed the clip", Assert.Throws<InvalidDataException>(() => Compile(catalog, tooLong)).Message);
        var jump = StarterContent.PlayerEntity(); jump.Actions.Single(a => a.Id == "jump").Mask = StarterContent.Upper;
        Assert.Contains("cannot be masked", Assert.Throws<InvalidDataException>(() => Compile(catalog, jump)).Message);

        var model = PersonTemplate.Model();
        model.Groups.Add(new() { Id = "knees", Targets = ["left-knee"] });
        Assert.Contains("solved by IK; name its chain instead", Assert.Throws<InvalidDataException>(model.Validate).Message);
    }

    [Fact]
    public void AnUpperAttackDoesNotStealLegContacts()
    {
        var catalog = AuthoredCatalog.Load(_root); var entity = Compile(catalog, SkirmishingGuard());
        var attack = entity.Actions["attack"];
        Assert.NotNull(attack.Mask);
        Assert.Equal(0, attack.Weight(0)); Assert.Equal(1, attack.Weight(attack.Clip.Duration / 2)); Assert.Equal(0, attack.Weight(attack.Clip.Duration), 4);

        var animator = new EntityAnimator(entity); var walk = entity.Clip("walk")!;
        var speed = PoseEvaluator.CycleTravel(entity.Model, walk).X / walk.Duration;
        var position = Vector2.Zero; var events = new List<AnimationEvent>();
        void Step() { position.X += speed * Dt; animator.Step(Dt, position, 1, "walk", speed * Dt, false, events); }
        for (var i = 0; i < 90; i++) Step();
        var before = animator.Anchors.ToDictionary(); var phase = animator.RoleTime;
        Assert.NotEmpty(before);

        Assert.True(animator.TryStart("attack"));
        Assert.Equal(before, animator.Anchors); // starting an upper action keeps every held contact
        Step();
        Assert.True(animator.RoleTime > phase, "the walk keeps its phase under the attack");
        foreach (var (chain, anchor) in before)
            if (animator.Anchors.TryGetValue(chain, out var held)) Assert.Equal(anchor, held);

        var strikes = 0; var planted = 0;
        while (animator.Action is not null)
        {
            events.Clear(); Step();
            strikes += events.Count(e => e.Id == "swing");
            Assert.Equal("walk", animator.Role);
            foreach (var chain in new[] { "left-leg", "right-leg" })
                if (animator.Anchors.TryGetValue(chain, out var anchor)) { TestModels.Near(anchor, animator.Pose.World(entity.Model.Chains[chain].End), 1e-4f, chain); planted++; }
            if (animator.ActionComplete) animator.EndAction();
        }
        Assert.Equal(1, strikes);
        Assert.True(planted > 60, "feet stay planted on their anchors through the attack");
        Assert.NotEmpty(animator.Anchors); // ending the upper action keeps the legs' contacts too

        // The whole-body thrust, by contrast, owns the legs and starts with fresh contacts.
        var standing = new EntityAnimator(catalog.Entities["spear-guard"]);
        for (var i = 0; i < 30; i++) standing.Step(Dt, Vector2.Zero, 1, "idle", 0, false, []);
        Assert.NotEmpty(standing.Anchors);
        Assert.True(standing.TryStart("attack")); Assert.Empty(standing.Anchors);
    }

    // ---- Motion sets, looks and entities through the session -------------------------------------------------------

    [Fact]
    public void MotionSetsAreAssignedIndependentlyOfSize()
    {
        var session = Session();
        // Heavy motion on the short build and on the tall build; standard motion on the tall build too.
        Ok(session, session.NewEntity("heavy-short", "Heavy short", "short-broad", EntityAuthoring.Guard));
        Ok(session, session.Edit(session.EntityDocument, () => { session.EntityDocument!.Asset.MotionSet = "heavy"; session.EntityDocument.Asset.Actions[0].Clip = "person-thrust"; session.EntityDocument.Asset.Actions[0].Role = null; }));
        Ok(session, session.DuplicateEntity("heavy-short", "heavy-tall", "Heavy tall"));
        Ok(session, session.Edit(session.EntityDocument, () => session.EntityDocument!.Asset.Model = "tall-thin"));
        Ok(session, session.DuplicateEntity("heavy-tall", "standard-tall", "Standard tall"));
        Ok(session, session.Edit(session.EntityDocument, () => session.EntityDocument!.Asset.MotionSet = "standard"));

        var heavyShort = session.Assets.CompileEntity("heavy-short", out var error); Assert.True(heavyShort is not null, error);
        var heavyTall = session.Assets.CompileEntity("heavy-tall")!; var standardTall = session.Assets.CompileEntity("standard-tall")!;
        Assert.Equal(StarterContent.HeavyWalk, heavyShort.Clip("walk")!.Id);
        Assert.Equal(StarterContent.HeavyWalk, heavyTall.Clip("walk")!.Id);
        Assert.Equal("person-walk", standardTall.Clip("walk")!.Id);
        Assert.Equal("set:heavy", heavyTall.Roles["walk"].Source);
        // The same heavy walk plays on both sizes and rescales its stride with leg length; it is one clip, not a copy.
        Assert.Same(heavyShort.Clip("walk"), heavyTall.Clip("walk"));
        Assert.True(PoseEvaluator.CycleTravel(heavyTall.Model, heavyTall.Clip("walk")!).X > PoseEvaluator.CycleTravel(heavyShort.Model, heavyShort.Clip("walk")!).X * 1.3f);
        // The heavy walk is slower over the same stride as the standard walk on one build.
        var standardPace = PoseEvaluator.CycleTravel(standardTall.Model, standardTall.Clip("walk")!).X / standardTall.Clip("walk")!.Duration;
        var heavyPace = PoseEvaluator.CycleTravel(heavyTall.Model, heavyTall.Clip("walk")!).X / heavyTall.Clip("walk")!.Duration;
        Assert.True(heavyPace < standardPace * .85f);

        // An explicit entity override wins over the set and says so; resetting returns to the set.
        session.Open("heavy-tall");
        Ok(session, session.SetEntityRole("walk", "person-walk"));
        Assert.Equal("entity", session.Entity!.Roles["walk"].Source);
        Assert.False(session.SetEntityRole("walk", "stalker-walk"), "a clip for another base is refused");
        Ok(session, session.SetEntityRole("walk", null));
        Assert.Equal("set:heavy", session.Entity!.Roles["walk"].Source);
    }

    [Fact]
    public void MotionSetsAreEditedOnTheBaseByCopyingNeverInheriting()
    {
        var session = Session();
        Ok(session, session.NewMotionSet("person", "brisk", "Brisk", copyFrom: "standard"));
        var person = session.Assets.Model("person")!;
        var brisk = person.Asset.MotionSets.Single(s => s.Id == "brisk");
        Assert.Equal(person.Asset.MotionSets.Single(s => s.Id == "standard").Roles, brisk.Roles);
        Ok(session, session.AssignRole("person", "brisk", "walk", "person-run"));
        Assert.Equal("person-walk", person.Asset.MotionSets.Single(s => s.Id == "standard").Roles["walk"]); // a copy, not a parent
        Ok(session, session.AssignRole("person", "brisk", "jump", null));
        Assert.False(brisk.Roles.ContainsKey("jump"));
        Assert.False(session.AssignRole("person", "brisk", "walk", "stalker-walk"), "clips for another base are refused");
        Assert.False(session.RemoveMotionSet("person", "deliberate"), "a set an entity selects cannot be removed");
        Assert.Contains("spear-guard", session.Message);
        Ok(session, session.RemoveMotionSet("person", "brisk"));
        person.Undo();
        Assert.Contains(person.Asset.MotionSets, s => s.Id == "brisk");
    }

    [Fact]
    public void LooksWriteVariantOverridesAndSavingOneEditsOnlyTheBase()
    {
        var session = Session();
        Ok(session, session.NewVariant("ranger", "Ranger", "person", "tall-thin"));
        Ok(session, session.ApplyLook("ember"));
        var variant = session.SubjectVariant!;
        Assert.Equal("#eab596", variant.Asset.Parts["body"].Fill);
        Assert.Equal("determined", session.Assets.Resolve("ranger")!.Parts.Single(p => p.Id == "head").Face);
        var person = session.Assets.Model("person")!;
        Assert.False(person.Dirty);

        Ok(session, session.Edit(variant, () => variant.Asset.Parts["body"].Fill = "#88aa66"));
        Ok(session, session.SaveLook("fern", "Fern"));
        Assert.True(person.Dirty);
        Assert.Equal("#88aa66", person.Asset.Looks.Single(l => l.Id == "fern").Parts["body"].Fill);
        Assert.Null(person.Asset.Looks.Single(l => l.Id == "fern").Parts["body"].Width); // looks carry appearance, never sizes
        Assert.Equal("#d8e9db", person.Asset.Parts.Single(p => p.Id == "body").Fill); // the base's own parts are untouched
    }

    [Fact]
    public void AnEntityFromATemplateShowsWhatItStillNeedsThenSavesAndReopens()
    {
        var session = Session();
        Ok(session, session.NewEntity("pike", "Pike guard", "tall-thin", EntityAuthoring.Guard));
        Assert.Equal(Workspace.Entity, session.Mode);
        Assert.Equal("tall-thin", session.SubjectId);
        var document = session.EntityDocument!;
        Assert.Contains(session.Assets.Problems(document), p => p.Contains("role 'attack' is unassigned"));
        Assert.Null(session.Entity);

        Ok(session, session.SetEntityRole("attack", "person-thrust"));
        Assert.Empty(session.Assets.Problems(document));
        Ok(session, session.Edit(document, () =>
        {
            document.Asset.Equipment.Add(new() { Prop = "spear", Socket = "right-grip" });
            document.Asset.Actions[0].Hits.Add(new() { Id = "tip", Prop = "spear", Start = new() { Marker = "strike" }, Finish = new() { Marker = "recover" } });
        }));
        Assert.Equal("role:attack", session.Entity!.Actions["attack"].Source);
        Assert.Equal("entity", session.Entity.Roles["attack"].Source);

        // Preview the thrust in the viewport: the hit region shows only inside strike → recover.
        session.PreviewActionOf("attack");
        var strike = session.Entity.Actions["attack"].Hits[0];
        session.Seek(strike.Start - .01f); Assert.Empty(session.EvaluateEntity()!.Attacks);
        session.Seek(strike.Start + .01f); var preview = session.EvaluateEntity()!;
        Assert.Single(preview.Attacks);
        Assert.Equal(3, preview.Hurt.Count);
        Assert.Single(session.Scene()); Assert.NotNull(session.Scene()[0].Entity);

        // A new entity plays in a test snapshot before it is ever saved.
        Assert.Contains(session.Assets.SnapshotEntities().Entities, e => e.Id == "pike");
        Ok(session, session.Save(document));
        var reopened = Session();
        Assert.Empty(reopened.Assets.Problems(reopened.Assets.Entity("pike")!));
        Assert.Equal("tall-thin", reopened.Assets.CompileEntity("pike")!.Model.Id);

        // Duplicating keeps references: a sibling on the same variant, clips and props.
        Ok(reopened, reopened.DuplicateEntity("pike", "pike-2", "Pike guard 2"));
        var copy = reopened.Assets.CompileEntity("pike-2")!;
        Assert.Equal("tall-thin", copy.Model.Id); Assert.Equal("spear", copy.Equipment.Single().Prop.Id);
        Assert.Same(reopened.Assets.Clip("person-thrust")!.Asset, copy.Actions["attack"].Clip);
    }

    [Fact]
    public void DependenciesAreNavigableBothWays()
    {
        var session = Session();
        var walkUsers = session.Assets.UsedBy("person-walk");
        Assert.Contains(walkUsers, r => r.Id == "person" && r.Relation == "motion set Standard: walk");
        Assert.Contains(session.Assets.UsedBy("tall-thin"), r => r.Id == "spear-guard" && r.Relation == "model");
        Assert.Contains(session.Assets.UsedBy("person"), r => r.Id == "tall-thin" && r.Relation == "base");
        Assert.Contains(session.Assets.Uses("spear-guard"), r => r.Id == "spear" && r.Relation.StartsWith("equipment"));
        Assert.Contains(session.Assets.Uses("spear-guard"), r => r.Id == "person-thrust");
        Assert.Contains(session.Assets.EntitiesOn("person"), e => e.Id == "spear-guard");
        session.Open("spear-guard");
        Assert.Equal(Workspace.Entity, session.Mode);
        session.Open("tall-thin");
        Assert.Equal(Workspace.Model, session.Mode);
    }

    [Fact]
    public void ChangingASharedWalkOnceReachesEveryEntityThatWalks()
    {
        var session = Session();
        Ok(session, session.NewEntity("heavy-guard", "Heavy guard", "short-broad", EntityAuthoring.Stationary));
        Ok(session, session.Edit(session.EntityDocument, () => session.EntityDocument!.Asset.MotionSet = "standard"));
        var walk = session.Assets.Clip("person-walk")!;
        Ok(session, session.Edit(walk, () => walk.Asset.Markers.Add(new() { Id = "step", Time = .3f })));
        foreach (var id in new[] { "heavy-guard", "spear-guard", "player" })
        {
            var entity = session.Assets.CompileEntity(id, out var error); Assert.True(entity is not null, error);
            Assert.Same(walk.Asset, entity.Clip("walk"));
            Assert.Contains(entity.Clip("walk")!.Markers, m => m.Id == "step");
        }
        Assert.Equal(1, session.Assets.DirtyDocuments.Count(d => d.Kind == AssetKind.Animation)); // one clip changed, nothing copied
    }

    // ---- Scale -------------------------------------------------------------------------------------------------------

    [Fact]
    public void AFiftyVariantCatalogLoadsQuicklyAndSharesItsClips()
    {
        var person = AuthoredCatalog.Load(_root).Models["person"]; var random = new Random(4);
        var looks = person.Looks.Select(l => l.Id).ToArray(); var sets = new[] { "standard", "heavy", "deliberate" };
        for (var i = 0; i < 50; i++)
        {
            var build = new PersonBuild
            {
                Legs = .8f + (float)random.NextDouble() * .5f, Torso = .85f + (float)random.NextDouble() * .35f, Arms = .85f + (float)random.NextDouble() * .35f,
                Width = .7f + (float)random.NextDouble() * .7f, Head = .85f + (float)random.NextDouble() * .3f,
            };
            var variant = build.Apply(person, $"person-{i:00}", $"Person {i:00}");
            EntityAuthoring.ApplyLook(person, variant, looks[i % looks.Length]);
            variant.Save(Path.Combine(_root, "variants", variant.Id + ".json"));
            var entity = EntityAuthoring.New(EntityAuthoring.Stationary, $"villager-{i:00}", $"Villager {i:00}", ResolvedModel.From(person, variant));
            entity.MotionSet = sets[i % sets.Length];
            entity.Save(Path.Combine(_root, "entities", entity.Id + ".json"));
        }
        var clipFiles = Directory.GetFiles(Path.Combine(_root, "animations")).Length;

        var clock = Stopwatch.StartNew();
        var catalog = AuthoredCatalog.Load(_root);
        var loaded = clock.Elapsed;
        Assert.True(catalog.Errors.Count == 0, string.Join("\n", catalog.Errors));
        Assert.Equal(52, catalog.Variants.Count); Assert.Equal(53, catalog.Entities.Count);
        Assert.Equal(clipFiles, catalog.Animations.Count); // fifty people, no copied clips
        var idles = catalog.Entities.Values.Where(e => e.Id.StartsWith("villager-")).Select(e => e.Clip("idle")!).Distinct(ReferenceEqualityComparer.Instance).ToList();
        Assert.Single(idles); // every villager's idle is the one shared clip object
        Assert.True(loaded < TimeSpan.FromSeconds(5), $"catalog load took {loaded}");

        clock.Restart();
        var workspace = AuthoringWorkspace.Open(_root);
        foreach (var document in workspace.Documents) Assert.Empty(workspace.Problems(document));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"workspace open and check took {clock.Elapsed}");
    }
}
