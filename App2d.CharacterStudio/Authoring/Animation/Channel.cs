using App2d.Core.Animation;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>One animated value: a control's translate or rotate channel, or a chain's end-target channel.</summary>
public readonly record struct Channel(string Kind, string Target)
{
    public override string ToString() => $"{Target} {Kind}";
}
