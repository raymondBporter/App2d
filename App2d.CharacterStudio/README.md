# Character Studio source map

Start with [the character system guide](../docs/character-system.md) for shared
vocabulary and the flow from saved definitions to runtime poses and editor drafts.
See [the Studio workflow](../docs/character-studio.md) for using the application.

| Folder | Responsibility |
| --- | --- |
| `Authoring/` | Graphics-free model, animation, Part, entity, and collider edits |
| `Editing/` | Documents/workspace, session/transport, preview data, and picking |
| `Templates/` | Model-template recipes/contracts/catalog and look presets |
| `Importing/` | Point-library and prototype-puppet import paths |
| `Editor/` | Shell/browser, workspace views, inspectors, shared components, viewports |
| `Preview/Arena/` | Authoring arena simulation, rendering, and UI host |
| `Seeds/` | Explicit generators for templates, library assets, modules, and moves |
| `Diagnostics/` | Editor smokes, rendering captures, proof renders, studies, and measurements |

`Program.cs` remains the executable entry point. Studio references Core, the game,
and EditorUi; the game does not reference Studio. These folders share one project.
The source organization retains existing namespaces and APIs, including the
`CharacterSeeds` namespaces now used by files under `Seeds/` and `Diagnostics/`.

Run the editor:

```powershell
dotnet run --project App2d.CharacterStudio
```

The existing CLI switches are declared in `Program.cs` and `Seeds/SeedCommands.cs`.
Generators operate on authored assets explicitly. Diagnostics write captures and
reports to the requested output directory.
