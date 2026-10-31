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

/// <summary>A model or variant evaluated for display. <see cref="Clip"/> is null when it shows rest. <see cref="Entity"/>, when set, draws its equipment too.</summary>
public sealed record Subject(string Id, ResolvedModel Model, EvaluatedPose Pose, MotionClip? Clip, ResolvedEntity? Entity = null);
