using App2d.Core.Characters.Authored;

namespace App2d.CharacterSeeds;

/// <summary>
/// How `--convert-models` names the solved bones it inserts beneath a pivot (character-system migration, P11a): the Person's
/// upper arms take their humanoid names; every other pivot's bone is <c>&lt;root&gt;-bone</c>.
/// </summary>
public static class ModelConversionPlans
{
    public static ModelConversion.BoneNaming? Naming(string modelId) => modelId == PersonTemplate.Id ? PersonTemplate.UpperArmBone : null;
}
