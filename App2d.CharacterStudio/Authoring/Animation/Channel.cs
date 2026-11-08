namespace App2d.CharacterStudio.Authoring.Animation;

/// <summary>One animated value: a control's translate or rotate channel, or a chain's end-target channel.</summary>
public readonly record struct Channel(string Kind, string Target)
{
    public override string ToString() => $"{Target} {Kind}";
}
