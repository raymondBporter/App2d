using App2d.Core.Collision;
using App2d.Core.Collision.Contacts;
using App2d.Core.Collision.Filtering;
using App2d.Core.Geometry;
using App2d.Core.Physics.Filtering;
using App2d.Core.Physics.Integration;
using App2d.Core.Physics.Solvers;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Physics;

public sealed partial class PhysicsWorld2D
{
    private readonly List<PhysicsBody2D> _bodies = [];
    private readonly List<PhysicsContact2D> _lastContacts = [];
    private readonly List<IPhysicsConstraint2D> _constraints = [];
    // Contacts keep first-seen order explicitly so solver order never depends on
    // dictionary enumeration; a pair seen again in a later iteration is updated in place.
    private readonly ContactSet _frameContacts = new();
    private readonly ContactSet _substepContacts = new();
    private readonly List<CollisionPair2D> _collisionContacts = [];
    private readonly PhysicsColliderPairFilter _physicsColliderPairFilter;

    public PhysicsWorld2D()
        : this(new CollisionSystem2D())
    {
    }

    public PhysicsWorld2D(CollisionSystem2D collisionSystem)
    {
        CollisionSystem = ArgGuard.RequireNotNull(collisionSystem);
        _physicsColliderPairFilter = new PhysicsColliderPairFilter(this);
    }

    public Vector2 Gravity { get; set; }
    public int PositionIterations { get; set; } = 4;
    public int VelocityIterations { get; set; } = 1;
    public float MaxSubstepSeconds { get; set; } = 1f / 60f;
    public IPhysicsIntegrator2D Integrator { get; set; } = new SemiImplicitEulerIntegrator2D();
    public IPairFilter2D<PhysicsBody2D> PairFilter { get; set; } = new DefaultPhysicsPairFilter2D();
    public IPhysicsPositionSolver2D PositionSolver { get; set; } = new MassWeightedPositionSolver2D();
    public IPhysicsVelocitySolver2D VelocitySolver { get; set; } = new ImpulseVelocitySolver2D();
    public CollisionSystem2D CollisionSystem { get; }
    public IReadOnlyList<PhysicsBody2D> Bodies => _bodies;
    public IReadOnlyList<PhysicsContact2D> LastContacts => _lastContacts;
    public int LastCandidatePairCount => CollisionSystem.LastCandidatePairCount;
    public IList<IPhysicsConstraint2D> Constraints => _constraints;

    public PhysicsBody2D AddBody(SpatialObject2D worldObject, BodyMotionType2D motionType, int? restoredColliderId = null)
    {
        var collider = CollisionSystem.AddCollider(
            worldObject,
            motionType == BodyMotionType2D.Static
                ? ColliderMobility2D.Static
                : ColliderMobility2D.Dynamic, restoredColliderId);
        var body = new PhysicsBody2D(worldObject, motionType, collider);
        collider.UserData = body;
        _bodies.Add(body);
        return body;
    }

    public bool RemoveBody(PhysicsBody2D body)
    {
        ArgGuard.ThrowIfNull(body);
        if (!_bodies.Remove(body))
            return false;
        CollisionSystem.RemoveCollider(body.Collider);
        _lastContacts.RemoveAll(c => ReferenceEquals(c.First, body) || ReferenceEquals(c.Second, body));
        foreach (var remaining in _bodies)
            remaining.RemoveIgnoredOneWayPlatformsWhere(platform => ReferenceEquals(platform, body));
        return true;
    }

    public void Step(float deltaSeconds)
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(deltaSeconds);
        if (deltaSeconds == 0f)
            return;
        StateGuard.ThrowIfNotPositive(MaxSubstepSeconds);
        StateGuard.ThrowIfLessThan(PositionIterations, 1);
        StateGuard.ThrowIfLessThan(VelocityIterations, 1);

        TransferContactMotion(deltaSeconds);
        foreach (var body in _bodies)
        {
            body.PreviousPosition = body.WorldObject.Transform.Position;
            body.PreviousRotation = body.WorldObject.Transform.Rotation;
        }

        _lastContacts.Clear();
        _frameContacts.Clear();
        var substepCount = Math.Max(1, (int)MathF.Ceiling(deltaSeconds / MaxSubstepSeconds));
        var substepSeconds = deltaSeconds / substepCount;

        for (var substep = 0; substep < substepCount; substep++)
            StepOnce(substepSeconds);

        _lastContacts.AddRange(_frameContacts.Contacts);
        foreach (var body in _bodies)
        {
            body.LastStepLinearVelocity = body.LinearVelocity;
            body.ClearAccumulators();
        }
    }

    private void TransferContactMotion(float deltaSeconds)
    {
        foreach (var contact in _lastContacts)
        {
            PhysicsBody2D surface;
            PhysicsBody2D rider;
            Vector2 normal;
            if (contact.First.MotionType == BodyMotionType2D.Kinematic &&
                contact.First.TransfersContactMotion)
            {
                surface = contact.First;
                rider = contact.Second;
                normal = -contact.Geometry.Normal;
            }
            else if (contact.Second.MotionType == BodyMotionType2D.Kinematic &&
                contact.Second.TransfersContactMotion)
            {
                surface = contact.Second;
                rider = contact.First;
                normal = contact.Geometry.Normal;
            }
            else
            {
                continue;
            }

            if (rider.MotionType != BodyMotionType2D.Dynamic || rider.IsSensor ||
                rider.IsIgnoringOneWayPlatform(surface))
            {
                continue;
            }

            const float separationSpeedTolerance = 0.0001f;
            var previousRelativeSpeed = Vector2.Dot(
                rider.LastStepLinearVelocity - surface.LastStepLinearVelocity, normal);
            var riderVelocityChange = Vector2.Dot(
                rider.LinearVelocity - rider.LastStepLinearVelocity, normal);
            if (previousRelativeSpeed > separationSpeedTolerance ||
                riderVelocityChange > separationSpeedTolerance)
            {
                continue; // Preserve a rebound or jump applied after the last contact.
            }

            var displacement = surface.LinearVelocity * deltaSeconds;
            rider.WorldObject.Transform.Position += displacement - normal * Vector2.Dot(displacement, normal);
            var relativeNormalSpeed = Vector2.Dot(rider.LinearVelocity - surface.LinearVelocity, normal);
            rider.LinearVelocity -= normal * relativeNormalSpeed;
        }
    }

    public bool IsTouching(PhysicsBody2D body)
    {
        foreach (var contact in _lastContacts)
        {
            if (contact.First == body || contact.Second == body)
                return true;
        }

        return false;
    }

    public bool IsTouching(PhysicsBody2D body, Vector2 direction, float minimumDot = 0.5f)
    {
        if (direction.LengthSquared() <= float.Epsilon)
            return false;

        direction = Vector2.Normalize(direction);
        foreach (var contact in _lastContacts)
        {
            if (contact.First == body && Vector2.Dot(contact.Geometry.Normal, direction) >= minimumDot)
                return true;
            if (contact.Second == body && Vector2.Dot(-contact.Geometry.Normal, direction) >= minimumDot)
                return true;
        }

        return false;
    }

    private void StepOnce(float deltaSeconds)
    {
        foreach (var body in _bodies)
            Integrator.Integrate(body, Gravity, deltaSeconds);

        _substepContacts.Clear();
        for (var iteration = 0; iteration < PositionIterations; iteration++)
        {
            var foundContact = false;
            CollisionSystem.CollectContacts(
                _collisionContacts,
                _physicsColliderPairFilter);
            foreach (var pair in _collisionContacts)
            {
                if (pair.First.UserData is not PhysicsBody2D firstBody ||
                    pair.Second.UserData is not PhysicsBody2D secondBody)
                {
                    continue;
                }

                var contact = new PhysicsContact2D(firstBody, secondBody, pair.Contact);
                if (!AllowsDirectionalContact(contact))
                    continue;

                foundContact = true;
                _substepContacts.Upsert(contact);
                _frameContacts.Upsert(contact);
                if (!firstBody.IsSensor && !secondBody.IsSensor)
                    PositionSolver.Solve(contact);
            }

            if (iteration % 2 == 0)
            {
                foreach (var constraint in _constraints)
                    foundContact |= constraint.SolvePosition(deltaSeconds);
            }
            else
            {
                for (var constraintIndex = _constraints.Count - 1; constraintIndex >= 0; constraintIndex--)
                    foundContact |= _constraints[constraintIndex].SolvePosition(deltaSeconds);
            }

            if (!foundContact)
                break;
        }

        foreach (var contact in _substepContacts.Contacts)
        {
            if (!contact.First.IsSensor && !contact.Second.IsSensor)
                VelocitySolver.Solve(contact);
        }

        for (var iteration = 0; iteration < VelocityIterations; iteration++)
        {
            if (iteration % 2 == 0)
            {
                foreach (var constraint in _constraints)
                    constraint.SolveVelocity(deltaSeconds);
            }
            else
            {
                for (var constraintIndex = _constraints.Count - 1; constraintIndex >= 0; constraintIndex--)
                    _constraints[constraintIndex].SolveVelocity(deltaSeconds);
            }
        }
    }

    private static bool AllowsDirectionalContact(PhysicsContact2D contact)
    {
        if (contact.First.OneWaySurfaceNormal is { } firstNormal &&
            !AllowsOneWaySurface(contact.First, contact.Second, firstNormal,
                -contact.Geometry.Normal, contact.Geometry))
        {
            return false;
        }

        if (contact.Second.OneWaySurfaceNormal is { } secondNormal &&
            !AllowsOneWaySurface(contact.Second, contact.First, secondNormal,
                contact.Geometry.Normal, contact.Geometry))
        {
            return false;
        }

        return true;
    }

    private static bool AllowsOneWaySurface(
        PhysicsBody2D surface,
        PhysicsBody2D other,
        Vector2 surfaceNormal,
        Vector2 otherSeparationNormal,
        CollisionContact2D geometry)
    {
        if (other.IsIgnoringOneWayPlatform(surface))
            return false;

        // A one-way surface has no collidable corner or side. Rounded shapes
        // report diagonal normals at corners, which should pass through.
        const float minimumNormalAlignment = 0.999999f;
        if (Vector2.Dot(otherSeparationNormal, surfaceNormal) < minimumNormalAlignment)
        {
            return false;
        }

        var surfaceBounds = surface.WorldObject.WorldBounds;
        var otherBounds = other.WorldObject.WorldBounds;
        if (!surfaceBounds.IsFinite || !otherBounds.IsFinite)
            return false;

        var face = MaximumProjection(surface.WorldObject, surfaceNormal);
        if (MathF.Abs(Vector2.Dot(geometry.Point, surfaceNormal) - face) >
            surface.OneWaySlop + geometry.PenetrationDepth)
        {
            return false;
        }

        var previousNearSide = MinimumProjection(other.WorldObject, surfaceNormal) +
            Vector2.Dot(other.PreviousPosition - other.WorldObject.Transform.Position, surfaceNormal);
        if (previousNearSide < face - surface.OneWaySlop)
            return false;

        var relativeMotion = Vector2.Dot(
            other.WorldObject.Transform.Position - other.PreviousPosition -
            (surface.WorldObject.Transform.Position - surface.PreviousPosition), surfaceNormal);
        if (relativeMotion > 0f)
            return false;

        var relativeSpeed = Vector2.Dot(other.LinearVelocity - surface.LinearVelocity, surfaceNormal);
        return relativeSpeed <= 0f;
    }

    private static float MaximumProjection(SpatialObject2D worldObject, Vector2 normal)
    {
        if (worldObject.Shape is IConvexShape2D shape)
        {
            var pose = worldObject.CollisionPose;
            var point = pose.TransformPoint(shape.GetSupportPoint(pose.TransposeTransformDirection(normal)));
            return Vector2.Dot(point, normal);
        }

        var bounds = worldObject.WorldBounds;
        return Vector2.Dot(bounds.Center, normal) +
            (MathF.Abs(normal.X) * bounds.Size.X + MathF.Abs(normal.Y) * bounds.Size.Y) * 0.5f;
    }

    private static float MinimumProjection(SpatialObject2D worldObject, Vector2 normal) =>
        -MaximumProjection(worldObject, -normal);

    private sealed class ContactSet
    {
        private readonly List<PhysicsContact2D> _contacts = [];
        private readonly Dictionary<(PhysicsBody2D First, PhysicsBody2D Second), int> _index = [];

        public IReadOnlyList<PhysicsContact2D> Contacts => _contacts;

        public void Upsert(PhysicsContact2D contact)
        {
            var key = (contact.First, contact.Second);
            if (_index.TryGetValue(key, out var position))
            {
                _contacts[position] = contact;
            }
            else
            {
                _index.Add(key, _contacts.Count);
                _contacts.Add(contact);
            }
        }

        public void Clear()
        {
            _contacts.Clear();
            _index.Clear();
        }
    }

    private sealed class PhysicsColliderPairFilter(PhysicsWorld2D world) :
        IPairFilter2D<Collider2D>
    {
        public bool ShouldTest(Collider2D first, Collider2D second) =>
            first.UserData is PhysicsBody2D firstBody &&
            second.UserData is PhysicsBody2D secondBody &&
            world.PairFilter.ShouldTest(firstBody, secondBody);
    }

}
