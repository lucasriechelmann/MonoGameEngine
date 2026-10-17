# Feature map

> All six spec milestones are implemented, plus project agnostic opening. Build and 76 tests pass; editor interactions are not covered by automated tests and were only smoke-run.

## Runtime (Engine)

| Feature | What it does | Owner |
|---|---|---|
| Scene graph | Entities with parent/children, stable Guids, id lookup | `src/MonoGameEngine.Engine/` (`Scene`, `Entity`) |
| Components and lifecycle | Awake, Start, Update, Draw, Destroy hooks; enable/disable; deferred destroy | `Component`, `SceneRunner` |
| Transform hierarchy | Local and world position, rotation, scale | `Transform` |
| Sprites and camera | Textured sprites with tint, pivot and layer depth; 2D camera | `SpriteRenderer`, `Camera2D` |
| Scene running | One runner for the game, editor play mode and editor drawing | `SceneRunner` |
| Scene files | JSON save/load with polymorphic components, nested entities, relative asset paths | `Serialization/` |
| Asset loading | Built (MGCB) textures looked up by source path | `AssetManager`, `content/Content.mgcb` |
| Inspector metadata | Attributes and reflection lists shared by serializer and Inspector | `Attributes.cs`, `Reflection/` |
| Sprite sheet animation | Plays a grid of frames from a sprite sheet | `Animation/SpriteAnimator` |
| Physics | Rigid bodies, box/circle colliders, triggers, scene gravity, collision callbacks | `Physics/` |
| Undo/redo model | Command pattern for property, create, delete, reparent, add/remove component | `Commands/` |
| Logging | Static log with event for the Console | `Log.cs` |

## Game

| Feature | What it does | Owner |
|---|---|---|
| Run a scene | Loads `Scenes/main.scene.json` and runs it | `src/MonoGameEngine/` (entry `GameApp.cs`) |
| Sample component | `Spinner` rotates an entity | `src/MonoGameEngine/` |

## Editor

| Feature | What it does | Owner |
|---|---|---|
| ImGui backend | MonoGame renderer, input mapping, docking | `src/MonoGameEngine.Editor/` (`ImGuiRenderer`, `ImGuiInput`) |
| Scene view | Scene in a render target, pan, zoom at cursor, grid, picking, camera frames | `Ui/SceneViewPanel`, `Ui/SceneViewRenderer` |
| Collider outlines | Green box/circle outlines (orange for triggers) in the Scene view, with a toggle | `Ui/SceneViewPanel` |
| Scene settings | Edit the scene name and gravity in the Inspector when nothing is selected | `Ui/InspectorPanel` |
| Gizmo | 2D translate (X, Y, free), one undo step per drag | `Ui/TranslateGizmo` |
| Hierarchy | Tree, select, rename, drag to reparent or reorder, create/duplicate/delete | `Ui/HierarchyPanel` |
| Inspector | Reflection-driven editing, Add/Remove Component, asset field with drop and picker | `Ui/InspectorPanel`, `State/EditTracker` |
| Assets | Browse `content/` with a folder tree (resizable) beside the file grid, thumbnails, drag a texture into the scene or an asset field, double-click a scene to open | `Ui/AssetsPanel`, `State/AssetBrowser` |
| Console | Log capture with level filters, clear, auto-scroll | `Ui/ConsolePanel`, `State/ConsoleLog` |
| Toolbar and menus | Play/Pause/Stop, File/Edit/GameObject/View, shortcuts, unsaved-changes prompt | `Ui/MainMenu` |
| File dialogs | Built-in open and save-as dialog for `.scene.json` | `Ui/FileDialog` |
| Undo/redo | Ctrl+Z / Ctrl+Y over every edit while not playing | `State/EditorState`, Engine `Commands/` |
| Play mode | Runs a JSON copy of the scene; Stop discards it | `State/EditorState` |
| Windows 11 look and icons | Dark Fluent theme, Segoe UI, Material Icons on menus, buttons, tree rows and asset tiles | `Ui/Win11Theme`, `Ui/Icons`, `ImGuiRenderer` |
| Persistence | Last and recent projects, last scene per project, grid flag, window size, proportional panel layout, ImGui layout | `State/EditorSettings`, `Ui/PanelLayout` |
| Open any game project | Open a `.mgeproj`, or a `.sln`/`.slnx`/`.csproj` through the Project Setup dialog (code project and launch project dropdowns, content folder, startup scene); builds in the background, loads the game's components | `State/ProjectSession`, `Ui/ProjectDialogs`, `EditorGame`, Tooling `ProjectFile`/`ProjectSetup` |
| Project menu | Open Project, Open Recent, Project Settings, Rebuild and Reload Game Code (Ctrl+B, keeps the open scene) | `Ui/MainMenu` (`ProjectActions`) |
| Start screen | Shown when no project is open: Open Project and recent list | `Ui/ProjectDialogs` |
| Engine package | `dotnet pack` produces `MonoGameEngine.Engine` for games outside this repo | `src/MonoGameEngine.Engine/MonoGameEngine.Engine.csproj` |

## Known gaps

- One animation clip per `SpriteAnimator`; no animation timeline or state machine.
- Physics: boxes and circles only, no joints, layers or raycasts yet; collider settings apply at scene start.
- No rotate/scale gizmos, multi-select or snapping.
- Closing the window does not prompt for unsaved changes (File > Exit does).
- Textures are cached for the session, and new textures must be added to `content/Content.mgcb` manually.
- Linux and macOS are untested.
- No New Project templates yet (planned next: embedded templates, a platform-neutral core plus heads, a local feed holding the Engine package). Until then a game outside this repo is set up by hand.
- "Launch project" is recorded and used for building content only; there is no Run Game button yet. The Engine package is not shipped with the editor as a feed yet.
- Loading a game whose Engine version differs from the editor's only logs a warning. Unloaded game contexts are freed by the GC, so a reload may keep the old code in memory for a while.
- Panel layout and window size are shared by all projects.

<!-- manual:start -->
<!-- manual:end -->
