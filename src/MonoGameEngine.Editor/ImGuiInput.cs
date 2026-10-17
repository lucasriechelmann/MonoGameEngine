using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoGameEngine.Editor;

/// <summary>Feeds MonoGame keyboard, text and mouse input into ImGui's event queue.</summary>
public sealed class ImGuiInput
{
    private static readonly (Keys Key, ImGuiKey ImKey)[] KeyMap = BuildKeyMap();

    private KeyboardState _prevKeys;
    private MouseState _prevMouse;

    private bool _wasActive = true;

    public ImGuiInput(GameWindow window)
    {
        window.TextInput += (_, e) => ImGui.GetIO().AddInputCharacterUTF16(e.Character);
    }

    /// <summary>
    /// Call once per frame before <see cref="ImGuiRenderer.BeginFrame"/>. While the window is not
    /// focused, no input is forwarded and ImGui is told it lost focus so held keys are released.
    /// </summary>
    public void Update(bool isActive)
    {
        var io = ImGui.GetIO();

        if (isActive != _wasActive)
        {
            io.AddFocusEvent(isActive);
            _wasActive = isActive;
        }
        if (!isActive)
        {
            _prevKeys = default;
            _prevMouse = Mouse.GetState();
            return;
        }

        var keys = Keyboard.GetState();
        var mouse = Mouse.GetState();

        foreach (var (key, imKey) in KeyMap)
        {
            bool down = keys.IsKeyDown(key);
            if (down != _prevKeys.IsKeyDown(key)) io.AddKeyEvent(imKey, down);
        }
        io.AddKeyEvent(ImGuiKey.ModCtrl, keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl));
        io.AddKeyEvent(ImGuiKey.ModShift, keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift));
        io.AddKeyEvent(ImGuiKey.ModAlt, keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
        io.AddKeyEvent(ImGuiKey.ModSuper, keys.IsKeyDown(Keys.LeftWindows) || keys.IsKeyDown(Keys.RightWindows));

        io.AddMousePosEvent(mouse.X, mouse.Y);
        AddButton(io, 0, mouse.LeftButton, _prevMouse.LeftButton);
        AddButton(io, 1, mouse.RightButton, _prevMouse.RightButton);
        AddButton(io, 2, mouse.MiddleButton, _prevMouse.MiddleButton);

        int wheel = mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue;
        int hwheel = mouse.HorizontalScrollWheelValue - _prevMouse.HorizontalScrollWheelValue;
        if (wheel != 0 || hwheel != 0) io.AddMouseWheelEvent(hwheel / 120f, wheel / 120f);

        _prevKeys = keys;
        _prevMouse = mouse;
    }

    private static void AddButton(ImGuiIOPtr io, int button, ButtonState now, ButtonState before)
    {
        if (now != before) io.AddMouseButtonEvent(button, now == ButtonState.Pressed);
    }

    private static (Keys, ImGuiKey)[] BuildKeyMap()
    {
        var map = new List<(Keys, ImGuiKey)>
        {
            (Keys.Tab, ImGuiKey.Tab), (Keys.Left, ImGuiKey.LeftArrow), (Keys.Right, ImGuiKey.RightArrow),
            (Keys.Up, ImGuiKey.UpArrow), (Keys.Down, ImGuiKey.DownArrow), (Keys.PageUp, ImGuiKey.PageUp),
            (Keys.PageDown, ImGuiKey.PageDown), (Keys.Home, ImGuiKey.Home), (Keys.End, ImGuiKey.End),
            (Keys.Insert, ImGuiKey.Insert), (Keys.Delete, ImGuiKey.Delete), (Keys.Back, ImGuiKey.Backspace),
            (Keys.Space, ImGuiKey.Space), (Keys.Enter, ImGuiKey.Enter), (Keys.Escape, ImGuiKey.Escape),
            (Keys.OemComma, ImGuiKey.Comma), (Keys.OemPeriod, ImGuiKey.Period), (Keys.OemMinus, ImGuiKey.Minus),
            (Keys.OemPlus, ImGuiKey.Equal), (Keys.OemSemicolon, ImGuiKey.Semicolon),
            (Keys.OemQuestion, ImGuiKey.Slash), (Keys.OemTilde, ImGuiKey.GraveAccent),
            (Keys.OemOpenBrackets, ImGuiKey.LeftBracket), (Keys.OemCloseBrackets, ImGuiKey.RightBracket),
            (Keys.OemPipe, ImGuiKey.Backslash), (Keys.OemQuotes, ImGuiKey.Apostrophe),
            (Keys.LeftControl, ImGuiKey.LeftCtrl), (Keys.RightControl, ImGuiKey.RightCtrl),
            (Keys.LeftShift, ImGuiKey.LeftShift), (Keys.RightShift, ImGuiKey.RightShift),
            (Keys.LeftAlt, ImGuiKey.LeftAlt), (Keys.RightAlt, ImGuiKey.RightAlt),
        };
        for (int i = 0; i < 26; i++) map.Add(((Keys)((int)Keys.A + i), (ImGuiKey)((int)ImGuiKey.A + i)));
        for (int i = 0; i < 10; i++) map.Add(((Keys)((int)Keys.D0 + i), (ImGuiKey)((int)ImGuiKey._0 + i)));
        for (int i = 0; i < 12; i++) map.Add(((Keys)((int)Keys.F1 + i), (ImGuiKey)((int)ImGuiKey.F1 + i)));
        return map.ToArray();
    }
}
