using App2d.Core.Characters.Authored;

namespace App2d.CharacterStudio.Editing.Preview;

/// <summary>A model or variant evaluated for display. <see cref="Clip"/> is null when it shows rest. <see cref="Entity"/>, when set, draws its equipment too.</summary>
public sealed record Subject(string Id, ResolvedModel Model, EvaluatedPose Pose, MotionClip? Clip, ResolvedEntity? Entity = null);
