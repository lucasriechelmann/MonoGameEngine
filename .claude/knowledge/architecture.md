# Architecture

> Rebuilt after Milestones 1-6 and updated for the project agnostic editor. Verified by building and by the 89 xUnit tests; the editor UI was driven for opening a project, the Project Setup dialog and New Project > Create, but is otherwise not interactively tested. Items marked `(inferred)` are unconfirmed.

## Projects and dependencies

```
Engine  <--  Game          (Game references Engine only)
Engine  <--  Editor  -->  Tooling   (Editor references Engine, Tooling, ImGui.NET; NOT Game)
Engine, Tooling  <--  Engine.Tests
```

The editor edits any game project: the game's code is built and loaded at runtime (see "Projects" below), so the editor has no compile-time link to a game.

- **Engine** (`src/MonoGameEngine.Engine/`, namespace `MonoGameEngine.Engine`): no ImGui, no Game/Editor reference. Depends on MonoGame DesktopGL and `System.Text.Json`.
- **Game** (`src/MonoGameEngine/`, namespace `MonoGameEngine`): `GameApp` loads `Scenes/main.scene.json` from the content folder and runs it with `SceneRunner`. `Spinner` is a sample game component. Textures are built from `content/Content.mgcb` into the exe's `Content` folder; scene files are copied into `Content/Scenes` (`None ... CopyToOutputDirectory` in the csproj).
- **Tooling** (`src/MonoGameEngine.Tooling/`, namespace `MonoGameEngine.Tooling.Projects`): BCL only. `ProjectFile` (`.mgeproj`), `SolutionReader` (`.sln` regex and `.slnx` XML), `CsprojInfo` (target frameworks, output type, Engine reference, content reference, `IsEditorLoadable`), `ProjectSetup`, `ProjectBuilder`, `GameAssemblyContext`. Kept out of Engine so shipped games do not carry editor tooling, and out of Editor so it is unit tested.
- **Editor** (`src/MonoGameEngine.Editor/`, namespaces `MonoGameEngine.Editor`, `.State`, `.Ui`): references Engine and Tooling. It builds nothing at compile time; the open project's content comes from that project's own build output.
- **Tests** (`tests/MonoGameEngine.Engine.Tests/`): xUnit against Engine and Tooling.
- **Content** (`content/`): the sample game's source `Textures/`, `Scenes/*.scene.json`, and `Content.mgcb` listing each texture (no premultiplied alpha, no colour key). Built by the Game project. A new texture must be added to `Content.mgcb`.
- **Engine package**: the Engine packs as `MonoGameEngine.Engine` (`dotnet pack`, version in its csproj). Its MonoGame reference is `PrivateAssets="all"`, so the package depends only on Aether and each game picks its own platform package (all ship an assembly named `MonoGame.Framework`).

## Projects (`.mgeproj`)

- `ProjectFile` holds `Solution`, `CodeProject`, `LaunchProject` (null = code project), `ContentRoot`, `StartupScene`, `EngineVersion`; paths are relative to the file. The Project Setup dialog (`Ui/ProjectDialogs`, logic in `ProjectSetup`) guesses defaults: code = first loadable library using the Engine, launch = first loadable project that builds content, content folder from `MonoGameContentReference` or a scan for `Content.mgcb`.
- `State/ProjectSession` opens a project: builds the launch project (or the code project if the launch project is a mobile head) with `dotnet build -getProperty:TargetPath` on a background task, locates the code DLL, then on the main thread loads it into a `GameAssemblyContext`, registers it with `ComponentRegistry`, and creates a `ContentManager` over `<output>/Content` plus an `AssetManager` over the source content folder. Disposing a session unregisters the assembly (also clearing `MemberCache`), unloads the context and the content.
- `ProjectBuilder.BuildAsync` runs a plain `dotnet build` and only then asks `dotnet msbuild -getProperty:TargetPath` where the output is. Never combine `-getProperty` with `dotnet build`: it only evaluates the project and skips both restore and build, so a fresh project gets no assembly.
- New Project (`ProjectTemplate`, `Ui/ProjectDialogs.DrawNew`): templates are embedded resources (`Templates/Shared|Core|Head.<Platform>/**/*.tmpl`, binary files keep their image extension), tokens `__NAME__`, `__ID__` (identifier-safe name), `__ENGINE_VERSION__`, `__MONOGAME_VERSION__`, `__GUID_*__`. Output is `<Name>.sln`, `<Name>.Core`, one `<Name>.<Platform>` head per ticked platform (first is the launch project), `content/`, `nuget.config` (source `packages`), `.config/dotnet-tools.json` (mgcb), `packages/<Engine>.nupkg`, and `<Name>.mgeproj`. The `.sln` is written by code (classic format, readable by every IDE); the `.mgeproj` by `ProjectFile`.
- `GameAssemblyContext` shares every assembly the editor already has loaded (Engine, MonoGame, Aether), so a game `Component` derives from the editor's own `Component`; the game's own assemblies are read from memory so a rebuild never hits a locked file.
- `EditorGame` owns at most one open session and one pending one. Open, change settings and rebuild all start a new session; when it is ready the editor state and panels are recreated. Rebuild and settings changes carry a `SceneSnapshot` (JSON, path, unsaved flag) so the open scene survives; undo history is cleared. With no project open only the start screen shows.
- Shared settings: `Directory.Build.props` (net10.0, C# latest, nullable, implicit usings, `MonoGameVersion` 3.8.6), `global.json` (SDK 10.0.100, roll forward), `.config/dotnet-tools.json` (`dotnet-mgcb`).

## Engine model

`Scene` -> `Entity` -> `Component`.

- **Scene**: `Roots`, flat `Entities` list, id lookup (`Find`), `CreateEntity`, `Add`/`Remove` (detach/attach a subtree without lifecycle callbacks), `FlushDestroyed`, `Assets` (an `AssetManager`, null in tests).
- **Entity**: `Id` (Guid, stable), `Name`, `Active`, `Parent`/`Children`, `Components`, `Transform`. Entities are created only through `Scene.CreateEntity` (constructor is internal). `SetParent(parent, index)` where index is the final sibling position; rejects cycles.
- **Component** (abstract): `Enabled`, `Entity`, lifecycle `OnAwake/OnStart/OnUpdate(GameTime)/OnDraw(SpriteBatch)/OnDestroy` declared `protected internal virtual`. Subclasses in other assemblies override with `protected override`.
- **Transform** (always present, cannot be removed): local `Position/Rotation(radians)/Scale`; cached world values (`WorldPosition/Rotation/Scale`, settable) composed from the parent without shear; dirty flag propagates to children.
- **SpriteRenderer**: `TexturePath` (`[AssetPath]`), `Tint`, `Origin` (fraction of texture size, default 0.5,0.5), `LayerDepth` (0 front, 1 back).
- **Camera2D**: `Position`, `Zoom`, `GetViewMatrix(viewport)`. `SceneRunner` draws through the first enabled camera unless a view matrix is passed.
- **SceneRunner**: `Start` (Awake pass then Start pass), `Update` (picks up late-added components, updates, flushes destroyed entities), `Stop` (OnDestroy, resets so it can start again), `Draw(spriteBatch, viewport, viewOverride?)`. Used by Game, Editor play mode and the Editor's edit-mode drawing (without `Start`).
- **AssetManager**: takes a `ContentManager` (built `.xnb` output) and a source folder. Asset paths are source paths such as `Textures/crate.png`; the manager loads `Textures/crate` through the pipeline output, caches it, and logs once on failure. `FindContentRoot(preferSource)` finds the `Content` folder beside the exe or the repo's `content/` (the folder containing `Content.mgcb`); the Game uses it; the Editor is given the project's source folder and built `Content` folder directly by `ProjectSession`, so it browses and saves scenes in the source folder.
- **Animation**: `SpriteAnimator` (`Animation/`) cuts the sibling `SpriteRenderer`'s texture into a grid (`FrameWidth/Height`, `StartFrame`, `FrameCount`, `FramesPerSecond`, `Loop`, `PlayOnStart`). `SpriteRenderer.GetSourceRectangle` returns the current frame (or the whole texture); drawing, picking and collider fitting all use it. Edit mode shows `StartFrame`; the animator advances in `OnUpdate`.
- **Physics** (`Physics/`): `Rigidbody2D` (static/dynamic/kinematic, gravity scale, damping, fixed rotation, runtime velocity/force API), `Collider2D` base with `BoxCollider2D` and `CircleCollider2D` (size 0 fits the sprite; scaled by world scale), and the internal `PhysicsWorld2D` wrapping Aether.Physics2D. The world is created lazily by `Scene.GetOrCreatePhysics()` when a physics component wakes up, stepped by `SceneRunner.Update` after the component updates (fixed 1/60 s step, accumulator capped at 5 steps), and released by `SceneRunner.Stop`. Static bodies read the transform each step; dynamic and kinematic bodies write their pose back to the transform. Bodies are reference counted per entity (rigidbody plus colliders). Collisions and triggers are queued during the step and delivered afterwards through `Component.OnCollisionEnter/OnTriggerEnter`. `Scene.Gravity` (pixels per second squared, default 0,980) is saved in the scene file as `gravity`. Aether types stay internal to the Engine.
- **Log**: static, with a `Message` event the editor Console subscribes to.
- **Inspector attributes** (`Attributes.cs`): `Range`, `Tooltip`, `ReadOnly`, `HideInInspector`, `Header`, `AssetPath`.
- **Reflection** (`Reflection/`): `MemberCache.GetMembers(type)` returns public read/write properties not marked `JsonIgnore` (base class first) with attribute data; both the serializer and the Inspector use it. `ComponentRegistry` lists concrete `Component` types (Engine assembly plus assemblies registered with `RegisterAssembly`) and resolves the JSON `$type`.
- **Serialization** (`Serialization/SceneSerializer.cs`): JSON with `version`, `name`, nested `entities`/`children`; components carry `$type` (full type name) plus properties. Unknown types and bad values are skipped with a warning. Also `SerializeEntity`, `DeserializeEntity(newIds)`, `Clone` (used for duplicate). Converters: `Vector2` as `{x,y}`, `Color` as `#RRGGBBAA`, enums as strings.
- **Commands** (`Commands/`): `ICommand`, `CommandHistory` (`Execute`, `Record` for already-applied edits, `Undo`, `Redo`, capacity 200), and `SetPropertyCommand`, `CreateEntityCommand`, `DeleteEntityCommand`, `ReparentEntityCommand` (keeps world transform), `AddComponentCommand`, `RemoveComponentCommand`. They live in Engine, not Editor, so xUnit can test undo/redo.

## Editor structure

State (no ImGui drawing) in `State/`: `EditorState` (scenes, selection by Guid, history, play mode, file operations, entity creation helpers), `EditTracker` (merges live inspector edits into one command), `SceneViewCamera`, `AssetBrowser` (cached folder listing), `ConsoleLog`, `EditorSettings`.
Drawing in `Ui/`: `HierarchyPanel`, `InspectorPanel`, `SceneViewPanel` + `SceneViewRenderer` (render target, grid) + `TranslateGizmo` + `ScenePicker`, `AssetsPanel`, `ConsolePanel`, `PanelLayout`, `Win11Theme`, `Icons`, `MainMenu` (menus, Play/Pause/Stop, shortcuts, unsaved-changes prompt), `FileDialog`. `EditorGame` wires everything. `ImGuiRenderer`/`ImGuiInput` are the MonoGame backend.

## Data flow

1. **Game**: scene file -> `SceneSerializer.Load` -> `SceneRunner.Start/Update/Draw` each frame.
2. **Editor frame**: input -> ImGui frame -> panels read/modify `EditorState` -> `SceneViewRenderer.Render` draws grid and scene into the render target -> ImGui draws the window list, including the render target through `ImGui.Image`. Mouse position is converted to world space with `SceneViewCamera.ScreenToWorld` using the image's top-left.
3. **Edits**: panels create commands. Live edits (inspector drag, gizmo drag) change values directly and are recorded afterwards (`Record`), one command per edit. Structural edits (create, delete, reparent, add/remove component) use `Execute` or `Record` of a command that holds the entity/component instance so undo restores the same objects and ids.
4. **Play**: `Play` serializes the edited scene to JSON and runs a deserialized copy (`PlayScene`) in a `SceneRunner`. `Stop` discards the copy. Edits during play apply to the copy and are not recorded. Selection is a Guid so it survives play/stop.
5. **Files**: File menu uses `SceneSerializer`; `EditorSettings` stores the last and recent projects, the last scene per project, grid and demo-window flags and window size in `%APPDATA%/MonoGameEngine/editor-settings.json`; ImGui stores the layout in `imgui.ini` there. Panel layout is global, not per project.

## Data entities

| Entity | Defined in |
|---|---|
| `Scene`, `Entity`, `Component`, `Transform`, `SpriteRenderer`, `Camera2D` | `src/MonoGameEngine.Engine/` |
| Scene JSON schema | `src/MonoGameEngine.Engine/Serialization/SceneSerializer.cs`, example `content/Scenes/main.scene.json` |
| Editor settings | `src/MonoGameEngine.Editor/State/EditorSettings.cs` |

## External services

None. Third-party packages: MonoGame.Framework.DesktopGL and MonoGame.Content.Builder.Task 3.8.6, Aether.Physics2D 2.2.0 (Engine), ImGui.NET 1.91.6.1 (Editor only), xUnit (tests).

## Architecture decisions

- Engine, Game and Editor are separate projects so the shipped game never pulls in ImGui. *Why: spec rule.*
- `SceneRunner` is shared by Game, Editor edit-mode drawing and play mode. *Why: play mode behaves like the shipped game.*
- `Transform` stores local TRS and composes world values without shear (2D). *Why: cheap, exact for uniform parent scale, and it can be widened to 3D.*
- Undo commands live in Engine. *Why: the spec wants xUnit undo/redo tests in the Engine test project.*
- Play runs a JSON copy; Stop discards it. *Why: "Stop restores the snapshot exactly" holds by construction and the undo history stays valid (history holds references to the edited scene's objects).*
- Physics uses Aether.Physics2D (MS-PL), animation is our own. *Why: MonoGame.Extended 6.1.1 (MIT) was evaluated; its animation classes are code-driven and not Inspector-friendly, and its collisions detect overlaps but there is no physics simulation (no gravity, forces, bounce or friction). Aether gives a real rigid-body engine; a small `SpriteAnimator` component serializes and edits like every other component.*
- Physics runs only while the scene runs, on the play copy in the editor. Static bodies follow the transform, dynamic and kinematic bodies drive it. *Why: the editor never mutates the edited scene, and moving static colliders by script still works.*
- Textures load from built MGCB output in both Game and Editor; scene files keep the source path with extension. *Why: developer decision (the pipeline is the shipped format); keeping source paths means the Assets panel, scene files and the pipeline agree.* Cost: new textures must be added to `Content.mgcb` by hand (TODO: automate, for example by appending an entry when a texture is dropped into the editor).
- The editor does not build `Content.mgcb` or reference Game; it builds the opened project and reads that project's built `Content`. *Why: project agnostic editor (developer decision). Supersedes the earlier "both build Content.mgcb" and "Editor references Game" decisions.*
- `.mgeproj` separates the code project (loaded into the editor) from the launch project (platform head, run as the game), and solutions are opened through a setup dropdown. *Why: MonoGame games have one head per platform; Android and iOS heads cannot be loaded by a desktop process, so components belong in a shared desktop-loadable project and heads are launch-only. Developer asked for the solution dropdown.*
- Game code loads into a collectible `AssemblyLoadContext` that defers to assemblies already loaded in the editor, reading DLLs from memory. *Why: one shared `Component` type, and rebuild and reload without restarting or file locks.*
- Engine ships as a NuGet package without a MonoGame platform dependency. *Why: a platform dependency would force DesktopGL on Android and iOS heads. Cost: a game must reference its own MonoGame package.*
- New projects are generated from embedded files, not a `dotnet new` template pack, and carry their own copy of the Engine package in a relative `packages/` feed (not a path into the editor install). *Why: the templates always match the editor's Engine version, nothing has to be installed, and the project stays portable. Cost: the generator is ours to maintain, and NuGet's global cache keys on id and version, so rebuilding the Engine under the same `EngineVersion` can leave an older cached copy in already-restored projects; bump `EngineVersion` when the Engine's API changes.*
- Generated heads: DesktopGL (`net10.0`) and WindowsDX (`net10.0-windows`). Android and iOS heads are not generated (workloads and platform boilerplate not verified). *Why: only what was built and run here is offered.*
- The editor ships the Engine package by packing it into `<editor output>/Packages` in an incremental `AfterTargets="Build"` target. *Why: the template needs the nupkg at runtime and a developer should not have to pack by hand.*
- Project tooling lives in its own `MonoGameEngine.Tooling` project. *Why: Engine is the shipped runtime, Editor cannot be unit tested; Tooling is both small and testable.*
- .NET 10 and MonoGame 3.8.6 instead of the spec's .NET 8 / 3.8.x. *Why: developer asked to try .NET 10 and noted 3.8.6.*
- The editor theme is `Ui/Win11Theme.cs` (dark Fluent look, applied in `ImGuiRenderer`). Fonts are loaded in `ImGuiRenderer.LoadFonts`: Segoe UI from the Windows fonts folder (not redistributed; falls back to ImGui's default font) merged with Material Icons (`src/MonoGameEngine.Editor/Assets/Fonts/`, Apache 2.0, copied next to the exe), plus a second large icon font (`ImGuiRenderer.LargeIcons`) for Assets tiles. *Why: Windows 11 look with open source icons; only the icons used are rasterized, so the atlas stays small.*
- Panels are floating ImGui windows kept proportional to the window by `PanelLayout` (fractions of the work area, saved in `editor-settings.json`, re-applied on resize and maximize). *Why: ImGui.NET has no DockBuilder API, so a default docked layout cannot be created in code, and floating windows otherwise keep their old pixel size when the window is maximized.* Docked windows are left to ImGui docking.
- `EditorGame` and `GameApp` resize the back buffer on `ClientSizeChanged`. *Why: without it, maximizing leaves rendering at the old size.*
- Inspector undo uses `EditTracker`: a change starts an edit session that is committed when no widget is active and the mouse is up. *Why: one undo step per drag or typing session without per-widget bookkeeping.*
