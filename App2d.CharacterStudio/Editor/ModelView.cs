using App2d.CharacterStudio.Editor.Model;
using App2d.Core.Characters.Editing;

namespace App2d.CharacterStudio.Editor;

/// <summary>Connects the model outline, inspectors, and viewport to the shared editor shell.</summary>
internal sealed class ModelView : IWorkspaceView
{
    private readonly EditorSession _session;
    private readonly ModelOutline _outline;
    private readonly BaseModelInspector _baseInspector;
    private readonly VariantModelInspector _variantInspector;
    private readonly ModelViewport _viewport;

    public ModelView(EditorSession session, Viewport viewport)
    {
        _session = session;
        var structural = new ModelStructuralEdits(session);
        var cutout = new ModelCutoutEditor(session);
        var parts = new ModelPartInspector(session, cutout);
        _outline = new(session, structural);
        _baseInspector = new(session, structural, parts);
        _variantInspector = new(session, parts);
        _viewport = new(session, viewport, cutout);
    }

    public Workspace Mode => Workspace.Model;
    public float TimelineHeight => 64;
    public void Outline() => _outline.Draw();
    public void Overlay(ViewportFrame frame) => _viewport.Draw(frame);
    public void Timeline() => TransportBar.Draw(_session, preview: true);

    public void Inspector()
    {
        if (_session.SubjectVariant is { } variant)
        {
            _variantInspector.Draw(variant);
        }
        else if (_session.SubjectModel is { } model)
        {
            _baseInspector.Draw(model);
        }
        else
        {
            Ui.Help("Open a model or variant from the browser, or create one with New.");
            return;
        }

        foreach (var problem in _session.ActiveDocument is { } document ? _session.Assets.Problems(document) : [])
        {
            Ui.Problem(problem);
        }

        if (_session.SubjectVariant is null && _session.SubjectModel is { } opened)
        {
            References.Draw(_session, opened.Id);
        }
    }
}
