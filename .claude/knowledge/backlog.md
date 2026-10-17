# Backlog

> Open work for another day, from the project agnostic editor and New Project milestone. Tick a box and delete the line from `features.md` "Known gaps" when an item is done.

## Features

- [ ] **Run Game button.** Build and start the launch project (the platform head) from the editor, with its output in the Console. Today the launch project is only used to build content. Owners: `State/ProjectSession`, `Ui/MainMenu`, Tooling `ProjectBuilder`.
- [ ] **Android and iOS heads in New Project.** Needs the workloads installed and the platform boilerplate (manifest, activity, app delegate) verified by building and running each. Mobile heads stay launch-only because the desktop editor cannot load them. Owners: `src/MonoGameEngine.Tooling/Templates/`, `ProjectTemplate`, `TemplatePlatform`.
- [ ] **Per-project panel layout and window size.** Both are global today, so every project shares one layout. Owners: `State/EditorSettings`, `Ui/PanelLayout`.

## Limits to fix

- [ ] **Stale Engine package in the NuGet cache.** The cache keys on id and version, so rebuilding the Engine under the same `EngineVersion` can leave older code in already-restored projects. Options: bump the version automatically, or add a content hash to the package version. Owners: `Directory.Build.props`, `PackEngineFeed` target in the Editor csproj.
- [ ] **Old game code stays in memory after a reload.** The collectible load context is freed by the GC, not at once. Check for leaked references (ImGui texture bindings of the old content, caches) and unload deterministically where possible. Owners: `State/ProjectSession.Dispose`, `Ui/AssetsPanel`, `ImGuiRenderer`.
- [ ] **Engine version mismatch only logs a warning.** Decide whether to block opening, or offer to upgrade the project's Engine package reference. Owner: `EditorGame.FinishSession`.
- [ ] **`README.md` still describes the old single-game setup.** Rewrite for opening and creating projects, the `.mgeproj` format and the Engine package.
- [ ] **A bare `dotnet run` in a folder with a `.mgeproj` fails** (MSBuild takes any `*proj` file as a project). `dotnet build` and `dotnet test` are fine. Decide whether to live with it or rename the extension.

## Not yet verified in the running editor

- [ ] Open Recent, Project Settings (changing the code or launch dropdown), and the folder picker's Browse button in New Project.
- [ ] Rebuild and Reload Game Code (Ctrl+B): the open scene and its unsaved changes survive, components of the new code appear, undo history is cleared. Add a unit test around `EditorState.TakeSnapshot` and `RestoreSnapshot` (move the logic into Engine or Tooling if it needs a graphics-free test).
- [ ] The WindowsDX head launched (it only builds so far), and Play in a generated project.
- [ ] Earlier editor interactions that were never checked by hand: gizmo, drag and drop, undo in the UI.
