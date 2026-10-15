using App2d.Core.Characters;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Authoring;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Model;

internal static class ModelCurveInspector
{
    public static void Draw(PuppetPart part, IReadOnlyList<string> controls, Action<Action<PuppetPart>> change)
    {
        if (part.Geometry is not CurveDefinition2D curve) return;
        if (ImGui.CollapsingHeader("Normal wave"))
        {
            var amplitude = part.WaveAmplitude; var cycles = part.WaveCycles;
            if (Ui.Drag("Bump height", ref amplitude, .001f, -.5f, .5f)) change(p => p.WaveAmplitude = amplitude);
            if (Ui.Drag("Bumps along curve", ref cycles, .05f, .1f, 16)) change(p => p.WaveCycles = cycles);
            if (Ui.Combo("Wave travel control", part.WavePhaseControl ?? "(none)", controls.Prepend("(none)")) is { } phase)
                change(p => p.WavePhaseControl = phase == "(none)" ? null : phase);
            Ui.Help("Bumps follow the curve normal. Animate the travel control along the curve's endpoint span to move them. Negative height bends to the other side.");
        }
        var kind = curve switch
        {
            LineCurveDefinition2D => "Line",
            QuadraticBezierCurveDefinition2D => "Quadratic Bézier",
            CubicBezierCurveDefinition2D => "Cubic Bézier",
            _ => null
        };
        if (kind is null) return;
        ImGui.BeginDisabled(part.Bindings is { Count: > 0 });
        var selected = Ui.Combo("Curve", kind, ["Line", "Quadratic Bézier", "Cubic Bézier"]);
        ImGui.EndDisabled();
        if (selected is not null && selected != kind)
        {
            var geometry = curve.Build(); var start = geometry.Evaluate(0); var end = geometry.Evaluate(1);
            change(p => p.Geometry = selected switch
            {
                "Quadratic Bézier" => new QuadraticBezierCurveDefinition2D { Start = start, Control = Vector2.Lerp(start, end, .5f), End = end },
                "Cubic Bézier" => new CubicBezierCurveDefinition2D { Start = start, Control1 = Vector2.Lerp(start, end, 1f / 3), Control2 = Vector2.Lerp(start, end, 2f / 3), End = end },
                _ => new LineCurveDefinition2D { Start = start, End = end }
            });
            return;
        }
        if (curve is not QuadraticBezierCurveDefinition2D and not CubicBezierCurveDefinition2D || !ImGui.CollapsingHeader("Edit curve handles")) return;
        Ui.Help("Draw the rest curve here. Follow attaches a point to a normal rig control, preserving its rest offset. Animate those controls in the animation editor.");
        var points = PartCurves2D.ControlPoints(curve);
        for (var i = 0; i < points.Count; i++)
        {
            var index = i; var point = points[i];
            var label = i == 0 ? "Start" : i == points.Count - 1 ? "End" : "Handle " + i;
            ImGui.PushID("curve-" + i);
            if (Ui.Drag2(label, ref point, .005f, -2, 2))
            {
                var edited = points.ToArray(); edited[index] = point;
                change(p => p.Geometry = PartCurves2D.WithControlPoints(curve, edited));
            }
            ModelPointBindingInspector.Draw(part, index, controls, change);
            ImGui.PopID();
        }
    }
}
