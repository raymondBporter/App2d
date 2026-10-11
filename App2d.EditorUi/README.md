# Shared editor UI

`App2d.EditorUi` contains the common ImGui widgets, layout, theme and MonoGame GPU renderer. It has no dependency on App2d.Core, character documents or editor sessions.

The host takes a `GraphicsDevice` and an `IImGuiPlatform`. A `MonoGameImGuiPlatform` supplies input for an existing `Game`; a `WinFormsImGuiPlatform` supplies event input from a graphics control. The application owns the window, device and frame loop. ImGui does not require a `Game`.

```csharp
using var gui = new ImGuiHost(device, new WinFormsImGuiPlatform(surface));

// Each frame, before dispatching viewport input:
gui.Begin(elapsedSeconds);
DrawEditor();
// Route input using the capture flags, update the scene, then draw it.
gui.Render();
```

`ImGuiHost` owns its context, platform adapter and UI GPU resources. Dispose it before its graphics device or input control. Dispose and recreate it when the device is replaced. Textures registered with `Register` remain caller-owned; unregister them before disposing them. Input adapters buffer text/window events until their receiving context is current.

The view code describes screen structure. `Root` fills the drawing surface; `Sidebar` places a fixed tool window at the right edge; `Panel` supplies a child region and optional header; `Row` places its child callbacks horizontally; `Section` supplies a title and an ID scope. Panel sizes use device pixels; multiply authored sizes by `Ui.Scale` for the current monitor and user preference. Zero size fills the remaining space.

```csharp
Ui.Panel("tree-properties",
    header: () => Ui.Row(
        () => Ui.Button("Regenerate", editor.Regenerate),
        () => Ui.Button("Save", editor.Save)),
    content: () =>
    {
        Ui.Section("Trunk", DrawTrunkControls);
        Ui.Section("Branches", DrawBranchControls);
        Ui.Section("Foliage", DrawFoliageControls);
    });
```

Each `Row` callback is one horizontal child, which can itself be a panel or group. Widgets inside a panel or section stack vertically by default. Use stable, distinct panel IDs and pass a section `id` when titles repeat. The layout helpers balance their ImGui scopes even when a region is clipped or a view throws. Direct ImGui calls remain available for specialized viewports and timelines.

Views read editor state and invoke session commands. Validation, saving and document undo belong to the editor/session; a continuous slider edit should commit as one document transaction. ImGui owns the text cursor, selection and text editing undo. Use the host's `WantCaptureMouse` and `WantCaptureKeyboard` flags to route interactions to the UI and prevent the same input from editing the world underneath it. Keyboard shortcuts should also respect focused text controls.

`PropertyInspector` draws existing .NET properties using their `Category`, `DisplayName`, `Description`, `Browsable` and `ReadOnly` metadata. It supports strings, numbers, booleans, enums and drawing colors. Text drafts commit on leaving the field or through `Commit` before Apply/export. A rejected draft stays editable without replacing the model; validation errors appear in the panel.

Character Studio uses this library in its editor and gallery. The map editor's `MapEditorUi2D` draws the tile palette and thing inspector; editing and undo commands remain in `TileEditor2D`. The core `IGameToolUi2D` contract keeps ImGui out of the core project. `GameHost` builds the tool UI and applies capture before fixed simulation ticks. Noodle's `NoodleToolHost` shares the WinForms frame loop and renders the bones, IK and rigid pose views with separate controls and scene input.

Character Studio's `--smoke-editor-surface <output-directory>` renders its real editor at two sizes/scales without constructing `MonoGame.Game`. App2d and Noodle each support `--smoke-editor-ui <output-directory>` for hidden GPU checks of their actual views. The map check uses a scratch copy of the level database; the Noodle check does not save documents.
