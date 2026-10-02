# Kinematic entities

`KinematicEntity2D` accepts any `IShape2D` and any deterministic
`IKinematicMotion2D`. A motion defines world-space `Position(t)` and `Velocity(t)`.
Create the entity with a physics world and collision layers, call `Update(dt)`
before `PhysicsWorld2D.Step(dt)`, and dispose it when removed.

```csharp
public sealed class RisingMotion(Vector2 origin) : IKinematicMotion2D
{
    public Vector2 Position(double seconds) => origin + new Vector2(0f, (float)seconds * 20f);
    public Vector2 Velocity(double seconds) => new(0f, 20f);
}

using var mover = new KinematicEntity2D(id, physics, shape,
    new RisingMotion(origin), worldLayer, actorMask);
mover.Update(dt);
physics.Step(dt);
```

The entity sets the physics body's velocity to the displacement needed to reach
`Position(t + dt)` during that step. This is the average velocity over the step;
`MotionVelocity` exposes the path's instantaneous `Velocity(t)`. That distinction
matters when the path reverses inside a step. `MovingPlatform2D` saves the motion's
elapsed time for rollback; the physics snapshot owns its body pose and velocity.

For periodic motion, `CurveMotion2D` combines an `ICurve2D`, a period, and a
normalized progress map. The curve determines *where* progress lies; the map
determines *when* it gets there. `ProgressMaps.Loop` runs forward and wraps to
zero. Use it with a closed curve, such as a full `Arc2D`. The constructor rejects
a wrap that would jump to a different position. `ProgressMaps.PingPong` runs from
0 to 1 and back, so an open line or spline can repeat without a position jump.

```csharp
var line = new LineSegmentCurve2D(start, end);
var smoothRoundTrip = ProgressMaps.Compose(ProgressMaps.PingPong, Easing.Smooth);
var motion = new CurveMotion2D(line, periodSeconds: 4, smoothRoundTrip);

var circle = new Arc2D(center, radius, 0f, 2f * MathF.PI);
var orbit = new CurveMotion2D(circle, periodSeconds: 8, ProgressMaps.Loop);
```

`CurveMotion2D.Velocity(t)` uses the curve derivative and the map derivative.
Other easings live in `App2d.Core.Mathematics.Easing`; custom maps can implement
`IProgressMap`. A closed curve whose endpoint tangents differ will still have a
velocity change at the loop seam. B-spline parameter progress is not generally
constant distance, so a constant-speed spline would need an arc-length map.

`MovingPlatform2D` composes a line segment with ping-pong and smoothstep easing.
It sets `OneWaySurfaceNormal` and `TransfersContactMotion` on its body. Its `Speed`
keeps the original round-trip duration: smoothstep slows near endpoints and
reaches 1.5 times that speed at mid-leg. Existing level and editor records still
describe a straight travel vector and speed; other curves can be used in code.

Physics bodies can set `OneWaySurfaceNormal` to the outward normal of their only
blocking face. `IsOneWayPlatform` remains a `+Y` shorthand. Kinematic entities
start with zero restitution, so a resting body is not bounced by their motion.
Friction still defaults to zero. `TransfersContactMotion` uses the previous step's
contact to carry a dynamic body along a kinematic surface and match its supporting
normal velocity. The physics solver does not keep a persistent contact manifold or
apply damping.
