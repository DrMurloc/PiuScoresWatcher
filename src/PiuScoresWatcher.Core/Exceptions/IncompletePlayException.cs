namespace PiuScoresWatcher.Core.Exceptions;

/// <summary>A reading that is not a postable play: not complete, or without a title.</summary>
[ExcludeFromCodeCoverage]
public sealed class IncompletePlayException(string message) : WatcherException(message);
