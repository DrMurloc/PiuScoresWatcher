# Architecture

Two sections: the **philosophy** (why the code is shaped this way) and the **code map** (where things are). RISE terms are defined in [DOMAIN.md](DOMAIN.md); the design of record, decisions and phases, is [design/watcher.md](design/watcher.md).

---

## 1. Philosophy

### One feature, two halves

The watcher does one thing: see a RISE result screen, read it, post it. It is not a system, so it borrows PIU Scores' *shape* — ports and adapters around a headless core — and none of its *machinery* (no MediatR, no bus, no verticals, no database).

- **`PiuScoresWatcher.Core`** is headless. It holds what can be reasoned about without Windows: the recognizer (pixels in, a `ResultScreen` out), the checksum (the three scoring rules copied from PIU Scores), the API contracts, the settings model, the launch options, and every port. It targets plain `net10.0` with no OS platform and references no UI, Windows or Velopack assembly — ratcheted, because that is what makes a fixture screenshot a unit test that runs on any machine with the SDK.
- **`PiuScoresWatcher.App`** is the Windows half: WPF for the tray icon and the settings window, and an adapter for every port that touches the OS — window capture, the Steam screenshot folder, OCR, toasts, DPAPI for the token, the Run key for Start with Windows, the wall clock, the settings file, Velopack updates.

Core never knows it is inside a tray app; App never reads a pixel.

### Ports and adapters

Every boundary is a port defined in Core and an adapter in App: `IClock` → `SystemClock`, `ISettingsStore` → `JsonSettingsStore`, and, as the pipeline lands, `IScreenSource` (one adapter per capture mode), `IResultScreenReader`, `IPlaysClient`, `ITokenStore`, `INotifier`, `IStartupRegistration`. One implementation per port, wired explicitly in the generic host — no reflection scan, there are a dozen services.

### The pipeline

```
IScreenSource ──► ResultScreenDetector ──► ResultScreenReader ──► PlayChecksum ──► Deduplicator ──► IPlaysClient ──► INotifier
 (window capture      (a few pixel anchors:    (digit templates on     (score and accuracy   (the same screen      (POST api/v2/        (toast + sound)
  once a second,       is this a result         the fixed layout,       recompute from the    seen for a minute     players/me/plays)
  or the Steam F12     screen, and of which     title by OCR)           judgments, or the     is one play)
  folder)              station)                                         screen is refused)
```

Two properties hold the whole thing up:

1. **Cheap until it matters.** Polling a window once a second through the OS capture API costs nothing measurable; the detector looks at a handful of pixels; only a result screen triggers the read, once. F12 mode costs nothing at all during play — the game writes a file, the watcher reads a file.
2. **Two checksums, one authority.** The reader's output must recompute to the score on screen before it leaves the machine — exactly, in integer arithmetic — and the accuracy the screen shows must land within a hundredth of the one the judgments make; the server recomputes the score again and answers `400 judgments-do-not-reconcile` when it does not. A misread digit almost never survives, so a bad read is refused, not recorded. The same check catches a frame taken while the score is still counting up: the judgments land a beat before the score does, and until they agree the frame is "not yet". The server is the authority; the local check exists so the player hears "couldn't read that one" instead of a rejection.

The reader itself is template matching, not OCR: each layout's fields sit at fixed fractions of the frame, a field's ink is cut into glyphs by column projection, and each glyph, reduced to a 12×20 bitmap, is the character of its most similar template. The templates come from the owner's screens and from the game's own number sprites (`tools/reader-lab`), and every fixture screen reads back correctly with any one screen held out of the training set. Only the song title is OCR'd, by Windows, through the `ITitleReader` port.

The reader **fails loud**: an unreadable screen is saved under `failed/` and surfaced through the tray, never guessed at. RISE patches monthly and a moved layout breaks the reader; the fix is a release, which installed copies pick up on their next launch, and the failed screen is how the fix gets written.

### Runtime shape

`Program.Main` runs the Velopack hook first (install, update and uninstall events exit before any window exists), then takes the single-instance mutex, then parses the launch options and starts WPF. `App.OnStartup` builds the generic host — Serilog to a rolling file, the services, the background work — and instantiates the tray icon from `App.xaml`'s resources. The tray icon *is* the app (`ShutdownMode=OnExplicitShutdown`): the settings window opens on demand and closes without exiting; only Quit does. When the game is not running the watcher sleeps, checking every few seconds whether a RISE window exists.

### Enforcement over convention

The rules above are ratcheted by `tests/PiuScoresWatcher.Tests/ArchitectureTests/`: Core stays headless (no OS target, no forbidden references); the wall clock is read in `SystemClock.cs` and nowhere else. Rules are added, never removed. The machine-readable conventions live in [CLAUDE.md](../CLAUDE.md).

---

## 2. Code map

```
PiuScoresWatcher.sln
├── src/PiuScoresWatcher.Core        net10.0 — headless
│   ├── Startup/                     LaunchOptions (--replay, --dry-run, --base-url)
│   ├── Settings/                    WatcherSettings, CaptureMode, ISettingsStore
│   ├── Time/                        IClock
│   ├── Domain/                      RiseMix, ChartType, Judgments
│   ├── Exceptions/                  WatcherException and its kinds (player-showable messages)
│   ├── Recognition/                 ScreenImage, Layouts (both screens as fractions of the frame),
│   │                                ResultScreenDetector, ResultScreenReader → ResultScreenReading,
│   │                                the mask/segmenter/template machinery, templates.json (generated),
│   │                                ITitleReader
│   ├── Scoring/                     PhoenixScoring (the formula, copied from PIU Scores), PlayChecksum
│   └── Api/          (planned)      the request/response records, IPlaysClient, the problem types
├── src/PiuScoresWatcher.App         net10.0-windows10.0.19041.0 — WPF + adapters
│   ├── Program.cs                   Velopack hook → single instance → launch options → replay or WPF
│   ├── App.xaml(.cs)                the generic host, the tray icon, the settings window on demand
│   ├── Views/                       SettingsWindow
│   ├── Storage/                     AppPaths (%LOCALAPPDATA%\PiuScoresWatcher), JsonSettingsStore
│   ├── Time/                        SystemClock
│   ├── Updates/                     UpdateService (GitHub Releases, applied on next launch)
│   ├── Replay/                      ReplayRunner (a file through the pipeline, JSON on the console), WpfScreenDecoder
│   ├── Ocr/                         WindowsOcrTitleReader (Windows.Media.Ocr over the title bar)
│   ├── Capture/      (planned)      WindowCaptureSource, SteamScreenshotSource, RiseProcessWatch
│   ├── Notifications/(planned)      toasts
│   ├── Security/     (planned)      DpapiTokenStore
│   └── Assets/                      app.ico (placeholder art)
├── tests/PiuScoresWatcher.Tests     xUnit + Moq (+ SkiaSharp to decode fixtures), references Core only
│   ├── StartupTests/                LaunchOptionsTests
│   ├── ArchitectureTests/           CoreStaysHeadlessTests, ClockSeamTests
│   ├── RecognitionTests/            every fixture screen through the detector, the reader and the checksum
│   ├── ScoringTests/                PhoenixScoringTests, PlayChecksumTests
│   ├── DomainTests/                 JudgmentsTests
│   ├── TestHelpers/                 RepositoryFiles, FixtureScreens
│   └── Fixtures/screens/            the owner's result screens (player card masked) + expected.json
├── tools/reader-lab                 the Python prototype: labels, leave-one-out, sprite export, the template generator
├── .github/workflows                ci.yml (PR gate), release.yml (tag → signed GitHub release)
└── docs/                            this set; design/watcher.md is the design of record
```

### Data on disk

Everything the watcher writes lives under `%LOCALAPPDATA%\PiuScoresWatcher\`:

| Path | What |
|---|---|
| `settings.json` | the player's choices — mode, Start with Windows, the screenshots folder override. Never the token |
| `token.bin` (planned) | the PIU Scores token, DPAPI-encrypted for the Windows account |
| `logs\watcher-<date>.log` | rolling daily logs, seven kept |
| `failed\` | result screens the reader could not parse, for the "send to the developer" button |

No telemetry. What leaves the machine is exactly one HTTP request per play, described in [PRIVACY.md](PRIVACY.md).

### Deliberately absent

MediatR, MassTransit, EF, Aspire, Docker, Testcontainers, Playwright, bUnit, resx localization, Azure Pipelines. Each exists in PIU Scores for a reason that does not apply here: nothing is hosted, nothing is stored beyond a settings file, nothing is rendered in a browser, and the audience of a settings window with a dozen strings is served by English until it is not.
