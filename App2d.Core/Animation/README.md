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

Motion and color keys store `ProgressMapKind` directly and resolve it through `ProgressMaps.FromKind`.
The enum converter keeps named JSON values, including existing `linear`, `smooth`, and `step` files.
Key validation and editor choices require forward travel; continuous forward maps and the explicit `Step` hold are allowed.
`KeyframeSampling` selects timeline segments; `KeyframeSegmentSampler` evaluates scalar channels within one.

Authored cubic channels share a `KeyframeSegmentSampler` for each segment sample. Matching X handles reuse the same
Bezier timing solve, even across different curve objects with different Y handles or absolute/relative value modes.
Each channel still evaluates its own value curve, including overshoot between equal endpoints. Different X handles
are solved independently. The four-entry cache lives only for that sample, allocates no managed memory, and cannot
retain stale handles after editor changes. Named easing is also evaluated once per sample through an `IProgressMap`.
Maps evaluate in double precision before converting to the channel's float; smooth easing can differ from the former
float polynomial by float rounding. Custom curves replace named easing. Held segments are handled by the timeline
sampler before the scalar segment sampler is invoked.
The segment sampler accepts geometric `CubicBezier2D` values, so Animation does not depend on authored character types.
The curve editor can replace a preset with an editable cubic. Only Linear and Smooth have the existing exact cubic
conversion; excerpts through other named presets must cut on key times or first replace that segment with a curve.
An unsupported interior cut reports an error instead of changing the interpolation silently.
Studio excerpts share boundary-key creation, original-segment lookup, and time shifting for motion and color. Each
caller supplies its sampled values and scalar curve mappings; color retains setup-before-first and bounded boundary
values, motion retains sticky bends, and rotation slices only its active angle curve. Visibility excerpts remain held
regardless of the key's stored easing or curves. Numeric slicing uses exact times, preserving closely spaced keys and
the sampled value at each cut; the editor's key-matching tolerance does not snap excerpt boundaries.

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
