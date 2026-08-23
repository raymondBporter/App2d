using App2d.Engine.Collision.Filtering;

namespace App2d.Engine.Physics.Filtering;

public sealed class DefaultPhysicsPairFilter2D : IPairFilter2D<PhysicsBody2D>
{
    public bool TestStaticStatic { get; set; }
    public bool TestStaticKinematic { get; set; }
    public bool TestStaticDynamic { get; set; } = true;
    public bool TestKinematicKinematic { get; set; }
    public bool TestKinematicDynamic { get; set; } = true;
    public bool TestDynamicDynamic { get; set; } = true;

    public bool ShouldTest(PhysicsBody2D first, PhysicsBody2D second)
    {
        if (!first.CanCollideWith(second))
            return false;

        var firstMotion = first.MotionType;
        var secondMotion = second.MotionType;
        if (firstMotion > secondMotion)
            (firstMotion, secondMotion) = (secondMotion, firstMotion);

        return (firstMotion, secondMotion) switch
        {
            (BodyMotionType2D.Static, BodyMotionType2D.Static) => TestStaticStatic,
            (BodyMotionType2D.Static, BodyMotionType2D.Kinematic) => TestStaticKinematic,
            (BodyMotionType2D.Static, BodyMotionType2D.Dynamic) => TestStaticDynamic,
            (BodyMotionType2D.Kinematic, BodyMotionType2D.Kinematic) => TestKinematicKinematic,
            (BodyMotionType2D.Kinematic, BodyMotionType2D.Dynamic) => TestKinematicDynamic,
            (BodyMotionType2D.Dynamic, BodyMotionType2D.Dynamic) => TestDynamicDynamic,
            _ => false
        };
    }
}
