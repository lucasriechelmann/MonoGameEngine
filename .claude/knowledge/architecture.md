# Architecture

> Rebuilt after Milestones 1-6 were implemented. Verified by building and by the 38 xUnit tests; the editor UI itself has only been smoke-run (starts, loads the last scene, persists settings), not interactively tested. Items marked `(inferred)` are unconfirmed.

## Projects and dependencies

```
Engine  <--  Game          (Game references Engine only)
Engine  <--  Editor  -->  Game   (Editor references Engine, Game, ImGui.NET)
Engine  <--  Engine.Tests
```

- **Engine** (`src/MonoGameEngine.Engine/`, namespace `MonoGameEngine.Engine`): no ImGui, no Game/Editor reference. Depends on MonoGame DesktopGL and `System.Text.Json`.
- **Game** (`src/MonoGameEngine/`, namespace `MonoGameEngine`): `GameApp` loads `Scenes/main.scene.json` from the content folder and runs it with `SceneRunner`. `Spinner` is a sample game component. Textures are built from `content/Content.mgcb` into the exe's `Content` folder; scene files are copied into `Content/Scenes` (`None ... CopyToOutputDirectory` in the csproj).
- **Editor** (`src/MonoGameEngine.Editor/`, namespaces `MonoGameEngine.Editor`, `.State`, `.Ui`): also builds `Content.mgcb` itself and references Game so it can register the game assembly's components (`ComponentRegistry.RegisterAssembly(typeof(GameApp).Assembly)`).
- **Tests** (`tests/MonoGameEngine.Engine.Tests/`): xUnit against Engine.
- **Content** (`content/`): source `Textures/`, `Scenes/*.scene.json`, and `Content.mgcb` listing each texture (no premultiplied alpha, no colour key). Built by both Game and Editor into their `Content` folder. A new texture must be added to `Content.mgcb`.
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
- **AssetManager**: takes a `ContentManager` (built `.xnb` output) and a source folder. Asset paths are source paths such as `Textures/crate.png`; the manager loads `Textures/crate` through the pipeline output, caches it, and logs once on failure. `FindContentRoot(preferSource)` finds the `Content` folder beside the exe or the repo's `content/` (the folder containing `Content.mgcb`); the Editor uses the source folder for browsing and scenes.
- **Log**: static, with a `Message` event the editor Console subscribes to.
- **Inspector attributes** (`Attributes.cs`): `Range`, `Tooltip`, `ReadOnly`, `HideInInspector`, `Header`, `AssetPath`.
- **Reflection** (`Reflection/`): `MemberCache.GetMembers(type)` returns public read/write properties not marked `JsonIgnore` (base class first) with attribute data; both the serializer and the Inspector use it. `ComponentRegistry` lists concrete `Component` types (Engine assembly plus assemblies registered with `RegisterAssembly`) and resolves the JSON `$type`.
- **Serialization** (`Serialization/SceneSerializer.cs`): JSON with `version`, `name`, nested `entities`/`children`; components carry `$type` (full type name) plus properties. Unknown types and bad values are skipped with a warning. Also `SerializeEntity`, `DeserializeEntity(newIds)`, `Clone` (used for duplicate). Converters: `Vector2` as `{x,y}`, `Color` as `#RRGGBBAA`, enums as strings.
- **Commands** (`Commands/`): `ICommand`, `CommandHistory` (`Execute`, `Record` for already-applied edits, `Undo`, `Redo`, capacity 200), and `SetPropertyCommand`, `CreateEntityCommand`, `DeleteEntityCommand`, `ReparentEntityCommand` (keeps world transform), `AddComponentCommand`, `RemoveComponentCommand`. They live in Engine, not Editor, so xUnit can test undo/redo.

## Editor structure

State (no ImGui drawing) in `State/`: `EditorState` (scenes, selection by Guid, history, play mode, file operations, entity creation helpers), `EditTracker` (merges live inspector edits into one command), `SceneViewCamera`, `AssetBrowser` (cached folder listing), `ConsoleLog`, `EditorSettings`.
Drawing in `Ui/`: `HierarchyPanel`, `InspectorPanel`, `SceneViewPanel` + `SceneViewRenderer` (render target, grid) + `TranslateGizmo` + `ScenePicker`, `AssetsPanel`, `ConsolePanel`, `MainMenu` (menus, Play/Pause/Stop, shortcuts, unsaved-changes prompt), `FileDialog`. `EditorGame` wires everything. `ImGuiRenderer`/`ImGuiInput` are the MonoGame backend.

## Data flow

1. **Game**: scene file -> `SceneSerializer.Load` -> `SceneRunner.Start/Update/Draw` each frame.
2. **Editor frame**: input -> ImGui frame -> panels read/modify `EditorState` -> `SceneViewRenderer.Render` draws grid and scene into the render target -> ImGui draws the window list, including the render target through `ImGui.Image`. Mouse position is converted to world space with `SceneViewCamera.ScreenToWorld` using the image's top-left.
3. **Edits**: panels create commands. Live edits (inspector drag, gizmo drag) change values directly and are recorded afterwards (`Record`), one command per edit. Structural edits (create, delete, reparent, add/remove component) use `Execute` or `Record` of a command that holds the entity/component instance so undo restores the same objects and ids.
4. **Play**: `Play` serializes the edited scene to JSON and runs a deserialized copy (`PlayScene`) in a `SceneRunner`. `Stop` discards the copy. Edits during play apply to the copy and are not recorded. Selection is a Guid so it survives play/stop.
5. **Files**: File menu uses `SceneSerializer`; `EditorSettings` stores last scene, grid and demo-window flags and window size in `%APPDATA%/MonoGameEngine/editor-settings.json`; ImGui stores the layout in `imgui.ini` there.

## Data entities

| Entity | Defined in |
|---|---|
| `Scene`, `Entity`, `Component`, `Transform`, `SpriteRenderer`, `Camera2D` | `src/MonoGameEngine.Engine/` |
| Scene JSON schema | `src/MonoGameEngine.Engine/Serialization/SceneSerializer.cs`, example `content/Scenes/main.scene.json` |
| Editor settings | `src/MonoGameEngine.Editor/State/EditorSettings.cs` |

## External services

None. Third-party packages: MonoGame.Framework.DesktopGL and MonoGame.Content.Builder.Task 3.8.6, ImGui.NET 1.91.6.1 (Editor only), xUnit (tests).

## Architecture decisions

- Engine, Game and Editor are separate projects so the shipped game never pulls in ImGui. *Why: spec rule.*
- `SceneRunner` is shared by Game, Editor edit-mode drawing and play mode. *Why: play mode behaves like the shipped game.*
- `Transform` stores local TRS and composes world values without shear (2D). *Why: cheap, exact for uniform parent scale, and it can be widened to 3D.*
- Undo commands live in Engine. *Why: the spec wants xUnit undo/redo tests in the Engine test project.*
- Play runs a JSON copy; Stop discards it. *Why: "Stop restores the snapshot exactly" holds by construction and the undo history stays valid (history holds references to the edited scene's objects).*
- Textures load from built MGCB output in both Game and Editor; scene files keep the source path with extension. *Why: developer decision (the pipeline is the shipped format); keeping source paths means the Assets panel, scene files and the pipeline agree.* Cost: new textures must be added to `Content.mgcb` by hand (TODO: automate, for example by appending an entry when a texture is dropped into the editor).
- Both Game and Editor build `Content.mgcb`. *Why: the Editor needs the built assets next to its own exe; building twice is cheap while content is small.*
- .NET 10 and MonoGame 3.8.6 instead of the spec's .NET 8 / 3.8.x. *Why: developer asked to try .NET 10 and noted 3.8.6.*
- Editor references the Game project. *Why: spec; used to discover game components.*
- Inspector undo uses `EditTracker`: a change starts an edit session that is committed when no widget is active and the mouse is up. *Why: one undo step per drag or typing session without per-widget bookkeeping.*
