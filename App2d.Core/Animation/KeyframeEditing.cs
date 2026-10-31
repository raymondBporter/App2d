namespace App2d.Core.Animation;

/// <summary>An authored key whose time can be edited. Sampling needs only <see cref="ITimedKey"/>.</summary>
public interface IEditableTimedKey : ITimedKey
{
    new float Time { get; set; }
}

/// <summary>Edits ordered key lists without knowing their values, curves, or target bindings.</summary>
public static class KeyframeEditing
{
    /// <summary>Keys closer than this are treated as the same key during authoring.</summary>
    public const float TimeTolerance = 1e-4f;

    public static bool SameTime(float first, float second) => MathF.Abs(first - second) < TimeTolerance;

    public static T? AtTime<T>(this IEnumerable<T> keys, float time) where T : class, ITimedKey =>
        keys.FirstOrDefault(key => SameTime(key.Time, time));

    /// <summary>Replaces keys within authoring tolerance, inserting the new key in increasing time order.</summary>
    public static void Put<T>(this List<T> keys, T key) where T : ITimedKey
    {
        keys.DeleteAtTime(key.Time);
        var index = keys.FindIndex(existing => existing.Time > key.Time);
        keys.Insert(index < 0 ? keys.Count : index, key);
    }

    /// <summary>The caller copies the key at its new time, including any mutable payload that needs its own copy.</summary>
    public static void Move<T>(this List<T> keys, float from, float to, bool copy, Func<T, float, T> atTime)
        where T : class, ITimedKey
    {
        if (SameTime(from, to) || keys.AtTime(from) is not { } key) return;
        var moved = atTime(key, to);
        if (!copy) keys.Remove(key);
        keys.Put(moved);
    }

    public static void DeleteAtTime<T>(this List<T> keys, float time) where T : ITimedKey =>
        keys.RemoveAll(key => SameTime(key.Time, time));

    /// <summary>Copies a held timeline's active key and changes over [from, to], shifting the range to zero.</summary>
    public static List<T> SliceHeld<T>(this IReadOnlyList<T> keys, float from, float to, Func<T, float, T> atTime,
        bool holdBeforeFirst = false) where T : class, ITimedKey
    {
        var held = KeyframeSampling.Held(keys, from, holdBeforeFirst: false);
        var cut = keys.Where(key => key.Time > from && key.Time <= to).Select(key => atTime(key, key.Time - from)).ToList();
        if (held is not null) cut.Insert(0, atTime(held, 0));
        // A future first key selects its value at elapsed time zero; keep it at the end when the cut precedes it.
        else if (holdBeforeFirst && cut.Count == 0 && keys.Count > 0) cut.Add(atTime(keys[0], to - from));
        return cut;
    }
}
