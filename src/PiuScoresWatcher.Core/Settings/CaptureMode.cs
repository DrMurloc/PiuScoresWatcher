namespace PiuScoresWatcher.Core.Settings;

/// <summary>How the watcher sees a result screen — the first choice on the settings window.</summary>
public enum CaptureMode
{
    /// <summary>Look at the RISE window once a second while the game runs.</summary>
    Game,

    /// <summary>Read new files in the RISE Steam-screenshots folder (the player presses F12).</summary>
    SteamScreenshots,

    /// <summary>Both; a screen seen twice is still one play.</summary>
    Both
}
