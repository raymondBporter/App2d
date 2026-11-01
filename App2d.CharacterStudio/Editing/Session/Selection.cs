using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.Importing;
using App2d.Core.Animation;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Physics.Motion;
using System.Numerics;

namespace App2d.CharacterStudio.Editing;

/// <summary>Model edits rest geometry and appearance; Animate edits motion; Entity edits what a character does in the game.</summary>
public enum Workspace
{
    Model, Animate, Entity, Appearance, Library
}

/// <summary>What the author has picked. Session state, never saved into assets.</summary>
public sealed class Selection
{
    public string? Control { get; set; }
    public string? Part { get; set; }
    /// <summary>Model workspace: the library part instance (by instance ID) being edited on the character or overridden on the variant.</summary>
    public string? Attachment { get; set; }
    public string? Chain { get; set; }
    /// <summary>Animation workspace: the appearance slot being keyed.</summary>
    public string? Slot { get; set; }
    /// <summary>A selected key time on the timeline.</summary>
    public float? Key { get; set; }
    /// <summary>Entity workspace: the action, and the hit window inside it, being edited.</summary>
    public string? Action { get; set; }
    public string? Hit { get; set; }
    /// <summary>Entity workspace: the attached collider being edited.</summary>
    public string? Collider { get; set; }
    /// <summary>Selects one workspace target and clears the previous selection.</summary>
    public void Select(string? control = null, string? part = null, string? chain = null, string? slot = null, string? attachment = null)
    {
        Clear();
        Control = control;
        Part = part;
        Chain = chain;
        Slot = slot;
        Attachment = attachment;
    }

    public void Clear()
    {
        Control = Part = Chain = Slot = Action = Hit = Attachment = Collider = null;
        Key = null;
    }
}
