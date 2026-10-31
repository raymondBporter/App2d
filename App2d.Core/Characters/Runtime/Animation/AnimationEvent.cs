using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>A clip marker crossed ("marker") or an action event reached ("event"), dispatched once when time advances across it.</summary>
public readonly record struct AnimationEvent(string Kind, string Id, string? Sound, string? Action, int ActionSequence)
{
    public const string MarkerKind = "marker";
    public const string EventKind = "event";
}
