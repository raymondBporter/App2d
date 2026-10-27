using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>
/// P1c of the character-system migration: one bridge lifts a planar placement into the 3D frame props use, and one
/// projection brings a lifted axis back to gameplay XY. Grip, tip, muzzle, attack regions and both facings are pinned by
/// the P0 references; these tests pin the bridge's own contract.
/// </summary>
public sealed class PropPlacement3DTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    [Fact]
    public void LiftExtendsThePlanarFrameAndAppliesTiltTwistTurnBeforeIt()
    {
        var planar = LayeredFrame2D.Local(Vector2.One, .4f, new(1, 2), -.15f).Then(LayeredFrame2D.FromRotation(.3f, new(5, 1, .25f)));
        var flat = PartPlacement3D.Lift(planar, Vector3.Zero);
        Assert.Equal(PartPlacement3D.Extend(planar), flat);
        Assert.Equal(planar.Origin, flat.Origin);
        Assert.Equal(Vector3.UnitZ, flat.ZAxis);
        Assert.Equal(.1f, flat.Origin.Z, 6);

        var orientation = new Vector3(.5f, -.8f, .2f);
        var lifted = PartPlacement3D.Lift(planar, orientation);
        // The same frame the socket path built before the bridge: rotate about X, Y, then the planar turn plus Z, at the origin.
        var turn = MathF.Atan2(planar.XAxis.Y, planar.XAxis.X);
        var expected = Frame3D.FromRotation(new(orientation.X, orientation.Y, turn + orientation.Z), planar.Origin);
        TestModels.Near(expected.XAxis, lifted.XAxis, 1e-5f, "x");
        TestModels.Near(expected.YAxis, lifted.YAxis, 1e-5f, "y");
        TestModels.Near(expected.ZAxis, lifted.ZAxis, 1e-5f, "z");
        Assert.Equal(planar.Origin, lifted.Origin);
        // Thickness stays physical: a point off the prop's plane leaves the layer by the tilted Z axis.
        Assert.NotEqual(planar.Layer, lifted.TransformPoint(new(0, 0, .3f)).Z);

        // A mirrored planar frame mirrors the lifted prop, as the facing does for a whole actor.
        var mirrored = PartPlacement3D.Lift(planar.Then(new ActorPose(new EvaluatedPose(), Vector2.Zero, -1).Placement), orientation);
        TestModels.Near(lifted.XAxis with { X = -lifted.XAxis.X }, mirrored.XAxis, 1e-5f, "mirrored x");
        TestModels.Near(lifted.Origin with { X = -lifted.Origin.X }, mirrored.Origin, 1e-5f, "mirrored origin");
        Assert.Equal(lifted.Origin.Z, mirrored.Origin.Z);
    }

    [Fact]
    public void ProjectionIsUnitInXYAndFallsBackToTheFacingHeadOn()
    {
        var tilted = Frame3D.FromRotation(new(0, .7f, .4f));
        var axis = PartPlacement3D.ProjectAxis(tilted, 1);
        Assert.Equal(1, axis.Length(), 5);
        TestModels.Near(new(Vector2.Normalize(tilted.XAxis.XY), 0), new(axis, 0), 1e-6f, "projection");
        foreach (var facing in new[] { 1, -1 })
        {
            Assert.Equal(new Vector2(facing, 0), PartPlacement3D.ProjectAxis(Frame3D.FromRotation(new(0, MathF.PI / 2, 0)), facing));
        }
    }

    [Fact]
    public void PropPointsAndTheEditorPlaneAgreeWithTheLiftedFrame()
    {
        var hero = Catalog.Entities["hero"];
        var sword = Catalog.Parts["sword"];
        var clip = Catalog.Animations["player-sword-side-cut"];
        var strike = clip.Markers.First(m => m.Id == "strike").Time;
        var pose = new ActorPose(PoseEvaluator.Sample(hero.Model, clip, strike, false, new() { InPlace = true }), new(2, 0), -1);
        var frame = pose.Socket(hero.Sockets["sword-hand"]);
        Assert.Equal(frame.Origin, PartPlacement3D.Point(frame, sword, sword.Point(EquipmentPoints.Grip)!.Value));
        var tip = PartPlacement3D.Point(frame, sword, sword.Point(EquipmentPoints.Tip)!.Value);
        TestModels.Near(frame.TransformPoint((sword.Point(EquipmentPoints.Tip)!.Value.XYZ - sword.Point(EquipmentPoints.Grip)!.Value.XYZ) * sword.Scale), tip, 1e-6f, "tip");
        Assert.True(MathF.Abs(tip.Z - frame.Origin.Z) > .05f, "a tilted blade has real depth");

        var at = new PuppetPoint(.3f, .1f, 0);
        var plane = PartPlacement3D.Plane(frame, sword, at);
        Assert.Equal(PartPlacement3D.Point(frame, sword, at).XY, plane.Origin);
        TestModels.Near(new(PartPlacement3D.Point(frame, sword, at with { X = at.X + 1 }).XY, 0), new(plane.TransformPoint(Vector2.UnitX), 0), 1e-5f, "x step");
        TestModels.Near(new(PartPlacement3D.Point(frame, sword, at with { Y = at.Y + 1 }).XY, 0), new(plane.TransformPoint(Vector2.UnitY), 0), 1e-5f, "y step");
    }
}
