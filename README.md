# MonoGameEngine

A small, Unity-inspired 2D scene editor for [MonoGame](https://monogame.net/), built with
[ImGui.NET](https://github.com/ImGuiNET/ImGui.NET). It comes with a runtime engine library, a sample game, and a
docking editor with Hierarchy, Inspector, Scene view, Assets and Console panels.

- **Scene / Entity / Component** model with a parent-child hierarchy and a lifecycle
  (`OnAwake`, `OnStart`, `OnUpdate`, `OnDraw`, `OnDestroy`).
- **Reflection-driven Inspector**: write a component, add a few attributes, and it shows up in the editor.
- **JSON scenes** with stable ids, nested entities and polymorphic components.
- **Undo / redo** for every edit, **Play / Pause / Stop** with an exact restore, and persisted layout.
- The game has **no reference to the editor or ImGui**.

> Status: all milestones from the original spec are implemented. Windows is the verified platform; Linux and macOS
> are untested. Built on .NET 10 and MonoGame 3.8.6 (DesktopGL).

## Contents

- [Getting started](#getting-started)
- [Solution layout](#solution-layout)
- [Using the editor](#using-the-editor)
- [Add your own component](#add-your-own-component)
- [Adding textures](#adding-textures)
- [Scene file format](#scene-file-format)
- [Architecture notes](#architecture-notes)
- [Known limitations](#known-limitations)

## Getting started

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone <your-repo-url>
cd <your-repo>
dotnet tool restore          # installs dotnet-mgcb for the content pipeline
dotnet build MonoGameEngine.sln
```

Run the editor, the game, or the tests:

```bash
dotnet run --project src/MonoGameEngine.Editor   # the editor
dotnet run --project src/MonoGameEngine          # the sample game
dotnet test                                      # unit tests
```

The editor reopens your last scene. Settings and the window layout are stored in `%APPDATA%/MonoGameEngine/`
(`editor-settings.json` and `imgui.ini`). Delete `imgui.ini` to reset the layout.

## Solution layout

| Project | Path | Role |
|---|---|---|
| `MonoGameEngine.Engine` | `src/MonoGameEngine.Engine/` | Runtime core: scene, entities, components, serialization, `SceneRunner`, undo commands |
| `MonoGameEngine` | `src/MonoGameEngine/` | The sample game. Loads `Scenes/main.scene.json`. No editor or ImGui code |
| `MonoGameEngine.Editor` | `src/MonoGameEngine.Editor/` | The editor. All ImGui code lives here |
| `MonoGameEngine.Engine.Tests` | `tests/MonoGameEngine.Engine.Tests/` | xUnit tests: serialization round trips, undo/redo, lifecycle, transforms |
| `content/` | `content/` | Source textures, `Content.mgcb` (MonoGame content pipeline), and scene files |

Dependencies: `Game -> Engine`, `Editor -> Engine + Game`, `Tests -> Engine`. The Engine depends only on MonoGame and
`System.Text.Json`.

## Using the editor

| Action | How |
|---|---|
| Pan / zoom the Scene view | Middle mouse drag / mouse wheel (zooms at the cursor). `F` focuses the selection |
| Select | Click in the Scene view or the Hierarchy |
| Move | Drag the gizmo arrows (X red, Y green) or the centre square. One drag is one undo step |
| Create | Hierarchy `+ Create`, right-click menu, or the GameObject menu |
| Create a sprite | Drag a texture from the Assets panel onto the Scene view |
| Reparent / reorder | Drag a row onto another row (child) or between rows. World position is kept |
| Rename | Double-click or `F2` in the Hierarchy |
| Duplicate / delete | `Ctrl+D` / `Del`, or the context menu |
| Undo / redo | `Ctrl+Z` / `Ctrl+Y` (or `Ctrl+Shift+Z`) |
| New / open / save | `Ctrl+N` / `Ctrl+O` / `Ctrl+S`, or the File menu |
| Play / Pause / Stop | Buttons in the menu bar |

**Play mode** runs a copy of the scene made from a JSON snapshot. Stop discards the copy, so the scene you were editing
is exactly as it was, and your undo history is kept. Edits made while playing are not undoable and are lost on Stop.

## Add your own component

1. Create a class that derives from `Component` in the **game** project (`src/MonoGameEngine/`). It needs a public
   parameterless constructor.
2. Expose settings as **public properties with a public getter and setter**. They are saved in scene files and drawn in
   the Inspector. Supported types: `float`, `int`, `bool`, `string`, `Vector2`, `Color` and enums.
3. Optionally decorate properties with `[Range(min, max)]`, `[Tooltip("...")]`, `[ReadOnly]`, `[HideInInspector]`,
   `[Header("...")]`, or `[AssetPath]` (a string holding a texture path).
4. Override the lifecycle hooks you need with `protected override`: `OnAwake`, `OnStart`, `OnUpdate(GameTime)`,
   `OnDraw(SpriteBatch)`, `OnDestroy`.

```csharp
public sealed class Spinner : Component
{
    [Range(-720, 720), Tooltip("Rotation speed in degrees per second.")]
    public float DegreesPerSecond { get; set; } = 90f;

    protected override void OnUpdate(GameTime gameTime) =>
        Entity.Transform.Rotation += MathHelper.ToRadians(DegreesPerSecond) * (float)gameTime.ElapsedGameTime.TotalSeconds;
}
```

Rebuild, and it appears under **Add Component** in the Inspector. The editor finds components in the Engine and in
the game assembly. Computed or editor-only properties should be get-only or marked
`[System.Text.Json.Serialization.JsonIgnore]` so they are not saved.

## Adding textures

Textures go through the MonoGame content pipeline, and scene files store the **source path**, for example
`Textures/crate.png`.

1. Put the image in `content/Textures/`.
2. Add it to `content/Content.mgcb` by copying an existing `#begin Textures/...` block and changing the path
   (or use the MGCB Editor: `dotnet mgcb-editor content/Content.mgcb`).
3. Rebuild. The texture then appears in the Assets panel and can be dragged into the Scene view.

If a texture is not listed in `Content.mgcb`, the Console shows a warning when something tries to use it.

## Scene file format

Scenes are JSON files (`*.scene.json`). Entities nest their `children` and keep their `id`. Components are objects
with a `$type` (the full class name) followed by their properties.

```json
{
  "version": 1,
  "name": "Main",
  "entities": [
    {
      "id": "6f1c0c1e-0000-0000-0000-000000000001",
      "name": "Crate",
      "active": true,
      "components": [
        { "$type": "MonoGameEngine.Engine.Transform", "Enabled": true,
          "Position": { "x": -120, "y": 60 }, "Rotation": 0, "Scale": { "x": 1.5, "y": 1.5 } },
        { "$type": "MonoGameEngine.Engine.SpriteRenderer", "Enabled": true,
          "TexturePath": "Textures/crate.png", "Tint": "#FFFFFFFF",
          "Origin": { "x": 0.5, "y": 0.5 }, "LayerDepth": 0.5 }
      ],
      "children": []
    }
  ]
}
```

Colors are `#RRGGBBAA`, vectors are `{"x":..,"y":..}`, enums are strings. Unknown component types and invalid values are
skipped with a warning in the Console. A full example is in `content/Scenes/main.scene.json`.

## Architecture notes

- **`SceneRunner`** drives the lifecycle and drawing. The game, the editor's Scene view and Play mode all use it.
- **`Transform`** stores local position, rotation (radians) and scale. World values compose the parent chain without
  shear, and are cached with a dirty flag.
- **Undo / redo** is a command pattern (`Engine/Commands`). It lives in the Engine so it can be unit tested.
  Live edits (dragging a value, moving the gizmo) are recorded as one command per edit, not one per frame.
- **Editor code** is split into `State/` (selection, history, play mode, no drawing) and `Ui/` (ImGui panels).
- Deeper notes and the reasons behind the design decisions are in [`CLAUDE.md`](CLAUDE.md) and
  [`.claude/knowledge/`](.claude/knowledge/).

## Known limitations

- Translate gizmo only: no rotate or scale gizmos, multi-select or snapping.
- Closing the window does not prompt about unsaved changes (File > Exit does).
- Textures are cached for the session, and new textures must be added to `content/Content.mgcb` by hand.
- Linux and macOS are untested.
