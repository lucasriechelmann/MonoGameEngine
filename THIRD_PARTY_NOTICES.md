# Third-party notices

This project is released under the MIT License (see `LICENSE`). It uses the following third-party components.

| Component | Use | License |
|---|---|---|
| [MonoGame](https://monogame.net/) | Game framework and content pipeline (NuGet) | Microsoft Public License (MS-PL) |
| [ImGui.NET](https://github.com/ImGuiNET/ImGui.NET) / [Dear ImGui](https://github.com/ocornut/imgui) | Editor UI (NuGet) | MIT |
| [Aether.Physics2D](https://github.com/nkast/Aether.Physics2D) | 2D rigid-body physics used by `Rigidbody2D` and the colliders (NuGet, Engine only) | Microsoft Public License (MS-PL) |
| [Material Icons](https://github.com/google/material-design-icons) | Icon font `MaterialIcons-Regular.ttf` in `src/MonoGameEngine.Editor/Assets/Fonts/` | Apache License 2.0 (copy in `LICENSE-MaterialIcons.txt`) |
| xUnit | Unit tests (NuGet) | Apache License 2.0 |

The editor also uses **Segoe UI** when it is installed (Windows). The font is read from the system at run time and is
**not** included in this repository. Without it, the editor falls back to ImGui's built-in font.
