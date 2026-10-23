using App2d.Core.Characters;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>The same control attachment fields for cutout vertices and curve handles.</summary>
internal static class ModelPointBindingInspector
{
    public static void Draw(PuppetPart part, int index, IReadOnlyList<string> controls, Action<Action<PuppetPart>> change)
    {
        var binding = part.Bindings?.FirstOrDefault(b => b.Point == index);
        void Attach(string? control, string? toward)
        {
            var bindings = (part.Bindings ?? []).Where(b => b.Point != index).Select(b => b with { }).ToList();
            if (control is not null)
            {
                bindings.Add(new()
                {
                    Point = index,
                    Control = control,
                    Toward = toward
                });
            }

            change(p => p.Bindings = bindings.Count > 0 ? bindings : null);
        }
        if (Ui.Combo("Follow", binding?.Control ?? "(part frame)", controls.Prepend("(part frame)")) is { } follow)
        {
            Attach(follow == "(part frame)" ? null : follow, null);
        }

        if (binding is not null && Ui.Combo("Point toward", binding.Toward ?? "(translation only)", controls.Where(c => c != binding.Control).Prepend("(translation only)")) is { } toward)
        {
            Attach(binding.Control, toward == "(translation only)" ? null : toward);
        }
    }
}
