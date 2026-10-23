using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;

namespace App2d.Tests.Physics;

public sealed class PhysicsBodySettings2DTests
{
    [Fact]
    public void SettingsCreateARegisteredBodyWithTheRequestedProperties()
    {
        var settings = new PhysicsBodySettings2D
        {
            MotionType = BodyMotionType2D.Kinematic,
            Mass = 3,
            MomentOfInertia = 20,
            FreezeRotation = false,
            GravityScale = .5f,
            Friction = .4f,
            Restitution = .15f
        };
        var world = new PhysicsWorld2D();
        var spatial = new SpatialObject2D(Circle2D.Builder.Create(2));

        var body = settings.CreateBody(world, spatial);

        Assert.Same(body, Assert.Single(world.Bodies));
        Assert.Same(spatial, body.WorldObject);
        Assert.Same(body, body.Collider.UserData);
        Assert.Equal(BodyMotionType2D.Kinematic, body.MotionType);
        Assert.Equal(3f, body.Mass);
        Assert.Equal(20f, body.MomentOfInertia);
        Assert.False(body.FreezeRotation);
        Assert.Equal(.5f, body.GravityScale);
        Assert.Equal(.4f, body.Friction);
        Assert.Equal(.15f, body.Restitution);
    }

    [Theory]
    [InlineData("Mass")]
    [InlineData("MomentOfInertia")]
    [InlineData("GravityScale")]
    [InlineData("Friction")]
    [InlineData("Restitution")]
    public void InvalidSettingsCannotLeaveAPartiallyRegisteredBody(string property)
    {
        var settings = new PhysicsBodySettings2D();
        typeof(PhysicsBodySettings2D).GetProperty(property)!.SetValue(settings, float.NaN);
        var world = new PhysicsWorld2D();

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.CreateBody(world, new SpatialObject2D(Circle2D.Builder.Create(2))));
        Assert.Empty(world.Bodies);
    }
}
