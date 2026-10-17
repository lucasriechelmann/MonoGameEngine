# Task: Build a MonoGame game + ImGui editor solution

You are building a small, Unity-inspired visual editor for MonoGame using ImGui.NET.
Work in milestones. After each milestone: make sure the solution builds and runs,
summarize what was done, list any deviations from this spec, and STOP for my review
before starting the next one.

## Tech constraints
- .NET 8 (LTS), C# latest, nullable enabled, file-scoped namespaces.
- MonoGame 3.8.x DesktopGL (cross-platform), latest stable NuGet packages.
- ImGui.NET (latest stable). Use docking if the package version supports it;
  otherwise use a fixed panel layout and tell me.
- System.Text.Json for all serialization. No other third-party dependencies
  unless you ask me first.
- 2D first, but don't make design choices that block 3D later
  (e.g. Transform should be extendable).

## Solution structure
```
MyGame.sln
├── src/
│   ├── MyGame.Engine/     # Runtime core: scene, entities, components, systems, serialization
│   ├── MyGame/            # The shipped game. References Engine only. Zero editor code.
│   └── MyGame.Editor/     # Editor app. References Engine + MyGame. All ImGui code lives here.
├── content/               # Shared assets (MGCB project + raw files)
└── tests/
    └── MyGame.Engine.Tests/  # xUnit: serialization round-trips, undo/redo, component lifecycle
```
Rule: the game must build and run with no reference to the editor or ImGui.

## Engine design (MyGame.Engine)
- Unity-like model: `Scene` → `Entity` (Guid Id, Name, Active, parent/children)
  → `Component` (abstract, with `OnAwake`, `OnStart`, `OnUpdate(GameTime)`,
  `OnDraw(SpriteBatch)`, `OnDestroy`).
- Built-in components: `Transform` (Position, Rotation, Scale, local/world with
  parent hierarchy), `SpriteRenderer` (texture asset path, tint, origin, layer depth),
  `Camera2D` (zoom, position, view matrix).
- Inspector attributes defined in Engine (no editor dependency): `[Range(min,max)]`,
  `[Tooltip("...")]`, `[ReadOnly]`, `[HideInInspector]`, `[Header("...")]`.
- Scene serialization to JSON with polymorphic components (type discriminator),
  stable Guids, and asset references stored as relative paths.
- `SceneRunner` that the game and the editor's play mode both use.

## Editor design (MyGame.Editor)
- `ImGuiRenderer` for MonoGame (font atlas texture, vertex/index buffers,
  input mapping including keyboard, text input, mouse wheel).
- Render the game scene into a `RenderTarget2D` and show it in a "Scene" window
  via `ImGui.Image`, with correct mouse-to-world mapping inside that window.
- Panels:
  1. **Hierarchy**: tree of entities, select, rename, drag-to-reparent,
     create/delete/duplicate via context menu.
  2. **Inspector**: reflection-driven. Draws widgets for float, int, bool, string,
     Vector2, Color, enums, and asset paths. Respects the attributes above.
     "Add Component" button that lists all Component subclasses.
  3. **Scene view**: pan (middle mouse), zoom (wheel), click to select,
     2D translate gizmo on the selected entity, optional grid.
  4. **Assets**: browse the content folder, drag a texture onto the scene
     to create an entity with a SpriteRenderer.
  5. **Console**: captures engine log messages with level filtering.
- Toolbar: Play / Pause / Stop. Play snapshots the scene to JSON, runs it;
  Stop restores the snapshot exactly.
- Undo/redo via a command pattern. Every inspector edit, gizmo drag (one command
  per drag, not per frame), create/delete/reparent is undoable. Ctrl+Z / Ctrl+Y.
- File menu: New / Open / Save / Save As scene. Remember the last opened scene
  and window layout between sessions.

## Milestones
1. Solution skeleton, all three projects building, MonoGame window in the editor
   with ImGui demo window rendering and input working.
2. Engine core: entities, components, Transform hierarchy, SpriteRenderer, SceneRunner.
   Game project loads and runs a hard-coded test scene.
3. JSON serialization + tests (round-trip a scene with nested entities and every
   built-in component). Game loads a scene file instead of the hard-coded one.
4. Editor panels: Scene view (render target), Hierarchy, Inspector (reflection +
   attributes), Console.
5. Selection, picking, translate gizmo, camera pan/zoom, Assets panel with drag-to-create.
6. Undo/redo + tests, Play/Pause/Stop with snapshot restore, file menu, persisted settings.

## Quality bar
- Small, focused classes. Editor drawing code separated from editor state
  (selection, command history).
- No per-frame allocations in the hot path of the renderer or the update loop
  where avoidable.
- XML doc comments on public Engine APIs.
- A README with how to build, run the game, run the editor, and how to add a
  new component so it appears in the Inspector.

Before writing code, give me a short plan for milestone 1 and any questions
where my answer would change the architecture. Then start.
