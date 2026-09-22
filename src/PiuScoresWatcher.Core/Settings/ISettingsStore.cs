namespace PiuScoresWatcher.Core.Settings;

/// <summary>Where the player's choices persist between launches; the App keeps them as a JSON file.</summary>
public interface ISettingsStore
{
    /// <summary>The saved settings, or the defaults when nothing has been saved yet or the file cannot be read.</summary>
    WatcherSettings Load();

    void Save(WatcherSettings settings);
}
