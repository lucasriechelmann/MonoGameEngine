namespace MonoGameEngine.Engine;

/// <summary>Severity of a log message.</summary>
public enum LogLevel
{
    /// <summary>Informational message.</summary>
    Info,

    /// <summary>Something unexpected that the engine recovered from.</summary>
    Warning,

    /// <summary>A failure.</summary>
    Error,
}

/// <summary>Engine-wide logging. The editor Console subscribes to <see cref="Message"/>.</summary>
public static class Log
{
    /// <summary>Raised for every message, after it was written to the debug output.</summary>
    public static event Action<LogLevel, string>? Message;

    /// <summary>Logs an informational message.</summary>
    public static void Info(string message) => Write(LogLevel.Info, message);

    /// <summary>Logs a warning.</summary>
    public static void Warning(string message) => Write(LogLevel.Warning, message);

    /// <summary>Logs an error.</summary>
    public static void Error(string message) => Write(LogLevel.Error, message);

    private static void Write(LogLevel level, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
        Message?.Invoke(level, message);
    }
}
