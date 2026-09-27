# Validation

All types remain in `App2d.Core`. Numeric rules are independent of throwing, so the same rules can be used by argument guards, state checks, editors, and validation messages.

| File | Responsibility |
| --- | --- |
| `NumericValidation.cs` | Boolean predicates for numeric scalars and `Vector2`, `Vector3`, `Vector4` |
| `ArgGuard.cs` | Null, text, collection and custom argument checks |
| `ArgGuard.Scalars.cs` | Generic numeric guards for `int`, `long`, `float`, `double`, `decimal`, and other `INumber<T>` types |
| `ArgGuard.Vectors.cs` | Vector overloads with explicit component-wise comparisons |
| `ArgGuard.Exceptions.cs` | Shared throw helpers, messages and fresh exception factories |
| `ArgGuard.Compatibility.cs` | Forwarding aliases for previous spellings |
| `StateGuard.cs` | Invalid engine state, reported as `InvalidOperationException` |

```csharp
// Ordinary numeric comparisons (particularly useful for integers).
ArgGuard.ThrowIfNotPositive(count);
ArgGuard.ThrowIfLessThan(index, 0);
ArgGuard.ThrowIfGreaterThanOrEqual(index, count);

// One call checks finiteness AND the condition.
ArgGuard.ThrowIfNotFiniteOrNotPositive(radius);
ArgGuard.ThrowIfNotFiniteOrNegative(deltaSeconds);
ArgGuard.ThrowIfNotFiniteOrZero(direction);
ArgGuard.ThrowIfNotFiniteOrLessThanOrEqual(maximum, minimum);
ArgGuard.ThrowIfNotFiniteOrNotInOpenRange(fraction, 0f, 1f);
ArgGuard.ThrowIfNotFiniteOrNotComponentWiseLessThan(min, max);

// Reuse the rule without throwing, or give a domain-specific explanation.
bool validSize = NumericValidation.IsFiniteAndPositive(size);
if (!validSize)
    ArgGuard.ThrowOutOfRange(size, "The viewport needs positive finite dimensions.");

// A small custom argument rule with a constant message.
ArgGuard.ThrowIf(!id.IsValid, "An entity ID is required.", nameof(id));
```

## Naming and numeric policy

Basic guards check the named condition. `ThrowIfLessThan(value, minimum)` throws when `value < minimum`; it does not mean “require less than.” `ThrowIfNotPositive` requires strictly greater than zero. `ThrowIfZero` requires a nonzero value. Signed negative zero counts as zero and remains valid for nonnegative checks.

Use `ThrowIfNotFiniteOr...` for finite floating-point inputs. Each finite variant rejects NaN and either infinity. Comparisons validate both operands; range guards also validate finite, ordered bounds, reporting the bound's own parameter name when it is invalid. Closed ranges include both endpoints; open ranges exclude them. A closed range may collapse to one value.

Plain relational operators follow normal floating-point rules: for example, `NaN < limit` is false, and positive infinity is positive. A plain comparison is not a finiteness check. Existing callers that previously got implicit finite checks from `ThrowIfNotPositive`, comparison or range guards have been migrated to the explicit finite variants. `ThrowIfNegativeOrNotFinite` and `ThrowIfNotPositiveFiniteValue` remain forwarding aliases with their previous finite policy.

Ray lengths intentionally allow positive infinity: use `ThrowIfNegativeOrNaN`. Unbounded rectangle and constraint sentinels likewise keep their specific policies; do not replace these with a finite-only guard.

## Vector policy

- Finite: every component is finite.
- Positive/nonnegative: every component satisfies the condition. `ThrowIfNegative` rejects any negative component.
- Nonzero: the whole vector is not zero. `(1, 0)` is a valid nonzero direction; no length-squared calculation or epsilon is used.
- `ComponentWiseLessThan`, `LessThanOrEqual`, `GreaterThan`, `GreaterThanOrEqual`: every corresponding component must satisfy the comparison. Mixed-axis values are not totally ordered.
- Ranges: every component is between its corresponding bounds.

Vector failures carry the whole original vector in `ActualValue`, including `RequireFinitePositive`. There is no conversion of scalar values to float, so large integers and double precision retain their original values and comparisons.

## Exceptions and caller information

Guards pass the caller expression or explicit parameter name through to the throw helper. Numeric failures use `ArgumentOutOfRangeException` with the rejected value, nulls use `ArgumentNullException`, and other argument rules use `ArgumentException`. `StateGuard` keeps invalid-state failures separate and reuses the same numeric predicates.

Throw helpers such as `ThrowLessThanException` are non-inlined and annotated `DoesNotReturn`. Formatting, boxing, and exception allocation occur on failure. Factories create a new exception each time. Null guards preserve `NotNull` annotations so they also participate in nullable flow analysis.

Keep checks at the boundary that owns the rule. A one-line wrapper such as `ValidateFixedDelta` adds no policy: call the combined guard directly. Keep domain validation routines when they combine real rules (convexity, animation ordering, distance limits, or authored content), and use the shared predicates and exception helpers inside them.
