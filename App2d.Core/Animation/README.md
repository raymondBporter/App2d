# Keyframe sampling

`KeyframeSampling.Sample` reads an ordered timeline at an absolute time. It finds the surrounding keys with binary
search, computes progress within that interval, and delegates value interpolation to an `IKeyframeInterpolation<TKey, TValue>`
struct. The generic call does not box the policy or allocate a callback. Policies can carry settings, such as whether
a motion track interpolates rotation or position.

```csharp
var value = KeyframeSampling.Sample(keys, seconds, emptyValue, interpolation);
```

Keys implement `ITimedKey` and are ordered by increasing `Time`; asset validation owns that requirement.
Times use the caller's units and are not restricted to [0, 1]. An empty timeline returns `emptyValue`.
A single key is held everywhere. Queries outside the range hold the first or last value. At an exact key time,
the key's own value is returned without segment interpolation, including for outgoing step segments.
`TrySegment` remains available for consumers needing the two keys and their local progress.
`Held(keys, time, holdBeforeFirst: false)` instead returns no key before the first time; attachment and draw-order
tracks use this to preserve setup state. A present key with a null value is distinct from having no key yet.

The interpolation policy reads a key's value, declares held segments through `IsHeld`, and interpolates other segments.
The timeline sampler returns held values before any component curves or easing are evaluated. The policy owns easing,
output range, and value-specific rules; the sampler does not clamp or ease values. Motion and color retain independent
component curves, absolute handles, authored full rotation turns, and their different empty-track defaults.
Setup-before-first behavior remains with the owning track, and color clamping remains with color evaluation.

Motion and color keys store `EasingCurve` and `EasingDirection` separately and resolve them through `Easingf.FromCurve`.
The enum converters keep existing `linear`, `smooth`, and `step` curve names; omitted direction defaults to `in`.
For example, an in-out sine uses `"ease": "sine", "easeDirection": "in-out"`. Travel belongs to the motion or
playback clock. Step with direction In holds the outgoing key; other step directions use the transformed function.
`KeyframeSampling` selects timeline segments; `KeyframeSegmentSampler` evaluates values within one.

The value contract `IInterpolatable<TSelf, TReal>` lives in Mathematics. It defines the type's interpolation rule;
`Interpolation.Interpolate(start, end, map, time)` applies a progress map before calling that rule. Keys and their
outgoing curve metadata remain separate from values. A timeline policy still chooses which curve belongs to which
channel, handles held values, and supplies the empty-track default.

`KeyframeSegmentSampler` samples a named `ProgressMapf` once for the channels in a segment and uses the value's
interpolation rule. Shared vector overloads apply independent component overrides; motion and color policies only
select their channel curves. Custom curves use the
same function type, constructed by `Easingf.TimedBezier`. Relative control values use the normalized factory and
`ToRange(start, end)`; absolute control values use its explicit endpoint-value overload. The map returns the actual
channel value, including excursions between equal endpoints. The timeline holds exact first and last key values.

Existing `curve` / `curveY` / `curveZ` and color component curve objects deserialize into `BezierParameters`.
This record contains only four control arguments, the `Absolute` flag, and validation. Its JSON fields remain
`x1`, `y1`, `x2`, `y2`, and `absolute`, including the previous sparse defaults. There is no separate key-curve
evaluation, geometry conversion, slicing, or editor conversion API. All function construction lives in the shared
easing factories. Serialization of arbitrary composed `ProgressMap` delegates is not implemented.

Sampling belongs to Animation. Numeric interpolation and progress maps remain in Mathematics; geometric curves remain
in Geometry. Interpolation computes a value between endpoints; easing maps its progress. Integrating a velocity over
time is a separate operation. The sampler adds neither clock advancement nor animation events, loops, or serialized
track types. Existing authored JSON and editor key data remain unchanged.

`KeyframeEditing` supplies `AtTime`, `Put`, `Move`, and `DeleteAtTime` on the existing key lists. Authoring uses a shared
time tolerance; sampling still selects exact key times. `Put` maintains time order when inserting, and `Move` delegates
copying the key's payload to its caller (draw-order keys also copy their mutable slot list). Easing and curve metadata
travel with the copied key. Callers own clip bounds and which lists an edit targets.
Editable keys implement `IEditableTimedKey`, adding a writable time to the read-only sampling contract. Studio uses one
enumeration of motion, travel, expression, attachment, color, and draw-order keys for key times and retiming; retiming
updates the existing objects. Markers, events, and contact intervals retain their separate editing semantics.
`SliceHeld` shares excerpt creation for expression, attachment, and draw-order timelines, including exact end keys.
Attachments and draw order retain setup state until their first key; expressions retain their first selection before
its key, with playback waiting for that key. A selection active when the excerpt starts restarts at excerpt time zero,
as before. Payload copying stays with the caller so draw-order slot lists remain independent.

`AnimationClip2D<TFrame>` is a discrete frame sequence. Uniform timing uses an implicit `UniformSequence<float>`
of end times and one shared frame duration; there are no retained timing arrays. Variable timing keeps cumulative
end times for lookup and the original durations for exact retrieval. `OrderedSearch.UpperBound` selects the next
frame at an exact end time, holding the final frame at or past the clip duration. `KeyframeSampling` uses the same
search helper's `LowerBound` with a time selector, retaining exact key values and endpoint holds.
