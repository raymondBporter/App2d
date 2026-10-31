using App2d.Core.Geometry.Operations.Convex;

namespace App2d.Core.Geometry.Shapes;

/// <summary>A finite convex shape. Its support mapping powers bounds, distances and contacts without sampling curves.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Serialization.GeometryJsonConverterFactory))]
public interface IConvexShape2D : IShape2D, IConvexSupport2D;