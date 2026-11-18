using App2d.Core.Physics;
using App2d.Demos;

namespace App2d.Tests.Physics;

public sealed class HexWheelDemoTests
{
    [Fact]
    public void LatticeBuildsAnAnnulusUsingOnePolygonAndTwoAnchorsPerEdge()
    {
        var wheel = CreateWheel();
        var initialCenter = wheel.DriveCenter;
        var sharedShape = wheel.Tiles[0].WorldObject.Shape;

        Assert.InRange(wheel.Tiles.Count, 250, 400);
        Assert.NotEmpty(wheel.DrivenTiles);
        Assert.True(wheel.DrivenTiles.Count < wheel.Tiles.Count);
        Assert.All(wheel.Tiles, tile =>
        {
            Assert.Same(sharedShape, tile.WorldObject.Shape);
            Assert.Equal(BodyMotionType2D.Dynamic, tile.MotionType);
            Assert.False(tile.FreezeRotation);
            var distance = Vector2f.Distance(initialCenter, tile.WorldCenterOfMass);
            Assert.InRange(distance, wheel.InnerRadius - 1e-5f, HexWheelSimulation.OuterRadius + 1e-5f);
        });
        foreach (var pair in wheel.Seams.GroupBy(joint => (joint.First, joint.Second)))
        {
            Assert.Equal(2, pair.Count());
            Assert.All(pair, joint => Assert.InRange(Vector2f.Distance(joint.FirstWorldAnchor, joint.SecondWorldAnchor), 0f, 2e-6f));
        }

        var count = wheel.World.Constraints.Count;
        Assert.Equal(wheel.Seams.Count, count);
        Assert.Equal(wheel.Tiles.Count + 2, wheel.World.Bodies.Count); // Tiles, ground, and grab cursor.
        Assert.True(wheel.BeginGrab(wheel.Tiles[0].WorldCenterOfMass));
        Assert.Equal(count + 1, wheel.World.Constraints.Count);
        wheel.EndGrab();
        Assert.Equal(count, wheel.World.Constraints.Count);
    }

    [Theory]
    [InlineData(-1.4f)]
    [InlineData(1.4f)]
    public void MotorRollsTheWheelInEitherDirection(float targetSpeed)
    {
        var wheel = CreateWheel();
        var initialCenter = wheel.DriveCenter;
        var tile = wheel.DrivenTiles[0];
        var initialRadius = tile.WorldCenterOfMass - initialCenter;
        var landed = false;
        for (var i = 0; i < 100; i++)
        {
            wheel.Step(targetSpeed);
            landed |= wheel.World.LastContacts.Any(contact =>
                contact.First.MotionType == BodyMotionType2D.Static || contact.Second.MotionType == BodyMotionType2D.Static);
        }

        Assert.True(landed);
        var center = wheel.DriveCenter;
        Assert.True(center.Y < initialCenter.Y);
        Assert.True((center.X - initialCenter.X) * targetSpeed < -.1f);
        Assert.True(Vector2f.Dot(initialRadius.PerpCcw, tile.WorldCenterOfMass - center) * targetSpeed > .1f);
        Assert.All(wheel.Tiles, body =>
        {
            Assert.True(Vector2f.IsFinite(body.WorldCenterOfMass));
            Assert.True(float.IsFinite(body.WorldObject.Rotation));
        });
    }

    [Fact]
    public void StifferSeamsStretchLessUnderTheWheelsWeight()
    {
        var wheel = CreateWheel();
        var softWheel = CreateWheel();
        softWheel.SetSuspension(8f, HexWheelSimulation.DefaultJointDamping);

        for (var i = 0; i < 100; i++)
        {
            wheel.Step(null);
            softWheel.Step(null);
        }

        var stretch = MeanSquaredSeamGap(wheel);
        var softStretch = MeanSquaredSeamGap(softWheel);
        Assert.True(stretch < .5f * softStretch, $"Squared seam gaps were {stretch:G4} and {softStretch:G4}.");
    }

    private static float MeanSquaredSeamGap(HexWheelSimulation wheel) => wheel.Seams.Average(joint =>
        Vector2f.DistanceSquared(joint.FirstWorldAnchor, joint.SecondWorldAnchor));

    private static HexWheelSimulation CreateWheel() =>
        new(HexWheelSimulation.DefaultTileRadius, HexWheelSimulation.DefaultInnerRadius, .4f);
}
