using ImGuiNET;
using System.Text.Json;

namespace App2d.CharacterStudio;

internal sealed partial class StudioGame
{
    private int _smokeWidth, _smokeHeight;
    private float _smokeDpi, _smokeFontSize;
    private readonly List<object> _smokeMeasurements = [];
    private static readonly string[] WolfSmokeClips = ["idle", "walk", "run", "jump", "fly", "land", "cry", "attack", "die"];
    private static readonly string[] SmokeScenarios = ["dpi-resized", "dpi-user-scale", "dpi-restored", "weapon-rapier", "weapon-mace", "weapon-hammer", "person-custom-head", "person-head-editor", "quadruped-custom-head", "quadruped-head-editor", "entity-player", "entity-needle", "entity-maul", "entity-cinder", "entity-hound", "entity-action-editor", "entity-collision-editor", "entity-playtest", "kevin-melee", "kevin-dance", "kevin-archer", "kevin-soldier", "kevin-spellcasting", "kevin-throwing"];

    private bool PrepareSmokeFrame()
    {
        if (_smokeFrame == 1)
        {
            _smokeWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            _smokeHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
            _smokeDpi = _gui.DpiScale;
        }
        if (_smokeIndex < _libraries.Length)
        {
            ChooseLibrary(_libraries[_smokeIndex]);
            _document.Playback.Seek(_document.Playback.Clip.Duration * .37);
            return true;
        }
        switch (_smokeIndex - _libraries.Length)
        {
            case 0:
                ChooseLibrary(_libraries[0]);
                _graphics.PreferredBackBufferWidth = (int)(_smokeWidth * .75f);
                _graphics.PreferredBackBufferHeight = (int)(_smokeHeight * .75f);
                _graphics.ApplyChanges();
                break;
            case 1:
                _gui.UserScale = 1.25f;
                break;
            case 2:
                _gui.UserScale = 1;
                _graphics.PreferredBackBufferWidth = _smokeWidth;
                _graphics.PreferredBackBufferHeight = _smokeHeight;
                _graphics.ApplyChanges();
                break;
            case 3:
            case 4:
            case 5:
                ChooseLibrary(_libraries[0]);
                _document.Playback.Select("sword_attack"); _document.Playback.Seek(_document.Playback.Clip.Duration * .37);
                _document.Appearance.Weapon = new[] { "rapier", "mace", "hammer" }[_smokeIndex - _libraries.Length - 3];
                _document.Appearance.BladeLength = 1.6f; _document.Appearance.WeaponHeadSize = 1.2f;
                FitMotion();
                break;
            case 6:
                SetHead(new() { Muzzle = .25f, Width = 1.4f, FaceX = .2f }); FitMotion();
                break;
            case 7:
                _headEditorOpen = true; _headFit = true;
                break;
            case 8:
                _headEditorOpen = false;
                ChooseLibrary(_libraries.First(l => l.Anatomy == "hound"));
                SetHead(new() { Muzzle = .9f, FaceX = .3f, FaceAngle = -12 });
                _document.Playback.Seek(_document.Playback.Clip.Duration * .37); FitMotion();
                break;
            case 9:
                _headEditorOpen = true; _headFit = true; _headFaceMode = true;
                break;
            case 10:
            case 11:
            case 12:
            case 13:
            case 14:
                ActivateEntity(_entityDocuments[new[] { "player", "needle", "maul", "cinder", "scrap-hound" }[_smokeIndex - _libraries.Length - 10]]);
                _actionId = "attack"; _actionTime = CurrentAction.Duration * (CurrentAction.ActiveStart + CurrentAction.ActiveEnd) / 2; _actionPlaying = false; FitMotion();
                break;
            case 15:
                ActivateEntity(_entityDocuments["maul"]); _actionId = "attack"; _actionTime = CurrentAction.Duration * .56; _selectEntityTab = "Action"; FitMotion();
                break;
            case 16:
                _selectEntityTab = "Collision";
                break;
            case 17:
                StartArena(); _arenaCollision = true;
                for (var i = 0; i < 240; i++) _arena!.Step(new(.5f, false, i > 100, false));
                break;
            case 18:
            case 19:
            case 20:
            case 21:
            case 22:
            case 23:
                _arena = null; _headEditorOpen = false;
                ChooseLibrary(_libraries.First(l => l.Id == "person")); _document.ResetAppearance();
                var kevinIndex = _smokeIndex - _libraries.Length - 18;
                _filter = "kevin:" + new[] { "melee", "dance", "archer", "soldier", "spellcasting", "throwing" }[kevinIndex];
                _document.Playback.Select(new[] { "kevin_humanm_attack1h01_r", "kevin_humanm_dance01", "kevin_humanf_bowidle01", "kevin_humanf_assaultrifle_aim01", "kevin_humanf_castingidle01", "kevin_humanm_throwball01_r_hold" }[kevinIndex]);
                _document.Playback.Seek(_document.Playback.Clip.Duration * .37); FitMotion();
                break;
            default:
                var wolfIndex = _smokeIndex - _libraries.Length - SmokeScenarios.Length;
                if (wolfIndex < WolfSmokeClips.Length * 2)
                {
                    _arena = null; _headEditorOpen = false;
                    ChooseLibrary(_libraries.First(l => l.Id == "quadruped")); _document.ResetAppearance();
                    _filter = "tomek_wolf";
                    _document.Appearance.Flip = wolfIndex >= WolfSmokeClips.Length;
                    _document.Playback.Select("tomek_wolf_" + WolfSmokeClips[wolfIndex % WolfSmokeClips.Length]);
                    _document.Playback.Seek(_document.Playback.Clip.Duration * .65); FitMotion();
                    return true;
                }
                File.WriteAllText(Path.Combine(_smokePath!, "dpi-checks.json"), JsonSerializer.Serialize(_smokeMeasurements, new JsonSerializerOptions { WriteIndented = true }));
                return false;
        }
        return true;
    }

    private void CaptureSmokeFrame()
    {
        var scenario = _smokeIndex - _libraries.Length;
        var name = scenario < 0 ? _libraries[_smokeIndex].Id : scenario < SmokeScenarios.Length ? SmokeScenarios[scenario]
            : "wolf-" + WolfSmokeClips[(scenario - SmokeScenarios.Length) % WolfSmokeClips.Length]
                + (scenario - SmokeScenarios.Length >= WolfSmokeClips.Length ? "-left" : "-right");
        var fontSize = ImGui.GetFontSize();
        if (_smokeFrame == 3) _smokeFontSize = fontSize;
        if (scenario >= 0)
        {
            if (MathF.Abs(_gui.DpiScale - _smokeDpi) > .001f)
                throw new InvalidOperationException("DPI smoke checks require the window to stay on one monitor.");
            if (MathF.Abs(_gui.UiScale - _smokeDpi * _gui.UserScale) > .001f || MathF.Abs(fontSize - _smokeFontSize * _gui.UserScale) > 1f)
                throw new InvalidOperationException("Window resizing changed the UI scale, or the user's font scale did not apply.");
            if (scenario == 0 && (_capture!.Width >= _smokeWidth || _capture.Height >= _smokeHeight))
                throw new InvalidOperationException("The resize smoke check did not resize the actual backbuffer.");
        }
        if (_smokeIndex == 0 || scenario >= 0)
            _smokeMeasurements.Add(new { scenario = name, width = _capture!.Width, height = _capture.Height, windowsScale = _gui.DpiScale, userScale = _gui.UserScale, uiScale = _gui.UiScale, fontPixels = fontSize });
        using var stream = File.Create(Path.Combine(_smokePath!, name + ".png"));
        _capture!.SaveAsPng(stream, _capture.Width, _capture.Height);
    }
}
