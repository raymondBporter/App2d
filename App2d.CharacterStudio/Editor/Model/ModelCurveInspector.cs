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

        Wave(part, controls, change);
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

    /// <summary>The curve's wave modifier: its fields appear only while the part carries one; add and remove are single edits.</summary>
    private static void Wave(PuppetPart part, IReadOnlyList<string> controls, Action<Action<PuppetPart>> change)
    {
        if (!ImGui.CollapsingHeader("Normal wave"))
        {
            return;
        }

        if (part.Wave is not { } wave)
        {
            if (ImGui.SmallButton("Add normal wave"))
            {
                change(p => p.Wave = CurveWave.Starter);
            }

            Ui.Help("Only a linked curve can carry a wave: sine bumps along its normal, sized by its endpoint span.");
            return;
        }

        var amplitude = wave.Amplitude;
        var cycles = wave.Cycles;
        if (Ui.Drag("Bump height", ref amplitude, .001f, -.5f, .5f))
        {
            change(p => p.Wave = p.Wave! with { Amplitude = amplitude });
        }

        if (Ui.Drag("Bumps along curve", ref cycles, .05f, CurveWave.CyclesLimit.Min, CurveWave.CyclesLimit.Max))
        {
            change(p => p.Wave = p.Wave! with { Cycles = cycles });
        }

        if (Ui.Combo("Wave travel control", wave.PhaseControl ?? "(none)", controls.Prepend("(none)")) is { } phase)
        {
            change(p => p.Wave = p.Wave! with { PhaseControl = phase == "(none)" ? null : phase });
        }

        Ui.Help("Bumps follow the curve normal. Animate the travel control along the curve's endpoint span to move them. Negative height bends to the other side.");
        if (ImGui.SmallButton("Remove normal wave"))
        {
            change(p => p.Wave = null);
        }
    }
}
