# Kinematic entities

The reusable runtime types live in `App2d.Core.Physics.Motion`.
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
matters when the path reverses inside a step. Authored definitions and published observations
are separate from live motion clocks and physics bodies.

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
Other easings live in `App2d.Core.Mathematics.Progress.Easing`; custom maps can implement
`IProgressMap`. A closed curve whose endpoint tangents differ will still have a
velocity change at the loop seam. B-spline parameter progress is not generally
constant distance, so a constant-speed spline would need an arc-length map.

`MovingPlatform2D` composes a line segment with ping-pong and smoothstep easing.
It sets `OneWaySurfaceNormal` through App2d's `PlatformCollisionRules2D` surface data
and enables the generic `TransfersContactMotion` behavior on its body. Its `Speed`
keeps the original round-trip duration: smoothstep slows near endpoints and
reaches 1.5 times that speed at mid-leg. Existing level and editor records still
describe a straight travel vector and speed. New JSON objects use generic curve definitions.

## JSON objects

`ObjectAsset2D` (`App2d.Core.Objects`) serializes one finite local `geometry`, optional
inline `material`, optional `physics`, optional `surface` and optional `motion`.
Load with `ObjectAsset2D.Load(path)` or `FromJson(json)`; `Save(path)` and `ToJson()`
preserve the authored definition. Geometry and curves reuse their existing kind-based codecs.
Unknown properties, unknown progress names and invalid combinations are rejected on loading.

The working example is [objects/lift.json](../../../Assets/Static/levels/cavern/objects/lift.json).
It has rectangle geometry, fill and outline, a kinematic body, rider carrying, a one-way normal,
and a cubic Bézier path. The default world places it near the original playground arrival:

```json
"objects": [
  {
    "id": "arrival-lift",
    "asset": "objects/lift.json",
    "position": { "x": 80, "y": -240 }
  }
]
```

Asset paths are relative to the world manifest; texture paths are relative to the object asset's
directory. Placement IDs are unique within the world. Multiple placements can reference the
same definition and have independent runtime identities and clocks. No thing-type registration
is needed for each asset.

`motion.curve` is relative to the placement origin, using the same world units as geometry.
`periodSeconds` is the entire cycle, including both legs of ping-pong. Its `progress` list
applies maps left to right:

| Progress | Behavior |
| --- | --- |
| `["smooth"]` | There, with smooth acceleration and deceleration |
| `["reverse", "smooth"]` | Back, with smooth acceleration and deceleration |
| `["ping-pong", "smoother"]` | There and back, with smoother turnaround |

Forward/backward open paths use `"repeat": false`; they hold their endpoint and report zero
velocity after completion. Repetition defaults to true and requires the curve/progress
combination to return to its initial position. Constant parameter progress does not imply
constant distance speed on a spline.

A material opts into rendering (fill, outline, texture and RGBA tint). A physics block opts into
collision and body simulation; it reuses `PhysicsBodySettings2D`, including friction, mass,
inertia and restitution. `transfersContactMotion` controls rider carrying. Path motion on a
physical object requires `"motionType": "Kinematic"`; visual-only motion is also supported.
Without motion, static and dynamic bodies use the same asset. One geometry supplies both drawing
and collision in this version. Shared character materials, part paint, skeleton bindings,
rotation paths and contact effects are outside this slice.

`AuthoredObject2D` binds definitions to the game. The session advances motion before physics
through `UpdateWorldMotion`, then publishes object definitions and transform states alongside
legacy platforms. Presentation owns separate visuals and consumes those observations.

`dotnet run --project App2d -- --object-study artifacts/object-study` exports five actual
renderer frames of the JSON lift and a circle orbit using the same definition and runtime.

`PlatformCollisionRules2D.Attach(physics).Surface(body).OneWaySurfaceNormal` specifies
the outward normal of the only blocking face. Use `Vector2.UnitY` for a one-way floor.
The game's pair filter owns temporary drop-through exclusions, and its
`OnBeforeCollision` callback checks contact direction and approach. Core bodies
carry no one-way or wall-grip metadata. Kinematic entities
start with zero restitution, so a resting body is not bounced by their motion.
Friction still defaults to zero. `TransfersContactMotion` uses the previous step's
contact to carry a dynamic body along a kinematic surface and match its supporting
normal velocity. The physics solver does not keep a persistent contact manifold or
apply damping.
