using App2d.Core;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.HeightFields;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Input;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Trials;

namespace App2d.Tests.Trials;

public sealed class TrialsTerrain2DTests
{
    [Fact]
    public void AnalyticHeightFieldUsesTheSharedVisibleAndCollidableSampledSurface()
    {
        var field = new FunctionHeightField2D(-100f, 100f, -100f,
            x => 20f * MathF.Sin(x * 0.05f), x => MathF.Cos(x * 0.05f), yMax: 20f);
        var world = new PhysicsWorld2D();
        var terrain = new TrialsTerrain2D(world, field);
        Assert.Same(field, terrain.HeightField);
        Assert.IsType<HeightFieldShape2D>(terrain.Ground.Shape);
        Assert.Single(world.Bodies);
        for (var x = -100f; x <= 100f; x += 2f)
        {
            Assert.InRange(MathF.Abs(terrain.HeightAt(x) - field.HeightAt(x)), 0f, 0.1f);
            Assert.True(terrain.Ground.Shape.ContainsPoint(new(x, terrain.HeightAt(x) - 1f)));
            Assert.False(terrain.Ground.Shape.ContainsPoint(new(x, terrain.HeightAt(x) + 1f)));
        }
        Assert.Equal(terrain.HeightAt(-100f), terrain.HeightAt(-200f));
        Assert.Equal(terrain.HeightAt(100f), terrain.HeightAt(200f));
        Assert.Throws<ArgumentException>(() => new TrialsTerrain2D(new PhysicsWorld2D(),
            new FunctionHeightField2D(0f, 100f, float.NegativeInfinity, _ => 0f)));
    }

    [Fact]
    public void CourseHasFlatSpawnSmoothHillsAndTwoAuthoredRamps()
    {
        using var game = new TrialsGame2D();
        Assert.Equal(3, Assert.IsType<BSpline2D>(game.Terrain.Curve).Degree);
        Assert.Equal(0f, game.Terrain.HeightAt(0f));
        Assert.Equal(0f, game.Terrain.HeightAt(300f));
        Assert.True(game.Terrain.HeightAt(2280f) > 40f, $"First ramp height {game.Terrain.HeightAt(2280f)}");
        Assert.True(game.Terrain.HeightAt(5160f) > 100f, $"Second ramp height {game.Terrain.HeightAt(5160f)}");
        Assert.NotEqual(game.Terrain.HeightAt(3000f), game.Terrain.HeightAt(3400f));
        Assert.True(game.Terrain.Ground.Shape.ContainsPoint(new(2280f, game.Terrain.HeightAt(2280f) - 10f)));
        Assert.False(game.Terrain.Ground.Shape.ContainsPoint(new(2280f, game.Terrain.HeightAt(2280f) + 10f)));
    }

    [Fact]
    public void BikeLandsOnRampAndCameraFollowsItsHeight()
    {
        using var game = new TrialsGame2D();
        game.Initialize();
        game.Bike.Reset(new(2150f, game.Terrain.HeightAt(2150f) + 180f));
        var input = new InputState();
        input.SetKey(Keys.Space, true);
        for (var step = 0; step < 480; step++)
        {
            var time = new FrameTime(1f / 120f, step / 120d, step);
            game.Update(time, input);
            game.AdvancePresentation(time);
            input.EndFrame();
        }
        foreach (var wheel in new[] { game.Bike.FrontWheel, game.Bike.RearWheel })
        {
            var clearance = ShapeDistance2D.SignedDistance(wheel.WorldCenterOfMass, game.Terrain.Ground.Shape);
            Assert.InRange(clearance, TrialsBike2D.WheelRadius - 0.5f, TrialsBike2D.WheelRadius + 0.5f);
        }
        var targetHeight = game.Terrain.HeightAt(game.Bike.Frame.WorldObject.Transform.Position.X) + 160f;
        Assert.InRange(Math.Abs(game.Camera.Position.Y - targetHeight), 0f, 5f);
        Assert.InRange(Math.Abs(game.Bike.FrontJoint.LateralError), 0f, 0.2f);
    }

    [Fact]
    public void ManualPolylineTerrainUsesItsAuthoredHeightsAndRejectsBacktracking()
    {
        var world = new PhysicsWorld2D();
        var terrain = new TrialsTerrain2D(world, PolylineCurve2D.Builder.Create([new(0, 0), new(100, 50), new(200, 50)]), -100f);
        Assert.Equal(25f, terrain.HeightAt(50f), 4);
        Assert.Equal(50f, terrain.HeightAt(150f), 4);
        Assert.Throws<ArgumentException>(() => new TrialsTerrain2D(new PhysicsWorld2D(),
            PolylineCurve2D.Builder.Create([new(0, 0), new(100, 50), new(50, 0)])));
    }

    [Fact]
    public void BikeCanDriveFromTheFlatStartOntoTheFirstHills()
    {
        using var game = new TrialsGame2D();
        game.Initialize();
        var input = new InputState();
        input.SetKey(Keys.D, true);
        for (var step = 0; step < 1200; step++)
        {
            game.Update(new FrameTime(1f / 120f, step / 120d, step), input);
            input.EndFrame();
        }
        var position = game.Bike.Frame.WorldObject.Transform.Position;
        Assert.True(position.X > 1400f, $"Bike reached {position}; velocity {game.Bike.Frame.LinearVelocity}");
        Assert.True(float.IsFinite(position.Y));
    }
}
