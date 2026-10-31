using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// World contact anchors for in-place playback. A contact is captured where the clip puts it at touchdown and held there
/// until the clip releases it, so the controller can move the actor without feet sliding; <see cref="Shift"/> carries the
/// anchors when the ground itself moves. A facing change releases every
/// anchor; callers release them explicitly (<see cref="Clear"/>) when the clip or action changes.
/// </summary>
public sealed class ContactHold
{
    private readonly Dictionary<string, Vector3> _anchors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);
    private int _facing = 1;

    public IReadOnlyDictionary<string, Vector3> Anchors => _anchors;
    public void Clear() => _anchors.Clear();

    /// <summary>Moves every held anchor by <paramref name="delta"/>: the ground they stand on moved, as a platform does.</summary>
    public void Shift(Vector3 delta)
    {
        foreach (var chain in _anchors.Keys.ToList())
        {
            _anchors[chain] += delta;
        }
    }

    /// <summary>Samples in place at <paramref name="position"/> (the feet origin) and returns the placed final pose.</summary>
    /// <remarks>With <paramref name="hold"/> false every anchor is released and contacts follow the clip.</remarks>
    public ActorPose Evaluate(ResolvedModel model, MotionClip? clip, double seconds, bool repeat, Vector2 position, int facing, PoseInput input, bool hold = true)
    {
        if (facing is not (1 or -1))
        {
            ArgGuard.ThrowOutOfRange(facing, "Facing must be -1 or 1.");
        }

        if (facing != _facing)
        {
            _facing = facing;
            _anchors.Clear();
        }
        var placed = new ActorPose(new EvaluatedPose(), position, facing);
        _held.Clear();
        Vector3 Hold(string chain, Vector3 authored)
        {
            _held.Add(chain);
            if (_anchors.TryGetValue(chain, out var anchor))
            {
                return placed.ToLocal(anchor);
            }

            _anchors[chain] = placed.Place(authored);
            return authored;
        }
        if (!hold)
        {
            _anchors.Clear();
            return placed with
            {
                Local = PoseEvaluator.Sample(model, clip, seconds, repeat, input with
                {
                    InPlace = true,
                    Contact = null
                })
            };
        }
        var local = PoseEvaluator.Sample(model, clip, seconds, repeat, input with
        {
            InPlace = true,
            Contact = Hold
        });
        foreach (var chain in _anchors.Keys.Where(c => !_held.Contains(c)).ToList())
        {
            _anchors.Remove(chain);
        }

        return placed with
        {
            Local = local
        };
    }
}
