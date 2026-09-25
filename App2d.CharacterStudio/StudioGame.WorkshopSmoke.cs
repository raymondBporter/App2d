using App2d.Core.Characters;

namespace App2d.CharacterStudio;

internal sealed partial class StudioGame
{
    private static readonly string[] WorkshopSmokeNames = ["build", "step-contact", "step-landing", "headless", "empty", "unreachable-contact"];
    private bool PrepareWorkshopSmoke()
    {
        switch (_smokeIndex)
        {
            case 0: VerifyWorkshopDocument(); ResetWorkshop(PuppetTemplates.StickFigure()); _puppetPart = "body"; break;
            case 1: ResetWorkshop(PuppetTemplates.StepStudy()); _workshopRest = false; _puppetTime = .3f; _puppetControl = "right-foot"; break;
            case 2: _puppetTime = .9f; _puppetControl = "left-foot"; break;
            case 3: ResetWorkshop(PuppetTemplates.StickFigure()); Puppet.RemoveControl("head"); _puppetControl = "chest"; break;
            case 4: ResetWorkshop(new()); break;
            case 5: ResetWorkshop(PuppetTemplates.StepStudy()); _workshopRest = false; Puppet.Motions[0].Contacts[0].Target = new(2, .025f); _puppetTime = .3f; _puppetControl = "right-foot"; break;
            default:
                if (_smokeIndex >= 102) return false;
                if (_smokeIndex is 6 or 54)
                {
                    ResetWorkshop(_smokeIndex == 6 ? PuppetTemplates.StepStudy() : PuppetTemplates.RunStudy());
                    _workshopRest = false; _workshopGhosts = false; _workshopHandles = false;
                }
                else AdvanceWorkshop(PuppetMotion.Duration / 24);
                break;
        }
        return true;
    }
    private void CaptureWorkshopSmoke()
    {
        if (_smokeIndex >= WorkshopSmokeNames.Length)
        {
            var name = _smokeIndex < 54 ? $"walk-{_smokeIndex - WorkshopSmokeNames.Length:D2}" : $"run-{_smokeIndex - 54:D2}";
            using var frame = File.Create(Path.Combine(_smokePath!, name + ".png"));
            _puppetTarget!.SaveAsPng(frame, _puppetTarget.Width, _puppetTarget.Height); return;
        }
        using var stream = File.Create(Path.Combine(_smokePath!, WorkshopSmokeNames[_smokeIndex] + ".png"));
        _capture!.SaveAsPng(stream, _capture.Width, _capture.Height);
    }
    private void VerifyWorkshopDocument()
    {
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        var document = new PuppetDocument(PuppetTemplates.StepStudy()); var original = document.Definition.ToJson();
        document.Definition.Controls[0].Rest = new(.1f, 1); document.Record(true);
        document.Definition.Controls[0].Rest = new(.2f, 1); document.Record(true); document.Record(false);
        var edited = document.Definition.ToJson();
        document.Undo(); Check(document.Definition.ToJson() == original && !document.CanUndo, "A puppet drag must undo as one step.");
        document.Redo(); Check(document.Definition.ToJson() == edited, "Puppet redo did not restore the edit.");
        var path = Path.Combine(_smokePath!, "step-study.puppet.json"); document.Save(path);
        var loaded = new PuppetDocument(PuppetDefinition.FromJson(File.ReadAllText(path)), path);
        Check(!loaded.Dirty && loaded.Definition.ToJson() == edited, "Puppet save/load lost authored data.");
        document.Definition.RemoveControl("head"); document.Record(false); document.Undo();
        Check(!document.Dirty && document.Definition.Controls.Any(c => c.Id == "head"), "Undo must restore deleted controls and return to the saved state.");
        // Exercise the same creation handlers as the UI, starting without any anatomy.
        ResetWorkshop(new()); _newControl = "anchor"; AddPuppetControl(false); _newControl = "tip"; AddPuppetControl(true);
        AddPuppetPart("ellipse"); Puppet.Validate();
        Check(Puppet.Controls.Count == 2 && Puppet.Bones.Count == 1 && Puppet.Parts.Count == 2, "Empty-document authoring failed.");
        StorePuppetPose(CurrentPuppetPose()); _workshopRest = false; _puppetTime = .4f;
        var pose = CurrentPuppetPose(); MovePuppetControl(pose, "tip", new(.6f, 1.1f, 0)); Puppet.Validate();
        Check(PuppetMotion.Keys.Count == 1 && PuppetMotion.Keys[0].Time == .4f, "Pose manipulation must create an editable key.");
        ResetWorkshop(PuppetTemplates.StepStudy()); _puppetTime = PuppetMotion.Duration - .02f; _workshopPlaying = true;
        AdvanceWorkshop(.05f);
        Check(_workshopPlaying && _puppetCycles == 1 && MathF.Abs(_puppetTime - .03f) < .00001f, "Loop playback must carry excess frame time across its boundary.");
        PuppetMotion.Loop = false; _puppetTime = PuppetMotion.Duration - .02f; AdvanceWorkshop(.05f);
        Check(!_workshopPlaying && _puppetTime == PuppetMotion.Duration, "One-shot playback must still stop at the endpoint.");
        File.WriteAllText(Path.Combine(_smokePath!, "checks.txt"), "PASS: grouped undo/redo, save/load, deletion undo, create from empty, pose key creation.\n");
    }
}
