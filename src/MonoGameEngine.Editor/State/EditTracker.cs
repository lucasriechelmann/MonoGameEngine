using System.Reflection;
using ImGuiNET;
using MonoGameEngine.Engine.Commands;

namespace MonoGameEngine.Editor.State;

/// <summary>
/// Turns a stream of live inspector changes into one undo command per edit. A drag across many frames, or
/// typing several characters, is one command: it is recorded once no widget is active and the mouse is up.
/// </summary>
public sealed class EditTracker(EditorState state)
{
    private object? _target;
    private PropertyInfo? _property;
    private object? _oldValue;

    /// <summary>Call after a widget changed <paramref name="property"/> on <paramref name="target"/>.</summary>
    /// <param name="before">The value before this frame's change.</param>
    public void Changed(object target, PropertyInfo property, object? before)
    {
        if (_target == target && _property == property) return; // same edit continues
        Commit();
        _target = target;
        _property = property;
        _oldValue = before;
    }

    /// <summary>Call once per frame after all panels have been drawn.</summary>
    public void Update()
    {
        if (_target == null) return;
        if (ImGui.IsAnyItemActive() || ImGui.IsMouseDown(ImGuiMouseButton.Left)) return;
        Commit();
    }

    private void Commit()
    {
        if (_target == null || _property == null) return;
        var now = _property.GetValue(_target);
        if (!Equals(_oldValue, now)) state.Record(new SetPropertyCommand(_target, _property, _oldValue, now));
        _target = null;
        _property = null;
        _oldValue = null;
    }
}
