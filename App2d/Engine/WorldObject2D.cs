using App2d.Engine.Geometry;
using App2d.Engine.Rendering;

namespace App2d.Engine;

/// <summary>
/// The optional rendering component for a spatial object.
/// </summary>
public sealed class WorldObject2D(IShape2D shape, IShader2D shader) : SpatialObject2D(shape)
{
    private int _zIndex;

    internal event Action? ZIndexChanged;

    public IShader2D Shader { get; set; } = ArgGuard.RequireNotNull(shader);
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Controls this object's draw order. Lower values are drawn first; objects
    /// with the same value retain their scene insertion order.
    /// </summary>
    public int ZIndex
    {
        get => _zIndex;
        set
        {
            if (_zIndex == value)
                return;

            _zIndex = value;
            ZIndexChanged?.Invoke();
        }
    }
}
