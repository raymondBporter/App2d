using App2d.Core.Characters;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>What each workspace contributes to the shared layout. The shell owns the frame; a view fills its regions.</summary>
internal interface IWorkspaceView
{
    Workspace Mode { get; }
    /// <summary>The structure tree under the asset browser.</summary>
    void Outline();
    void Inspector();
    /// <summary>Handles, overlays and viewport input for the evaluated scene.</summary>
    void Overlay(ViewportFrame frame);
    float TimelineHeight { get; }
    void Timeline();
}

/// <summary>
/// One window, one viewport, one document workflow: asset browser and outline on the left, the shared viewport in the middle,
/// the selection inspector on the right and the timeline below. Model and Animate are views over the same session.
/// </summary>
internal sealed class EditorShell : IDisposable
{
    private readonly ImGuiHost _gui;
    private readonly AssetBrowser _browser;
    private readonly IWorkspaceView[] _views;

    public EditorShell(EditorSession session, Viewport viewport, ArenaTest test, ImGuiHost gui)
    {
        Session = session; Viewport = viewport; Test = test; _gui = gui;
        _browser = new(session);
        _views = [new ModelView(session, viewport), new AnimateView(session, viewport)];
    }

    public EditorSession Session { get; }
    public Viewport Viewport { get; }
    /// <summary>The arena test. Shell state: entering and leaving it never changes the session's documents or selection.</summary>
    public ArenaTest Test { get; }
    /// <summary>False while a smoke run drives the arena itself.</summary>
    public bool LiveTest { get; set; } = true;
    private IWorkspaceView View => _views.First(v => v.Mode == Session.Mode);

    public void Draw()
    {
        Ui.Scale = _gui.UiScale; var scale = Ui.Scale;
        var io = ImGui.GetIO();
        ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(io.DisplaySize);
        ImGui.Begin("Character editor", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
        TopBar();
        Shortcuts(io);
        ImGui.Separator();
        var view = View;
        var available = ImGui.GetContentRegionAvail(); var status = 26 * scale;
        // Side panels and the timeline keep their design size where there is room, but never crowd out the viewport.
        var timeline = MathF.Min(view.TimelineHeight * scale, available.Y * .34f);
        var left = MathF.Min(250 * scale, available.X * .2f); var right = MathF.Min(330 * scale, available.X * .25f);
        var body = available.Y - timeline - status - 2 * ImGui.GetStyle().ItemSpacing.Y;
        ImGui.BeginChild("left", new(left, body));
        ImGui.BeginChild("browser", new(0, body * .5f), ImGuiChildFlags.Borders); _browser.Draw(); ImGui.EndChild();
        ImGui.BeginChild("outline", Vector2.Zero, ImGuiChildFlags.Borders); if (Test.Active) Test.RosterPanel(); else view.Outline(); ImGui.EndChild();
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("stage", new(Math.Max(200, available.X - left - right - 2 * ImGui.GetStyle().ItemSpacing.X), body), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (Test.Active) Test.Stage(ImGui.GetContentRegionAvail(), LiveTest); else Stage(view);
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("inspector", new(right, body), ImGuiChildFlags.Borders); if (Test.Active) Test.Inspector(); else view.Inspector(); ImGui.EndChild();
        ImGui.BeginChild("timeline", new(0, timeline), ImGuiChildFlags.Borders); if (Test.Active) Test.Timeline(); else view.Timeline(); ImGui.EndChild();
        StatusLine();
        _browser.DrawDialogs();
        ImGui.End();
        // A widget or drag in progress keeps its transaction open; the moment nothing is active, the step is committed.
        if (!ImGui.IsAnyItemActive()) Session.CommitAll();
    }

    private void TopBar()
    {
        var document = Session.ActiveDocument; var rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        ImGui.TextColored(Ui.Accent, "CHARACTER EDITOR"); ImGui.SameLine();
        foreach (var mode in new[] { Workspace.Model, Workspace.Animate })
        {
            var active = Session.Mode == mode;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
            if (ImGui.Button(mode.ToString())) Session.SetMode(mode);
            if (active) ImGui.PopStyleColor();
            ImGui.SameLine();
        }
        ImGui.BeginDisabled(); ImGui.Button("Entity"); ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Entity editing is not built yet; entities are JSON files under authored/entities. Use Test to play them.");
        ImGui.SameLine();
        if (Test.Active) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
        if (ImGui.Button(Test.Active ? "Stop test" : "Test")) { if (Test.Active) Test.Stop(); else { Session.CommitAll(); Test.Start(); } }
        if (Test.Active) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Play the entity arena from a snapshot of the current drafts, unsaved edits included.");
        ImGui.SameLine(); ImGui.TextDisabled("|"); ImGui.SameLine();
        if (Ui.Button("Save", document is not null)) Session.Save(document);
        ImGui.SameLine();
        var dirty = Session.Assets.DirtyDocuments.Count();
        if (Ui.Button(dirty > 0 ? $"Save all ({dirty})" : "Save all", dirty > 0)) Session.SaveAll();
        if (dirty > 0 && ImGui.IsItemHovered()) ImGui.SetTooltip("Unsaved: " + string.Join(", ", Session.Assets.DirtyDocuments.Select(d => d.Id)) + "\nEach file is written separately; this is not atomic.");
        ImGui.SameLine(); if (Ui.Button("Undo", document?.CanUndo == true)) Session.Undo();
        ImGui.SameLine(); if (Ui.Button("Redo", document?.CanRedo == true)) Session.Redo();
        ImGui.SameLine();
        ImGui.TextUnformatted(document is null ? "Nothing open" : $"{document.Name}{(document.Dirty || document.IsNew ? " *" : "")}");
        ImGui.SameLine(); ImGui.TextDisabled(document is null ? "" : Describe(document));
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), rightEdge - ImGui.CalcTextSize("UI").X - 2 * ImGui.GetStyle().FramePadding.X));
        if (ImGui.Button("UI")) ImGui.OpenPopup("ui-scale");
        if (ImGui.BeginPopup("ui-scale"))
        {
            foreach (var factor in new[] { .75f, 1f, 1.25f, 1.5f, 1.75f, 2f })
                if (ImGui.Selectable($"{factor:P0}", MathF.Abs(_gui.UserScale - factor) < .001f)) _gui.UserScale = factor;
            ImGui.EndPopup();
        }
    }

    private string Describe(AssetDocument document) => document switch
    {
        AssetDocument<ModelVariant> v => "variant of " + (Session.Assets.Model(v.Asset.Base)?.Name ?? v.Asset.Base + " (missing)"),
        AssetDocument<MotionClip> c => "animation for " + (Session.Assets.Model(c.Asset.Model)?.Name ?? c.Asset.Model + " (missing)"),
        _ => "base model",
    };

    private void Stage(IWorkspaceView view)
    {
        ViewportToolbar();
        var frame = Viewport.Draw(ImGui.GetContentRegionAvail(), Session.Scene());
        frame.Draw.PushClipRect(frame.Origin, frame.Origin + frame.Size, true);
        DrawContacts(frame);
        view.Overlay(frame);
        if (frame.Primary is null) frame.Draw.AddText(frame.Origin + new Vector2(20, 20) * Ui.Scale, Ui.Color(90, 90, 80), "Open or create a model to begin.");
        frame.Draw.PopClipRect();
    }

    private void ViewportToolbar()
    {
        if (ImGui.SmallButton("Fit")) Viewport.Fit(Session.Scene().FirstOrDefault());
        ImGui.SameLine(); var follow = Viewport.Follow; if (ImGui.Checkbox("Follow", ref follow)) Viewport.Follow = follow;
        ImGui.SameLine(); var game = Viewport.GameSize; if (ImGui.Checkbox("Game size", ref game)) Viewport.GameSize = game;
        ImGui.SameLine(); if (ImGui.SmallButton(Session.Compare.Count > 0 ? $"Compare ({Session.Compare.Count + 1})" : "Compare")) ImGui.OpenPopup("compare");
        if (ImGui.BeginPopup("compare"))
        {
            Ui.Help("Pin up to two more builds of the same base. They play the same clip at the same phase and world scale.");
            var basis = Session.Assets.BaseOf(Session.SubjectId ?? "");
            foreach (var id in Session.Assets.Models.Select(m => m.Id).Concat(Session.Assets.Variants.Select(v => v.Id)).Where(id => id != Session.SubjectId && Session.Assets.BaseOf(id) == basis).Order())
            {
                var pinned = Session.Compare.Contains(id);
                if (ImGui.Checkbox(Session.Assets.Find(id)!.Name + "##" + id, ref pinned)) { if (pinned) Session.Pin(id); else Session.Compare.Remove(id); }
            }
            ImGui.EndPopup();
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(150 * Ui.Scale);
        if (ImGui.BeginCombo("##expression", Session.Expression is null ? "Game face: none" : "Game face: " + Session.Expression))
        {
            if (ImGui.Selectable("none (clip, then model default)", Session.Expression is null)) Session.Expression = null;
            foreach (var face in FaceExpressions.Names) if (ImGui.Selectable(face, Session.Expression == face)) Session.Expression = face;
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Preview the expression gameplay would request. It wins over the clip's face channel and never moves the body.");
    }

    private static void DrawContacts(ViewportFrame frame)
    {
        if (frame.Primary is not { } primary) return;
        foreach (var contact in primary.Subject.Pose.Contacts)
        {
            var p = frame.Screen(contact.Target); var good = contact.Residual < .005f; var color = good ? Ui.Color(36, 140, 104) : Ui.Color(240, 106, 50);
            frame.Draw.AddLine(p - new Vector2(8, 0), p + new Vector2(8, 0), color, 2); frame.Draw.AddLine(p - new Vector2(0, 8), p + new Vector2(0, 8), color, 2);
            if (!good) frame.Draw.AddText(p + new Vector2(8, 4), color, $"{contact.Chain} misses by {contact.Residual:F3}");
        }
        foreach (var chain in primary.Subject.Pose.Chains.Where(c => !c.Reached && c.Residual > .005f))
            frame.Draw.AddText(frame.Screen(primary.Subject.Pose.World(primary.Subject.Model.Chains[chain.Chain].End)) + new Vector2(8, -18), Ui.Color(240, 106, 50), $"{chain.Chain} short {chain.Residual:F3}");
    }

    private void StatusLine()
    {
        if (Session.Message.Length > 0) { if (Session.MessageIsError) Ui.Problem(Session.Message); else ImGui.TextDisabled(Session.Message); }
        else ImGui.TextDisabled("Space play  |  Ctrl+S save  |  Ctrl+Shift+S save all  |  Ctrl+Z undo  |  Scroll zoom  |  Middle drag pan");
    }

    private void Shortcuts(ImGuiIOPtr io)
    {
        if (io.WantTextInput) return;
        if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S)) { if (io.KeyShift) Session.SaveAll(); else Session.Save(Session.ActiveDocument); }
        if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Z)) Session.Undo();
        if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Y)) Session.Redo();
        if (!Test.Active && !ImGui.IsAnyItemActive() && ImGui.IsKeyPressed(ImGuiKey.Space, false)) Session.TogglePlay();
    }

    public void Dispose() { Viewport.Dispose(); Test.Dispose(); }
}
