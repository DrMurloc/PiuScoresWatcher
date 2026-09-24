using System.Globalization;
using System.Resources;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Scoring;
using PiuScoresWatcher.Core.Settings;
using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.App;

/// <summary>
///     Every string a player reads, in one place (D36), in English — the owner's copy; the mocks' wording is
///     approved. XAML binds with <c>{x:Static app:Copy.Name}</c>, code formats through the methods below, and
///     nothing player-facing is spelled anywhere else. Each English line is also the key its translations are
///     looked up by, in <c>Resources/Strings.&lt;code&gt;.resx</c> (D60): <c>L</c> for a line, <c>Plural</c> for a
///     count, <c>Verbatim</c> for what the game prints as-is.
/// </summary>
public static class Copy
{
    public const string AppName = "PIU Scores Watcher";

    private static readonly ResourceManager Translations = new("PiuScoresWatcher.App.Resources.Strings", typeof(Copy).Assembly);
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo(Languages.English);
    private static readonly AsyncLocal<CultureInfo?> Scoped = new();
    private static CultureInfo _culture = EnglishCulture;

    /// <summary>
    ///     The language every line is looked up in and every number and date is written in (D59, D61): the one
    ///     picked in settings, or Windows'. Never the thread's culture, which a capture loop keeps from before a change.
    /// </summary>
    public static CultureInfo Culture => Scoped.Value ?? Volatile.Read(ref _culture);

    /// <summary>Speaks <paramref name="culture" /> from now on; <c>Localization.WatcherLanguage</c> is the one caller.</summary>
    public static void Use(CultureInfo culture)
    {
        Volatile.Write(ref _culture, culture);
    }

    /// <summary>A line as the log writes it: in English, whatever the player reads (D61).</summary>
    public static string English(Func<string> line)
    {
        var before = Scoped.Value;
        Scoped.Value = EnglishCulture;
        try
        {
            return line();
        }
        finally
        {
            Scoped.Value = before;
        }
    }

    /// <summary>The name on a window and in the tray tooltip; a dev run against another site adds the site (D44).</summary>
    public static string AppNameFor(SiteScope scope) => scope.IsProduction ? AppName : $"{AppName} · {scope.Label}";

    /// <summary>A window with a name of its own: <c>PIU Scores Watcher · Bulk capture</c>.</summary>
    public static string AppNameFor(SiteScope scope, string window) => $"{AppNameFor(scope)} · {window}";

    // ---- First run ----
    public static string FirstRunHeadline => L("Set up in three steps");
    public static string FirstRunLede => L("Two minutes, once. After this it just runs.");
    public static string TokenLabel => L("Your PIU Scores token");
    public static string Connect => L("Connect");
    public static string Checking => L("Checking…");

    /// <summary>{0} is the player's name, which is set in bold.</summary>
    public static string ConnectedAs => L("Connected as {0}");

    public static string ConnectedUnchecked => L("Token saved — PIU Scores hasn't answered yet");

    /// <summary>{0} is <see cref="TokenPageLink" />, set as the link to the token page.</summary>
    public static string TokenHelp => L("Create one on {0}. It is stored encrypted on this PC and only ever sent to PIU Scores.");

    public static string TokenNotAccepted => L("That token wasn't accepted. Check it on the site and paste it again.");
    public static string TokenUnchecked => L("Couldn't reach PIU Scores to check the token. Try again in a moment.");
    public static string ModeQuestion => L("How should it watch?");
    public static string ModeGame => L("The game window");
    public static string ModeGameDetail => L("Looks at RISE once a second while it is running. Nothing else on your screen.");
    public static string ModeSteam => L("My Steam screenshots");
    public static string ModeSteamDetail => L("Press F12 on the result screen; the screenshot is read. Costs nothing during play.");
    public static string Recommended => L("(recommended)");
    public static string ModeBothDetail => L("A screen seen twice is still one play.");
    public static string StartWithWindows => L("Start with Windows");
    public static string StartWithWindowsDetail => L("Sits in the tray, wakes when RISE starts, sleeps when it closes. Off, it only runs when you open it.");
    public static string PrivacyLink => L("What it looks at and sends");
    public static string NotRecordedShort => L("not recorded");
    public static string Done => L("Done");

    // ---- Settings ----
    public static string SectionAccount => Upper(L("Account"));
    public static string SectionWatching => Upper(L("Watching"));
    public static string SectionStartup => Upper(L("Startup"));
    public static string SectionNotifications => Upper(L("Notifications"));
    public static string SectionRecent => Upper(L("Recent"));
    public static string Disconnect => L("Disconnect");
    public static string ModeSteamShort => L("Steam screenshots");
    public static string ModeBothShort => L("Both");
    public static string Change => L("Change");
    public static string StartWithWindowsShort => L("In the tray at sign-in; wakes when RISE starts.");
    public static string ShowNotifications => L("Show notifications");
    public static string NotifyRecorded => L("Every recorded play");
    public static string NotifyNotRecorded => L("A play PIU Scores didn't take");
    public static string NotifyUnreadable => L("A screen that couldn't be read");
    public static string NotifyTokenRejected => L("When the token stops working");
    public static string NotifyUpdated => L("When the watcher updates");
    public static string NoPlaysYet => L("No plays yet since the watcher started.");
    public const string NoMark = "—";
    public static string Broken => L("broken");
    public static string Review => L("Review");
    public static string OpenLogs => L("Open logs folder");
    public static string Quit => L("Quit");
    public static string Pause => L("Pause");
    public static string Resume => L("Resume");

    public static string NotifyBulkFinished => L("When a bulk capture finishes");

    // ---- Settings: a play's sound (D63, D64) ----
    public static string SoundsWhilePlaying => L("Sounds while playing");

    /// <summary>Under the switch: what each of the three sounds means for a play.</summary>
    public static string SoundsWhilePlayingDetail =>
        L("A chime when a play is recorded, a low tone when a screen can't be read, a tick when PIU Scores doesn't take it.");

    // ---- Settings: the language (D58, D59) ----
    public static string SectionLanguage => Upper(L("Language"));

    /// <summary>The picker's first entry and everyone's start: follow Windows (D59).</summary>
    public static string MachineDefault => L("Machine Default");

    /// <summary>A language as the picker lists it, in its own words — never translated (D58).</summary>
    public static string LanguageName(string code) => code switch
    {
        Languages.English => Verbatim("English"),
        Languages.SpanishMexico => Verbatim("Español (México)"),
        Languages.SpanishSpain => Verbatim("Español (España)"),
        Languages.Portuguese => Verbatim("Português"),
        Languages.Korean => Verbatim("한국어"),
        Languages.Japanese => Verbatim("日本語"),
        Languages.French => Verbatim("Français"),
        Languages.Italian => Verbatim("Italiano"),
        _ => code
    };

    // ---- Tray ----
    public static string TrayOpenSettings => L("Open settings");
    public static string TrayPause => L("Pause watching");
    public static string TrayResume => L("Resume watching");
    public static string TrayOpenSite => L("Open PIU Scores");
    public static string TrayStartBulk => L("Start bulk capture…");
    public static string TrayStopBulk => L("Stop bulk capture");

    // ---- Bulk capture: the start window (D51) ----
    public static string BulkWindowName => L("Bulk capture");
    public static string BulkHeadline => L("Grab your Warm Up bests");
    public static string BulkLede => L("Each best on Warm Up's song list becomes a play on PIU Scores: the song, the chart and the score, no judgments.");
    public static string BulkStep1 => L("Open Warm Up and go to the song list.");

    /// <summary>Each {KEY} is drawn as a key cap: A, D, TAB, W, S.</summary>
    public static string BulkStep2 => L("Move through every chart: {A} {D} change the level, {TAB} switches 5K SINGLE and 6K DOUBLE, {W} {S} change the song.");

    public static string BulkStep3 => L("Wait for the sound before moving on.");
    public static string SoundChime => L("Chime");
    public static string SoundChimeMeaning => L("Sent to PIU Scores.");
    public static string SoundTick => L("Tick");
    public static string SoundTickMeaning => L("PIU Scores already has this best, or a higher one.");
    public static string SoundLow => L("Low tone");
    public static string SoundLowMeaning => L("Couldn't read it. It's kept for review; move on.");
    public static string BulkChecking => L("Checking your bests on PIU Scores…");
    public static string BulkNotConnected => L("Connect to PIU Scores in settings first.");
    public static string BulkCouldNotLoad => L("Couldn't load your bests from PIU Scores. Try again in a moment.");
    public static string PlaySounds => L("Play sounds");
    public static string BulkStopsItself => L("It stops by itself when a song starts or RISE closes.");
    public static string Cancel => L("Cancel");
    public static string Start => L("Start");

    // ---- Bulk capture: settings, Recent, the summary ----
    public static string SectionBulk => Upper(L("Bulk capture"));
    public static string BulkSettingsLine => L("Grab your Warm Up bests from the song list.");
    public static string StartEllipsis => L("Start…");
    public static string SoundsWhileCapturing => L("Sounds while capturing");
    public static string Stop => L("Stop");
    public static string BulkRunName => L("Bulk capture");
    public static string BulkFinishedTitle => L("Bulk capture finished");
    public static string ReviewListTitle => L("This best couldn't be read");

    // ---- Status: the tray's first line and the settings window's header ----
    public static string StatusWatching => L("Watching — RISE is running");
    public static string StatusWaiting => L("Waiting for RISE");
    public static string StatusPaused => L("Paused");
    public static string StatusNotConnected => L("Not connected — plays are kept, not posted");

    // ---- Notifications ----
    public static string RecordedTitle => L("Recorded");
    public static string UnreadableTitle => L("Couldn't read that result screen");
    public static string UnreadableBody => L("Saved it. Nothing was recorded.");
    public static string NotRecordedTitle => L("PIU Scores didn't accept this play");
    public static string TokenRejectedTitle => L("Your token stopped working");
    public static string TokenRejectedBody => L("Plays aren't being recorded. Reconnect in settings.");
    public static string NotConnectedTitle => L("The watcher isn't connected");
    public static string NotConnectedBody => L("Plays are kept, not posted. Connect in settings.");
    public static string Ignore => L("Ignore");
    public static string OpenSettings => L("Open settings");
    public static string WhatChanged => L("What changed");

    // ---- Review ----
    public static string ReviewUnreadableTitle => L("This screen couldn't be read");
    public static string ReviewNotRecordedTitle => L("This play wasn't recorded");
    public static string ReviewStaysHere => L("It stays on this PC. Nothing is sent anywhere unless you choose to.");
    public static string ReviewNothing => L("Nothing to review.");
    public static string ShowFile => L("Show the file");
    public static string Delete => L("Delete");
    public static string Privacy => L("Privacy");

    // ---- Sentences with numbers in them ----

    /// <summary>The way to the token page; "API tokens" stays English in every language, as the site shows it.</summary>
    public static string TokenPageLink(Uri site) => L("{0} → Account → API tokens", site.Host);

    /// <summary>The tray's first line during a run.</summary>
    public static string StatusBulk(BulkTally tally) => L("Bulk capture · {0} sent, {1} already there", tally.Sent, tally.Already);

    /// <summary>The settings window's status card during a run.</summary>
    public static string BulkOn(BulkTally tally) => L("Bulk capture on · {0} sent", tally.Sent);

    public static string BulkDetail(BulkTally tally) => L("{0} already there · {1} couldn't be read", tally.Already, tally.NotSent);

    public static string BulkStoredBests(int count) => L("PIU Scores has {0} of your Warm Up bests. Only higher ones are sent.", count);

    /// <summary>A run's line in Recent, after the bold <see cref="BulkRunName" />.</summary>
    public static string BulkRunDetail(BulkTally tally) => " · " + L("Warm Up · {0} sent, {1} already there", tally.Sent, tally.Already);

    public static string BulkSummary(BulkTally tally) =>
        L("{0} new bests sent · {1} already on PIU Scores · {2} couldn't be read", tally.Sent, tally.Already, tally.NotSent);

    public static string StatusDetail(DateTimeOffset? lastRecorded, int playsToday, DateTimeOffset now)
    {
        var last = lastRecorded is { } at ? L("Last recorded {0}", Ago(at, now)) : L("Nothing recorded yet");
        var plays = Plural(playsToday, "{0} play today", "{0} plays today");
        return $"{last} · {plays}";
    }

    public static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var span = now - at;
        if (span < TimeSpan.FromMinutes(1))
            return L("just now");
        if (span < TimeSpan.FromHours(1))
            return L("{0} min ago", (int)span.TotalMinutes);
        return span < TimeSpan.FromDays(1)
            ? L("{0} h ago", (int)span.TotalHours)
            : MonthDay(at);
    }

    public static string FolderLine(bool chosen, bool exists) =>
        chosen ? L("Steam screenshots folder · chosen") : exists ? L("Steam screenshots folder · found") : L("Steam screenshots folder · not found yet");

    /// <summary>The amber row under Recent: what is waiting in the review window.</summary>
    public static string ToReview(int unreadable, int notRecorded)
    {
        var parts = new List<string>();
        if (unreadable > 0)
            parts.Add(Plural(unreadable, "1 screen couldn't be read", "{0} screens couldn't be read"));
        if (notRecorded > 0)
            parts.Add(Plural(notRecorded, "1 play wasn't recorded", "{0} plays weren't recorded"));
        return string.Join(" · ", parts);
    }

    /// <summary>A Recent row's age: <c>2 min</c>.</summary>
    public static string AgoShort(DateTimeOffset at, DateTimeOffset now)
    {
        var span = now - at;
        if (span < TimeSpan.FromMinutes(1))
            return L("now");
        if (span < TimeSpan.FromHours(1))
            return L("{0} min", (int)span.TotalMinutes);
        return span < TimeSpan.FromDays(1)
            ? L("{0} h", (int)span.TotalHours)
            : MonthDay(at);
    }

    public static string VersionLine(string version, bool? upToDate) => upToDate switch
    {
        true => L("Version {0} · up to date", version),
        false => L("Version {0} · an update applies at the next start", version),
        null => L("Version {0}", version)
    };

    public static string UpdatedTitle(string version) => L("Updated to {0}", version);

    public static string Score(int score) => score.ToString("N0", Culture);

    /// <summary>The chart as the game names it: <c>5K S18</c>, <c>6K HD23</c>, <c>Arcade 5K S20</c> — never translated.</summary>
    public static string ChartLabel(RiseMix mix, ChartType type, int level) => (mix, type) switch
    {
        (RiseMix.Rise, ChartType.Single) => Verbatim($"5K S{level}"),
        (RiseMix.Rise, ChartType.HalfDouble) => Verbatim($"6K HD{level}"),
        (RiseMix.RiseArcade, ChartType.Single) => Verbatim($"Arcade 5K S{level}"),
        (RiseMix.RiseArcade, ChartType.Double) => Verbatim($"Arcade 10K D{level}"),
        _ => $"{type} {level}"
    };

    /// <summary>The award as the station names it: RISE mode's marks, the Arcade Station's plates.</summary>
    public static string AwardName(RiseMix mix, Award award) => (mix, award) switch
    {
        (RiseMix.Rise, Award.UltimateGame) => L("Full Combo"),
        (RiseMix.Rise, Award.SuperbGame) => L("No Miss"),
        (_, Award.PerfectGame) => L("Perfect Game"),
        (_, Award.UltimateGame) => L("Ultimate Game"),
        (_, Award.ExtremeGame) => L("Extreme Game"),
        (_, Award.SuperbGame) => L("Superb Game"),
        (_, Award.MarvelousGame) => L("Marvelous Game"),
        (_, Award.TalentedGame) => L("Talented Game"),
        (_, Award.FairGame) => L("Fair Game"),
        _ => L("Rough Game")
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
        PostOutcome.Refused { ProblemType: PostOutcome.Refused.JudgmentsDoNotReconcile } =>
            L("The judgments don't add up to the score — probably a misread. Not recorded."),
        PostOutcome.Refused => L("PIU Scores refused it. Not recorded."),
        PostOutcome.SongUnknown => L("PIU Scores doesn't know that song on this mix — probably a misread title. Not recorded."),
        PostOutcome.RateLimited => L("PIU Scores asked the watcher to slow down. Not recorded."),
        PostOutcome.Unauthorized => L("The token wasn't accepted. Not recorded."),
        PostOutcome.NotConnected => L("The watcher isn't connected to PIU Scores. Not recorded."),
        _ => L("Couldn't reach PIU Scores. Not recorded.")
    };

    public static string ReviewTitle(KeptBecause because) =>
        because.WasRead() ? ReviewNotRecordedTitle : because.IsSongList() ? ReviewListTitle : ReviewUnreadableTitle;

    /// <summary>The review window's one sentence on what went wrong.</summary>
    public static string ReviewReason(KeptBecause because) => because switch
    {
        KeptBecause.NumbersUnreadable => L("Some of the numbers couldn't be read, so nothing was recorded."),
        KeptBecause.NumbersNotShown => L("The screenshot was taken before the numbers finished counting, so nothing was recorded."),
        KeptBecause.NumbersDisagree => L("The judgment counts didn't add up to the score, so nothing was recorded."),
        KeptBecause.TitleUnreadable => L("The song title couldn't be read, so nothing was recorded."),
        KeptBecause.ListUnreadable => L("Some of the numbers couldn't be read, so nothing was sent."),
        KeptBecause.GradeDisagrees => L("The grade didn't match the score, so nothing was sent."),
        KeptBecause.TitleUnmatched => L("The song title didn't match any song on PIU Scores, so nothing was sent."),
        KeptBecause.ChartDisagrees => L("The chart it read has a different number of notes than the judgments add up to, so nothing was recorded."),
        KeptBecause.Refused => L("PIU Scores refused the play, so it wasn't recorded."),
        KeptBecause.SongUnknown => L("PIU Scores doesn't know that song on this mix, so it wasn't recorded. The title was probably misread."),
        KeptBecause.TokenRejected => L("The token wasn't accepted, so it wasn't recorded."),
        KeptBecause.NotConnected => L("The watcher wasn't connected to PIU Scores, so it wasn't recorded."),
        KeptBecause.RateLimited => L("PIU Scores asked the watcher to slow down, so it wasn't recorded."),
        _ => L("PIU Scores couldn't be reached, so it wasn't recorded.")
    };

    /// <summary>Under the review window's picture: <c>Result screen · 1920×1080 · today 5:57 PM · game window</c>, or <c>Song list · …</c>.</summary>
    public static string ReviewSeen(KeptBecause because, CaptureSource source, int width, int height, DateTimeOffset seenAt, DateTimeOffset now)
    {
        var local = seenAt.LocalDateTime;
        var time = local.ToString("t", Culture);
        var date = MonthDay(seenAt);
        var day = local.Date == now.LocalDateTime.Date ? L("today {0}", time)
            : local.Date == now.LocalDateTime.Date.AddDays(-1) ? L("yesterday {0}", time)
            : $"{date} {time}";
        var screen = because.IsSongList() ? L("Song list") : L("Result screen");
        return $"{screen} · {width}×{height} · {day} · {SourceName(source)}";
    }

    public static string SourceName(CaptureSource source) => source switch
    {
        CaptureSource.GameWindow => L("game window"),
        CaptureSource.SteamScreenshot => L("Steam screenshot"),
        _ => L("replay")
    };

    public static string DeleteAll(int count) => L("Delete all {0} unread screens", count);

    // ---- The lookups ----

    /// <summary>The line in the current language: its translation, or the English when there is none (English itself has none).</summary>
    private static string L(string english)
    {
        return Translations.GetString(english, Culture) ?? english;
    }

    /// <summary>A line with holes, filled in the current language's formats.</summary>
    private static string L(string english, params object?[] values)
    {
        return string.Format(Culture, L(english), values);
    }

    /// <summary>A count: <paramref name="one" /> or <paramref name="other" /> by the language's rule, the count in <c>{0}</c> (D61).</summary>
    private static string Plural(int count, string one, string other)
    {
        return L(IsOne(count) ? one : other, count);
    }

    /// <summary>French and Portuguese say 0 in the one form; English, Spanish and Italian only 1; Korean and Japanese have both keys and no difference.</summary>
    private static bool IsOne(int count)
    {
        return Culture.Name is Languages.French or Languages.Portuguese ? count is 0 or 1 : count == 1;
    }

    /// <summary>A section label, upper-cased in the language rather than kept as a second key (D60).</summary>
    private static string Upper(string text)
    {
        return text.ToUpper(Culture);
    }

    /// <summary>Words the game prints as-is (D60): never a key, never translated.</summary>
    private static string Verbatim(string text)
    {
        return text;
    }

    /// <summary>A day as a short month and day, <c>Sep 24</c>, in the language's own pattern.</summary>
    private static string MonthDay(DateTimeOffset at)
    {
        return at.LocalDateTime.ToString(L("MMM d"), Culture);
    }
}
