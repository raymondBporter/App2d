using App2d.Levels;
using System.ComponentModel;
using System.Drawing;

namespace App2d.Editor;

internal sealed class MovingPlatformDefinitionProperties2D
{
    [Category("Definition"), DisplayName("Name"), Description("Name shown in this level's moving-platform palette.")]
    public string Name { get; set; } = "Teal lift";

    [Category("Shape"), DisplayName("Width")]
    public float Width { get; set; } = 96f;

    [Category("Shape"), DisplayName("Height")]
    public float Height { get; set; } = 16f;

    [Category("Art"), DisplayName("Color")]
    public Color Color { get; set; } = Color.FromArgb(unchecked((int)0xFF25D2BEu));

    public static MovingPlatformDefinitionProperties2D From(MovingPlatformDefinitionRecord2D record) =>
        new()
        {
            Name = record.Name,
            Width = record.Width,
            Height = record.Height,
            Color = Color.FromArgb(record.ColorArgb)
        };
}

internal sealed class MovingPlatformInstanceProperties2D
{
    [Category("Thing"), DisplayName("Name"), Description("Optional name for this placed instance.")]
    public string? Name { get; set; }

    [Category("Thing"), DisplayName("Enabled")]
    public bool Enabled { get; set; } = true;

    [Category("Transform"), DisplayName("Position X")]
    public float PositionX { get; set; }

    [Category("Transform"), DisplayName("Position Y")]
    public float PositionY { get; set; }

    [Category("Ping-pong motor"), DisplayName("Travel X")]
    public float TravelX { get; set; }

    [Category("Ping-pong motor"), DisplayName("Travel Y")]
    public float TravelY { get; set; }

    [Category("Ping-pong motor"), DisplayName("Speed")]
    public float Speed { get; set; }

    public static MovingPlatformInstanceProperties2D From(MovingPlatformThingRecord2D record) =>
        new()
        {
            Name = record.Name,
            Enabled = record.Enabled,
            PositionX = record.X,
            PositionY = record.Y,
            TravelX = record.TravelX,
            TravelY = record.TravelY,
            Speed = record.Speed
        };
}
