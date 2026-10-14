using ImGuiNET;
using System.ComponentModel;
using System.Numerics;
using System.Reflection;

namespace App2d.EditorUi;

/// <summary>Shared typed controls for properties declared with Category, DisplayName, Browsable and ReadOnly metadata.</summary>
public sealed class PropertyInspector
{
    private object? _model;
    private readonly Dictionary<string, string> _text = [];
    private readonly Dictionary<string, string> _errors = [];
    public string? Error => _errors.Values.FirstOrDefault();

    public bool Draw(object? model)
    {
        Bind(model);
        if (model is null) return false;
        var changed = false;
        foreach (var group in Properties(model).GroupBy(p => p.Category).OrderBy(g => g.Key))
        {
            Ui.Section(group.Key, () =>
            {
                foreach (var property in group.OrderBy(p => p.DisplayName))
                {
                    ImGui.PushID(property.Name);
                    try { changed |= Field(model, property); }
                    finally { ImGui.PopID(); }
                    if (property.Description.Length > 0 && ImGui.IsItemHovered()) ImGui.SetTooltip(property.Description);
                }
            });
        }

        if (Error is { } error) Ui.Problem(error);
        return changed;
    }

    /// <summary>Commit text drafts before Apply or export. Invalid input remains editable and never replaces the model.</summary>
    public bool Commit(object? model)
    {
        Bind(model);
        if (model is null) return true;
        _errors.Clear();
        foreach (var property in Properties(model).Where(p => !p.IsReadOnly && p.PropertyType == typeof(string)))
            if (_text.TryGetValue(property.Name, out var text) && !Equals(property.GetValue(model) ?? "", text)) Set(model, property, text);
        return Error is null;
    }

    public void Reset() { _model = null; _text.Clear(); _errors.Clear(); }
    private void Bind(object? model)
    {
        if (ReferenceEquals(_model, model)) return;
        Reset(); _model = model;
    }

    private static IEnumerable<PropertyDescriptor> Properties(object model) =>
        TypeDescriptor.GetProperties(model).Cast<PropertyDescriptor>().Where(p => p.IsBrowsable);

    private bool Field(object model, PropertyDescriptor property)
    {
        var value = property.GetValue(model); var label = property.DisplayName; var type = property.PropertyType;
        if (property.IsReadOnly) { ImGui.TextUnformatted($"{label}: {value}"); return false; }
        if (type == typeof(string))
        {
            if (!_text.TryGetValue(property.Name, out var text)) text = value as string ?? "";
            Ui.Text(label, ref text, 8192); _text[property.Name] = text;
            if (!ImGui.IsItemDeactivatedAfterEdit()) return false;
            if (!Equals(value ?? "", text)) return Set(model, property, text);
            _errors.Remove(property.Name); return false;
        }
        if (type == typeof(float))
        {
            var number = (float)value!;
            return Ui.Drag(label, ref number, .1f, 0, 0) && Set(model, property, number);
        }
        if (type == typeof(int))
        {
            var number = (int)value!;
            ImGui.TextUnformatted(label); ImGui.SetNextItemWidth(-1);
            return ImGui.DragInt("##value", ref number) && Set(model, property, number);
        }
        if (type == typeof(bool))
        {
            var enabled = (bool)value!;
            return ImGui.Checkbox(label, ref enabled) && Set(model, property, enabled);
        }
        if (type.IsEnum)
        {
            return Ui.Combo(label, value!.ToString()!, Enum.GetNames(type)) is { } selected && Set(model, property, Enum.Parse(type, selected));
        }
        if (type == typeof(Color))
        {
            var color = (Color)value!;
            var rgba = new Vector4(color.R, color.G, color.B, color.A) / 255;
            ImGui.TextUnformatted(label); ImGui.SetNextItemWidth(-1);
            return ImGui.ColorEdit4("##value", ref rgba) && Set(model, property,
                Color.FromArgb(Byte(rgba.W), Byte(rgba.X), Byte(rgba.Y), Byte(rgba.Z)));
        }
        ImGui.TextUnformatted($"{label}: {value}");
        return false;
    }

    private static int Byte(float value) => (int)MathF.Round(Math.Clamp(value, 0, 1) * 255);
    private bool Set(object model, PropertyDescriptor property, object value)
    {
        try { property.SetValue(model, value); _errors.Remove(property.Name); return true; }
        catch (Exception exception) when (Root(exception) is ArgumentException or InvalidOperationException or FormatException)
        {
            _errors[property.Name] = $"{property.DisplayName}: {Root(exception).Message}"; return false;
        }
    }
    private static Exception Root(Exception exception) => exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
}
