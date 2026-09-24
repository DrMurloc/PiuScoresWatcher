using System.Globalization;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Scoring;
using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.App;

/// <summary>
///     Every string a player reads, in one place (D36). The owner writes this copy; what is here is the
///     mocks' placeholder text until he does. XAML binds with <c>{x:Static app:Copy.Name}</c>, code formats
///     through the methods below, and nothing player-facing is spelled anywhere else.
/// </summary>
public static class Copy
{
    public const string AppName = "PIU Scores Watcher";

    /// <summary>The name on a window and in the tray tooltip; a dev run against another site adds the site (D44).</summary>
    public static string AppNameFor(SiteScope scope) => scope.IsProduction ? AppName : $"{AppName} · {scope.Label}";

    /// <summary>A window with a name of its own: <c>PIU Scores Watcher · Bulk capture</c>.</summary>
    public static string AppNameFor(SiteScope scope, string window) => $"{AppNameFor(scope)} · {window}";

    // ---- First run ----
    public const string FirstRunHeadline = "Set up in three steps";
    public const string FirstRunLede = "Two minutes, once. After this it just runs.";
    public const string TokenLabel = "Your PIU Scores token";
    public const string Connect = "Connect";
    public const string Checking = "Checking…";

    /// <summary>{0} is the player's name, which is set in bold.</summary>
    public const string ConnectedAs = "Connected as {0}";

    public const string ConnectedUnchecked = "Token saved — PIU Scores hasn't answered yet";

    /// <summary>{0} is <see cref="TokenPageLink" />, set as the link to the token page.</summary>
    public const string TokenHelp = "Create one on {0}. It is stored encrypted on this PC and only ever sent to PIU Scores.";
    public const string TokenNotAccepted = "That token wasn't accepted. Check it on the site and paste it again.";
    public const string TokenUnchecked = "Couldn't reach PIU Scores to check the token. Try again in a moment.";
    public const string ModeQuestion = "How should it watch?";
    public const string ModeGame = "The game window";
    public const string ModeGameDetail = "Looks at RISE once a second while it is running. Nothing else on your screen.";
    public const string ModeSteam = "My Steam screenshots";
    public const string ModeSteamDetail = "Press F12 on the result screen; the screenshot is read. Costs nothing during play.";
    public const string Recommended = "(recommended)";
    public const string ModeBothDetail = "A screen seen twice is still one play.";
    public const string StartWithWindows = "Start with Windows";
    public const string StartWithWindowsDetail = "Sits in the tray, wakes when RISE starts, sleeps when it closes. Off, it only runs when you open it.";
    public const string PrivacyLink = "What it looks at and sends";
    public const string NotRecordedShort = "not recorded";
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
    public const string StartWithWindowsShort = "In the tray at sign-in; wakes when RISE starts.";
    public const string ShowNotifications = "Show notifications";
    public const string NotifyRecorded = "Every recorded play";
    public const string NotifyNotRecorded = "A play PIU Scores didn't take";
    public const string NotifyUnreadable = "A screen that couldn't be read";
    public const string NotifyTokenRejected = "When the token stops working";
    public const string NotifyUpdated = "When the watcher updates";
    public const string NoPlaysYet = "No plays yet since the watcher started.";
    public const string NoMark = "—";
    public const string Broken = "broken";
    public const string Review = "Review";
    public const string OpenLogs = "Open logs folder";
    public const string Quit = "Quit";
    public const string Pause = "Pause";
    public const string Resume = "Resume";

    public const string NotifyBulkFinished = "When a bulk capture finishes";

    // ---- Tray ----
    public const string TrayOpenSettings = "Open settings";
    public const string TrayPause = "Pause watching";
    public const string TrayResume = "Resume watching";
    public const string TrayOpenSite = "Open PIU Scores";
    public const string TrayStartBulk = "Start bulk capture…";
    public const string TrayStopBulk = "Stop bulk capture";

    // ---- Bulk capture: the start window (D51) ----
    public const string BulkWindowName = "Bulk capture";
    public const string BulkHeadline = "Grab your Warm Up bests";
    public const string BulkLede = "Each best on Warm Up's song list becomes a play on PIU Scores: the song, the chart and the score, no judgments.";
    public const string BulkStep1 = "Open Warm Up and go to the song list.";

    /// <summary>Each {KEY} is drawn as a key cap: A, D, TAB, W, S.</summary>
    public const string BulkStep2 = "Move through every chart: {A} {D} change the level, {TAB} switches 5K SINGLE and 6K DOUBLE, {W} {S} change the song.";

    public const string BulkStep3 = "Wait for the sound before moving on.";
    public const string SoundChime = "Chime";
    public const string SoundChimeMeaning = "Sent to PIU Scores.";
    public const string SoundTick = "Tick";
    public const string SoundTickMeaning = "PIU Scores already has this best, or a higher one.";
    public const string SoundLow = "Low tone";
    public const string SoundLowMeaning = "Couldn't read it. It's kept for review; move on.";
    public const string BulkChecking = "Checking your bests on PIU Scores…";
    public const string BulkNotConnected = "Connect to PIU Scores in settings first.";
    public const string BulkCouldNotLoad = "Couldn't load your bests from PIU Scores. Try again in a moment.";
    public const string PlaySounds = "Play sounds";
    public const string BulkStopsItself = "It stops by itself when a song starts or RISE closes.";
    public const string Cancel = "Cancel";
    public const string Start = "Start";

    // ---- Bulk capture: settings, Recent, the summary ----
    public const string SectionBulk = "BULK CAPTURE";
    public const string BulkSettingsLine = "Grab your Warm Up bests from the song list.";
    public const string StartEllipsis = "Start…";
    public const string SoundsWhileCapturing = "Sounds while capturing";
    public const string Stop = "Stop";
    public const string BulkRunName = "Bulk capture";
    public const string BulkFinishedTitle = "Bulk capture finished";
    public const string ReviewListTitle = "This best couldn't be read";

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
    public const string NotConnectedTitle = "The watcher isn't connected";
    public const string NotConnectedBody = "Plays are kept, not posted. Connect in settings.";
    public const string Ignore = "Ignore";
    public const string OpenSettings = "Open settings";
    public const string WhatChanged = "What changed";

    // ---- Review ----
    public const string ReviewUnreadableTitle = "This screen couldn't be read";
    public const string ReviewNotRecordedTitle = "This play wasn't recorded";
    public const string ReviewStaysHere = "It stays on this PC. Nothing is sent anywhere unless you choose to.";
    public const string ReviewNothing = "Nothing to review.";
    public const string ShowFile = "Show the file";
    public const string Delete = "Delete";
    public const string Privacy = "Privacy";

    // ---- Sentences with numbers in them ----
    public static string TokenPageLink(Uri site) => $"{site.Host} → Account → API tokens";

    /// <summary>The tray's first line during a run.</summary>
    public static string StatusBulk(BulkTally tally) => $"Bulk capture · {tally.Sent} sent, {tally.Already} already there";

    /// <summary>The settings window's status card during a run.</summary>
    public static string BulkOn(BulkTally tally) => $"Bulk capture on · {tally.Sent} sent";

    public static string BulkDetail(BulkTally tally) => $"{tally.Already} already there · {tally.NotSent} couldn't be read";

    public static string BulkStoredBests(int count) => $"PIU Scores has {count} of your Warm Up bests. Only higher ones are sent.";

    /// <summary>A run's line in Recent, after the bold <see cref="BulkRunName" />.</summary>
    public static string BulkRunDetail(BulkTally tally) => $" · Warm Up · {tally.Sent} sent, {tally.Already} already there";

    public static string BulkSummary(BulkTally tally) =>
        $"{tally.Sent} new bests sent · {tally.Already} already on PIU Scores · {tally.NotSent} couldn't be read";

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

    /// <summary>The amber row under Recent: what is waiting in the review window.</summary>
    public static string ToReview(int unreadable, int notRecorded)
    {
        var parts = new List<string>();
        if (unreadable > 0)
            parts.Add(unreadable == 1 ? "1 screen couldn't be read" : $"{unreadable} screens couldn't be read");
        if (notRecorded > 0)
            parts.Add(notRecorded == 1 ? "1 play wasn't recorded" : $"{notRecorded} plays weren't recorded");
        return string.Join(" · ", parts);
    }

    /// <summary>A Recent row's age: <c>2 min</c>.</summary>
    public static string AgoShort(DateTimeOffset at, DateTimeOffset now)
    {
        var span = now - at;
        if (span < TimeSpan.FromMinutes(1))
            return "now";
        if (span < TimeSpan.FromHours(1))
            return $"{(int)span.TotalMinutes} min";
        return span < TimeSpan.FromDays(1)
            ? $"{(int)span.TotalHours} h"
            : at.LocalDateTime.ToString("MMM d", CultureInfo.CurrentCulture);
    }

    public static string VersionLine(string version, bool? upToDate) => upToDate switch
    {
        true => $"Version {version} · up to date",
        false => $"Version {version} · an update applies at the next start",
        null => $"Version {version}"
    };

    public static string UpdatedTitle(string version) => $"Updated to {version}";

    public static string Score(int score) => score.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>The chart as the game names it: <c>5K S18</c>, <c>6K HD23</c>, <c>Arcade 5K S20</c>.</summary>
    public static string ChartLabel(RiseMix mix, ChartType type, int level) => (mix, type) switch
    {
        (RiseMix.Rise, ChartType.Single) => $"5K S{level}",
        (RiseMix.Rise, ChartType.HalfDouble) => $"6K HD{level}",
        (RiseMix.RiseArcade, ChartType.Single) => $"Arcade 5K S{level}",
        (RiseMix.RiseArcade, ChartType.Double) => $"Arcade 10K D{level}",
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
            parts.Add(Broken);
        return string.Join(" · ", parts);
    }

    public static string NotRecordedBody(PostOutcome outcome) => outcome switch
    {
        PostOutcome.Refused { ProblemType: "judgments-do-not-reconcile" } => "The judgments don't add up to the score — probably a misread. Not recorded.",
        PostOutcome.Refused => "PIU Scores refused it. Not recorded.",
        PostOutcome.SongUnknown => "PIU Scores doesn't know that song on this mix — probably a misread title. Not recorded.",
        PostOutcome.RateLimited => "PIU Scores asked the watcher to slow down. Not recorded.",
        PostOutcome.Unauthorized => "The token wasn't accepted. Not recorded.",
        PostOutcome.NotConnected => "The watcher isn't connected to PIU Scores. Not recorded.",
        _ => "Couldn't reach PIU Scores. Not recorded."
    };

    public static string ReviewTitle(KeptBecause because) =>
        because.WasRead() ? ReviewNotRecordedTitle : because.IsSongList() ? ReviewListTitle : ReviewUnreadableTitle;

    /// <summary>The review window's one sentence on what went wrong.</summary>
    public static string ReviewReason(KeptBecause because) => because switch
    {
        KeptBecause.NumbersUnreadable => "Some of the numbers couldn't be read, so nothing was recorded.",
        KeptBecause.NumbersNotShown => "The screenshot was taken before the numbers finished counting, so nothing was recorded.",
        KeptBecause.NumbersDisagree => "The judgment counts didn't add up to the score, so nothing was recorded.",
        KeptBecause.TitleUnreadable => "The song title couldn't be read, so nothing was recorded.",
        KeptBecause.ListUnreadable => "Some of the numbers couldn't be read, so nothing was sent.",
        KeptBecause.GradeDisagrees => "The grade didn't match the score, so nothing was sent.",
        KeptBecause.TitleUnmatched => "The song title didn't match any song on PIU Scores, so nothing was sent.",
        KeptBecause.Refused => "PIU Scores refused the play, so it wasn't recorded.",
        KeptBecause.SongUnknown => "PIU Scores doesn't know that song on this mix, so it wasn't recorded. The title was probably misread.",
        KeptBecause.TokenRejected => "The token wasn't accepted, so it wasn't recorded.",
        KeptBecause.NotConnected => "The watcher wasn't connected to PIU Scores, so it wasn't recorded.",
        KeptBecause.RateLimited => "PIU Scores asked the watcher to slow down, so it wasn't recorded.",
        _ => "PIU Scores couldn't be reached, so it wasn't recorded."
    };

    /// <summary>Under the review window's picture: <c>Result screen · 1920×1080 · today 5:57 PM · game window</c>, or <c>Song list · …</c>.</summary>
    public static string ReviewSeen(KeptBecause because, CaptureSource source, int width, int height, DateTimeOffset seenAt, DateTimeOffset now)
    {
        var local = seenAt.LocalDateTime;
        var time = local.ToString("t", CultureInfo.CurrentCulture);
        var day = local.Date == now.LocalDateTime.Date ? $"today {time}"
            : local.Date == now.LocalDateTime.Date.AddDays(-1) ? $"yesterday {time}"
            : $"{local.ToString("MMM d", CultureInfo.CurrentCulture)} {time}";
        var screen = because.IsSongList() ? "Song list" : "Result screen";
        return $"{screen} · {width}×{height} · {day} · {SourceName(source)}";
    }

    public static string SourceName(CaptureSource source) => source switch
    {
        CaptureSource.GameWindow => "game window",
        CaptureSource.SteamScreenshot => "Steam screenshot",
        _ => "replay"
    };

    public static string DeleteAll(int count) => $"Delete all {count} unread screens";
}
