using System.Numerics;
using App2d.Collision;
using App2d.Collision.Contacts;
using App2d.Collision.Filtering;
using App2d.Core;
using App2d.Physics.Filtering;
using App2d.Physics.Integration;
using App2d.Physics.Solvers;

namespace App2d.Physics;

public sealed class PhysicsWorld2D
{
    private readonly List<PhysicsBody2D> _bodies = [];
    private readonly List<PhysicsContact2D> _lastContacts = [];
    private readonly List<IPhysicsConstraint2D> _constraints = [];
    private readonly Dictionary<(PhysicsBody2D First, PhysicsBody2D Second), PhysicsContact2D> _frameContacts = [];
    private readonly Dictionary<(PhysicsBody2D First, PhysicsBody2D Second), PhysicsContact2D> _substepContacts = [];
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

    public PhysicsBody2D AddBody(SpatialObject2D worldObject, BodyMotionType2D motionType)
    {
        var collider = CollisionSystem.AddCollider(
            worldObject,
            motionType == BodyMotionType2D.Static
                ? ColliderMobility2D.Static
                : ColliderMobility2D.Dynamic);
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
        return true;
    }

    public void Step(float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        if (deltaSeconds == 0f)
            return;
        StateGuard.ThrowIfNotPositive(MaxSubstepSeconds);
        StateGuard.ThrowIfLessThan(PositionIterations, 1);
        StateGuard.ThrowIfLessThan(VelocityIterations, 1);

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

        _lastContacts.AddRange(_frameContacts.Values);
        foreach (var body in _bodies)
            body.ClearAccumulators();
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
                if (!AllowsOneWayContact(contact))
                    continue;

                foundContact = true;
                _substepContacts[(firstBody, secondBody)] = contact;
                _frameContacts[(firstBody, secondBody)] = contact;
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

        foreach (var contact in _substepContacts.Values)
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

    private static bool AllowsOneWayContact(PhysicsContact2D contact)
    {
        if (contact.First.IsOneWayPlatform && !AllowsOneWayPlatform(contact.First, contact.Second, -contact.Geometry.Normal, contact.Geometry))
        {
            return false;
        }

        if (contact.Second.IsOneWayPlatform && !AllowsOneWayPlatform(contact.Second, contact.First, contact.Geometry.Normal, contact.Geometry))
        {
            return false;
        }

        return true;
    }

    private static bool AllowsOneWayPlatform(
        PhysicsBody2D platform,
        PhysicsBody2D other,
        Vector2 otherSeparationNormal,
        CollisionContact2D geometry)
    {
        if (other.IsIgnoringOneWayPlatform(platform))
            return false;

        const float minimumTopNormalY = 0.9f;
        if (otherSeparationNormal.Y < minimumTopNormalY)
            return false;

        var platformBounds = platform.WorldObject.WorldBounds;
        var otherBounds = other.WorldObject.WorldBounds;
        if (!platformBounds.IsFinite || !otherBounds.IsFinite)
            return false;

        var top = platformBounds.Max.Y;
        if (MathF.Abs(geometry.Point.Y - top) > platform.OneWaySlop + geometry.PenetrationDepth)
            return false;

        var previousBottom = otherBounds.Min.Y +
            other.PreviousPosition.Y - other.WorldObject.Transform.Position.Y;
        if (previousBottom < top - platform.OneWaySlop)
            return false;

        var relativeVerticalMotion =
            other.WorldObject.Transform.Position.Y - other.PreviousPosition.Y -
            (platform.WorldObject.Transform.Position.Y - platform.PreviousPosition.Y);
        if (relativeVerticalMotion > 0f)
            return false;

        var relativeVerticalSpeed = other.LinearVelocity.Y - platform.LinearVelocity.Y;
        return relativeVerticalSpeed <= 0f;
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
