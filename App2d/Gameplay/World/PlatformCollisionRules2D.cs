using App2d.Core.Collision.Contacts;
using App2d.Core.Collision.Filtering;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Gameplay.World;

/// <summary>Side-scroller one-way surfaces and per-actor drop-through exclusions, installed on a generic physics world.</summary>
public sealed class PlatformCollisionRules2D : IPairFilter2D<RigidBody2D>, IPhysicsCollisionCallbacks2D
{
    private static readonly ConditionalWeakTable<PhysicsWorld2D, PlatformCollisionRules2D> Rules = [];
    private readonly PhysicsWorld2D _world;
    private readonly IPairFilter2D<RigidBody2D> _previousPairFilter;
    private readonly IPhysicsCollisionCallbacks2D? _previousCallbacks;
    private readonly Dictionary<RigidBody2D, PlatformSurface2D> _surfaces = [];
    private readonly Dictionary<RigidBody2D, HashSet<RigidBody2D>> _ignored = [];

    private PlatformCollisionRules2D(PhysicsWorld2D world)
    {
        _world = world;
        _previousPairFilter = world.PairFilter;
        _previousCallbacks = world.CollisionCallbacks;
        world.PairFilter = this;
        world.CollisionCallbacks = this;
        world.BodyRemoved += RemoveBody;
    }

    /// <summary>Installs once per world, preserving any existing pair filter and collision callbacks.</summary>
    public static PlatformCollisionRules2D Attach(PhysicsWorld2D world) => Rules.GetValue(ArgGuard.RequireNotNull(world), static world => new(world));

    public PlatformSurface2D Surface(RigidBody2D body)
    {
        ArgGuard.ThrowIfNull(body);
        if (!_surfaces.TryGetValue(body, out var surface))
        {
            RequireOwned(body);
            _surfaces.Add(body, surface = new());
        }
        return surface;
    }

    public void IgnorePlatform(RigidBody2D actor, RigidBody2D platform)
    {
        RequireOwned(actor);
        RequireOwned(platform);
        ArgGuard.ThrowIfSameReference(actor, platform, "A body cannot ignore itself as a platform.");
        if (!_ignored.TryGetValue(actor, out var ignored))
        {
            _ignored.Add(actor, ignored = []);
        }
        ignored.Add(platform);
    }

    public bool IsIgnoringPlatform(RigidBody2D actor, RigidBody2D platform) =>
        _ignored.TryGetValue(actor, out var ignored) && ignored.Contains(platform);

    public int IgnoredPlatformCount(RigidBody2D actor) => _ignored.TryGetValue(actor, out var ignored) ? ignored.Count : 0;
    public void ClearIgnoredPlatforms(RigidBody2D actor) => _ignored.Remove(actor);

    public void RemoveIgnoredPlatformsWhere(RigidBody2D actor, Predicate<RigidBody2D> match)
    {
        ArgGuard.ThrowIfNull(match);
        if (_ignored.TryGetValue(actor, out var ignored))
        {
            ignored.RemoveWhere(match);
            if (ignored.Count == 0) _ignored.Remove(actor);
        }
    }

    public bool ShouldTest(RigidBody2D first, RigidBody2D second) =>
        _previousPairFilter.ShouldTest(first, second) && !IsExcluded(first, second);

    public bool OnBeforeCollision(PhysicsContact2D contact)
    {
        if (IsExcluded(contact.First, contact.Second) || _previousCallbacks?.OnBeforeCollision(contact) == false)
        {
            return false;
        }
        if (_surfaces.TryGetValue(contact.First, out var first) && first.OneWaySurfaceNormal is { } firstNormal &&
            !AllowsOneWaySurface(contact.First, contact.Second, first, firstNormal, -contact.Geometry.Normal, contact.Geometry))
        {
            return false;
        }
        if (_surfaces.TryGetValue(contact.Second, out var second) && second.OneWaySurfaceNormal is { } secondNormal &&
            !AllowsOneWaySurface(contact.Second, contact.First, second, secondNormal, contact.Geometry.Normal, contact.Geometry))
        {
            return false;
        }
        return true;
    }

    public void OnAfterCollision(PhysicsContact2D contact) => _previousCallbacks?.OnAfterCollision(contact);

    private bool IsExcluded(RigidBody2D first, RigidBody2D second) =>
        IsIgnoringPlatform(first, second) || IsIgnoringPlatform(second, first);

    private void RequireOwned(RigidBody2D body)
    {
        ArgGuard.ThrowIfNull(body);
        ArgGuard.ThrowIf(!_world.Bodies.Contains(body), "The body must belong to this platform-rules world.", nameof(body));
    }

    private void RemoveBody(RigidBody2D body)
    {
        _surfaces.Remove(body);
        _ignored.Remove(body);
        foreach (var ignored in _ignored.Values) ignored.Remove(body);
    }

    private static bool AllowsOneWaySurface(RigidBody2D surface, RigidBody2D other, PlatformSurface2D settings,
        Vector2 surfaceNormal, Vector2 otherSeparationNormal, CollisionContact2D geometry)
    {
        // Rounded corner contacts must pass through just like vertical platform sides.
        if (Vector2.Dot(otherSeparationNormal, surfaceNormal) < 0.999999f)
        {
            return false;
        }
        if (!surface.WorldObject.WorldBounds.IsFinite || !other.WorldObject.WorldBounds.IsFinite)
        {
            return false;
        }

        var face = MaximumProjection(surface.WorldObject, surfaceNormal);
        if (MathF.Abs(Vector2.Dot(geometry.Point, surfaceNormal) - face) > settings.OneWaySlop + geometry.PenetrationDepth)
        {
            return false;
        }
        // A tumbling body's previous support point must use its previous rotation as well as position.
        var pose = other.WorldObject.CollisionPose;
        var rotation = Matrix3x2.CreateRotation(other.PreviousRotation - other.WorldObject.Transform.Rotation);
        var previousPose = Similarity2D.FromAxis(other.PreviousPosition,
            Vector2.TransformNormal(pose.XAxis, rotation), pose.IsMirrored, pose.Scale);
        var previousNearSide = -MaximumProjection(other.WorldObject.Shape, previousPose, -surfaceNormal);
        if (previousNearSide < face - settings.OneWaySlop)
        {
            return false;
        }
        var relativeMotion = Vector2.Dot(
            other.WorldObject.Transform.Position - other.PreviousPosition -
            (surface.WorldObject.Transform.Position - surface.PreviousPosition), surfaceNormal);
        return relativeMotion <= 0f && Vector2.Dot(other.LinearVelocity - surface.LinearVelocity, surfaceNormal) <= 0f;
    }

    private static float MaximumProjection(SpatialObject2D worldObject, Vector2 normal) =>
        MaximumProjection(worldObject.Shape, worldObject.CollisionPose, normal);

    private static float MaximumProjection(IShape2D shape, Similarity2D pose, Vector2 normal)
    {
        if (shape is IConvexShape2D convex)
        {
            var point = pose.TransformPoint(convex.GetSupportPoint(pose.TransposeTransformDirection(normal)));
            return Vector2.Dot(point, normal);
        }
        if (shape is CompositeShape2D composite)
        {
            var maximum = float.NegativeInfinity;
            foreach (var part in composite.Parts)
                maximum = MathF.Max(maximum, MaximumProjection(part, pose, normal));
            return maximum;
        }
        var bounds = ShapeBounds2D.CalculateTransformed(shape, pose.ToMatrix());
        return Vector2.Dot(bounds.Center, normal) + (MathF.Abs(normal.X) * bounds.Size.X + MathF.Abs(normal.Y) * bounds.Size.Y) * 0.5f;
    }
}
