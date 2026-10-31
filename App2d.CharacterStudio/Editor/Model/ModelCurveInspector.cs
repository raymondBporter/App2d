using App2d.Core.Geometry.Operations.Curves;
using App2d.Core.Characters;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

internal static class ModelCurveInspector
{
    public static void Draw(PartGeometry part, IReadOnlyList<string> controls, Action<Action<PartGeometry>> change)
    {
        if (part.Geometry is not ICurve2D curve)
        {
            return;
        }

        if (curve is RoundedPolylineCurve2D rounded)
        {
            var softness = rounded.Softness;
            if (Ui.Drag("Bend softness", ref softness, .01f, 0, 1))
            {
                change(p => p.Geometry = ((RoundedPolylineCurve2D)p.Geometry!) with { Softness = softness });
            }

            var sections = part.CurveSections;
            if (ImGui.SliderInt("Curve sections", ref sections, 2, 128))
            {
                change(p => p.CurveSections = sections);
            }

            Ui.Help("Softness sets how much of each span the fillets trim. Sections are the base sample density; fillet boundaries are always sampled.");
        }
        if (curve is not QuadraticBezier2D and not CubicBezier2D || !ImGui.CollapsingHeader("Edit curve handles"))
        {
            return;
        }

        Ui.Help("Draw the rest curve here. Follow attaches a point to a normal rig control, preserving its rest offset. Animate those controls in the animation editor.");
        var points = CurveControlPoints2D.Get(curve);
        for (var i = 0; i < points.Count; i++)
        {
            var index = i;
            var point = points[i];
            var label = i == 0 ? "Start" : i == points.Count - 1 ? "End" : "Handle " + i;
            ImGui.PushID("curve-" + i);
            if (Ui.Drag2(label, ref point, .005f, -2, 2))
            {
                var edited = points.ToArray();
                edited[index] = point;
                change(p => p.Geometry = CurveControlPoints2D.With(curve, edited));
            }
            ModelPointBindingInspector.Draw(part, index, controls, change);
            ImGui.PopID();
        }
    }
}
