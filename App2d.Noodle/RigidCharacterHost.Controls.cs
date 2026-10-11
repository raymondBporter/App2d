using System.Numerics;

namespace App2d.Noodle;

internal sealed partial class RigidCharacterHost
{
    protected override void DrawControls()
    {
        Ui.Header("Spline / rigid rig lab");
        Ui.Section("Character", () =>
        {
            if (Ui.Combo("Art set", _puppet.PartSet.Name, RigidPartSet2D.PrototypeSets.Select(set => set.Name)) is { } name)
            {
                _partSetIndex = RigidPartSet2D.PrototypeSets.ToList().FindIndex(set => set.Name == name);
                _puppet.PartSet = RigidPartSet2D.PrototypeSets[_partSetIndex];
            }
            ImGui.Checkbox("Show bones (B)", ref _showBones);
            Ui.Row(() => Ui.Button(_authoring ? "Resume game (F1)" : "Edit pose (F1)", () => HandleCommand(Keys.F1)),
                () => Ui.Button("Reset (R)", () => HandleCommand(Keys.R)));
            Ui.Help(CurrentAnimationLabel);
        });
        if (_authoring) DrawAuthoringPanel();
        else Ui.Section("Play", () =>
        {
            Ui.Row(() => Ui.Button("Jump", () => HandleCommand(Keys.Space)),
                () => Ui.Button("Attack", () => HandleCommand(Keys.J)), () => Ui.Button("Hit", () => HandleCommand(Keys.H)));
            Ui.Help("A/D or arrows move. Space jumps. J attacks. H plays hit. V swaps art.");
            Ui.Help($"Feet: L {(_demo.LeftFootPlanted ? "planted" : "swing")} / R {(_demo.RightFootPlanted ? "planted" : "swing")}");
        });
        Ui.Section("Status", () => Ui.Help(_notice));
        Ui.Help("Wheel zooms. Escape closes.");
    }

    private void DrawAuthoringPanel() => Ui.Section("Pose", () =>
    {
        if (Ui.Combo("Control", _selectedControl.ToString(), Enum.GetNames<RigidControl2D>()) is { } control)
            _selectedControl = Enum.Parse<RigidControl2D>(control);
        var target = _author.Pose.GetTarget(_selectedControl);
        var torso = _author.Pose.TorsoAngle;
        var head = _author.Pose.HeadAngle;
        if (Ui.Drag2("Target", ref target, .5f, 0, 0))
        { _author.Pose = _author.Pose.WithTarget(_selectedControl, target); _customPlayback = false; }
        if (Ui.Drag("Torso angle (radians)", ref torso, .01f, 0, 0))
        { _author.Pose = _author.Pose with { TorsoAngle = torso }; _customPlayback = false; }
        if (Ui.Drag("Head angle (radians)", ref head, .01f, 0, 0))
        { _author.Pose = _author.Pose with { HeadAngle = head }; _customPlayback = false; }
        Ui.Row(() => Ui.Button("Capture (K)", () => HandleCommand(Keys.K)),
            () => Ui.Button(_customPlayback ? "Stop" : "Play (P)", () =>
            { if (_customPlayback) _customPlayback = false; else HandleCommand(Keys.P); }));
        Ui.Row(() => Ui.Button("Save (Ctrl+S)", () => HandleCommand(Keys.S, true)),
            () => Ui.Button("Clear (Del)", () => HandleCommand(Keys.Delete)));
        Ui.Help($"{_author.Frames.Count} captured poses");
        Ui.Help("Drag a viewport handle to edit. Q/E turn torso. Z/X turn head.");
    });
}
