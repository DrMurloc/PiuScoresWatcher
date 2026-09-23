namespace PiuScoresWatcher.Core.Api;

/// <summary>PIU Scores, as far as the watcher needs it: who the token is, and one play at a time.</summary>
public interface IPlaysClient
{
    Task<IdentityCheck> WhoAmIAsync(CancellationToken cancellationToken);

    Task<PostOutcome> PostAsync(ObservedPlay play, CaptureSource source, CancellationToken cancellationToken);
}

/// <summary>Where the personal token lives between launches; the App keeps it DPAPI-encrypted, never in the settings file.</summary>
public interface ITokenStore
{
    string? Load();

    void Save(string token);

    void Clear();
}
