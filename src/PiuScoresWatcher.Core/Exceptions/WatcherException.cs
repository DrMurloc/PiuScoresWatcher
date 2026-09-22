namespace PiuScoresWatcher.Core.Exceptions;

/// <summary>
///     The base of every exception the watcher raises on purpose. A message here is written to be
///     shown to the player as-is, which is what lets the UI show it; anything else that escapes is
///     logged and reported as a generic sentence, never printed.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class WatcherException(string message) : Exception(message);
