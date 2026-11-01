using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;
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
/// the selection inspector on the right and the timeline below. Model and Animate are views over the same session. Drag
/// handles between the panels resize them; <paramref name="preferencesChanged"/> hears about a dropped handle or a new UI scale.
/// </summary>
internal sealed class EditorShell(EditorSession session, Viewport viewport, ArenaTest test, ImGuiHost gui, SpatialPreview? spatial = null, EditorLayout? layout = null, Action? preferencesChanged = null) : IDisposable
{
    private readonly AssetBrowser _browser = new(session);
    private readonly EditorLayout _layout = layout ?? new();
    private bool _layoutDirty;
    private readonly IWorkspaceView[] _views =
    [
        new ModelView(session, viewport),
        new AnimateView(session, viewport),
        new EntityView(session),
        new AppearanceView(session)
    ];

    public EditorSession Session { get; } = session;
    public Viewport Viewport { get; } = viewport;
    /// <summary>The arena test. Shell state: entering and leaving it never changes the session's documents or selection.</summary>
    public ArenaTest Test { get; } = test;
    /// <summary>False while a smoke run drives the arena itself.</summary>
    public bool LiveTest { get; set; } = true;
    public bool Show3DPreview { get; set; }
    private IWorkspaceView View => _views.First(v => v.Mode == Session.Mode);
    /// <summary>The Entity workspace's view, for the smokes to frame an inspector section.</summary>
    public EntityView EntityInspector => _views.OfType<EntityView>().First();

    public void Draw()
    {
        var view = View;
        Ui.Root("Character editor", () =>
        {
            TopBar();
            Shortcuts(ImGui.GetIO());
            ImGui.Separator();
            var scale = Ui.Scale;
            var available = ImGui.GetContentRegionAvail();
            var status = 26 * scale;
            if (Show3DPreview && spatial is not null)
            {
                spatial.Draw(new(available.X, MathF.Max(100, available.Y - status - ImGui.GetStyle().ItemSpacing.Y)));
                StatusLine();
                return;
            }
            // Side panels and the timeline keep the author's sizes where there is room, but never crowd out the viewport. The
            // handles between them take the place of the item spacing, so the dragged sizes are the panels' own.
            var handle = Ui.SplitterThickness;
            var minPanel = 100 * scale;
            var minStage = 200 * scale;
            var columns = available.X - 2 * handle;
            var (left, right) = EditorLayout.FitColumns(columns, _layout.LeftWidth * scale, _layout.RightWidth * scale, minPanel, minStage);
            var stage = MathF.Max(1, columns - left - right);
            var minTimeline = 32 * scale;
            var rows = available.Y - status - ImGui.GetStyle().ItemSpacing.Y - handle;
            var timeline = Math.Clamp(_layout.TimelineHeight(view.Mode, view.TimelineHeight) * scale, minTimeline, MathF.Max(minTimeline, rows - minStage));
            var body = MathF.Max(1, rows - timeline);

            Ui.Panel("left", content: () =>
            {
                var column = ImGui.GetContentRegionAvail();
                var minHalf = 60 * scale;
                var browser = Math.Clamp(_layout.BrowserShare * column.Y, minHalf, MathF.Max(minHalf, column.Y - handle - minHalf));
                Ui.Panel("browser", _browser.Draw, size: new(0, browser));
                if (Ui.RowSplitter("browser-split", ref browser, 0, minHalf, column.Y - handle - minHalf))
                {
                    _layout.BrowserShare = browser / column.Y;
                    _layoutDirty = true;
                }

                Ui.Panel("outline", () =>
                {
                    if (Test.Active)
                    {
                        Test.RosterPanel();
                    }
                    else
                    {
                        view.Outline();
                    }
                });
            }, size: new(left, body), border: false);
            if (Ui.ColumnSplitter("left-split", ref left, body, minPanel, columns - right - minStage))
            {
                _layout.LeftWidth = left / scale;
                _layoutDirty = true;
            }

            Ui.Panel("stage", content: () =>
            {
                if (Test.Active)
                {
                    Test.Stage(LiveTest);
                }
                else
                {
                    Stage(view);
                }
            }, size: new(stage, body), flags: ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            if (Ui.ColumnSplitter("right-split", ref right, body, minPanel, columns - left - minStage, panelOnRight: true))
            {
                _layout.RightWidth = right / scale;
                _layoutDirty = true;
            }

            Ui.Panel("inspector", content: () =>
            {
                if (Test.Active)
                {
                    Test.Inspector();
                }
                else
                {
                    view.Inspector();
                }
            }, size: new(right, body));
            if (Ui.RowSplitter("timeline-split", ref timeline, 0, minTimeline, rows - minStage, panelBelow: true))
            {
                _layout.SetTimelineHeight(view.Mode, timeline / scale);
                _layoutDirty = true;
            }

            Ui.Panel("timeline", content: () =>
            {
                if (Test.Active)
                {
                    Test.Timeline();
                }
                else
                {
                    view.Timeline();
                }
            }, size: new(0, timeline));
            StatusLine();
            _browser.DrawDialogs();
        });
        // A widget or drag in progress keeps its transaction open; the moment nothing is active, the step is committed and a
        // dropped panel handle is saved.
        if (!ImGui.IsAnyItemActive())
        {
            Session.CommitAll();
            if (_layoutDirty)
            {
                _layoutDirty = false;
                preferencesChanged?.Invoke();
            }
        }
    }

    private void TopBar()
    {
        var document = Session.ActiveDocument;
        var rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        ImGui.TextColored(UiTheme.Accent, "CHARACTER EDITOR");
        ImGui.SameLine();
        foreach (var mode in new[] { Workspace.Model, Workspace.Animate, Workspace.Entity, Workspace.Appearance })
        {
            var active = !Show3DPreview && Session.Mode == mode;
            if (active)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
            }

            if (ImGui.Button(mode.ToString()))
            {
                Session.SetMode(mode);
                Show3DPreview = false;
            }

            if (active)
            {
                ImGui.PopStyleColor();
            }

            ImGui.SameLine();
        }
        if (spatial is not null)
        {
            var showing = Show3DPreview;
            if (showing) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
            if (ImGui.Button("3D preview")) Show3DPreview = !Show3DPreview;
            if (showing) ImGui.PopStyleColor();
            ImGui.SameLine();
        }
        if (Test.Active)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
        }

        if (ImGui.Button(Test.Active ? "Stop test" : "Test"))
        {
            Show3DPreview = false;
            if (Test.Active)
            {
                Test.Stop();
            }
            else
            {
                Session.CommitAll();
                Test.Start();
            }
        }
        if (Test.Active)
        {
            ImGui.PopStyleColor();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Play the entity arena from a snapshot of the current drafts, unsaved edits included.");
        }

        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        if (Ui.Button("Save", document is not null))
        {
            Session.Save(document);
        }

        ImGui.SameLine();
        var dirty = Session.Assets.DirtyDocuments.Count();
        if (Ui.Button(dirty > 0 ? $"Save all ({dirty})" : "Save all", dirty > 0))
        {
            Session.SaveAll();
        }

        if (dirty > 0 && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Unsaved: " + string.Join(", ", Session.Assets.DirtyDocuments.Select(d => d.Id)) + "\nEach file is written separately; this is not atomic.");
        }

        ImGui.SameLine();
        if (Ui.Button("Undo", Session.CanUndo))
        {
            Session.Undo();
        }

        ImGui.SameLine();
        if (Ui.Button("Redo", Session.CanRedo))
        {
            Session.Redo();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(document is null ? "Nothing open" : $"{document.Name}{(document.Dirty || document.IsNew ? " *" : "")}");
        ImGui.SameLine();
        ImGui.TextDisabled(document is null ? "" : Describe(document));
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), rightEdge - ImGui.CalcTextSize("UI").X - 2 * ImGui.GetStyle().FramePadding.X));
        if (ImGui.Button("UI"))
        {
            ImGui.OpenPopup("ui-scale");
        }

        if (ImGui.BeginPopup("ui-scale"))
        {
            foreach (var factor in new[] { .75f, 1f, 1.25f, 1.5f, 1.75f, 2f })
            {
                if (ImGui.Selectable($"{factor:P0}", MathF.Abs(gui.UserScale - factor) < .001f))
                {
                    gui.UserScale = factor;
                    preferencesChanged?.Invoke();
                }
            }

            ImGui.EndPopup();
        }
    }

    private string Describe(AssetDocument document) => document switch
    {
        AssetDocument<ModelVariant> v => "variant of " + (Session.Assets.Model(v.Asset.Base)?.Name ?? v.Asset.Base + " (missing)"),
        AssetDocument<MotionClip> c => "animation for " + (Session.Assets.Model(c.Asset.Model)?.Name ?? c.Asset.Model + " (missing)"),
        AssetDocument<Part> e when e.Asset.Behavior is not null => "entity on " + (Session.Assets.Find(e.Asset.Model)?.Name ?? e.Asset.Model + " (missing)"),
        AssetDocument<Part> p => p.Asset.IsModule ? "shared rig module" : "shared part",
        _ => "base model",
    };

    private void Stage(IWorkspaceView view)
    {
        ViewportToolbar();
        var frame = Viewport.Draw(ImGui.GetContentRegionAvail(), Session.Scene(), Session);
        frame.Draw.PushClipRect(frame.Origin, frame.Origin + frame.Size, true);
        DrawContacts(frame);
        view.Overlay(frame);
        if (frame.Primary is null)
        {
            frame.Draw.AddText(frame.Origin + new Vector2(20, 20) * Ui.Scale, StudioTheme.EmptyMessage.PackedValue, Session.Mode == Workspace.Entity ? Session.EntityError ?? "Open or create an entity." : "Open or create a model to begin.");
        }

        frame.Draw.PopClipRect();
    }

    private void ViewportToolbar()
    {
        if (Session.Assets.Resolve(Session.SubjectId) is { Base.Skins.Count: > 0 } skinned)
        {
            if (Ui.Combo("Skin", Session.Skin ?? "default", skinned.Base.Skins.Select(s => s.Id)) is { } skin)
            {
                Session.Skin = skin;
            }

            ImGui.SameLine();
        }
        if (ImGui.SmallButton("Fit"))
        {
            Viewport.Fit(Session.Scene().FirstOrDefault());
        }

        ImGui.SameLine();
        var follow = Viewport.Follow;
        if (ImGui.Checkbox("Follow", ref follow))
        {
            Viewport.Follow = follow;
        }

        ImGui.SameLine();
        var game = Viewport.GameSize;
        if (ImGui.Checkbox("Game size", ref game))
        {
            Viewport.GameSize = game;
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(Session.Mode == Workspace.Entity);
        if (ImGui.SmallButton(Session.Compare.Count > 0 ? $"Compare ({Session.Compare.Count + 1})" : "Compare"))
        {
            ImGui.OpenPopup("compare");
        }

        if (ImGui.BeginPopup("compare"))
        {
            Ui.Help("Pin up to two more builds of the same base. They play the same clip at the same phase and world scale.");
            var basis = Session.Assets.BaseOf(Session.SubjectId ?? "");
            foreach (var id in Session.Assets.Models.Select(m => m.Id).Concat(Session.Assets.Variants.Select(v => v.Id)).Where(id => id != Session.SubjectId && Session.Assets.BaseOf(id) == basis).Order())
            {
                var pinned = Session.Compare.Contains(id);
                if (ImGui.Checkbox(Session.Assets.Find(id)!.Name + "##" + id, ref pinned))
                {
                    if (pinned)
                    {
                        Session.Pin(id);
                    }
                    else
                    {
                        Session.Compare.Remove(id);
                    }
                }
            }
            ImGui.EndPopup();
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150 * Ui.Scale);
        if (ImGui.BeginCombo("##expression", Session.Expression is null ? "Game face: none" : "Game face: " + Session.Expression))
        {
            if (ImGui.Selectable("none (clip, then instance default)", Session.Expression is null))
            {
                Session.Expression = null;
            }

            // Every word the subject's animated part instances bind (P7a/P7c); there is no fixed vocabulary any more.
            var bound = Session.Scene().SelectMany(s => s.Model.Attachments.Where(a => a.IsAnimated).SelectMany(a => a.Asset.Expressions.Keys.Append(Part.None)));
            foreach (var face in bound.Distinct(StringComparer.Ordinal))
            {
                if (ImGui.Selectable(face, Session.Expression == face))
                {
                    Session.Expression = face;
                }
            }

            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Preview the expression gameplay would request. It wins over the clip's face channel and never moves the body.");
        }
    }

    private static void DrawContacts(ViewportFrame frame)
    {
        if (frame.Primary is not { } primary)
        {
            return;
        }

        foreach (var contact in primary.Subject.Pose.Contacts)
        {
            var p = frame.Screen(contact.Target);
            var good = contact.Residual < .005f;
            var color = good ? StudioTheme.ConstraintSatisfied.PackedValue : StudioTheme.ConstraintWarning.PackedValue;
            frame.Draw.AddLine(p - new Vector2(8, 0), p + new Vector2(8, 0), color, 2);
            frame.Draw.AddLine(p - new Vector2(0, 8), p + new Vector2(0, 8), color, 2);
            if (!good)
            {
                frame.Draw.AddText(p + new Vector2(8, 4), color, $"{contact.Chain} misses by {contact.Residual:F3}");
            }
        }
        foreach (var chain in primary.Subject.Pose.Chains.Where(c => !c.Reached && c.Residual > .005f))
        {
            frame.Draw.AddText(frame.Screen(primary.Subject.Pose.World(primary.Subject.Model.Chains[chain.Chain].End)) + new Vector2(8, -18), StudioTheme.ConstraintWarning.PackedValue, $"{chain.Chain} short {chain.Residual:F3}");
        }
    }

    private void StatusLine()
    {
        if (Session.Message.Length > 0)
        {
            if (Session.MessageIsError)
            {
                Ui.Problem(Session.Message);
            }
            else
            {
                ImGui.TextDisabled(Session.Message);
            }
        }
        else
        {
            ImGui.TextDisabled("Space play  |  Ctrl+S save  |  Ctrl+Shift+S save all  |  Ctrl+Z undo  |  Scroll zoom  |  Middle drag pan");
        }
    }

    private void Shortcuts(ImGuiIOPtr io)
    {
        if (io.WantTextInput)
        {
            return;
        }

        if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S))
        {
            if (io.KeyShift)
            {
                Session.SaveAll();
            }
            else
            {
                Session.Save(Session.ActiveDocument);
            }
        }
        if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Z))
        {
            Session.Undo();
        }

        if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Y))
        {
            Session.Redo();
        }

        if (!Test.Active && !ImGui.IsAnyItemActive() && ImGui.IsKeyPressed(ImGuiKey.Space, false))
        {
            Session.TogglePlay();
        }
    }

    public void Dispose()
    {
        Viewport.Dispose();
        Test.Dispose();
        spatial?.Dispose();
    }
}
