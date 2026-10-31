using App2d.Core.Animation;
using App2d.Core.Characters.Geometry;
using App2d.Core.Constraints;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Timing;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The final pose: one evaluated frame per control in the character frame, with the residuals that produced it. Drawing,
/// sockets and collision all read this. <see cref="Points"/>, <see cref="Angles"/> and <see cref="Bones"/> are views of the
/// same placement, written together by <see cref="Place"/> so no consumer sees a position that disagrees with its frame.
/// </summary>
public sealed class EvaluatedPose
{
    private readonly Dictionary<string, EvaluatedControl2D> _controls = new(StringComparer.Ordinal);

    public EvaluatedPose()
    {
        Points = new ControlView<Vector3>(_controls, static control => new(control.Position, control.Layer));
        Angles = new ControlView<float>(_controls, static control => control.Angle);
        Bones = new ControlView<Matrix3x2>(_controls, static control => control.Frame.Matrix);
    }

    /// <summary>The canonical control states. Position is XY; drawing layer is a separate scalar.</summary>
    public IReadOnlyDictionary<string, EvaluatedControl2D> Controls => _controls;

    /// <summary>Compatibility view of XY plus drawing layer in Z, derived from Controls.</summary>
    public IReadOnlyDictionary<string, Vector3> Points { get; }
    /// <summary>Accumulated XY rotation of each control's frame, unwrapped so full turns stay meaningful; inherited by its children.</summary>
    public IReadOnlyDictionary<string, float> Angles { get; }
    /// <summary>Each control's frame in the character frame (row-vector convention: <c>nodeInCharacter = nodeLocal * parentInCharacter</c>).</summary>
    public IReadOnlyDictionary<string, Matrix3x2> Bones { get; }
    public List<EvaluatedSlot2D> Slots { get; } = [];
    public Dictionary<string, Vector3> SocketAngles { get; } = new(StringComparer.Ordinal);
    /// <summary>The expression each animated part instance shows (P7a/P7c): a gameplay request, then the clip's selection channel, then the instance's and the asset's default.</summary>
    public Dictionary<string, string> Expressions { get; } = new(StringComparer.Ordinal);
    /// <summary>The locomotion frame's origin at this sample.</summary>
    public Vector3 Locomotion { get; set; }
    public List<ChainResult> Chains { get; } = [];
    public List<ContactResult> Contacts { get; } = [];
    /// <summary>Each limited bone node's joint-limit state at this sample: the requested local rotation, the held one and whether a bound was active.</summary>
    public Dictionary<string, ConstraintLimitEvaluation1D> Limits { get; } = new(StringComparer.Ordinal);
    /// <summary>Each drawing part's animated artwork fit at this sample (fit tracks), as multipliers of its leaf scale; absent is 1.</summary>
    public Dictionary<string, Vector2> Fits { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Vector4> Tints { get; } = new(StringComparer.Ordinal);
    /// <summary>Time sampled by this pose, shared by texture frames. No rendering clock advances it.</summary>
    public double SampleSeconds { get; init; }
    /// <summary>Drawing parts a show track hides at this sample.</summary>
    public HashSet<string> HiddenParts { get; } = new(StringComparer.Ordinal);
    /// <summary>Every animated part instance evaluated inside this pose, by instance ID: the nested runtime graph (P7a).</summary>
    public Dictionary<string, EvaluatedAttachment> Attachments { get; } = new(StringComparer.Ordinal);
    /// <summary>Compatibility lookup packing XY and layer; this is not a physical 3D position.</summary>
    public Vector3 World(string id)
    {
        var control = _controls[id];
        return new(control.Position, control.Layer);
    }

    /// <summary>A control's evaluated placement: its bone frame at its drawing layer. Artwork, sockets and handles attach to this.</summary>
    public LayeredFrame2D Placement(string id) => _controls[id].Placement;

    /// <summary>The part with its animated artwork fit applied to its leaf scale; the part itself when nothing fits it.</summary>
    public PartGeometry Fitted(PartGeometry part) => Fits.TryGetValue(part.Id, out var fit) ? part with { ScaleX = part.ScaleX * fit.X, ScaleY = part.ScaleY * fit.Y } : part;

    public PartGeometry Drawn(PartGeometry part)
    {
        var fitted = Fitted(part);
        if (part.Material is not { } material) return fitted;
        var sampled = material.Sample(SampleSeconds, Tints.GetValueOrDefault(part.Id, Vector4.One));
        return ReferenceEquals(sampled, material) ? fitted : fitted with { Material = sampled };
    }

    /// <summary>Whether a part draws at this sample: not hidden on the part and not hidden by a show track.</summary>
    public bool Visible(PartGeometry part) => !part.Hidden && !HiddenParts.Contains(part.Id);

    /// <summary>Writes a control's frame, layer and accumulated angle together; the origin is read from the frame.</summary>
    internal void Place(string id, Matrix3x2 frame, float layer, float angle)
    {
        Place(id, new(new Frame2D(frame), layer), angle);
    }

    internal void Place(string id, LayeredFrame2D placement, float angle) => _controls[id] = new(placement, angle);

    // Live compatibility views share the same dictionary; no position or angle copies need synchronizing.
    private sealed class ControlView<T>(IReadOnlyDictionary<string, EvaluatedControl2D> controls, Func<EvaluatedControl2D, T> select)
        : IReadOnlyDictionary<string, T>
    {
        public T this[string key] => select(controls[key]);
        public int Count => controls.Count;
        public IEnumerable<string> Keys => controls.Keys;
        public IEnumerable<T> Values => controls.Values.Select(select);
        public bool ContainsKey(string key) => controls.ContainsKey(key);
        public bool TryGetValue(string key, out T value)
        {
            var found = controls.TryGetValue(key, out var control);
            value = found ? select(control) : default!;
            return found;
        }
        public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
        {
            foreach (var (key, control) in controls) yield return new(key, select(control));
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
