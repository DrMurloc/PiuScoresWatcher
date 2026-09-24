using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.Core.Settings;

namespace PiuScoresWatcher.App.Storage;

/// <summary>
///     The settings file, written whole and swapped into place so a crash mid-write leaves the
///     previous file rather than half of a new one. An unreadable file falls back to the defaults
///     and is logged, never thrown: the watcher starting matters more than one bad edit.
/// </summary>
public sealed class JsonSettingsStore(ILogger<JsonSettingsStore> log) : ISettingsStore
{
    public event EventHandler<WatcherSettings>? Changed;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public WatcherSettings Load()
    {
        if (!File.Exists(AppPaths.SettingsFile))
            return WatcherSettings.Default;

        try
        {
            return JsonSerializer.Deserialize<WatcherSettings>(File.ReadAllText(AppPaths.SettingsFile), Options)
                   ?? WatcherSettings.Default;
        }
        catch (Exception failure) when (failure is JsonException or IOException)
        {
            log.LogWarning(failure, "Settings file {File} could not be read; using the defaults", AppPaths.SettingsFile);
            return WatcherSettings.Default;
        }
    }

    public void Save(WatcherSettings settings)
    {
        Directory.CreateDirectory(AppPaths.Root);
        var staging = AppPaths.SettingsFile + ".tmp";
        File.WriteAllText(staging, JsonSerializer.Serialize(settings, Options));
        File.Move(staging, AppPaths.SettingsFile, overwrite: true);
        Changed?.Invoke(this, settings);
    }
}
