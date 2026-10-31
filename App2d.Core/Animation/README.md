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

The interpolation policy reads a key's value and interpolates a segment. It owns easing, output range, and value-specific
rules; the sampler does not clamp or ease values. Motion and color use the same sampler while retaining independent
component curves, absolute handles, authored full rotation turns, and their different empty-track defaults.
Setup-before-first behavior remains with the owning track, and color clamping remains with color evaluation.

Authored cubic channels share a `ClipEase.Sampler` for each segment sample. Matching X handles reuse the same
Bezier timing solve, even across different curve objects with different Y handles or absolute/relative value modes.
Each channel still evaluates its own value curve, including overshoot between equal endpoints. Different X handles
are solved independently. The four-entry cache lives only for that sample, allocates no managed memory, and cannot
retain stale handles after editor changes. Named easing is also evaluated once per sample.

Sampling belongs to Animation. Numeric interpolation and progress maps remain in Mathematics; geometric curves remain
in Geometry. Interpolation computes a value between endpoints; easing maps its progress. Integrating a velocity over
time is a separate operation. The sampler adds neither clock advancement nor animation events, loops, or serialized
track types. Existing authored JSON and editor key data remain unchanged.
