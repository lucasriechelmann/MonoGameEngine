using MonoGameEngine.Engine;

namespace MonoGameEngine.Editor.State;

/// <summary>One captured log line.</summary>
public readonly record struct ConsoleEntry(DateTime Time, LogLevel Level, string Text);

/// <summary>Captures <see cref="Log"/> messages for the Console panel.</summary>
public sealed class ConsoleLog : IDisposable
{
    private const int MaxEntries = 2000;
    private readonly List<ConsoleEntry> _entries = new();

    /// <summary>Starts capturing.</summary>
    public ConsoleLog() => Log.Message += OnMessage;

    /// <summary>Captured lines, oldest first.</summary>
    public IReadOnlyList<ConsoleEntry> Entries => _entries;

    /// <summary>Incremented whenever lines are added, so the panel can auto-scroll.</summary>
    public int Version { get; private set; }

    /// <summary>Number of lines with the given level.</summary>
    public int Count(LogLevel level)
    {
        int n = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Level == level) n++;
        }
        return n;
    }

    /// <summary>Removes all lines.</summary>
    public void Clear()
    {
        _entries.Clear();
        Version++;
    }

    /// <inheritdoc />
    public void Dispose() => Log.Message -= OnMessage;

    private void OnMessage(LogLevel level, string text)
    {
        if (_entries.Count >= MaxEntries) _entries.RemoveRange(0, MaxEntries / 4);
        _entries.Add(new ConsoleEntry(DateTime.Now, level, text));
        Version++;
    }
}
