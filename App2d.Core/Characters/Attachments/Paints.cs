namespace App2d.Core.Characters.Attachments;

/// <summary>The plain materials attachments are painted with.</summary>
public static class Paints
{
    /// <summary>The near-black every character is outlined in.</summary>
    public const string Ink = "#222b32";

    /// <summary>A plain fill, edged in ink unless told otherwise; a stroke width makes it a line.</summary>
    public static AttachmentMaterial Paint(string fill, float edge = 0, string edgeColor = Ink, float? stroke = null) =>
        new() { Fill = fill, StrokeWidth = stroke, Outline = edge > 0 ? new() { Color = edgeColor, Width = edge } : null };
}
