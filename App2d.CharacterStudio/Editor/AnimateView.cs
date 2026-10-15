using App2d.Core.Characters.Editing;

namespace App2d.CharacterStudio.Editor;

/// <summary>Connects the animation outline, inspector, viewport, and timeline to the shared editor shell.</summary>
internal sealed class AnimateView : IWorkspaceView
{
    private readonly AnimationOutline _outline;
    private readonly AnimationInspector _inspector;
    private readonly AnimationViewport _viewport;
    private readonly AnimationTimeline _timeline;

    public AnimateView(EditorSession session, Viewport viewport)
    {
        var weapons = new AnimationWeaponEditor(session);
        _viewport = new(session, viewport, weapons);
        _outline = new(session, weapons);
        _inspector = new(session, _viewport, weapons);
        _timeline = new(session);
    }

    public Workspace Mode => Workspace.Animate;
    public float TimelineHeight => 230;
    public void Outline() => _outline.Draw();
    public void Inspector() => _inspector.Draw();
    public void Overlay(ViewportFrame frame) => _viewport.Draw(frame);
    public void Timeline() => _timeline.Draw();
}
