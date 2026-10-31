namespace App2d.Core.Characters.Authored;

/// <summary>Conventional equipment location names. Parts and colliders may also use any other named location.</summary>
public static class EquipmentPoints
{
    public const string Grip = "grip";
    public const string Tip = "tip";
    public const string SecondGrip = "second-grip";
    public const string Muzzle = "muzzle";
    public static readonly IReadOnlyList<string> All = [Grip, Tip, SecondGrip, Muzzle];
}
