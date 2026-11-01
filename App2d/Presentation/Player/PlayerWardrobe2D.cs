using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Presentation.Persons;

namespace App2d.Presentation.Player;

internal enum WardrobeSlot2D { Shoes, Gloves, Shins }

/// <summary>One inventory item equips both sides of a pair through the model's existing sockets.</summary>
internal sealed record WardrobeItem2D(string Id, string Name, WardrobeSlot2D Slot, Part Artwork,
    EquipmentSettings Settings, IReadOnlyList<ResolvedAttachment> Parts);

/// <summary>A player's cosmetic equipment and finite inventory. Shared models and library parts are never edited.</summary>
internal sealed class PlayerWardrobe2D
{
    public const int Columns = 6, Rows = 6;
    private readonly WardrobeItem2D?[] _inventory = new WardrobeItem2D?[Columns * Rows];
    private readonly WardrobeItem2D?[] _equipped = new WardrobeItem2D?[Enum.GetValues<WardrobeSlot2D>().Length];
    private readonly Dictionary<string, ModelSocket> _sockets;

    public PlayerWardrobe2D(PersonMoves moves)
    {
        Moves = moves;
        _sockets = moves.Model.Base.Sockets.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var items = new List<WardrobeItem2D>
        {
            Pair("boot", WardrobeSlot2D.Shoes, Humanoid.LeftFootSocket, Humanoid.RightFootSocket, sided: true),
            Pair("sandal", WardrobeSlot2D.Shoes, Humanoid.LeftFootSocket, Humanoid.RightFootSocket, sided: true),
            Pair("glove-leather", WardrobeSlot2D.Gloves, Humanoid.LeftHandSocket, Humanoid.RightHandSocket),
            Pair("glove-steel", WardrobeSlot2D.Gloves, Humanoid.LeftHandSocket, Humanoid.RightHandSocket),
        };
        // Prototype ownership: saved equippable Pieces join the starter inventory. Their art stays shared.
        foreach (var (id, settings) in moves.Equipment.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (!moves.Props.TryGetValue(id, out var part)) throw new InvalidDataException($"Equipment Piece '{id}' did not resolve; check its geometry and material.");
            var slot = settings.Slot switch
            {
                PersonEquipmentSlots.Shoes => WardrobeSlot2D.Shoes,
                PersonEquipmentSlots.Gloves => WardrobeSlot2D.Gloves,
                PersonEquipmentSlots.Shins => WardrobeSlot2D.Shins,
                _ => throw new InvalidDataException($"Unknown equipment slot '{settings.Slot}'."),
            };
            items.Add(new(id, part.Name, slot, part, settings, PersonEquipment.Placements(part, settings, _sockets)));
        }
        if (items.Count > _inventory.Length) throw new InvalidDataException($"Starter wardrobe has {items.Count} items but only {_inventory.Length} cells.");
        for (var i = 0; i < items.Count; i++) _inventory[i] = items[i];
        Items = items;
    }

    public PersonMoves Moves { get; }
    public IReadOnlyList<WardrobeItem2D> Items { get; }
    public IReadOnlyList<WardrobeItem2D?> Inventory => _inventory;
    public WardrobeItem2D? Equipped(WardrobeSlot2D slot) => Enum.IsDefined(slot) ? _equipped[(int)slot] : null;
    public IReadOnlyList<ResolvedAttachment> Attachments { get; private set; } = [];
    public CharacterArtworkOverrides Artwork { get; private set; } = new();
    public event Action? Changed;

    private WardrobeItem2D Pair(string id, WardrobeSlot2D slot, string left, string right, bool sided = false)
    {
        var art = Moves.Props[id];
        var settings = new EquipmentSettings { Slot = slot == WardrobeSlot2D.Shoes ? PersonEquipmentSlots.Shoes : PersonEquipmentSlots.Gloves, MirrorRight = false };
        ResolvedAttachment Place(string part, string socket, string side) => ResolvedAttachment.Resolve(
            new() { Id = id + "-" + side, Asset = part, Socket = socket }, key => Moves.Props.GetValueOrDefault(key), _sockets, "Player wardrobe");
        return new(id, art.Name, slot, art, settings,
            [Place(sided ? id + "-left" : id, left, "left"), Place(sided ? id + "-right" : id, right, "right")]);
    }

    public bool Equip(int cell, WardrobeSlot2D slot)
    {
        if (!Enum.IsDefined(slot) || !ValidCell(cell) || _inventory[cell] is not { } item || item.Slot != slot) return false;
        (_inventory[cell], _equipped[(int)slot]) = (_equipped[(int)slot], item);
        Refresh();
        return true;
    }

    public bool Unequip(WardrobeSlot2D slot, int cell = -1)
    {
        if (!Enum.IsDefined(slot)) return false;
        if (cell == -1) cell = Array.FindIndex(_inventory, item => item is null);
        if (!ValidCell(cell) || _equipped[(int)slot] is not { } worn ||
            (_inventory[cell] is { } replacement && replacement.Slot != slot)) return false;
        (_inventory[cell], _equipped[(int)slot]) = (worn, _inventory[cell]);
        Refresh();
        return true;
    }

    public bool Move(int from, int to)
    {
        if (!ValidCell(from) || !ValidCell(to) || from == to || _inventory[from] is null) return false;
        (_inventory[from], _inventory[to]) = (_inventory[to], _inventory[from]);
        return true;
    }

    private static bool ValidCell(int cell) => cell >= 0 && cell < Columns * Rows;

    private void Refresh()
    {
        Attachments = [.. _equipped.OfType<WardrobeItem2D>().SelectMany(item => item.Parts)];
        Artwork = PersonEquipment.Replacements(Moves.Model, _equipped.OfType<WardrobeItem2D>().Select(item => item.Settings));
        Changed?.Invoke();
    }

    /// <summary>Ordinary hair/weapons. Equipped pairs draw through Attachments so their per-side transforms are retained.</summary>
    public IReadOnlyList<(Part Prop, ModelSocket Socket)> Dress(MotionClip clip, float seconds, PersonGear gear)
    {
        var props = PersonLoadout.Dressed(clip, seconds, gear, Moves.Hero, Moves.Gear)
            .Where(w => w.Socket is not (Humanoid.LeftFootSocket or Humanoid.RightFootSocket or Humanoid.LeftHandSocket or Humanoid.RightHandSocket) && !Artwork.HiddenSockets.Contains(w.Socket))
            .Select(w => (Prop: Moves.Props[w.Prop], Socket: _sockets[w.Socket])).ToList();
        return props;
    }
}
