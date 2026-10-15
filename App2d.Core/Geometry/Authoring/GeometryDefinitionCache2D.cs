using App2d.Core.Geometry.Builders;
using App2d.Core.Curves;
using App2d.Core.Shapes;
using System.Runtime.CompilerServices;

namespace App2d.Core.Geometry.Authoring;

/// <summary>Caches built geometry by definition identity without keeping unused authoring records alive.</summary>
public static class GeometryDefinitionCache2D
{
    private static readonly ConditionalWeakTable<GeometryDefinition2D, object> Built = [];

    public static IShape2D ShapeOf(ShapeBuilder2D definition) => (IShape2D)Built.GetValue(definition, key => ((ShapeBuilder2D)key).Build());
    public static ICurve2D CurveOf(CurveDefinition2D definition) => (ICurve2D)Built.GetValue(definition, key => ((CurveDefinition2D)key).Build());

}
