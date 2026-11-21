using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Core.Rendering.Characters;

/// <summary>Draws a character's mesh in a depth layer of its own, so its pieces are layered against each other and nothing else.</summary>
public sealed class PointCharacterRenderer(GraphicsDevice device) : LayeredMeshRenderer(device)
{
    /// <summary>A new depth layer, the swoosh (if any) behind, then the character.</summary>
    public void DrawActor(LayeredMesh mesh, Matrix projection, Matrix world, LayeredMesh? swoosh = null)
    {
        ClearDepth();
        if (swoosh is { Count: > 0 })
        {
            Draw(swoosh, projection, world);
        }

        Draw(mesh, projection, world);
    }
}
