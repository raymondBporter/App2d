using App2d.Core.Constraints;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Physics;
using App2d.Core.Physics.Constraints;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Trials;

/// <summary>A front triangle, a hinged rear triangle with a shock, and a sliding front suspension fork.</summary>
public sealed class TrialsBike2D
{
    public const float WheelRadius = 26f;
    public const uint BikeCollisionLayer = 2;
    public const uint GroundCollisionLayer = 1;

    // Bike-local geometry: change these points to reshape both the graphics and attachment locations.
    public static readonly Vector2 RearAxle = new(-78f, 0f);
    public static readonly Vector2 FrontAxle = new(86f, 0f);
    private static readonly Vector2 Crank = new(-10f, 0f);
    private static readonly Vector2 SeatTop = new(-24f, 56f);
    private static readonly Vector2 Head = new(52f, 51f);
    private static readonly Vector2 SwingPivot = new(-12f, 4f);
    private static readonly Vector2 RearShockMount = new(-42f, 28f);
    private static readonly Vector2 ForkAxis = Vector2.Normalize(Head - FrontAxle);
    private static readonly XnaColor FrameColor = new(255, 197, 102);
    private static readonly XnaColor TireColor = new(26, 32, 40);
    private static readonly XnaColor MetalColor = new(175, 197, 210);
    private static readonly Circle2D Rim = Circle2D.Builder.Create(21f);
    private static readonly Circle2D InnerRim = Circle2D.Builder.Create(18f);
    private static readonly Circle2D Hub = Circle2D.Builder.Create(4f);

    public TrialsBike2D(PhysicsWorld2D world)
    {
        IConvexShape2D[] tubes =
        [
            Tube(SeatTop, Crank, 5f), Tube(SeatTop, Head, 5f), Tube(Head, Crank, 5f),
            Tube(Head, Head - ForkAxis * 30f, 7f), Tube(Crank, SwingPivot, 5f),
            Tube(SeatTop, new(-27f, 65f), 4f), Tube(new(-39f, 65f), new(-10f, 65f), 6f),
            Tube(Head, new(49f, 71f), 4f), Tube(new(49f, 71f), new(67f, 74f), 4f)
        ];
        Frame = AddBody(CompositeShape2D.Builder.Create(tubes), 6f);
        Swingarm = AddBody(CompositeShape2D.Builder.Create([
            Tube(RearAxle, RearShockMount, 5f), Tube(RearShockMount, SwingPivot, 5f), Tube(SwingPivot, RearAxle, 5f)
        ]), 2f);
        RearWheel = AddBody(Circle2D.Builder.Create(WheelRadius), 1.2f);
        FrontWheel = AddBody(Circle2D.Builder.Create(WheelRadius), 1.2f);
        Bodies = Array.AsReadOnly(new[] { Frame, Swingarm, RearWheel, FrontWheel });
        SwingarmJoint = new RevoluteJoint2D(Frame, Swingarm, SwingPivot, SwingPivot);
        RearJoint = new RevoluteJoint2D(Swingarm, RearWheel, RearAxle, Vector2.Zero);
        FrontJoint = new WheelJoint2D(Frame, FrontWheel, FrontAxle, Vector2.Zero, ForkAxis)
        {
            FrequencyHertz = 6f,
            DampingRatio = 0.7f,
            TranslationLimits = ConstraintLimit1D.Between(0f, 28f)
        };
        var shockLength = Vector2.Distance(SeatTop, RearShockMount);
        RearShock = new SpringDamperJoint2D(Frame, Swingarm, SeatTop, RearShockMount, shockLength)
        {
            FrequencyHertz = 7f,
            DampingRatio = 0.7f,
            LengthLimits = ConstraintLimit1D.Between(shockLength - 14f, shockLength + 2f)
        };
        world.Constraints.Add(SwingarmJoint);
        world.Constraints.Add(RearJoint);
        world.Constraints.Add(RearShock);
        world.Constraints.Add(FrontJoint);

        RigidBody2D AddBody(IShape2D shape, float mass)
        {
            var body = world.AddBody(new SpatialObject2D(shape), BodyMotionType2D.Dynamic);
            body.SetMassFromShape(mass);
            body.FreezeRotation = false;
            body.Restitution = 0f;
            body.Friction = 1.4f;
            body.CollisionLayer = BikeCollisionLayer;
            body.CollisionMask = GroundCollisionLayer;
            return body;
        }
    }

    public RigidBody2D Frame { get; }
    public RigidBody2D Swingarm { get; }
    public RigidBody2D RearWheel { get; }
    public RigidBody2D FrontWheel { get; }
    public RevoluteJoint2D SwingarmJoint { get; }
    public RevoluteJoint2D RearJoint { get; }
    public WheelJoint2D FrontJoint { get; }
    public SpringDamperJoint2D RearShock { get; }
    public IReadOnlyList<RigidBody2D> Bodies { get; }

    public void Drive(float throttle, float lean, bool brake)
    {
        // Prototype hub motor: equal reaction torque goes to the wheel's carrier.
        if (throttle != 0f) Motor(RearWheel, Swingarm, -throttle * 44f, 40_000f);
        if (brake)
        {
            Motor(RearWheel, Swingarm, 0f, 30_000f);
            Motor(FrontWheel, Frame, 0f, 30_000f);
        }
        // Prototype rider input; a separate rider body can supply this weight shift later.
        Frame.AddTorque(lean * 24_000f);

        void Motor(RigidBody2D wheel, RigidBody2D carrier, float targetSpeed, float maximumTorque)
        {
            var relativeSpeed = wheel.AngularVelocity - carrier.AngularVelocity;
            var torque = Math.Clamp((targetSpeed - relativeSpeed) * 2_500f, -maximumTorque, maximumTorque);
            wheel.AddTorque(torque);
            carrier.AddTorque(-torque);
        }
    }

    public void Reset(Vector2 position)
    {
        foreach (var body in Bodies)
        {
            body.WorldObject.Transform.Rotation = 0f;
            body.WorldObject.Transform.Position = position;
            body.LinearVelocity = Vector2.Zero;
            body.AngularVelocity = 0f;
            body.ClearAccumulators();
        }
        RearWheel.WorldObject.Transform.Position += RearAxle;
        FrontWheel.WorldObject.Transform.Position += FrontAxle;
    }

    public void Translate(Vector2 displacement)
    {
        foreach (var body in Bodies) body.WorldObject.Transform.Position += displacement;
    }

    public void Render(Renderer2D renderer)
    {
        renderer.DrawShape(Frame.WorldObject, FrameColor);
        renderer.DrawShape(Swingarm.WorldObject, FrameColor);
        DrawFork();
        DrawShock();
        var pivot = SwingarmJoint.FirstWorldAnchor;
        renderer.DrawShape(Hub, Matrix3x2.CreateTranslation(pivot), MetalColor);
        DrawWheel(RearWheel);
        DrawWheel(FrontWheel);
        var crank = Vector2.Transform(Crank, Frame.WorldObject.Transform.Matrix);
        renderer.DrawShape(Hub, Matrix3x2.CreateTranslation(crank), MetalColor);

        void DrawFork()
        {
            var head = Vector2.Transform(Head, Frame.WorldObject.Transform.Matrix);
            var axis = FrontJoint.WorldAxis;
            renderer.DrawWorldThickSegment(head, head - axis * 34f, 7f, MetalColor);
            renderer.DrawWorldThickSegment(head - axis * 20f, FrontJoint.SecondWorldAnchor, 4f, new XnaColor(224, 234, 240));
        }

        void DrawShock()
        {
            var start = RearShock.FirstWorldAnchor;
            var end = RearShock.SecondWorldAnchor;
            var axis = Vector2.Normalize(end - start);
            var normal = new Vector2(-axis.Y, axis.X);
            renderer.DrawWorldThickSegment(start, end, 3f, MetalColor);
            var previous = start;
            for (var segment = 1; segment <= 10; segment++)
            {
                var point = Vector2.Lerp(start, end, segment / 11f) + normal * (segment % 2 == 0 ? -4f : 4f);
                renderer.DrawWorldThickSegment(previous, point, 2f, new XnaColor(230, 116, 94));
                previous = point;
            }
            renderer.DrawWorldThickSegment(previous, end, 2f, new XnaColor(230, 116, 94));
        }

        void DrawWheel(RigidBody2D wheel)
        {
            var matrix = wheel.WorldObject.Transform.Matrix;
            renderer.DrawShape(wheel.WorldObject, TireColor);
            renderer.DrawShape(Rim, matrix, MetalColor);
            renderer.DrawShape(InnerRim, matrix, TireColor);
            for (var spoke = 0; spoke < 4; spoke++)
            {
                var angle = spoke * MathF.PI / 2f;
                var end = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 19f;
                renderer.DrawWorldThickSegment(Vector2.Transform(Vector2.Zero, matrix),
                    Vector2.Transform(end, matrix), 1.2f, MetalColor);
            }
            renderer.DrawShape(Hub, matrix, FrameColor);
        }
    }

    private static ConvexPolygon2D Tube(Vector2 start, Vector2 end, float width)
    {
        var direction = Vector2.Normalize(end - start);
        var normal = new Vector2(-direction.Y, direction.X) * (width / 2f);
        return ConvexPolygon2D.Builder.Create([start - normal, end - normal, end + normal, start + normal]);
    }
}
