using System.Globalization;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Scoring;

namespace PiuScoresWatcher.App;

/// <summary>
///     Every string a player reads, in one place (D36). The owner writes this copy; what is here is the
///     mocks' placeholder text until he does. XAML binds with <c>{x:Static app:Copy.Name}</c>, code formats
///     through the methods below, and nothing player-facing is spelled anywhere else.
/// </summary>
public static class Copy
{
    public const string AppName = "PIU Scores Watcher";

    // ---- First run ----
    public const string FirstRunHeadline = "Set up in three steps";
    public const string FirstRunLede = "Two minutes, once. After this it just runs.";
    public const string TokenLabel = "Your PIU Scores token";
    public const string Connect = "Connect";
    public const string Checking = "Checking…";
    public const string TokenHelp = "Create one on the PIU Scores site under Account → API tokens. It is stored encrypted on this PC and only ever sent to PIU Scores.";
    public const string TokenNotAccepted = "That token wasn't accepted. Check it on the site and paste it again.";
    public const string TokenUnchecked = "Couldn't reach PIU Scores to check the token. Try again in a moment.";
    public const string ModeQuestion = "How should it watch?";
    public const string ModeGame = "The game window";
    public const string ModeGameDetail = "Looks at RISE once a second while it is running. Nothing else on your screen.";
    public const string ModeSteam = "My Steam screenshots";
    public const string ModeSteamDetail = "Press F12 on the result screen; the screenshot is read. Costs nothing during play.";
    public const string ModeBoth = "Both (recommended)";
    public const string ModeBothDetail = "A screen seen twice is still one play.";
    public const string StartWithWindows = "Start with Windows";
    public const string StartWithWindowsDetail = "Sits in the tray, wakes when RISE starts, sleeps when it closes. Off, it only runs when you open it.";
    public const string PrivacyLink = "What it looks at and sends";
    public const string Done = "Done";

    // ---- Settings ----
    public const string SectionAccount = "ACCOUNT";
    public const string SectionWatching = "WATCHING";
    public const string SectionStartup = "STARTUP";
    public const string SectionNotifications = "NOTIFICATIONS";
    public const string SectionRecent = "RECENT";
    public const string Disconnect = "Disconnect";
    public const string ModeSteamShort = "Steam screenshots";
    public const string ModeBothShort = "Both";
    public const string Change = "Change";
    public const string UseDetected = "Use the one found";
    public const string StartWithWindowsShort = "In the tray at sign-in; wakes when RISE starts.";
    public const string ShowNotifications = "Show notifications";
    public const string NotifyRecorded = "Every recorded play";
    public const string NotifyNotRecorded = "A play PIU Scores didn't take";
    public const string NotifyUnreadable = "A screen that couldn't be read";
    public const string NotifyTokenRejected = "When the token stops working";
    public const string NotifyUpdated = "When the watcher updates";
    public const string NoPlaysYet = "No plays yet since the watcher started.";
    public const string Review = "Review";
    public const string OpenLogs = "Open logs folder";
    public const string Quit = "Quit";
    public const string Pause = "Pause";
    public const string Resume = "Resume";

    // ---- Tray ----
    public const string TrayOpenSettings = "Open settings";
    public const string TrayPause = "Pause watching";
    public const string TrayResume = "Resume watching";
    public const string TrayOpenSite = "Open PIU Scores";

    // ---- Status: the tray's first line and the settings window's header ----
    public const string StatusWatching = "Watching — RISE is running";
    public const string StatusWaiting = "Waiting for RISE";
    public const string StatusPaused = "Paused";
    public const string StatusNotConnected = "Not connected — plays are kept, not posted";

    // ---- Notifications ----
    public const string RecordedTitle = "Recorded";
    public const string UnreadableTitle = "Couldn't read that result screen";
    public const string UnreadableBody = "Saved it. Nothing was recorded.";
    public const string NotRecordedTitle = "PIU Scores didn't accept this play";
    public const string TokenRejectedTitle = "Your token stopped working";
    public const string TokenRejectedBody = "Plays aren't being recorded. Reconnect in settings.";
    public const string Ignore = "Ignore";
    public const string OpenSettings = "Open settings";

    // ---- Review ----
    public const string ReviewUnreadableTitle = "This screen couldn't be read";
    public const string ReviewNotRecordedTitle = "This play wasn't recorded";
    public const string ReviewStaysHere = "It stays on this PC. Nothing is sent anywhere unless you choose to.";
    public const string ReviewNothing = "Nothing to review.";
    public const string ShowFile = "Show the file";
    public const string Delete = "Delete";
    public const string Previous = "Previous";
    public const string Next = "Next";
    public const string Privacy = "Privacy";

    // ---- Sentences with numbers in them ----
    public static string ConnectedAs(string name) => $"Connected as {name}";

    public static string StatusDetail(DateTimeOffset? lastRecorded, int playsToday, DateTimeOffset now)
    {
        var last = lastRecorded is { } at ? $"Last recorded {Ago(at, now)}" : "Nothing recorded yet";
        return $"{last} · {playsToday} {(playsToday == 1 ? "play" : "plays")} today";
    }

    public static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var span = now - at;
        if (span < TimeSpan.FromMinutes(1))
            return "just now";
        if (span < TimeSpan.FromHours(1))
            return $"{(int)span.TotalMinutes} min ago";
        return span < TimeSpan.FromDays(1)
            ? $"{(int)span.TotalHours} h ago"
            : at.LocalDateTime.ToString("MMM d", CultureInfo.CurrentCulture);
    }

    public static string FolderLine(bool chosen, bool exists) =>
        chosen ? "Steam screenshots folder · chosen" : exists ? "Steam screenshots folder · found" : "Steam screenshots folder · not found yet";

    public static string ToReview(int count) => count == 1 ? "1 screen to review" : $"{count} screens to review";

    public static string VersionLine(string version, bool? upToDate) => upToDate switch
    {
        true => $"Version {version} · up to date",
        false => $"Version {version} · an update applies at the next start",
        null => $"Version {version}"
    };

    public static string UpdatedTitle(string version) => $"Updated to {version}";

    public static string Score(int score) => score.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>The chart as the game names it: <c>5K S18</c>, <c>6K HD23</c>, <c>Arcade D18</c>.</summary>
    public static string ChartLabel(RiseMix mix, ChartType type, int level) => (mix, type) switch
    {
        (RiseMix.Rise, ChartType.Single) => $"5K S{level}",
        (RiseMix.Rise, ChartType.HalfDouble) => $"6K HD{level}",
        (RiseMix.RiseArcade, ChartType.Single) => $"Arcade S{level}",
        (RiseMix.RiseArcade, ChartType.Double) => $"Arcade D{level}",
        _ => $"{type} {level}"
    };

    /// <summary>The award as the station names it: RISE mode's marks, the Arcade Station's plates.</summary>
    public static string AwardName(RiseMix mix, Award award) => (mix, award) switch
    {
        (RiseMix.Rise, Award.UltimateGame) => "Full Combo",
        (RiseMix.Rise, Award.SuperbGame) => "No Miss",
        (_, Award.PerfectGame) => "Perfect Game",
        (_, Award.UltimateGame) => "Ultimate Game",
        (_, Award.ExtremeGame) => "Extreme Game",
        (_, Award.SuperbGame) => "Superb Game",
        (_, Award.MarvelousGame) => "Marvelous Game",
        (_, Award.TalentedGame) => "Talented Game",
        (_, Award.FairGame) => "Fair Game",
        _ => "Rough Game"
    };

    /// <summary>One play in a line: song · chart · score · grade · award, and "broken" for a grey grade.</summary>
    public static string PlayLine(ObservedPlay play)
    {
        var parts = new List<string> { play.SongName, ChartLabel(play.Mix, play.ChartType, play.Level), Score(play.Score), Grades.Of(play.Mix, play.Score) };
        if (Awards.Of(play.Mix, play.Judgments, play.IsBroken) is { } award)
            parts.Add(AwardName(play.Mix, award));
        if (play.IsBroken)
            parts.Add("broken");
        return string.Join(" · ", parts);
    }

    public static string NotRecordedBody(PostOutcome outcome) => outcome switch
    {
        PostOutcome.Refused { ProblemType: "judgments-do-not-reconcile" } => "The judgments don't add up to the score — probably a misread. Kept for review.",
        PostOutcome.Refused refused => $"PIU Scores refused it ({refused.ProblemType}). Kept for review.",
        PostOutcome.SongUnknown => "PIU Scores doesn't know that song on this mix — probably a misread title. Kept for review.",
        PostOutcome.RateLimited => "PIU Scores asked it to slow down. Kept for review.",
        _ => "Couldn't reach PIU Scores. Kept for review."
    };

    public static string ReviewTitle(bool notRecorded) => notRecorded ? ReviewNotRecordedTitle : ReviewUnreadableTitle;

    public static string ReviewDetail(string reason) => $"What happened: {reason}.";

    public static string ReviewSeen(string source, int width, int height, DateTimeOffset seenAt) =>
        $"{source} · {width}×{height} · {seenAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)}";

    public static string SourceName(CaptureSource source) => source switch
    {
        CaptureSource.GameWindow => "Game window",
        CaptureSource.SteamScreenshot => "Steam screenshot",
        _ => "Replay"
    };

    public static string ReviewPosition(int index, int count) => $"{index} of {count}";

    public static string DeleteAll(int count) => count == 1 ? Delete : $"Delete all {count}";
}
