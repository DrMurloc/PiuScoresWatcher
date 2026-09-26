using PiuScoresWatcher.Core.Domain;

namespace PiuScoresWatcher.Core.Sessions;

/// <summary>A mix the watcher has a session on at PIU Scores, and when that session last had a play the site recorded.</summary>
[ExcludeFromCodeCoverage]
public sealed record MixSession(RiseMix Mix, DateTimeOffset LastPlayAt);

/// <summary>
///     What the watcher knows about its sessions between launches (D82): the mixes it has posted to since it last ended a
///     session, and the closes it decided on that haven't reached PIU Scores yet.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SessionState(IReadOnlyList<MixSession> Open, IReadOnlyList<MixSession> Owed)
{
    public static SessionState Empty { get; } = new([], []);
}

/// <summary>Where <see cref="SessionState" /> lives between launches; the App keeps it as a JSON file beside the settings.</summary>
public interface ISessionStore
{
    /// <summary>The saved state, or <see cref="SessionState.Empty" /> when nothing is saved or the file can't be read.</summary>
    SessionState Load();

    void Save(SessionState state);
}
