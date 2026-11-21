namespace App2d.Core.Characters;

/// <summary>
/// How a drawn piece is painted. A shape is filled with <see cref="Fill"/> and edged with <see cref="Outline"/>; a line is
/// stroked in <see cref="Fill"/> at <see cref="StrokeWidth"/>, with the outline as a wider line underneath. Colours are
/// "#rrggbb". Anything left out is not drawn.
/// </summary>
public sealed record AttachmentMaterial
{
    public string? Fill { get; init; }
    public float? StrokeWidth { get; init; }
    public AttachmentEdge? Outline { get; init; }
}

/// <summary>The line round a piece: its colour and its width in the skeleton's units.</summary>
public sealed record AttachmentEdge
{
    public string? Color { get; init; }
    public float? Width { get; init; }
}
