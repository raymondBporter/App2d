# Validation

`ArgGuard` validates arguments. `StateGuard` validates engine state. All types are in `App2d.Core.Validation`.

```csharp
ArgGuard.Finite(position);
ArgGuard.Positive(radius);                         // finite and > 0
ArgGuard.NonNegative(holdSeconds);                  // finite and >= 0
ArgGuard.NonZero(direction);                        // finite, whole vector != zero
ArgGuard.GreaterThan(recoveryPeak, 1f);              // finite and > 1
ArgGuard.InRange(minimumSpeed, 0f, 1f);              // finite, [0, 1]
ArgGuard.Index(index, TriangleCount);               // [0, TriangleCount)

// Checks return the value, so initializers need no separate Require API.
public Vector2 TileSize { get; } = ArgGuard.Positive(tileSize);

// Endpoint choices are exact; no epsilon tricks or extra method families.
ArgGuard.InRange(fraction, 0f, 1f, bounds: RangeBounds.Open);       // (0, 1)
ArgGuard.InRange(fraction, 0f, 1f, bounds: RangeBounds.ClosedOpen); // [0, 1)
ArgGuard.InRange(fraction, 0f, 1f, bounds: RangeBounds.OpenClosed); // (0, 1]

// Unusual or domain-specific rules use ordinary C# expressions.
StateGuard.ThrowIfNot(!scene.Any());
ArgGuard.ThrowIfNot(Math.Abs(a - b) < epsilon);
StateGuard.ThrowIfNot(clip.IsReady, "Load the clip before playing it.");
```

## Small numeric vocabulary

| Check | Requirement |
| --- | --- |
| `Number` | Not NaN; infinity is allowed |
| `Finite` | Neither NaN nor infinity |
| `Positive` | Finite and `> 0` |
| `NonNegative` | Finite and `>= 0` |
| `NonZero` | Finite and not zero |
| `AtLeast`, `GreaterThan`, `AtMost`, `LessThan` | Finite value satisfying `>=`, `>`, `<=`, `<` |
| `InRange` | Finite value between ordered bounds, with explicit endpoint inclusion |
| `Index` | Integer index in `[0, count)`; handles empty collections |

Scalars use generic math without conversion to float: `int`, `long`, `float`, `double`, `decimal`, and other `INumber<T>` types share the implementation. Every numeric guard rejects NaN. There are no plain/finite duplicate families or separate negative and epsilon variants. Use `LessThan(x, 0f)` for a finite negative value; keep tolerance formulas explicit in custom conditions.

`Positive` always means greater than zero. Use `NonNegative` when zero is valid. Signed negative zero is zero. Null is a separate concern: validate an optional numeric value only when it is present.

## Vectors and matrices

`Vector2`, `Vector3`, and `Vector4` use the same method names. Sign checks, comparisons, and ranges apply to **every component**, with matching vector bounds. `NonZero` checks the whole vector: `(1, 0)` is a valid direction but is not positive on every axis. Validate a single component directly when only that axis has a rule. No magnitude calculation or epsilon is used, so tiny directions and large finite components are supported.

`Finite(Matrix3x2)` checks all six entries and accepts finite singular matrices. Vector and matrix failures report the entire original value.

## Intentional infinity

Checked values are finite by default, while range **bounds** may be infinite. One-sided comparisons require both operands to be finite. `InRange(x, 0f, float.PositiveInfinity, bounds: RangeBounds.Open)` requires a finite `x > 0`; `Positive(x)` is the shorter spelling. Bounds must not contain NaN and must describe a nonempty range; only a closed range may collapse to one value.

Unlimited ray distances use `NonNegative(maxDistance, allowInfinity: true)`. This explicit opt-in still rejects NaN and negative infinity. Constraint bounds use `Number` because infinity disables a limit. Unbounded rectangles retain their domain-specific ordering rule. `NumericValidation.IsComponentWise...` predicates follow ordinary comparisons and support that sentinel policy.

## Exceptions and caller expressions

Numeric checks throw `ArgumentOutOfRangeException` with a generic requirement, the rejected value, and the caller's expression as `ParamName`. An explicit name such as `ArgGuard.Positive(value, nameof(Radius))` overrides it. Invalid comparison operands and range bounds report the bound's own caller expression. `Index` reports the rejected index even when `count == 0`.

`ThrowIf` and `ThrowIfNot` on both guard classes capture the actual C# condition using `CallerArgumentExpression`. Without a custom message, a failed `StateGuard.ThrowIfNot(!scene.Any())` reports `Condition must be true: !scene.Any().` Custom messages remain available when they explain a domain rule. Argument conditions throw `ArgumentException`; state conditions throw `InvalidOperationException`. Nullable flow annotations are preserved.

`NumericValidation` provides the same finite sign, nonzero, and range rules without throwing, for editors and other consumers. Exception construction, formatting, and boxing happen only on failure. Successful numeric checks allocate nothing; exception factories create a fresh exception each time.

Keep validation at the boundary that owns the rule. Use common numeric guards directly; retain domain validation routines that combine real rules such as convexity or animation ordering. Null, text, collection, and type checks keep their existing helpers.
