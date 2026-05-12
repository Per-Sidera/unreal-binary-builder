namespace UnrealBinaryBuilder.Core.Logging;

public interface IBuildLogger
{
	void Log(LogLevel level, string message);
}

public static class BuildLoggerExtensions
{
	public static void Trace(this IBuildLogger l, string m) => l.Log(LogLevel.Trace, m);
	public static void Debug(this IBuildLogger l, string m) => l.Log(LogLevel.Debug, m);
	public static void Info(this IBuildLogger l, string m) => l.Log(LogLevel.Info, m);
	public static void Warn(this IBuildLogger l, string m) => l.Log(LogLevel.Warning, m);
	public static void Error(this IBuildLogger l, string m) => l.Log(LogLevel.Error, m);
}

public sealed class NullLogger : IBuildLogger
{
	public static readonly NullLogger Instance = new();
	public void Log(LogLevel level, string message) { }
}

public sealed class ConsoleLogger : IBuildLogger
{
	private readonly LogLevel _minLevel;
	public ConsoleLogger(LogLevel minLevel = LogLevel.Info) => _minLevel = minLevel;

	public void Log(LogLevel level, string message)
	{
		if (level < _minLevel) return;
		var color = Console.ForegroundColor;
		Console.ForegroundColor = level switch
		{
			LogLevel.Error => ConsoleColor.Red,
			LogLevel.Warning => ConsoleColor.Yellow,
			LogLevel.Debug => ConsoleColor.Cyan,
			LogLevel.Trace => ConsoleColor.DarkGray,
			_ => color
		};
		Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {level,-7} {message}");
		Console.ForegroundColor = color;
	}
}
