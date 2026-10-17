# Conventions

> Derived from the Milestone 1-6 code. Competing patterns: none so far.

## Language and style

- C# latest, nullable enabled, file-scoped namespaces, implicit usings, .NET 10. Shared settings live in `Directory.Build.props`; do not repeat TargetFramework, Nullable or the MonoGame version per project.
- Primary constructors for small classes (`SceneRunner`, panels, `CommandHistory`). Collection expressions and pattern matching are used freely.
- XML doc comments on all public Engine APIs. Reference: `src/MonoGameEngine.Engine/Scene.cs`.

## Engine

- Components derive from `Component`, have a public parameterless constructor, and override lifecycle hooks with `protected override` (they are `protected internal virtual` in the base). Reference: `src/MonoGameEngine/Spinner.cs`.
- Serialized and inspectable settings are public read/write properties. Computed or runtime-only properties are get-only or `[JsonIgnore]`. Reference: `src/MonoGameEngine.Engine/Transform.cs` (world values are `[JsonIgnore]`).
- Inspector metadata uses the Engine attributes; never reference ImGui from Engine or Game. Reference: `src/MonoGameEngine.Engine/SpriteRenderer.cs`.
- Asset references are source paths relative to `content/` with forward slashes (`AssetManager.Normalize`), for example `Textures/crate.png`. The asset must also be listed in `content/Content.mgcb`.
- Update and draw loops use index `for` loops over `IReadOnlyList`, with no LINQ or allocations. Reference: `SceneRunner.cs`. Destroying entities during update is deferred with `Entity.Destroy()` and `Scene.FlushDestroyed()`.
- Runtime-only component state (velocities, current frame) is not saved: use a get-only or private-set property, or `[JsonIgnore]` (see `Physics/Rigidbody2D.cs`). Keep third-party types (Aether) internal to the Engine.
- Log with `Log.Info/Warning/Error`, not `Console`. Failures while loading assets or scenes are logged and recovered, not thrown to the frame loop.

## Editor

- State (`State/`) holds data and logic and has no ImGui drawing; panels (`Ui/`) only draw and call state methods. Reference: `State/EditorState.cs` vs `Ui/HierarchyPanel.cs`.
- Every user change is a command from `MonoGameEngine.Engine.Commands`. Use `state.Execute(cmd)` for a change not yet applied and `state.Record(cmd)` for one already applied live. Both skip history while playing.
- Live inspector edits go through `EditTracker.Changed(target, property, before)` and then the value is set; do not push a command per frame. Reference: `Ui/InspectorPanel.cs`.
- A gizmo drag changes the value live and records one `SetPropertyCommand` on release. Reference: `Ui/TranslateGizmo.cs`.
- Panels call `PanelLayout.Place(name, x, y, w, h)` before `ImGui.Begin` and `PanelLayout.Remember(name)` right after it, and use stable window names (they are keys in `imgui.ini` and `PanelLayout`). Reference: `Ui/HierarchyPanel.cs`.
- Drag and drop uses `Ui.SetPayload`/`Ui.AcceptPayload` with a payload type constant and keeps the real data in the panel or `EditorState` (`DraggedAsset`).
- Editor buffers that touch the GPU are created once and reused (`ImGuiRenderer.cs`, `SceneViewRenderer.EnsureTarget`). `ImGuiInput` forwards nothing while the window is unfocused.
- Icons are Material Icons string constants in `Ui/Icons.cs`, concatenated with a label (`Icons.Save + "  Save"`). Adding a constant makes `BuildGlyphRanges` load it. Large tiles push `ImGuiRenderer.LargeIcons`. Reference: `Ui/AssetsPanel.cs`.
- The editor never references a game type at compile time. Anything about the open game goes through `ProjectSession` (assembly, `AssetManager`); switching or rebuilding recreates `EditorState` and the panels in `EditorGame.CreateEditor`, so panels must not cache state across projects. Reference: `EditorGame.cs`.
- Project logic that is not drawing (file format, solution reading, defaults, load context) goes in `MonoGameEngine.Tooling` so it can be unit tested. Reference: `Projects/ProjectSetup.cs`, `tests/.../ProjectTests.cs`.
- Long work (builds) runs on a background task inside a session and is applied on the main thread from `Poll()`; do not call `Log` or touch ImGui or the GPU from the task.
- XNA and System.Numerics both define `Vector2`: panels alias `System.Numerics` as `Num` and convert with `ToNum()` / `ToXna()` from `Ui/Ui.cs`.

## Testing

- Physics and animation are tested without a graphics device by driving `SceneRunner.Update` with a fixed `GameTime` (`AnimationAndPhysicsTests.cs`).
- xUnit in `tests/MonoGameEngine.Engine.Tests/`: serialization round trips (`SerializationTests.cs`), commands (`CommandTests.cs`), lifecycle (`LifecycleTests.cs`), hierarchy and transforms (`SceneAndTransformTests.cs`), projects (`ProjectTests.cs`: temp-folder fixtures for solutions and project files). Tests that define custom components call `ComponentRegistry.RegisterAssembly`.
- Editor code has no automated tests; test editor logic by keeping it in Engine or `State/`.

<!-- manual:start -->
<!-- manual:end -->
