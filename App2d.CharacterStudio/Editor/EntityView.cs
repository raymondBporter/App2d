using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Operations.Conversion;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>
/// What a character does in the game: its model, motion set and role overrides, controller, movement geometry, attached
/// colliders (hurt, attack, guard, trigger), equipment and actions. Selecting an
/// action previews it and brings its activation windows into focus; the viewport shows the colliders the game registers,
/// placed from the same final pose. Every change is an entity edit: models, clips and sets are opened, never edited from here.
/// </summary>
internal sealed class EntityView(EditorSession session) : IWorkspaceView
{
    private string _newProp = "";
    private string _newSocket = "";
    private string _newEvent = "hit";
    private string _newCollider = "";
    private string _newColliderUse = ColliderUses.Hurt;
    private string _newPropertyKey = "";
    private string _newPropertyValue = "";

    public Workspace Mode => Workspace.Entity;
    public float TimelineHeight => 150;
    /// <summary>The inspector section header to keep at the top of the inspector while set ("Behaviors"); the smokes frame a section with it.</summary>
    public string? ScrollTo { get; set; }

    private AssetDocument<Part>? Document => session.EntityDocument;
    private string? BaseId => Document is { } document ? session.Assets.BaseOf(document.Asset.Model) : null;
    private CharacterModel? Base => session.Assets.Model(BaseId)?.Asset;

    // ---- Outline -------------------------------------------------------------------------------------------------

    public void Outline()
    {
        if (Document is not { } document)
        {
            Ui.Help("Open an entity from the browser, or create one from a model with New.");
            return;
        }
        var entity = session.Entity;
        Ui.Header("Preview");
        var roles = entity?.Roles.Keys.Order(StringComparer.Ordinal).ToArray() ?? [];
        foreach (var role in roles)
        {
            if (ImGui.Selectable($"{role}  ({entity!.Roles[role].Clip.Name})", session.PreviewAction is null && session.PreviewRole == role))
            {
                session.PreviewActionOf(null);
                session.PreviewRoleOf(role);
            }
        }

        if (roles.Length == 0)
        {
            Ui.Help("Nothing to preview until the entity compiles.");
        }

        Ui.Header("Actions");
        var spec = TryController(document.Asset.Behavior!.Controller.Kind);
        foreach (var action in spec?.Actions ?? [])
        {
            ImGui.PushID(action);
            var enabled = document.Asset.Behavior!.Actions.Any(a => a.Id == action);
            if (ImGui.Checkbox("##enabled", ref enabled))
            {
                session.Edit(document, () =>
                {
                    if (!enabled)
                    {
                        document.Asset.Behavior!.Actions.RemoveAll(a => a.Id == action);
                        return;
                    }
                    var def = new EntityActionDef { Id = action, Role = action };
                    if (action == EntityControllers.Jump)
                    {
                        def.Events.Add(new()
                        {
                            Id = EntityControllers.Launch,
                            At = new()
                            {
                                At = .25f
                            }
                        });
                    }

                    document.Asset.Behavior!.Actions.Add(def);
                }, enabled ? $"Enabled '{action}'." : $"Disabled '{action}'.");
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Only listed actions are enabled; a shared library containing a clip never enables it.");
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(!enabled);
            if (ImGui.Selectable(action + (document.Asset.Behavior!.Actions.FirstOrDefault(a => a.Id == action)?.Mask is { } mask ? $"  (over locomotion, {mask})" : ""), session.Selection.Action == action))
            {
                session.PreviewActionOf(action);
            }

            ImGui.EndDisabled();
            ImGui.PopID();
        }
        if (spec is null)
        {
            Ui.Problem($"Unknown controller '{document.Asset.Behavior!.Controller.Kind}'.");
        }

        Ui.Help($"The '{document.Asset.Behavior!.Controller.Kind}' controller supports these. Hit and death reactions play their roles when assigned.");
    }

    private static ControllerSpec? TryController(string kind) => EntityControllers.All.FirstOrDefault(s => s.Id == kind);

    // ---- Inspector -----------------------------------------------------------------------------------------------

    public void Inspector()
    {
        if (Document is not { } document)
        {
            Ui.Help("No entity open.");
            return;
        }
        var asset = document.Asset;
        foreach (var problem in session.Assets.Problems(document))
        {
            Ui.Problem(problem);
        }

        if (session.Selection.Action is { } action && asset.Behavior!.Actions.Any(a => a.Id == action))
        {
            ActionInspector(document, action);
            ImGui.Separator();
        }

        Ui.Header("Entity");
        var name = asset.Name;
        if (Ui.Text("Name", ref name, 100) && name.Trim().Length > 0)
        {
            session.Change(document, () => document.Asset.Name = name);
        }

        var subjects = session.Assets.Models.Select(m => m.Id).Concat(session.Assets.Variants.Select(v => v.Id)).Order(StringComparer.Ordinal);
        if (Ui.Combo("Model or variant", asset.Model ?? "", subjects, id => session.Assets.Find(id)?.Name is { } n ? $"{n} ({id})" : id + " (missing)") is { } model)
        {
            session.Edit(document, () => document.Asset.Model = model, $"'{asset.Id}' now uses '{model}'. Motion sets and sockets come from its base.");
        }

        if (Ui.Button("Open model", session.Assets.Find(asset.Model) is not null))
        {
            session.Open(asset.Model!);
        }

        if (Ui.Combo("Controller", asset.Behavior!.Controller.Kind, EntityControllers.All.Select(s => s.Id)) is { } kind)
        {
            session.Edit(document, () => document.Asset.Behavior!.Controller.Kind = kind);
        }

        var health = asset.Behavior!.Damage.Health;
        ImGui.TextUnformatted("Health");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.DragInt("##health", ref health, .2f, 1, 10000))
        {
            session.Change(document, () => document.Asset.Behavior!.Damage.Health = Math.Clamp(health, 1, 10000));
        }

        Motion(document);
        Controller(document);
        Behaviors(document);
        Movement(document);
        Colliders(document);
        Guard(document);
        Equipment(document);
        References.Draw(session, asset.Id);
    }

    // ---- Behaviors (P9c) -----------------------------------------------------------------------------------------

    private static BehaviorField Field(IReadOnlyList<BehaviorField> fields, string name) => fields.First(f => f.Name == name);

    /// <summary>An integer behavior field with its schema range and help.</summary>
    private void IntegerField(AssetDocument<Part> document, BehaviorField field, int value, Action<int> set)
    {
        ImGui.TextUnformatted(field.Label);
        ImGui.SetNextItemWidth(-1);
        if (ImGui.DragInt("##" + field.Name, ref value, .2f, (int)field.Min, (int)field.Max))
        {
            session.Change(document, () => set(Math.Clamp(value, (int)field.Min, (int)field.Max)));
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(field.Help);
        }
    }

    /// <summary>A factor or seconds behavior field with its schema range and help.</summary>
    private void NumberField(AssetDocument<Part> document, BehaviorField field, float value, Action<float> set)
    {
        if (Ui.Drag(field.Name.EndsWith(field.Kind, StringComparison.OrdinalIgnoreCase) ? field.Label : $"{field.Label} ({field.Kind})", ref value, field.Kind == BehaviorSchema.Seconds ? .005f : .01f, field.Min, field.Max))
        {
            session.Change(document, () => set(Math.Clamp(value, field.Min, field.Max)));
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(field.Help);
        }
    }

    /// <summary>A sound asset reference (empty for none) with its help.</summary>
    private void SoundField(AssetDocument<Part> document, BehaviorField field, string? value, Action<string?> set)
    {
        var text = value ?? "";
        if (Ui.Text(field.Label + " (empty for none)", ref text, 64))
        {
            session.Change(document, () => set(text.Length == 0 ? null : text));
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(field.Help);
        }
    }

    /// <summary>The typed gameplay behaviors of a Part, each field from <see cref="BehaviorSchema"/>.</summary>
    private void Behaviors(AssetDocument<Part> document)
    {
        var asset = document.Asset;
        Ui.Header("Behaviors");
        if (ScrollTo == "Behaviors")
        {
            ImGui.SetScrollHereY(0f);
        }
        var behaviors = asset.Behavior!;
        ImGui.TextColored(UiTheme.Accent, "Receiving damage");
        var damage = behaviors.Damage;
        IntegerField(document, Field(BehaviorSchema.Damage, nameof(ReceiveDamageDef.Health)), damage.Health, v => document.Asset.Behavior!.Damage.Health = v);
        NumberField(document, Field(BehaviorSchema.Damage, nameof(ReceiveDamageDef.KnockbackResistance)), damage.KnockbackResistance, v => document.Asset.Behavior!.Damage.KnockbackResistance = v);
        NumberField(document, Field(BehaviorSchema.Damage, nameof(ReceiveDamageDef.HurtSeconds)), damage.HurtSeconds, v => document.Asset.Behavior!.Damage.HurtSeconds = v);
        NumberField(document, Field(BehaviorSchema.Damage, nameof(ReceiveDamageDef.StaggerSeconds)), damage.StaggerSeconds, v => document.Asset.Behavior!.Damage.StaggerSeconds = v);
        ImGui.TextColored(UiTheme.Accent, "Strikes (one per hit group)");
        var groups = asset.Behavior!.Actions.SelectMany(a => a.Hits.Select(h => h.Id)).Concat(asset.Behavior!.Actions.Where(a => a.Projectile is not null).Select(a => a.Id)).Distinct(StringComparer.Ordinal).ToList();
        foreach (var (group, strike) in behaviors.Strikes.ToArray())
        {
            ImGui.PushID("strike-" + group);
            ImGui.TextUnformatted(group + (groups.Contains(group) ? "" : "  (no window or projectile of this name)"));
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                session.Edit(document, () => document.Asset.Behavior!.Strikes.Remove(group), $"Strike '{group}' removed; the group strikes with the defaults.");
                ImGui.PopID();
                continue;
            }
            IntegerField(document, Field(BehaviorSchema.Strike, nameof(StrikeDef.Damage)), strike.Damage, v => document.Asset.Behavior!.Strikes[group].Damage = v);
            SoundField(document, Field(BehaviorSchema.Strike, nameof(StrikeDef.Sound)), strike.Sound, v => document.Asset.Behavior!.Strikes[group].Sound = v);
            if (Ui.Combo(nameof(StrikeDef.Impact), strike.Impact, ImpactKinds.All) is { } impact)
            {
                session.Edit(document, () => document.Asset.Behavior!.Strikes[group].Impact = impact);
            }

            ImGui.PopID();
        }
        var unlisted = groups.Where(g => !behaviors.Strikes.ContainsKey(g)).ToList();
        if (unlisted.Count > 0)
        {
            ImGui.TextDisabled("Striking with the defaults: " + string.Join(", ", unlisted));
            if (Ui.Combo("Add strike for", "(choose a hit group)", unlisted.Prepend("(choose a hit group)")) is { } chosen && chosen != "(choose a hit group)")
            {
                session.Edit(document, () => document.Asset.Behavior!.Strikes[chosen] = new());
            }
        }
        if (asset.Behavior!.Guard is not null)
        {
            ImGui.TextColored(UiTheme.Accent, "Guarding");
            var guard = behaviors.Guard ?? new GuardBehaviorDef();
            var events = asset.Behavior!.Actions.SelectMany(a => a.Events.Select(e => e.Id)).Distinct(StringComparer.Ordinal).ToList();
            var openEvent = guard.OpenEvent;
            if (Ui.Combo(nameof(GuardBehaviorDef.OpenEvent), openEvent, events.Contains(openEvent) ? events : events.Prepend(openEvent)) is { } open)
            {
                session.Edit(document, () => (document.Asset.Behavior!.Guard ??= new()).OpenEvent = open);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Field(BehaviorSchema.Guard, nameof(GuardBehaviorDef.OpenEvent)).Help);
            }

            SoundField(document, Field(BehaviorSchema.Guard, nameof(GuardBehaviorDef.BlockSound)), guard.BlockSound, v => (document.Asset.Behavior!.Guard ??= new()).BlockSound = v);
        }
        ImGui.TextColored(UiTheme.Accent, "Custom properties");
        foreach (var (key, value) in behaviors.Properties.ToArray())
        {
            ImGui.PushID("property-" + key);
            var text = value;
            if (Ui.Text(key, ref text, 200))
            {
                session.Change(document, () => document.Asset.Behavior!.Properties[key] = text);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                session.Edit(document, () => document.Asset.Behavior!.Properties.Remove(key));
            }

            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(100 * Ui.Scale);
        ImGui.InputText("##property-key", ref _newPropertyKey, 64);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120 * Ui.Scale);
        ImGui.InputText("##property-value", ref _newPropertyValue, 200);
        ImGui.SameLine();
        if (ImGui.SmallButton("Add property") && session.Edit(document, () => document.Asset.Behavior!.Properties[_newPropertyKey] = _newPropertyValue))
        {
            _newPropertyKey = _newPropertyValue = "";
        }

        Ui.Help("Typed behaviors (P9a): strikes are damage on contact per hit group, receiving damage is the health pool and reaction timing, the guard its open event and block sound; custom properties are text gameplay reads by name.");
    }

    private void Motion(AssetDocument<Part> document)
    {
        var asset = document.Asset;
        var basis = Base;
        Ui.Header("Motion");
        if (basis is null)
        {
            Ui.Problem("The model does not resolve to a base; choose a model first.");
            return;
        }
        if (Ui.Combo("Motion set", asset.Behavior!.MotionSet, basis.MotionSets.Select(s => s.Id), id => basis.MotionSets.FirstOrDefault(s => s.Id == id)?.Name ?? id + " (missing)") is { } set)
        {
            session.Edit(document, () => document.Asset.Behavior!.MotionSet = set);
        }

        if (ImGui.SmallButton("Edit sets on " + basis.Name))
        {
            session.Open(basis.Id);
        }

        var selected = basis.MotionSets.FirstOrDefault(s => s.Id == asset.Behavior!.MotionSet);
        var spec = TryController(asset.Behavior!.Controller.Kind);
        var clips = session.Assets.ClipsFor(asset.Model).OrderBy(c => c.Name).ToArray();
        var roles = EntityAuthoring.CommonRoles.Concat(selected?.Roles.Keys ?? Enumerable.Empty<string>()).Concat(asset.Behavior!.Roles.Keys).Distinct().ToArray();
        Ui.Help("Effective assignment: the entity's override, then the selected set.");
        foreach (var role in roles)
        {
            ImGui.PushID(role);
            var overridden = asset.Behavior!.Roles.TryGetValue(role, out var own);
            var fromSet = selected?.Roles.GetValueOrDefault(role);
            var effective = overridden ? own : fromSet;
            var required = spec?.RequiredRoles.Contains(role) == true;
            var label = $"{role}{(required ? " (required)" : "")}";
            if (effective is null && required)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Warning);
                ImGui.TextUnformatted(label + ": unassigned");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.TextUnformatted(label);
            }

            if (overridden)
            {
                ImGui.SameLine();
                ImGui.TextColored(UiTheme.Override, "entity");
                ImGui.SameLine();
                if (ImGui.SmallButton("Use set"))
                {
                    session.SetEntityRole(role, null);
                }
            }
            else if (fromSet is not null)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("from " + selected!.Name);
            }
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##clip", effective is null ? "(unassigned)" : session.Assets.Clip(effective)?.Name ?? effective + " (missing)"))
            {
                foreach (var clip in clips)
                {
                    if (ImGui.Selectable(clip.Name + "##" + clip.Id, clip.Id == effective))
                    {
                        session.SetEntityRole(role, clip.Id);
                    }
                }

                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Choosing a clip here overrides the set for this entity only.");
            }

            ImGui.PopID();
        }
    }

    private void Controller(AssetDocument<Part> document)
    {
        var config = document.Asset.Behavior!.Controller;
        var spec = TryController(config.Kind);
        Ui.Header("Controller");
        void Value(string label, float value, float max, Action<ControllerConfig, float> set)
        {
            if (Ui.Drag(label, ref value, .01f, 0, max))
            {
                session.Change(document, () => set(document.Asset.Behavior!.Controller, Math.Clamp(value, 0, max)));
            }
        }
        if (spec?.Moves != false)
        {
            Value("Walk speed (units/s)", config.WalkSpeed, 100, (c, v) => c.WalkSpeed = v);
            Value("Run speed (0 never runs)", config.RunSpeed, 100, (c, v) => c.RunSpeed = v);
        }
        if (spec?.Jumps == true)
        {
            Value("Jump speed", config.JumpSpeed, 100, (c, v) => c.JumpSpeed = v);
        }

        Value("Attack range", config.Range, 100, (c, v) => c.Range = v);
        Value("Cooldown (s)", config.Cooldown, 60, (c, v) => c.Cooldown = v);
    }

    private void Movement(AssetDocument<Part> document)
    {
        Ui.Header("Movement shape");
        var geometry = ColliderOf(document, document.Asset.Behavior!.Movement);
        ShapeFields(document, (IShape2D)geometry.Geometry!, shape => geometry.Geometry = shape);
        if (ImGui.SmallButton("Fit to rest pose"))
        {
            session.FitMovement();
        }

        Ui.Help("Stable and body-local: feet at the origin, +X toward facing. Animation never changes it. Fit only on request.");
    }

    // ---- Colliders -----------------------------------------------------------------------------------------------

    private static PartGeometry ColliderOf(AssetDocument<Part> document, string id) => document.Asset.Geometry.First(c => c.Id == id);

    private void Colliders(AssetDocument<Part> document)
    {
        var asset = document.Asset;
        var basis = Base;
        Ui.Header("Colliders");
        Ui.Help("Attached collision geometry the game registers once per actor: shared convex shapes on a placement binding, with a stable id and a use. Attack colliders are enabled by their action's windows; hurt, guard and trigger colliders by their default.");
        var resolved = session.Entity;
        foreach (var (id, use) in asset.Behavior!.Colliders)
        {
            var summary = $"{id}  {use}  {BindingSummary(ColliderOf(document, id))}";
            if (ImGui.Selectable(summary, session.Selection.Collider == id))
            {
                session.Selection.Collider = id;
            }
        }
        ImGui.SetNextItemWidth(90 * Ui.Scale);
        ImGui.InputText("##collider", ref _newCollider, 64);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80 * Ui.Scale);
        if (ImGui.BeginCombo("##use", _newColliderUse))
        {
            foreach (var use in ColliderUses.All)
            {
                if (ImGui.Selectable(use, use == _newColliderUse))
                {
                    _newColliderUse = use;
                }
            }

            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("Add collider"))
        {
            var id = _newCollider.Length == 0 ? ModelAuthoring.UniqueId(_newColliderUse, asset.Geometry.Select(c => c.Id)) : _newCollider;
            var prop = asset.Equipment.FirstOrDefault()?.Prop;
            var control = basis?.Controls.FirstOrDefault()?.Id;
            var definition = _newColliderUse is ColliderUses.Attack or ColliderUses.Guard && prop is not null
                ? new PartGeometry { Id = id, Prop = prop, Point = EquipmentPoints.Tip }
                : control is not null ? new PartGeometry { Id = id, A = control, Frame = CharacterModel.Locomotion }
                : new PartGeometry { Id = id, OffsetY = .5f };
            definition.Geometry = Rectangle2D.Builder.FromSize(.3f, .3f);
            if (session.AddCollider(definition, _newColliderUse))
            {
                session.Selection.Collider = id;
                _newCollider = "";
            }
        }
        if (session.Selection.Collider is { } selected && asset.Behavior!.Colliders.ContainsKey(selected))
        {
            ColliderFields(document, selected);
        }
    }

    private static string BindingSummary(PartGeometry collider) => collider.Binding switch
    {
        ColliderBindings.Prop => $"on prop {collider.Prop} {collider.Point}{(collider.Along != 0 ? $" along {collider.Along:0.###}" : "")}",
        ColliderBindings.Socket => $"on socket {collider.Socket}{(collider.Along != 0 ? $" along {collider.Along:0.###}" : "")}",
        ColliderBindings.Control => $"on {collider.A}{(collider.Frame is { } frame ? $" in the {frame} frame" : "")}{(collider.B is { } toward ? $" toward {toward}{(collider.Stretch ? ", stretched" : "")}" : "")}",
        _ => $"on the actor at ({collider.OffsetX:0.###}, {collider.OffsetY:0.###})",
    };

    private void ColliderFields(AssetDocument<Part> document, string id)
    {
        PartGeometry Collider() => ColliderOf(document, id);
        var collider = Collider();
        var basis = Base;
        ImGui.Separator();
        ImGui.TextColored(UiTheme.Accent, $"Collider {id}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Remove"))
        {
            if (session.RemoveCollider(id))
            {
                session.Selection.Collider = null;
            }

            return;
        }
        if (Ui.Combo("Use", document.Asset.Behavior!.Colliders[id], ColliderUses.All) is { } use)
        {
            session.Edit(document, () => document.Asset.Behavior!.Colliders[id] = use);
        }

        var props = document.Asset.Equipment.Select(e => "prop:" + e.Prop);
        var sockets = basis?.Sockets.Select(s => "socket:" + s.Id) ?? [];
        var controls = basis?.Controls.Select(c => "control:" + c.Id) ?? [];
        var binding = collider.Binding switch
        {
            ColliderBindings.Prop => "prop:" + collider.Prop,
            ColliderBindings.Socket => "socket:" + collider.Socket,
            ColliderBindings.Control => "control:" + collider.A,
            _ => "actor:",
        };
        if (Ui.Combo("Bound to", binding, props.Concat(sockets).Concat(controls).Prepend("actor:"), a => a == "actor:" ? "the actor" : a) is { } chosen)
        {
            session.Edit(document, () =>
            {
                var c = Collider();
                var (kind, value) = (chosen[..chosen.IndexOf(':')], chosen[(chosen.IndexOf(':') + 1)..]);
                c.Prop = kind == "prop" ? value : null;
                c.Socket = kind == "socket" ? value : null;
                c.A = kind == "control" ? value : PartLocation.Origin;
                if (kind != "control")
                {
                    c.Frame = c.B = null;
                    c.Stretch = false;
                    c.Angle = 0;
                }
                else
                {
                    c.Along = 0;
                    c.Frame ??= CharacterModel.Locomotion;
                }
            });
        }

        switch (collider.Binding)
        {
            case ColliderBindings.Prop:
                if (Ui.Combo("Part location", collider.Point, session.Assets.Part(collider.Prop)?.Asset.Locations.Select(l => l.Id).Prepend(PartLocation.Origin) ?? []) is { } point)
                {
                    session.Edit(document, () => Collider().Point = point);
                }

                AlongField(document, Collider);
                break;
            case ColliderBindings.Socket:
                AlongField(document, Collider);
                break;
            case ColliderBindings.Control:
                {
                    const string Own = "(the control's own)";
                    var frames = (basis?.Controls.Select(c => c.Id) ?? []).Prepend(CharacterModel.Locomotion).Prepend(Own);
                    if (Ui.Combo("Frame", collider.Frame ?? Own, frames, f => f == CharacterModel.Locomotion ? "locomotion (screen axes)" : f) is { } frame)
                    {
                        session.Edit(document, () => Collider().Frame = frame == Own ? Collider().A : frame);
                    }

                    const string None = "(none)";
                    if (Ui.Combo("Toward (+Y)", collider.B ?? None, (basis?.Controls.Select(c => c.Id) ?? []).Prepend(None)) is { } toward)
                    {
                        session.Edit(document, () =>
                        {
                            var c = Collider();
                            c.B = toward == None ? null : toward;
                            c.Stretch = c.Stretch && c.B is not null;
                        });
                    }

                    if (collider.B is not null)
                    {
                        var stretch = collider.Stretch;
                        if (ImGui.Checkbox("Stretch with the span", ref stretch))
                        {
                            session.Edit(document, () => Collider().Stretch = stretch);
                        }
                    }
                    var offset = new Vector2(collider.OffsetX, collider.OffsetY);
                    if (Ui.Drag2("Offset in the frame", ref offset))
                    {
                        session.Change(document, () =>
                        {
                            var c = Collider();
                            c.OffsetX = offset.X;
                            c.OffsetY = offset.Y;
                        });
                    }

                    var degrees = float.RadiansToDegrees(collider.Angle);
                    if (Ui.Drag("Angle (deg)", ref degrees, .5f, -360, 360))
                    {
                        session.Change(document, () => Collider().Angle = float.DegreesToRadians(degrees));
                    }

                    break;
                }
            default:
                {
                    var offset = new Vector2(collider.OffsetX, collider.OffsetY);
                    if (Ui.Drag2("Centre ahead / above the feet", ref offset))
                    {
                        session.Change(document, () =>
                        {
                            var c = Collider();
                            c.OffsetX = offset.X;
                            c.OffsetY = offset.Y;
                        });
                    }

                    break;
                }
        }
        var enabled = collider.Collision!.Enabled;
        if (ImGui.Checkbox(document.Asset.Behavior!.Colliders[id] == ColliderUses.Attack ? "Can be enabled by windows" : "Enabled", ref enabled))
        {
            session.Edit(document, () => Collider().Collision!.Enabled = enabled);
        }

        ShapeFields(document, (IShape2D)collider.Geometry!, shape => Collider().Geometry = shape);
        Ui.Help(collider.Binding == ColliderBindings.Control
            ? "The shape sits in the control's planar frame (socket rules); the drawing layer never reaches collision."
            : "The shape sits in the anchor frame: +X along the projected axis, following facing.");
        var windows = document.Asset.Behavior!.Actions.SelectMany(a => a.Hits.Where(h => h.Collider == id).Select(h => $"{a.Id}/{h.Id}")).ToList();
        if (windows.Count > 0)
        {
            ImGui.TextDisabled("Enabled by: " + string.Join(", ", windows));
        }
    }

    private void AlongField(AssetDocument<Part> document, Func<PartGeometry> collider)
    {
        var along = collider().Along;
        if (Ui.Drag("Along the anchor's axis", ref along))
        {
            session.Change(document, () => collider().Along = along);
        }
    }

    private void Guard(AssetDocument<Part> document)
    {
        var asset = document.Asset;
        Ui.Header("Guard");
        var hasGuard = asset.Behavior!.Guard is not null;
        if (ImGui.Checkbox("Blocks front strikes", ref hasGuard))
        {
            session.Edit(document, () =>
            {
                if (!hasGuard)
                {
                    document.Asset.Behavior!.Guard = null;
                    return;
                }

                var guard = document.Asset.Behavior!.Colliders.FirstOrDefault(c => c.Value == ColliderUses.Guard).Key ?? throw new InvalidDataException("A guard needs a guard collider; add one under Colliders first.");
                document.Asset.Behavior!.Guard = new() { Collider = guard };
            });
        }

        if (asset.Behavior!.Guard is not { } def)
        {
            return;
        }

        var guards = asset.Behavior!.Colliders.Where(c => c.Value == ColliderUses.Guard).Select(c => c.Key).ToList();
        if (Ui.Combo("Collider", def.Collider, guards) is { } chosen)
        {
            session.Edit(document, () => document.Asset.Behavior!.Guard!.Collider = chosen);
        }

        ImGui.TextDisabled($"Guard geometry is collider '{def.Collider}'; edit it under Colliders.");
    }

    private void Equipment(AssetDocument<Part> document)
    {
        var equipment = document.Asset.Equipment;
        var basis = Base;
        Ui.Header("Equipment");
        var sockets = basis?.Sockets.Select(s => s.Id).ToArray() ?? [];
        foreach (var binding in equipment.ToArray())
        {
            ImGui.PushID(binding.Prop);
            ImGui.TextUnformatted(session.Assets.Part(binding.Prop)?.Name ?? binding.Prop + " (missing)");
            if (session.Assets.Part(binding.Prop)?.Asset.Usage is "hair" or "clothing")
            {
                if (ImGui.SmallButton("Edit appearance"))
                {
                    session.Open(binding.Prop);
                }

                ImGui.SameLine();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                var used = document.Asset.Geometry.Any(c => c.Prop == binding.Prop);
                if (used)
                {
                    session.Report($"'{binding.Prop}' anchors a collider; re-anchor it first.", true);
                }
                else
                {
                    session.Edit(document, () => document.Asset.Equipment.RemoveAll(e => e.Prop == binding.Prop));
                }
            }
            if (Ui.Combo("Socket", binding.Socket, sockets) is { } socket)
            {
                session.Edit(document, () => document.Asset.Equipment.First(e => e.Prop == binding.Prop).Socket = socket);
            }

            ImGui.PopID();
        }
        var available = session.Assets.Parts.Select(p => p.Id).Where(p => equipment.All(e => e.Prop != p)).Order(StringComparer.Ordinal).ToArray();
        if (available.Length == 0 || sockets.Length == 0)
        {
            Ui.Help(sockets.Length == 0 ? "The base has no sockets to hold props." : "Every prop is equipped.");
            return;
        }
        if (!available.Contains(_newProp))
        {
            _newProp = available[0];
        }

        if (!sockets.Contains(_newSocket))
        {
            _newSocket = sockets[0];
        }

        ImGui.SetNextItemWidth(110 * Ui.Scale);
        if (ImGui.BeginCombo("##prop", _newProp))
        {
            foreach (var p in available)
            {
                if (ImGui.Selectable(p, p == _newProp))
                {
                    _newProp = p;
                }
            }

            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(110 * Ui.Scale);
        if (ImGui.BeginCombo("##socket", _newSocket))
        {
            foreach (var s in sockets)
            {
                if (ImGui.Selectable(s, s == _newSocket))
                {
                    _newSocket = s;
                }
            }

            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("Equip"))
        {
            session.Edit(document, () => document.Asset.Equipment.Add(new() { Prop = _newProp, Socket = _newSocket }), $"Equipped '{_newProp}' on '{_newSocket}'.");
        }
    }

    // ---- Actions -------------------------------------------------------------------------------------------------

    private static EntityActionDef Def(AssetDocument<Part> document, string id) => document.Asset.Behavior!.Actions.First(a => a.Id == id);

    private void ActionInspector(AssetDocument<Part> document, string id)
    {
        var action = Def(document, id);
        var basis = Base;
        Ui.Header("Action " + id);
        var byRole = action.Role is not null;
        if (ImGui.RadioButton("Plays a role", byRole) && !byRole)
        {
            session.Edit(document, () =>
            {
                var a = Def(document, id);
                a.Clip = null;
                a.Role = id;
            });
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Plays a clip", !byRole) && byRole)
        {
            session.Edit(document, () =>
            {
                var a = Def(document, id);
                a.Clip = session.Entity?.Actions.GetValueOrDefault(id)?.Clip.Id ?? session.Assets.ClipsFor(document.Asset.Model).Select(c => c.Id).Order().FirstOrDefault() ?? id;
                a.Role = null;
            });
        }

        if (byRole)
        {
            var roles = EntityAuthoring.CommonRoles.Concat(document.Asset.Behavior!.Roles.Keys).Concat(basis?.MotionSets.SelectMany(s => s.Roles.Keys) ?? []).Distinct().Order(StringComparer.Ordinal);
            if (Ui.Combo("Role", action.Role!, roles) is { } role)
            {
                session.Edit(document, () => Def(document, id).Role = role);
            }
        }
        else if (Ui.Combo("Clip", action.Clip!, session.Assets.ClipsFor(document.Asset.Model).Select(c => c.Id).Order(StringComparer.Ordinal), c => session.Assets.Clip(c)?.Name is { } n ? $"{n} ({c})" : c + " (missing)") is { } clip)
        {
            session.Edit(document, () => Def(document, id).Clip = clip);
        }

        var resolved = session.Entity?.Actions.GetValueOrDefault(id);
        if (resolved is not null && ImGui.Button("Animate this action with weapon"))
        {
            var equipment = document.Asset.Equipment.FirstOrDefault();
            session.Open(resolved.Clip.Id);
            if (equipment is not null)
            {
                session.PreviewProp = equipment.Prop;
                session.PreviewSocket = equipment.Socket;
                session.PreviewWeapon = session.EditWeapon = true;
            }
        }

        const string None = "(none)";
        var others = document.Asset.Behavior!.Actions.Select(a => a.Id).Where(a => a != id).Prepend(None);
        if (Ui.Combo("Next on another press", action.Next ?? None, others) is { } next)
        {
            session.Edit(document, () => Def(document, id).Next = next == None ? null : next);
        }

        var clips = session.Assets.ClipsFor(document.Asset.Model).Select(c => c.Id).Order(StringComparer.Ordinal).Prepend(None);
        if (Ui.Combo("Recovery clip", action.Recovery ?? None, clips) is { } recovery)
        {
            session.Edit(document, () => Def(document, id).Recovery = recovery == None ? null : recovery);
        }

        if (action.Next is not null || action.Recovery is not null)
        {
            Ui.Help("A press during this action (or while its recovery still holds the weapon out) chains to the next one; the recovery plays when it ends.");
        }

        var groups = basis?.Groups.Select(g => g.Id).ToArray() ?? [];
        if (id != EntityControllers.Jump)
        {
            if (Ui.Combo("Body", action.Mask ?? "(whole body)", groups.Prepend("(whole body)"), g => g == "(whole body)" ? "Whole body: replaces locomotion" : $"Over locomotion: owns '{g}'") is { } mask)
            {
                session.Edit(document, () =>
                {
                    var a = Def(document, id);
                    a.Mask = mask == "(whole body)" ? null : mask;
                    if (a.Mask is null)
                    {
                        a.BlendIn = a.BlendOut = 0;
                    }
                });
            }

            if (action.Mask is not null)
            {
                var blend = new Vector2(action.BlendIn, action.BlendOut);
                if (Ui.Drag2("Blend in / out (seconds)", ref blend, .002f, 0, 5))
                {
                    session.Change(document, () =>
                    {
                        var a = Def(document, id);
                        a.BlendIn = Math.Clamp(blend.X, 0, 5);
                        a.BlendOut = Math.Clamp(blend.Y, 0, 5);
                    });
                }

                Ui.Help("The legs keep walking and keep their planted feet; the controller keeps moving the actor.");
                if (session.PreviewAction == id && ImGui.SmallButton($"Preview over: {session.PreviewRole}"))
                {
                    ImGui.OpenPopup("over");
                }

                if (ImGui.BeginPopup("over"))
                {
                    foreach (var role in session.Entity?.Roles.Keys.Order() ?? Enumerable.Empty<string>())
                    {
                        if (ImGui.Selectable(role, role == session.PreviewRole))
                        {
                            session.PreviewRoleOf(role);
                        }
                    }

                    ImGui.EndPopup();
                }
            }
            if (groups.Length == 0)
            {
                Ui.Help("The base has no control groups, so actions play on the whole body.");
            }
        }

        Ui.Header("Hit windows");
        foreach (var hit in action.Hits)
        {
            if (ImGui.Selectable($"{hit.Id}  {Describe(hit.Start)} to {Describe(hit.Finish)}  dmg {(document.Asset.Behavior!.Strikes.GetValueOrDefault(hit.Id) ?? new StrikeDef()).Damage}", session.Selection.Hit == hit.Id))
            {
                session.Selection.Hit = hit.Id;
            }
        }

        if (ImGui.SmallButton("Add hit window"))
        {
            var hitId = ModelAuthoring.UniqueId("hit", action.Hits.Select(h => h.Id));
            var attack = document.Asset.Behavior!.Colliders.FirstOrDefault(c => c.Value == ColliderUses.Attack).Key;
            if (attack is null)
            {
                session.Report("Add an attack collider first: a hit window enables one.", true);
            }
            else if (session.Edit(document, () =>
            {
                Def(document, id).Hits.Add(new() { Id = hitId, Collider = attack, Start = new() { At = .4f }, Finish = new() { At = .6f } });
                document.Asset.Behavior!.Strikes.TryAdd(hitId, new());
            }))
            {
                session.Selection.Hit = hitId;
            }
        }
        if (session.Selection.Hit is { } selectedHit && action.Hits.Any(h => h.Id == selectedHit))
        {
            HitFields(document, id, selectedHit, resolved);
        }

        Ui.Header("Events");
        foreach (var cue in action.Events.ToArray())
        {
            var index = action.Events.IndexOf(cue);
            ImGui.PushID(index);
            ImGui.TextUnformatted(cue.Id);
            ImGui.SameLine();
            ImGui.TextDisabled(Describe(cue.At) + (cue.Sound is { } sound ? $"  sound {sound}" : ""));
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                session.Edit(document, () => Def(document, id).Events.RemoveAt(index));
            }

            TimeField("At", cue.At, resolved?.Clip, time => Def(document, id).Events[index].At = time, document);
            var soundId = cue.Sound ?? "";
            if (Ui.Text("Sound (empty for none)", ref soundId, 64))
            {
                session.Change(document, () => Def(document, id).Events[index].Sound = soundId.Length == 0 ? null : soundId);
            }

            ImGui.PopID();
        }
        ImGui.SetNextItemWidth(110 * Ui.Scale);
        ImGui.InputText("##event", ref _newEvent, 64);
        ImGui.SameLine();
        if (ImGui.SmallButton("Add event"))
        {
            session.Edit(document, () => Def(document, id).Events.Add(new() { Id = _newEvent, At = new() { At = MathF.Round(session.Transport.Time / Math.Max(resolved?.Clip.Duration ?? 1, 1e-3f), 3) } }));
        }

        Ui.Help("Events fire when simulation crosses them, never while scrubbing. The controller interprets their IDs, such as 'launch'.");
    }

    private void HitFields(AssetDocument<Part> document, string actionId, string hitId, ResolvedAction? resolved)
    {
        HitWindow Hit() => Def(document, actionId).Hits.First(h => h.Id == hitId);
        var hit = Hit();
        var basis = Base;
        ImGui.Separator();
        ImGui.TextColored(UiTheme.Accent, "Hit " + hitId);
        ImGui.SameLine();
        if (ImGui.SmallButton("Remove hit"))
        {
            session.Edit(document, () =>
            {
                Def(document, actionId).Hits.RemoveAll(h => h.Id == hitId);
                // The strike goes with the last window of its hit group.
                if (document.Asset.Behavior!.Actions.All(a => a.Hits.All(h => h.Id != hitId)))
                {
                    document.Asset.Behavior!.Strikes.Remove(hitId);
                }
            });
            session.Selection.Hit = null;
            return;
        }
        TimeField("Opens", hit.Start, resolved?.Clip, time => Hit().Start = time, document);
        TimeField("Closes", hit.Finish, resolved?.Clip, time => Hit().Finish = time, document);
        Ui.Help("The window is the activation interval [opens, closes) of its attack collider: enabled for every step that crosses any of it.");
        var attackColliders = document.Asset.Behavior!.Colliders.Where(c => c.Value == ColliderUses.Attack).Select(c => c.Key).ToList();
        if (Ui.Combo("Collider", hit.Collider, attackColliders) is { } chosenCollider)
        {
            session.Edit(document, () => Hit().Collider = chosenCollider);
        }

        ImGui.TextDisabled($"Geometry is collider '{hit.Collider}'" + (document.Asset.Geometry.FirstOrDefault(c => c.Id == hit.Collider) is { } named ? $": {BindingSummary(named)}" : ""));
        if (ImGui.SmallButton("Edit collider"))
        {
            session.Selection.Collider = hit.Collider;
        }

        DamageField(document, hitId);
    }

    /// <summary>The window's strike damage: the typed strike of its hit group (the defaults until one is declared).</summary>
    private void DamageField(AssetDocument<Part> document, string hitId)
    {
        var damage = (document.Asset.Behavior!.Strikes.GetValueOrDefault(hitId) ?? new StrikeDef()).Damage;
        ImGui.TextUnformatted($"Damage (strike '{hitId}')");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.DragInt("##damage", ref damage, .1f, 0, 10000))
        {
            session.Change(document, () =>
            {
                var strikes = document.Asset.Behavior!.Strikes;
                if (!strikes.TryGetValue(hitId, out var strike))
                {
                    strikes[hitId] = strike = new();
                }

                strike.Damage = Math.Clamp(damage, 0, 10000);
            });
        }
    }

    private static readonly string[] EditableShapeKinds = [ShapeKinds2D.Rectangle, ShapeKinds2D.AxisAlignedRectangle, ShapeKinds2D.RoundedRectangle, ShapeKinds2D.Circle, ShapeKinds2D.Capsule, ShapeKinds2D.Ellipse, ShapeKinds2D.Triangle, ShapeKinds2D.ConvexPolygon];

    /// <summary>Kind and geometry fields for one shape definition. Kind changes are discrete edits; drags are continuous changes.</summary>
    private void ShapeFields(AssetDocument<Part> document, IShape2D shape, Action<IShape2D> set)
    {
        IEnumerable<string> kinds = EditableShapeKinds.Contains(shape.Kind) ? EditableShapeKinds : EditableShapeKinds.Append(shape.Kind);
        if (Ui.Combo("Shape", shape.Kind, kinds) is { } kind)
        {
            session.Edit(document, () => set(shape.WithKind(kind)));
        }

        ShapeInspector.Draw(shape, changed => session.Change(document, () => set(changed)),
            changed => session.Edit(document, () => set(changed)));
    }

    /// <summary>A moment: a clip marker, or a normalized time over the clip. Markers keep hits on the visual moment when the clip is retimed.</summary>
    private void TimeField(string label, ActionTime time, MotionClip? clip, Action<ActionTime> set, AssetDocument<Part> document)
    {
        const string Normalized = "(normalized time)";
        var markers = clip?.Markers.Select(m => m.Id).ToArray() ?? [];
        if (Ui.Combo(label, time.Marker ?? Normalized, markers.Prepend(Normalized), m => m == Normalized ? m : $"marker '{m}'") is { } chosen)
        {
            session.Edit(document, () => set(chosen == Normalized ? new() { At = clip is null || time.Marker is null ? time.At : (clip.Markers.FirstOrDefault(m => m.Id == time.Marker)?.Time ?? 0) / clip.Duration } : new() { Marker = chosen }));
        }

        if (time.Marker is null)
        {
            var at = time.At;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##" + label, ref at, 0, 1, clip is null ? "%.3f" : $"%.3f  ({at * clip.Duration:F3} s)"))
            {
                session.Change(document, () => set(new() { At = at }));
            }
        }
        else if (clip?.Markers.All(m => m.Id != time.Marker) == true)
        {
            Ui.Problem($"'{clip.Name}' has no marker '{time.Marker}'.");
        }
    }

    private static string Describe(ActionTime time) => time.Marker ?? $"{time.At:0.###}";

    // ---- Viewport ------------------------------------------------------------------------------------------------

    public void Overlay(ViewportFrame frame)
    {
        if (session.EvaluateEntity() is not { } preview || frame.Primary is null)
        {
            return;
        }

        var draw = frame.Draw;
        Vector2 Screen(Vector2 p) => frame.Screen(new(p, 0));
        void Outline(EntityRegion region, uint color, float width)
        {
            var points = region.Outline().Select(Screen).ToArray();
            for (var i = 0; i < points.Length; i++)
            {
                draw.AddLine(points[i], points[(i + 1) % points.Length], color, width);
            }
        }
        Outline(preview.Movement, StudioTheme.MovementRegion.PackedValue, 1.5f);
        // Read-only views of the colliders the game registers, placed by the same function at this preview pose.
        foreach (var placed in preview.Colliders)
        {
            var selected = session.Selection.Collider == placed.Id;
            var points = placed.Region.Outline().Select(Screen).ToArray();
            switch (placed.Use)
            {
                case ColliderUses.Attack when placed.Enabled:
                    for (var i = 1; i < points.Length - 1; i++)
                    {
                        draw.AddTriangleFilled(points[0], points[i], points[i + 1], StudioTheme.AttackFill.PackedValue);
                    }

                    Outline(placed.Region, StudioTheme.AttackOutline.PackedValue, selected ? 4 : 2.5f);
                    draw.AddText(points[2] + new Vector2(4, -16), StudioTheme.AttackLabel.PackedValue, placed.Id);
                    break;
                case ColliderUses.Attack:
                    Outline(placed.Region, StudioTheme.AttackDisabled.PackedValue, selected ? 3 : 1);
                    break;
                default:
                    {
                        var color = placed.Use switch
                        {
                            ColliderUses.Hurt => StudioTheme.HurtRegion,
                            ColliderUses.Guard => StudioTheme.GuardRegion,
                            _ => StudioTheme.TriggerRegion,
                        };
                        Outline(placed.Region, (placed.Enabled ? color : new Color4b(color, 80)).PackedValue, selected ? 4 : 2);
                        if (selected)
                        {
                            draw.AddText(points[0] + new Vector2(4, 4), color.PackedValue, placed.Id);
                        }

                        break;
                    }
            }
        }
        foreach (var equipment in preview.Entity.Equipment)
        {
            foreach (var location in equipment.Prop.Locations)
            {
                var tip = PartPlacement3D.Point(preview.Pose.Socket(equipment.Socket), equipment.Prop, location.Point);
                draw.AddCircleFilled(frame.Screen(tip), 4 * Ui.Scale, StudioTheme.WeaponTip.PackedValue);
            }
        }

        if (preview.Action is { Mask: not null } masked)
        {
            draw.AddText(frame.Origin + new Vector2(12, frame.Size.Y - 28 * Ui.Scale), StudioTheme.LayerMaskText.PackedValue, $"{masked.Id} over {session.PreviewRole}: layer weight {preview.Weight:P0}");
        }

        if (frame.Hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            session.Report("Collision overlays: grey movement box, blue hurt colliders, red enabled attack colliders (faint when their windows are closed), green guard, orange triggers, yellow prop tips.");
        }
    }

    // ---- Timeline ------------------------------------------------------------------------------------------------

    public void Timeline()
    {
        var clip = session.TransportClip;
        var transport = session.Transport;
        var label = session.PreviewAction is { } action ? $"Action {action}" : $"Role {session.PreviewRole}";
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        if (clip is null)
        {
            ImGui.TextDisabled(session.EntityError ?? "Nothing assigned to preview.");
            return;
        }
        ImGui.TextDisabled($"({clip.Name})");
        ImGui.SameLine();
        if (ImGui.Button(transport.Playing ? "Pause" : "Play", new(64 * Ui.Scale, 0)))
        {
            session.TogglePlay();
        }

        ImGui.SameLine();
        if (ImGui.Button("|<"))
        {
            session.Seek(0);
        }

        ImGui.SameLine();
        if (ImGui.Button(">|"))
        {
            session.Seek(clip.Duration);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(Math.Max(120, ImGui.GetContentRegionAvail().X - 100 * Ui.Scale));
        var time = transport.Time;
        if (ImGui.SliderFloat("##time", ref time, 0, clip.Duration, $"%.3f / {clip.Duration:F3} s"))
        {
            transport.Pause();
            session.Seek(time);
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var speed = transport.Speed;
        if (ImGui.SliderFloat("##speed", ref speed, .1f, 2, "%.2fx"))
        {
            transport.Speed = speed;
        }

        PlaybackModePicker.Draw(session, clip);
        Phases(clip, session.Entity?.Actions.GetValueOrDefault(session.PreviewAction ?? ""));
    }

    /// <summary>Anticipation, active and recovery intervals over the clip, with markers, events and the masked layer's weight. Click to scrub.</summary>
    private void Phases(MotionClip clip, ResolvedAction? action)
    {
        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos() + new Vector2(0, 4 * Ui.Scale);
        var width = ImGui.GetContentRegionAvail().X;
        var height = 26 * Ui.Scale;
        float X(float t) => start.X + t / clip.Duration * width;
        draw.AddRectFilled(start, start + new Vector2(width, height), StudioTheme.ActionTrackBackground.PackedValue);
        if (action is not null)
        {
            var hits = action.Hits;
            if (hits.Count > 0)
            {
                var open = hits.Min(h => h.Start);
                var close = hits.Max(h => h.Finish);
                draw.AddRectFilled(start, new(X(open), start.Y + height), StudioTheme.AnticipationFill.PackedValue);
                draw.AddRectFilled(new(X(close), start.Y), start + new Vector2(width, height), StudioTheme.RecoveryFill.PackedValue);
                foreach (var hit in hits)
                {
                    draw.AddRectFilled(new(X(hit.Start), start.Y), new(X(hit.Finish), start.Y + height), StudioTheme.AttackWindow.PackedValue);
                    draw.AddText(new(X(hit.Start) + 3, start.Y + 5 * Ui.Scale), StudioTheme.AttackLabel.PackedValue, hit.Window.Id == hit.Collider.Id ? hit.Window.Id : $"{hit.Window.Id} > {hit.Collider.Id}");
                }

                draw.AddText(start + new Vector2(4, 5 * Ui.Scale), StudioTheme.AnticipationText.PackedValue, "anticipation");
                draw.AddText(new(X(close) + 4, start.Y + 5 * Ui.Scale), StudioTheme.RecoveryText.PackedValue, "recovery");
            }
            if (action.Mask is not null)
            {
                for (var i = 1; i <= 64; i++)
                {
                    float T(int k) => clip.Duration * k / 64;
                    draw.AddLine(new(X(T(i - 1)), start.Y + height * (1 - action.Weight(T(i - 1)))), new(X(T(i)), start.Y + height * (1 - action.Weight(T(i)))), StudioTheme.ActionBlendCurve.PackedValue, 1.5f);
                }
            }

            foreach (var cue in action.Events)
            {
                var x = X(cue.Seconds);
                draw.AddTriangleFilled(new(x - 4, start.Y + height + 8), new(x + 4, start.Y + height + 8), new(x, start.Y + height), StudioTheme.ActionEvent.PackedValue);
                draw.AddText(new(x + 5, start.Y + height), StudioTheme.ActionEvent.PackedValue, cue.Event.Id);
            }
        }
        foreach (var marker in clip.Markers)
        {
            var x = X(marker.Time);
            draw.AddLine(new(x, start.Y - 2), new(x, start.Y + height + 2), StudioTheme.ActionMarker.PackedValue, 1);
            draw.AddText(new(x + 3, start.Y - 15 * Ui.Scale), StudioTheme.ActionMarker.PackedValue, marker.Id);
        }
        var playhead = X(session.Transport.Time);
        draw.AddLine(new(playhead, start.Y - 4), new(playhead, start.Y + height + 4), StudioTheme.Selection.PackedValue, 2);
        ImGui.SetCursorScreenPos(start);
        ImGui.InvisibleButton("phases", new(width, height + 14 * Ui.Scale));
        if (ImGui.IsItemActive())
        {
            session.Transport.Pause();
            session.Seek(Math.Clamp((ImGui.GetMousePos().X - start.X) / width, 0, 1) * clip.Duration);
        }
        if (action is null)
        {
            Ui.Help("Choose an action in the outline to see its anticipation, active hit windows and recovery.");
        }
    }
}
