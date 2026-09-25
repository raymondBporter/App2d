using App2d.Core.Characters;

namespace App2d.CharacterStudio;

/// <summary>Independent authoring document. A drag is one undo step; loading never mutates imported libraries.</summary>
internal sealed class PuppetDocument
{
    private readonly Stack<string> _undo = [], _redo = [];
    private string _observed, _saved;
    private string? _editStart;
    public PuppetDefinition Definition { get; private set; }
    public string? FilePath { get; private set; }
    public bool Dirty => Definition.ToJson() != _saved;
    public bool CanUndo => _undo.Count > 0 || _editStart is not null;
    public bool CanRedo => _redo.Count > 0;
    public PuppetDocument(PuppetDefinition definition, string? path = null)
    { Definition = definition; FilePath = path; _observed = definition.ToJson(); _saved = path is null ? "" : _observed; }
    public void Record(bool active)
    {
        var current = Definition.ToJson();
        if (current != _observed) { _editStart ??= _observed; _observed = current; }
        if (!active && _editStart is { } start) { _undo.Push(start); _redo.Clear(); _editStart = null; }
    }
    public void Undo()
    {
        Record(false);
        if (_undo.TryPop(out var previous)) { _redo.Push(_observed); Restore(previous); }
    }
    public void Redo()
    { if (_redo.TryPop(out var next)) { _undo.Push(_observed); Restore(next); } }
    private void Restore(string json) { Definition = PuppetDefinition.FromJson(json); _observed = json; _editStart = null; }
    public void Save(string path) { Record(false); Definition.Save(path); FilePath = path; _saved = Definition.ToJson(); }
}
