using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Sessions;

namespace PiuScoresWatcher.App.Storage;

/// <summary>
///     The sessions the watcher has open on PIU Scores and the closes it still owes (D82), in <c>session.json</c> beside
///     the settings, written whole and swapped into place like them. It is bookkeeping, not the player's choices, so a
///     file that can't be read or written is logged and passed over: PIU Scores' four hours end any session the watcher
///     loses track of.
/// </summary>
public sealed class JsonSessionStore(ILogger<JsonSessionStore> log) : ISessionStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SessionState Load()
    {
        if (!File.Exists(AppPaths.SessionFile))
            return SessionState.Empty;

        try
        {
            var state = JsonSerializer.Deserialize<SessionState>(File.ReadAllText(AppPaths.SessionFile), Options);
            // a file edited by hand may have lost a list
            return state is null ? SessionState.Empty : new SessionState(state.Open ?? [], state.Owed ?? []);
        }
        catch (Exception failure) when (failure is JsonException or IOException or UnauthorizedAccessException)
        {
            log.LogWarning(failure, "Session file {File} could not be read; starting with no session open", AppPaths.SessionFile);
            return SessionState.Empty;
        }
    }

    public void Save(SessionState state)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            var staging = AppPaths.SessionFile + ".tmp";
            File.WriteAllText(staging, JsonSerializer.Serialize(state, Options));
            File.Move(staging, AppPaths.SessionFile, overwrite: true);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            log.LogWarning(failure, "Session file {File} could not be written", AppPaths.SessionFile);
        }
    }
}
