namespace App2d.Noodle;

internal sealed partial class NoodleEditorHost
{
    protected override void DrawControls()
    {
        Ui.Header("Noodle / IK lab");
        Ui.Section("IK control", () =>
        {
            if (Ui.Combo("Selected", _person.SelectedControlName, _person.ControlNames) is { } selected)
            {
                _person.SelectControl(selected);
            }

            var target = _person.TargetLimb.Target;
            if (Ui.Drag2("Target", ref target, .5f, 0, 0))
            {
                _person.TargetLimb.SetTarget(target);
            }

            Ui.Button("Flip joint (Space)", () => _person.TargetLimb.FlipBend());
            Ui.Help(_person.TargetLimb.Pose.ReachesTarget ? "Target reached" : "Target clamped to limb reach");
        });
        Ui.Section("View", () =>
        {
            ImGui.Checkbox("Show weights (W)", ref _showWeights);
            Ui.Button("Reset (R)", Reset);
        });
        Ui.Section("Viewport", () => Ui.Help("Right click selects a hand or foot. Left drag moves the selected IK target. Wheel zooms. Escape closes."));
    }
}
