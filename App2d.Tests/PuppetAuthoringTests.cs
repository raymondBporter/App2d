using System.Numerics;
using System.Text.Json;
using App2d.Core.Characters;
using App2d.Rendering.Characters;
using Xunit;

namespace App2d.Tests;

public sealed class PuppetAuthoringTests
{
    [Fact]
    public void EmptyAndHeadlessCharactersNeedNoLibraryOrAnatomy()
    {
        var empty = PuppetDefinition.FromJson(new PuppetDefinition().ToJson());
        var drawing = new PuppetDrawing(); drawing.Build(empty, PuppetPose.Rest(empty));
        Assert.Equal(0, drawing.Mesh.Count);
        var puppet = PuppetTemplates.StickFigure(); puppet.RemoveControl("head");
        puppet = PuppetDefinition.FromJson(puppet.ToJson());
        var pose = PuppetPose.Sample(puppet, puppet.Motions[0], 0);
        drawing.Build(puppet, pose);
        Assert.True(drawing.Mesh.Count > 0);
        Assert.DoesNotContain(puppet.Parts, p => p.A == "head" || p.B == "head");
        Assert.DoesNotContain("head", pose.Points.Keys);
    }

    [Fact]
    public void JsonRoundTripPreservesCoordinatesAndRejectsMisspelledFields()
    {
        var puppet = PuppetTemplates.StepStudy();
        var restored = PuppetDefinition.FromJson(puppet.ToJson());
        Assert.Equal(puppet.ToJson(), restored.ToJson());
        Assert.Equal(.25f, restored.Motions[0].Keys[8].Position.X);
        Assert.Throws<JsonException>(() => PuppetDefinition.FromJson(puppet.ToJson().Replace("\"contacts\":", "\"contcats\":")));
        Assert.Throws<InvalidDataException>(() => PuppetDefinition.FromJson("{\"controls\":null}"));
        Assert.Throws<InvalidDataException>(() => PuppetDefinition.FromJson("{\"motions\":[{\"keys\":null}]}"));
    }

    [Fact]
    public void PlantedFeetStayInWorldSpaceDuringTravelBetweenKeys()
    {
        var puppet = PuppetTemplates.StepStudy(); var motion = puppet.Motions[0];
        for (var i = 0; i <= 120; i++)
        {
            var pose = PuppetPose.Sample(puppet, motion, i * motion.Duration / 120);
            Assert.NotEmpty(pose.Contacts);
            foreach (var contact in pose.Contacts)
            {
                Assert.InRange(contact.Error, 0, .00001f);
                Assert.InRange(Vector3.Distance(pose.World(contact.End), contact.Target), 0, .00001f);
            }
            AssertBoneLengths(puppet, pose);
        }
        Assert.InRange(PuppetPose.Sample(puppet, motion, .3).World("left-foot").Y, .14f, .15f);
    }

    [Fact]
    public void WalkNearlyExtendsEachSupportKneeWhileTheSwingKneeStillFlexes()
    {
        var puppet = PuppetTemplates.StepStudy(); var motion = puppet.Motions[0];
        foreach (var (time, support, swing) in new[] { (.3, "right", "left"), (.9, "left", "right") })
        {
            var pose = PuppetPose.Sample(puppet, motion, time);
            float KneeAngle(string side)
            {
                var thigh = Vector3.Normalize(pose.Points[side + "-hip"] - pose.Points[side + "-knee"]);
                var shin = Vector3.Normalize(pose.Points[side + "-foot"] - pose.Points[side + "-knee"]);
                return MathF.Acos(Math.Clamp(Vector3.Dot(thigh, shin), -1, 1)) * 180 / MathF.PI;
            }
            Assert.InRange(KneeAngle(support), 165, 178);
            Assert.InRange(KneeAngle(swing), 90, 145);
            AssertBoneLengths(puppet, pose);
            Assert.All(pose.Contacts, contact => Assert.InRange(contact.Error, 0, .00001f));
        }
    }

    [Fact]
    public void UnreachableContactPreservesLengthsAndReportsResidual()
    {
        var puppet = PuppetTemplates.StepStudy(); var motion = puppet.Motions[0];
        motion.Contacts[0].Target = new(20, 0);
        var pose = PuppetPose.Sample(puppet, motion, .2);
        Assert.True(pose.Contacts[0].Error > 18);
        AssertBoneLengths(puppet, pose);
    }

    [Fact]
    public void IkBendChoiceStaysOnItsChosenSideAcrossInterpolatedPoses()
    {
        var puppet = PuppetTemplates.StepStudy();
        foreach (var bend in new[] { -1, 1 })
        {
            foreach (var chain in puppet.Chains) chain.Bend = bend;
            for (var i = 0; i < 24; i++)
            {
                var pose = PuppetPose.Sample(puppet, puppet.Motions[0], i / 20f);
                foreach (var chain in puppet.Chains)
                {
                    var a = pose.Points[chain.Joint] - pose.Points[chain.Root];
                    var b = pose.Points[chain.End] - pose.Points[chain.Root];
                    Assert.True((b.X * a.Y - b.Y * a.X) * bend >= -1e-6f);
                }
            }
        }
    }

    [Fact]
    public void RemovingAChainControlCleansContactsAndEveryKey()
    {
        var puppet = PuppetTemplates.StepStudy(); puppet.RemoveControl("right-knee"); puppet.Validate();
        Assert.DoesNotContain(puppet.Chains, c => c.End == "right-foot");
        Assert.DoesNotContain(puppet.Motions[0].Contacts, c => c.End == "right-foot");
        Assert.All(puppet.Motions[0].Keys, k => Assert.False(k.Points.ContainsKey("right-knee")));
    }

    [Fact]
    public void InvalidTopologyAndOverlappingContactsAreRejected()
    {
        var puppet = PuppetTemplates.StickFigure(); puppet.Bones.Add(new() { From = "head", To = "hips" });
        Assert.Contains("cycle", Assert.Throws<InvalidDataException>(puppet.Validate).Message);
        puppet = PuppetTemplates.StepStudy(); puppet.Motions[0].Contacts.Add(puppet.Motions[0].Contacts[0] with { });
        Assert.Contains("overlap", Assert.Throws<InvalidDataException>(puppet.Validate).Message);
        puppet = PuppetTemplates.StickFigure(); puppet.Controls.Find(c => c.Id == "left-knee")!.Rest = puppet.Controls.Find(c => c.Id == "left-hip")!.Rest;
        Assert.Contains("length", Assert.Throws<InvalidDataException>(puppet.Validate).Message);
    }

    [Fact]
    public void BodyTranslationMovesDescendantsWhileContactsRemainPinned()
    {
        var puppet = PuppetTemplates.StepStudy(); var motion = puppet.Motions[0];
        var pose = PuppetPose.Sample(puppet, motion, .3);
        var originalHead = pose.Points["head"]; pose.Move(puppet, "hips", pose.Points["hips"] + new Vector3(.03f, -.02f, 0));
        Assert.InRange(Vector3.Distance(originalHead + new Vector3(.03f, -.02f, 0), pose.Points["head"]), 0, 1e-6f);
        motion.Keys = [pose.Key(.3f)];
        var evaluated = PuppetPose.Sample(puppet, motion, .3);
        Assert.InRange(evaluated.Contacts[0].Error, 0, .00001f); AssertBoneLengths(puppet, evaluated);
    }

    [Fact]
    public void RepeatedMotionCarriesRootTravelAndContactTargetsTogether()
    {
        var puppet = PuppetTemplates.StepStudy(); var motion = puppet.Motions[0]; motion.Loop = true;
        var first = PuppetPose.Sample(puppet, motion, .3, true);
        var later = PuppetPose.Sample(puppet, motion, motion.Duration * 3 + .3, true);
        Assert.InRange(later.Position.X - first.Position.X, 1.49999f, 1.50001f);
        Assert.InRange(later.Contacts[0].Target.X - first.Contacts[0].Target.X, 1.49999f, 1.50001f);
        Assert.InRange(later.Contacts[0].Error, 0, .00001f);
    }

    [Fact]
    public void WalkSeamMatchesEveryControlAndMaintainsForwardTravel()
    {
        var puppet = PuppetTemplates.StepStudy(); var motion = puppet.Motions[0];
        Assert.True(motion.Loop);
        var start = PuppetPose.Sample(puppet, motion, 0);
        var end = PuppetPose.Sample(puppet, motion, motion.Duration);
        foreach (var id in start.Points.Keys) Assert.InRange(Vector3.Distance(start.Points[id], end.Points[id]), 0, 1e-5f);
        var before = PuppetPose.Sample(puppet, motion, motion.Duration - .0001, true);
        var after = PuppetPose.Sample(puppet, motion, motion.Duration + .0001, true);
        foreach (var id in start.Points.Keys) Assert.InRange(Vector3.Distance(before.World(id), after.World(id)), 0, .001f);
        Assert.True(after.Position.X > before.Position.X);
        Assert.True(start.World("right-shoulder").Z > start.World("left-shoulder").Z);
        Assert.True(start.World("right-hand").X < start.World("right-shoulder").X);
        Assert.True(start.World("left-hand").X > start.World("left-shoulder").X);
    }

    [Fact]
    public void IkMovesAttachedDescendantsWithTheirTip()
    {
        var puppet = PuppetTemplates.StickFigure();
        var hand = puppet.Controls.Single(c => c.Id == "right-hand").Rest.XYZ;
        puppet.Controls.Add(new() { Id = "grip", Rest = PuppetPoint.From(hand + new Vector3(.1f, .1f, 0)) });
        puppet.Bones.Add(new() { From = "right-hand", To = "grip" }); puppet.Validate();
        var pose = PuppetPose.Rest(puppet); pose.Move(puppet, "right-hand", hand + new Vector3(.2f, .1f, 0));
        Assert.InRange(Vector3.Distance(pose.Points["grip"] - pose.Points["right-hand"], new(.1f, .1f, 0)), 0, 1e-6f);
        Assert.Throws<InvalidOperationException>(() => pose.Move(puppet, "right-elbow", Vector3.Zero));
    }

    [Fact]
    public void PrimitivesProduceFiniteGeometryAtAllStudyPoses()
    {
        var puppet = PuppetTemplates.StepStudy(); var drawing = new PuppetDrawing();
        for (var i = 0; i <= 30; i++)
        {
            drawing.Build(puppet, PuppetPose.Sample(puppet, puppet.Motions[0], i * .04));
            Assert.True(drawing.Mesh.TriangleCount > 100);
            Assert.True(float.IsFinite(drawing.Mesh.Min.X) && float.IsFinite(drawing.Mesh.Max.Y));
        }
    }

    [Fact]
    public void RunPlantsEachFootThenFliesTwicePerLoop()
    {
        var puppet = PuppetDefinition.FromJson(PuppetTemplates.RunStudy().ToJson()); var motion = puppet.Motions[0];
        var airborne = 0;
        for (var i = 0; i <= 240; i++)
        {
            var pose = PuppetPose.Sample(puppet, motion, i * motion.Duration / 240, repeat: true);
            foreach (var contact in pose.Contacts)
                Assert.InRange(Vector3.Distance(pose.World(contact.End), contact.Target), 0, .00001f);
            foreach (var foot in new[] { "left-foot", "right-foot" })
                Assert.True(pose.World(foot).Y >= .025f - .00001f, $"{foot} dips below the ground at sample {i}.");
            if (pose.Contacts.Count == 0) airborne++;
            AssertBoneLengths(puppet, pose);
        }
        Assert.InRange(airborne, 70, 110);
        var end = PuppetPose.Sample(puppet, motion, motion.Duration - 1e-4);
        var next = PuppetPose.Sample(puppet, motion, motion.Duration + 1e-4, repeat: true);
        foreach (var id in next.Points.Keys) Assert.InRange(Vector3.Distance(end.World(id), next.World(id)), 0, .01f);
    }

    private static void AssertBoneLengths(PuppetDefinition puppet, PuppetPose pose)
    {
        foreach (var chain in puppet.Chains)
            foreach (var (a, b) in new[] { (chain.Root, chain.Joint), (chain.Joint, chain.End) })
            {
                var rest = Vector2.Distance(puppet.Controls.Single(c => c.Id == a).Rest.XY, puppet.Controls.Single(c => c.Id == b).Rest.XY);
                var posed = pose.Points[a] - pose.Points[b];
                Assert.InRange(MathF.Abs(new Vector2(posed.X, posed.Y).Length() - rest), 0, .00001f);
            }
    }
}
