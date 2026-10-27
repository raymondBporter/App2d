namespace App2d.Core.Characters.Authored;

/// <summary>Conventional equipment location names. Parts and colliders may also use any other named location.</summary>
public static class EquipmentPoints
{
    public const string Grip = "grip", Tip = "tip", SecondGrip = "second-grip", Muzzle = "muzzle";
    public static readonly IReadOnlyList<string> All = [Grip, Tip, SecondGrip, Muzzle];
}
