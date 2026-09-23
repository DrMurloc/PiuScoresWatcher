namespace PiuScoresWatcher.Core.Exceptions;

/// <summary>Judgment counts that cannot be a play: a negative count, or no notes at all.</summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidJudgmentsException(string message) : WatcherException(message);
