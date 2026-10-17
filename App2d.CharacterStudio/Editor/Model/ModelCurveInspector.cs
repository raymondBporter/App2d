using App2d.Core.Characters;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

internal static class ModelCurveInspector
{
    public static void Draw(PuppetPart part, IReadOnlyList<string> controls, Action<Action<PuppetPart>> change)
    {
        if (part.Geometry is not ICurve2D curve)
        {
            return;
        }

        if (ImGui.CollapsingHeader("Normal wave"))
        {
            var amplitude = part.WaveAmplitude;
            var cycles = part.WaveCycles;
            if (Ui.Drag("Bump height", ref amplitude, .001f, -.5f, .5f))
            {
                change(p => p.WaveAmplitude = amplitude);
            }

            if (Ui.Drag("Bumps along curve", ref cycles, .05f, .1f, 16))
            {
                change(p => p.WaveCycles = cycles);
            }

            if (Ui.Combo("Wave travel control", part.WavePhaseControl ?? "(none)", controls.Prepend("(none)")) is { } phase)
            {
                change(p => p.WavePhaseControl = phase == "(none)" ? null : phase);
            }

            Ui.Help("Bumps follow the curve normal. Animate the travel control along the curve's endpoint span to move them. Negative height bends to the other side.");
        }
        if (curve is not QuadraticBezier2D and not CubicBezier2D || !ImGui.CollapsingHeader("Edit curve handles"))
        {
            return;
        }

        Ui.Help("Draw the rest curve here. Follow attaches a point to a normal rig control, preserving its rest offset. Animate those controls in the animation editor.");
        var points = PartCurves2D.ControlPoints(curve);
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
                change(p => p.Geometry = PartCurves2D.WithControlPoints(curve, edited));
            }
            ModelPointBindingInspector.Draw(part, index, controls, change);
            ImGui.PopID();
        }
    }
}
