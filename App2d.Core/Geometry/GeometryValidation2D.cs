namespace App2d.Core.Geometry;

/// <summary>Checks a geometry value and returns that same value; no builder state or intermediate object.</summary>
public static class GeometryValidation2D
{
    public static T Validated<T>(this T geometry) where T : IGeometryValue2D
    {
        // Skip the null test for structs: boxing them just to test for null allocates in debug builds.
        if (!typeof(T).IsValueType && geometry is null) throw new ArgumentNullException(nameof(geometry));
        geometry.Validate();
        return geometry;
    }
}
