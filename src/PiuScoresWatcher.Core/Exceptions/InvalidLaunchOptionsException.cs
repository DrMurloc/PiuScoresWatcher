namespace PiuScoresWatcher.Core.Exceptions;

/// <summary>A command-line switch the watcher does not know, or one missing its value.</summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidLaunchOptionsException(string message) : WatcherException(message);
