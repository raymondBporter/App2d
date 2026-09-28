using App2d.Core.Characters.Authored;

namespace App2d.Tests.Authored;

public sealed class HumanProportionsTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    [Theory, InlineData("person"), InlineData("tall-thin"), InlineData("short-broad"), InlineData("brute"), InlineData("cinder")]
    public void EveryHumanHasEightyPercentLegsAndKeepsItsUpperBody(string id)
    {
        var actual = Catalog.Resolve(id);
        var original = ResolvedModel.From(PersonTemplate.StudyReference(), actual.Variant);
        foreach (var side in new[] { "left", "right" })
        {
            Assert.Equal(original.Length(side + "-hip", side + "-knee") * .8f, actual.Length(side + "-hip", side + "-knee"), 5);
            Assert.Equal(original.Length(side + "-knee", side + "-foot") * .8f, actual.Length(side + "-knee", side + "-foot"), 5);
            TestModels.Near(original.Rest[side + "-foot"], actual.Rest[side + "-foot"]);
            Assert.Equal(original.Length(side + "-shoulder", side + "-elbow"), actual.Length(side + "-shoulder", side + "-elbow"), 5);
            Assert.Equal(original.Length(side + "-elbow", side + "-hand"), actual.Length(side + "-elbow", side + "-hand"), 5);
        }
        Assert.Equal(original.Measure("torso"), actual.Measure("torso"), 5);
        foreach (var part in new[] { "head", "body" })
        {
            var before = original.Parts.Single(p => p.Id == part); var after = actual.Parts.Single(p => p.Id == part);
            Assert.Equal(before.Width, after.Width); Assert.Equal(before.Height, after.Height);
        }
        foreach (var control in PersonTemplate.Model().Controls)
            TestModels.Near(control.Rest.XYZ, Catalog.Resolve("person").Rest[control.Id]);
    }

    [Theory, InlineData("person-walk"), InlineData("person-run"), InlineData("person-heavy-walk")]
    public void ShortLeggedHumansReachTheirGaitTargetsWithoutFootSliding(string clipId)
    {
        var clip = Catalog.Animations[clipId];
        foreach (var id in new[] { "person", "tall-thin", "short-broad", "brute", "cinder" })
        {
            var model = Catalog.Resolve(id);
            var original = ResolvedModel.From(PersonTemplate.StudyReference(), model.Variant);
            Assert.Equal(PoseEvaluator.CycleTravel(original, clip).X * .8f, PoseEvaluator.CycleTravel(model, clip).X, 5);
            for (var i = 0; i <= 240; i++)
            {
                var pose = PoseEvaluator.Sample(model, clip, i * clip.Duration / 120.0, repeat: true);
                foreach (var chain in pose.Chains.Where(c => c.Chain.EndsWith("-leg")))
                    Assert.True(chain.Reached, $"{id}/{clipId}/{chain.Chain} at frame {i}: {chain.Residual}");
                Assert.All(pose.Contacts, c => Assert.InRange(c.Residual, 0, 1e-4f));
            }
            foreach (var contact in clip.Contacts)
            {
                var end = model.Chains[contact.Chain].End;
                var start = PoseEvaluator.Sample(model, clip, contact.Start).World(end);
                var mid = PoseEvaluator.Sample(model, clip, (contact.Start + contact.Finish) / 2).World(end);
                TestModels.Near(start, mid, 1e-4f, id + "/" + clipId + "/planted foot");
            }
        }
    }
}
