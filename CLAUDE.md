# MonoGameEditor

A small, Unity-inspired visual editor for MonoGame, built with ImGui.NET. It has a runtime engine library, a shippable game, and an editor app.

> **Status: Milestones 1-6 implemented and pushed** to https://github.com/lucasriechelmann/MonoGameEngine (branch `main`, MIT license). The solution builds and 76 xUnit tests pass. The editor is project agnostic (see `.mgeproj` below): it opens any game project, builds it and loads its components at runtime. The editor was smoke-run (opening `MonoGameEngine.mgeproj`, the Project Setup dialog) but its interactions (gizmo, drag and drop, undo in the UI, Project menu actions, rebuild and reload) have not been manually verified, and the New Project templates are not built yet. See `.claude/knowledge/architecture.md` for the as-built design and decisions.

## Tech constraints

- **.NET 10** (developer-approved deviation from the spec's .NET 8; set in `Directory.Build.props`, SDK pinned in `global.json`), C# latest, nullable enabled, file-scoped namespaces.
- MonoGame **3.8.6** DesktopGL (`MonoGameVersion` in `Directory.Build.props`; tool manifest in `.config/dotnet-tools.json`). Windows is the verified platform; Linux/macOS untested.
- ImGui.NET 1.91.6.1 with docking (verified: `DockingEnable` exists and is enabled in the editor).
- `System.Text.Json` for all serialization. Aether.Physics2D 2.2.0 (physics, Engine only) is the one approved extra package; ask before adding any other. MonoGame.Extended was evaluated and not adopted (no physics; animations are not Inspector-friendly).
- 2D first, but nothing that blocks 3D later (e.g. `Transform` stays extendable).

## Projects

Project names use the `MonoGameEngine` prefix, replacing the spec's `MyGame` placeholder: `MonoGameEngine.sln`, `MonoGameEngine.Engine`, `MonoGameEngine` (the game), `MonoGameEngine.Editor`, `MonoGameEngine.Engine.Tests`.

| Project | Path | Role |
|---|---|---|
| Engine | `src/MonoGameEngine.Engine/` | Runtime core: scene, entities, components, serialization, `SceneRunner`, inspector attributes, undo commands |
| Game | `src/MonoGameEngine/` | The shipped game: loads and runs `content/Scenes/main.scene.json`. References Engine only. Zero editor/ImGui code |
| Tooling | `src/MonoGameEngine.Tooling/` | No dependencies, no ImGui. The `.mgeproj` format (`ProjectFile`), `.sln`/`.slnx`/`.csproj` reading, `ProjectSetup` (dropdown choices and guesses), `ProjectBuilder` (`dotnet build`), `GameAssemblyContext` (collectible load context for the game DLL) |
| Editor | `src/MonoGameEngine.Editor/` | Editor app. References Engine and Tooling, **not** Game: the game under edit is a project opened at runtime. All ImGui code lives here |
| Content | `content/` | The sample game's source textures (`Textures/`), scenes (`Scenes/*.scene.json`) and `Content.mgcb` (built by the Game project). Any project's content folder is set in its `.mgeproj` |
| Project file | `MonoGameEngine.mgeproj` | Opens this repo's sample game in the editor |
| Tests | `tests/MonoGameEngine.Engine.Tests/` | xUnit (references Engine and Tooling): serialization round-trips, undo/redo, lifecycle, transforms, project files, solution reading, setup guessing, load context |

## Projects and `.mgeproj`

The editor edits a game project, not itself. A `*.mgeproj` (JSON, next to the game's solution) names the solution, the **code project** (platform-neutral, holds the components; the editor builds it and loads its DLL), the **launch project** (a platform head, built for the content and run as the game; empty means the code project), the content folder and the startup scene. Open it directly, or open a `.sln`/`.slnx`/`.csproj` and the Project Setup dialog offers dropdowns and writes it. Mobile heads (`-android`, `-ios`) are launch-only because the desktop editor cannot load them. Package the Engine with `dotnet pack src/MonoGameEngine.Engine -c Release -o artifacts/packages`; it does not depend on a MonoGame platform package, so each game picks its own.

## Standing rules

- **The game must build and run with no reference to the editor or ImGui.** Never add an Editor or ImGui reference to Engine or Game.
- Search for an existing implementation before creating a new one.
- Inspector attributes (`[Range]`, `[Tooltip]`, `[ReadOnly]`, `[HideInInspector]`, `[Header]`) are defined in Engine, with no editor dependency.
- Keep editor drawing code separate from editor state (selection, command history).
- Avoid per-frame allocations in the renderer and update-loop hot paths where possible.
- Public Engine APIs get XML doc comments.
- Work in milestones. After each: the solution builds and runs, summarize what was done, list deviations from the spec, then **stop for developer review** before the next milestone.
- Asset references are source paths relative to `content/` (e.g. `Textures/crate.png`); new textures must be added to `content/Content.mgcb`. Entities have stable Guids.
- Undo commands live in Engine (`Commands/`) so they can be unit tested; edits in the editor must go through `EditorState.Execute/Record`.
- Build with `dotnet build MonoGameEngine.sln`, test with `dotnet test`. Commit and push only when the developer asks in that session, and add no `Co-Authored-By` trailer.

## Where to look

- Before adding or changing a data entity, a service boundary, or a cross-project dependency, read `.claude/knowledge/architecture.md`.
- Before writing new code in an area you have not touched this session, read `.claude/knowledge/conventions.md`.
- Before working on a feature, find its owning modules in `.claude/knowledge/features.md`.
- The original requirements and milestone list are in `MonoGameEditor-Prompt.md`.

## Context maintenance

In any change that alters the data schema, adds, removes, or renames a project or module, moves a feature to different code, or makes an architecture decision, update CLAUDE.md and the files in `.claude/knowledge/` in the same change, without being asked. When the change records an architecture decision, write the decision and its reason into the knowledge files. Before finishing such a change, re-read the affected sections and confirm they still match the code. Stale context is a bug.

## Team rules

<!-- manual:start -->
<!-- manual:end -->
