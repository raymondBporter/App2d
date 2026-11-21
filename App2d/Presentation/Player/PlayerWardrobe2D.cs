using App2d.Core.Animation;
using App2d.Core.Characters;
using App2d.Core.Kinematics;
using App2d.Presentation.Persons;
using System.Collections.Immutable;
using static App2d.Core.Characters.PersonSkeleton;

namespace App2d.Presentation.Player;

internal enum WardrobeSlot2D { Shoes, Gloves, Shins }

/// <summary>
/// Something the player can wear on both sides of a pair: its pieces on the hero's left and right mounts, and the same
/// thing on its own for an icon.
/// </summary>
internal sealed record WardrobeItem2D(string Id, string Name, WardrobeSlot2D Slot, ImmutableArray<Attachment> Worn, ImmutableArray<Attachment> Icon);

/// <summary>
/// What the player owns and wears, and what is drawn on the hero for it: the body and face, hair, the sword and pistol where
/// the clip puts them, and the equipped pairs.
/// </summary>
internal sealed class PlayerWardrobe2D
{
    public const int Columns = 6, Rows = 6;
    /// <summary>A skeleton of one joint with one mount, for drawing a thing on its own.</summary>
    public static Skeleton2D IconSkeleton { get; } = new(new Joint2D("item", Skeleton2D.NoParent, default)) { Mounts = [Mount2D.On("item", 0)] };

    private readonly WardrobeItem2D?[] _inventory = new WardrobeItem2D?[Columns * Rows];
    private readonly WardrobeItem2D?[] _equipped = new WardrobeItem2D?[Enum.GetValues<WardrobeSlot2D>().Length];
    // The sword rides on the back until a clip draws it; seen from behind it is worn across the back.
    private readonly ImmutableArray<Attachment> _swordOnBack = PersonWardrobe.Sword(Back), _swordAcrossBack = PersonWardrobe.Sword(BackView);

    public PlayerWardrobe2D(PersonMoves moves)
    {
        Moves = moves;
        var items = new List<WardrobeItem2D>
        {
            // Shoes and gloves are one shape on both sides; greaves are flipped for the right shin.
            new("boot", "Boot", WardrobeSlot2D.Shoes, [.. PersonWardrobe.Boot(LeftFootArt), .. PersonWardrobe.Boot(RightFootArt)], PersonWardrobe.Boot(0)),
            new("sandal", "Sandal", WardrobeSlot2D.Shoes, [.. PersonWardrobe.Sandal(LeftFootArt), .. PersonWardrobe.Sandal(RightFootArt)], PersonWardrobe.Sandal(0)),
            new("glove-leather", "Leather glove", WardrobeSlot2D.Gloves, [.. PersonWardrobe.GloveLeather(LeftHandArt), .. PersonWardrobe.GloveLeather(RightHandArt)], PersonWardrobe.GloveLeather(0)),
            new("glove-steel", "Steel glove", WardrobeSlot2D.Gloves, [.. PersonWardrobe.GloveSteel(LeftHandArt), .. PersonWardrobe.GloveSteel(RightHandArt)], PersonWardrobe.GloveSteel(0)),
            new("copper-greaves", "Copper greaves", WardrobeSlot2D.Shins,
                [.. PersonWardrobe.CopperGreaves(LeftShinArt), .. PersonWardrobe.CopperGreaves(RightShinArt).Select(piece => piece.Mirrored())], PersonWardrobe.CopperGreaves(0)),
        };
        for (var i = 0; i < items.Count; i++) _inventory[i] = items[i];
        Items = items;
    }

    public PersonMoves Moves { get; }
    public IReadOnlyList<WardrobeItem2D> Items { get; }
    public IReadOnlyList<WardrobeItem2D?> Inventory => _inventory;
    public WardrobeItem2D? Equipped(WardrobeSlot2D slot) => Enum.IsDefined(slot) ? _equipped[(int)slot] : null;
    public event Action? Changed;

    public bool Equip(int cell, WardrobeSlot2D slot)
    {
        if (!Enum.IsDefined(slot) || !ValidCell(cell) || _inventory[cell] is not { } item || item.Slot != slot) return false;
        (_inventory[cell], _equipped[(int)slot]) = (_equipped[(int)slot], item);
        Changed?.Invoke();
        return true;
    }

    public bool Unequip(WardrobeSlot2D slot, int cell = -1)
    {
        if (!Enum.IsDefined(slot)) return false;
        if (cell == -1) cell = Array.FindIndex(_inventory, item => item is null);
        if (!ValidCell(cell) || _equipped[(int)slot] is not { } worn || (_inventory[cell] is { } replacement && replacement.Slot != slot))
            return false;
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

    /// <summary>
    /// Everything drawn on the hero for a pose. <paramref name="clip"/> at <paramref name="seconds"/> is the clip that decides
    /// where the sword is: in the hand between its draw and sheathe markers, otherwise on the back. The pistol is in the hand
    /// whenever carried. Hair is always worn; the equipped pairs are drawn last.
    /// </summary>
    public ImmutableArray<Attachment> Dress(CharacterPose pose, SkeletonClip2D clip, float seconds, PersonGear gear)
    {
        var inHand = gear == PersonGear.Sword && clip.SwordInHand(seconds);
        var drawn = ImmutableArray.CreateBuilder<Attachment>();
        drawn.AddRange(Moves.Hero.Drawn(pose, worn => worn.Mount switch
        {
            SwordHand => inHand,
            GunHand => gear == PersonGear.Gun,
            _ => true,
        }));
        if (gear != PersonGear.None && !inHand) drawn.AddRange(clip.SeenFromBehind(seconds) ? _swordAcrossBack : _swordOnBack);
        foreach (var item in _equipped)
        {
            if (item is not null) drawn.AddRange(item.Worn);
        }
        return drawn.ToImmutable();
    }
}
