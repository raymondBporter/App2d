using App2d.Core.Characters;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

/// <summary>Small actor observation. Renderer-owned geometry workspaces are shared by library.</summary>
public sealed class PointCharacterShader(EntityCatalog catalog, EntityTypeDefinition type) : IShader2D
{
    public EntityCatalog Catalog { get; } = catalog;
    public EntityTypeDefinition Type { get; } = type;
    public string Action { get; set; } = "idle";
    public double Seconds { get; set; }
    public bool FacingLeft { get; set; }
    public string? Weapon { get; set; }
    public FacePose? Face { get; set; }
    public Color BaseColor => Color.White;
}
