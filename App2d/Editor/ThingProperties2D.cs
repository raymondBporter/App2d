using App2d.Levels;
using System.ComponentModel;

namespace App2d.Editor;

internal sealed class PositionThingDefinitionProperties2D
{
    [Category("Definition"), DisplayName("Name"), Description("Name shown in this level's thing palette.")]
    public string Name { get; set; } = "New definition";

    public static PositionThingDefinitionProperties2D From(ThingDefinitionRecord2D record) =>
        new()
        {
            Name = record.Name
        };
}

internal sealed class PositionThingInstanceProperties2D
{
    [Category("Thing"), DisplayName("Name"), Description("Optional name for this placed instance.")]
    public string? Name { get; set; }

    [Category("Thing"), DisplayName("Enabled")]
    public bool Enabled { get; set; } = true;

    [Category("Transform"), DisplayName("Position X")]
    public float PositionX { get; set; }

    [Category("Transform"), DisplayName("Position Y")]
    public float PositionY { get; set; }

    [Category("Thing"), DisplayName("Asset"), ReadOnly(true), Description("The object file this thing places; change it on the definition.")]
    public string? Asset { get; set; }

    public static PositionThingInstanceProperties2D From(PositionThingRecord2D record) =>
        new()
        {
            Name = record.Name,
            Enabled = record.Enabled,
            PositionX = record.X,
            PositionY = record.Y,
            Asset = record.Asset
        };
}
