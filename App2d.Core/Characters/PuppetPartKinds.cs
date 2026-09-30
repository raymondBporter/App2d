namespace App2d.Core.Characters;

/// <summary>Stable JSON kind IDs for editable character drawing parts.</summary>
public static class PuppetPartKinds
{
    public const string Ellipse = "ellipse";
    public const string Box = "box";
    public const string Trapezoid = "trapezoid";
    public const string Stroke = "stroke";

    public static IReadOnlyList<string> All { get; } =
        [Ellipse, Box, Trapezoid, Stroke];

    public static bool IsStroke(string kind) => kind == Stroke;
    public static bool HasRoundness(string kind) => kind is Box or Trapezoid;
}
