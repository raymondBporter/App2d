using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Presentation.Persons;

namespace App2d.Presentation.Player;

internal enum WardrobeSlot2D { Shoes, Gloves }

/// <summary>One inventory item equips both sides of a pair through the model's existing sockets.</summary>
internal sealed record WardrobeItem2D(string Id, string Name, WardrobeSlot2D Slot, Part Artwork,
    IReadOnlyList<(Part Prop, ModelSocket Socket)> Parts);

/// <summary>A player's cosmetic equipment and finite inventory. Shared models and library parts are never edited.</summary>
internal sealed class PlayerWardrobe2D
{
    public const int Columns = 6, Rows = 6;
    private readonly WardrobeItem2D?[] _inventory = new WardrobeItem2D?[Columns * Rows];
    private readonly WardrobeItem2D?[] _equipped = new WardrobeItem2D?[2];
    private readonly Dictionary<string, ModelSocket> _sockets;

    public PlayerWardrobe2D(PersonMoves moves)
    {
        Moves = moves;
        _sockets = moves.Model.Base.Sockets.ToDictionary(s => s.Id, StringComparer.Ordinal);
        _inventory[0] = Pair("boot", WardrobeSlot2D.Shoes, Humanoid.LeftFootSocket, Humanoid.RightFootSocket, sided: true);
        _inventory[1] = Pair("sandal", WardrobeSlot2D.Shoes, Humanoid.LeftFootSocket, Humanoid.RightFootSocket, sided: true);
        _inventory[2] = Pair("glove-leather", WardrobeSlot2D.Gloves, Humanoid.LeftHandSocket, Humanoid.RightHandSocket);
        _inventory[3] = Pair("glove-steel", WardrobeSlot2D.Gloves, Humanoid.LeftHandSocket, Humanoid.RightHandSocket);
        Items = _inventory.OfType<WardrobeItem2D>().ToArray();
    }

    public PersonMoves Moves { get; }
    public IReadOnlyList<WardrobeItem2D> Items { get; }
    public IReadOnlyList<WardrobeItem2D?> Inventory => _inventory;
    public WardrobeItem2D? Equipped(WardrobeSlot2D slot) => _equipped[(int)slot];
    public event Action? Changed;

    private WardrobeItem2D Pair(string id, WardrobeSlot2D slot, string left, string right, bool sided = false)
    {
        var art = Moves.Props[id];
        return new(id, art.Name, slot, art,
            [(Moves.Props[sided ? id + "-left" : id], _sockets[left]), (Moves.Props[sided ? id + "-right" : id], _sockets[right])]);
    }

    public bool Equip(int cell, WardrobeSlot2D slot)
    {
        if (!ValidCell(cell) || _inventory[cell] is not { } item || item.Slot != slot) return false;
        (_inventory[cell], _equipped[(int)slot]) = (_equipped[(int)slot], item);
        Changed?.Invoke();
        return true;
    }

    public bool Unequip(WardrobeSlot2D slot, int cell = -1)
    {
        if (cell == -1) cell = Array.FindIndex(_inventory, item => item is null);
        if (!ValidCell(cell) || _equipped[(int)slot] is not { } worn ||
            (_inventory[cell] is { } replacement && replacement.Slot != slot)) return false;
        (_inventory[cell], _equipped[(int)slot]) = (worn, _inventory[cell]);
        Changed?.Invoke();
        return true;
    }

    public bool Move(int from, int to)
    {
        if (!ValidCell(from) || !ValidCell(to) || from == to || _inventory[from] is null) return false;
        (_inventory[from], _inventory[to]) = (_inventory[to], _inventory[from]);
        return true;
    }

    private static bool ValidCell(int cell) => cell >= 0 && cell < Columns * Rows;

    /// <summary>The ordinary hair/weapons plus equipped pairs, using the very same pose and socket placement.</summary>
    public IReadOnlyList<(Part Prop, ModelSocket Socket)> Dress(MotionClip clip, float seconds, PersonGear gear)
    {
        var props = PersonLoadout.Dressed(clip, seconds, gear, Moves.Hero, Moves.Gear)
            .Where(w => w.Socket is not (Humanoid.LeftFootSocket or Humanoid.RightFootSocket or Humanoid.LeftHandSocket or Humanoid.RightHandSocket))
            .Select(w => (Prop: Moves.Props[w.Prop], Socket: _sockets[w.Socket])).ToList();
        foreach (var item in _equipped)
            if (item is not null) props.AddRange(item.Parts);
        return props;
    }
}
