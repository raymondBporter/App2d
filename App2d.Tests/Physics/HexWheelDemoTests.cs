using App2d.Core.Physics;
using App2d.Demos;

namespace App2d.Tests.Physics;

public sealed class HexWheelDemoTests
{
    [Fact]
    public void LatticeBuildsAnAnnulusUsingOnePolygonAndTwoAnchorsPerEdge()
    {
        var wheel = CreateWheel();
        var initialCenter = wheel.Hub.WorldCenterOfMass;
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
        Assert.True(wheel.BeginGrab(wheel.Tiles[0].WorldCenterOfMass));
        Assert.Equal(count + 1, wheel.World.Constraints.Count);
        wheel.EndGrab();
        Assert.Equal(count, wheel.World.Constraints.Count);
    }

    [Fact]
    public void MotorAndGravityRunThroughTheRigidBodyWorld()
    {
        var wheel = CreateWheel();
        var initialY = wheel.Hub.WorldCenterOfMass.Y;
        var landed = false;
        for (var i = 0; i < 100; i++)
        {
            wheel.Step(-.85f);
            landed |= wheel.World.LastContacts.Any(contact =>
                contact.First.MotionType == BodyMotionType2D.Static || contact.Second.MotionType == BodyMotionType2D.Static);
        }

        Assert.True(landed);
        Assert.True(wheel.Hub.WorldCenterOfMass.Y < initialY);
        Assert.InRange(wheel.Hub.WorldObject.Transform.Rotation, -2f, -.05f);
        Assert.Contains(wheel.DrivenTiles, tile => MathF.Abs(tile.WorldObject.Transform.Rotation) > .05f);
        Assert.All(wheel.Tiles, tile =>
        {
            Assert.True(Vector2f.IsFinite(tile.WorldCenterOfMass));
            Assert.True(float.IsFinite(tile.WorldObject.Transform.Rotation));
        });
    }

    private static HexWheelSimulation CreateWheel() =>
        new(HexWheelSimulation.DefaultTileRadius, HexWheelSimulation.DefaultInnerRadius, .4f);
}
