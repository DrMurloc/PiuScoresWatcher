# PiuScoresWatcher

Windows tray app (.NET 10, WPF) that reads PUMP IT UP RISE result screens and posts them to PIU Scores through its public API. Two halves: `PiuScoresWatcher.Core` (headless — recognizer, checksum, contracts, settings model) and `PiuScoresWatcher.App` (WPF plus every Windows adapter), one test project. A sibling of [PumpItUpScoreTracker](https://github.com/DrMurloc/PumpItUpScoreTracker): it shares that repo's conventions and none of its code — it talks to the site the way any partner tool does.

## Imports

- `@docs/ARCHITECTURE.md` — the two halves, the pipeline, the ports, what is deliberately absent. This file holds the machine-enforceable conventions that realize it.

@docs/ARCHITECTURE.md

## Documentation set

Reader-facing docs live in `docs/` (README.md at the root). Keep them current **in the same PR** as the change that invalidates them:

- [README.md](README.md) — intro + doc index
- [docs/HOW-TO-RUN.md](docs/HOW-TO-RUN.md) — prerequisites, running from source, the dev switches, packaging, releasing
- [docs/HOW-TO-TEST.md](docs/HOW-TO-TEST.md) — the rungs + the manual checklist with the game
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — update in the same PR that changes a structural pattern
- [docs/DOMAIN.md](docs/DOMAIN.md) — the RISE result screen, scoring, grades, marks, the two stations
- [docs/PRIVACY.md](docs/PRIVACY.md) — what the app looks at, sends and keeps; player-facing, the owner's copy
- [docs/TECHNOLOGIES.md](docs/TECHNOLOGIES.md) — new stack pieces get an entry
- [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) — the owner's contribution policies
- [docs/design/watcher.md](docs/design/watcher.md) — the design of record. Decisions are numbered `D1…`, owner decisions marked **(owner, date)**, the rest "decided unless objected"; open questions live in its last section

## Commands

Run from the repo root.

- **Build**: `dotnet build PiuScoresWatcher.sln -c Debug`. CI builds Release; there is no configuration trap here — nothing else runs these binaries.
- **Test**: `dotnet test tests/PiuScoresWatcher.Tests/PiuScoresWatcher.Tests.csproj` — unit tests over Core and the architecture ratchets. No Windows API, no game, no network.
- **Run**: `dotnet run --project src/PiuScoresWatcher.App` — the tray icon. Dev switches: `--replay <screenshot>` (one file through the pipeline, no game) with `--dry-run` (never post), and `--base-url <url>` or the `PIUSCORESWATCHER_BASE_URL` variable (a local PIU Scores instead of production). See [docs/HOW-TO-RUN.md](docs/HOW-TO-RUN.md).
- **Package locally**: `dotnet tool restore`, then the publish + `dotnet vpk pack` lines in HOW-TO-RUN §Packaging. Output in `Releases/` (ignored).
- **Release**: push a tag `vX.Y.Z`; `.github/workflows/release.yml` builds, tests, packages, signs when the Trusted Signing variables exist, and publishes the GitHub release. Never hand-upload a release.
- **Lint/static analysis**: none locally. CodeQL default setup on GitHub; Dependabot weekly (nuget grouped minor/patch, github-actions).

CI: GitHub Actions (`.github/workflows/ci.yml`), `windows-latest` because WPF builds only there. Every PR and push to `main` builds Release and runs the test project.

## Architecture conventions

Ratcheted by `tests/PiuScoresWatcher.Tests/ArchitectureTests/`; treat the rest as just as binding.

### Layer graph and per-project package allowlist

```
Core ◄── App          Tests → Core only
```

| Project | Allowed packages | Forbidden |
|---|---|---|
| `PiuScoresWatcher.Core` (`net10.0`) | none — the BCL | anything with a UI, a Windows API, a network client or a process model. The recognizer works on pixel buffers handed to it; the checksum is arithmetic; the API contracts are records. No OS platform target, no reference to a UI/Windows/Velopack assembly (arch-test enforced, `CoreStaysHeadlessTests`) |
| `PiuScoresWatcher.App` (`net10.0-windows10.0.19041.0`) | `H.NotifyIcon.Wpf`, `Microsoft.Extensions.Hosting`, `Serilog.Extensions.Hosting` + `Serilog.Sinks.File`, `Velopack`; planned: `SkiaSharp` (decode + pixels), the Windows Community Toolkit notifications package (toasts), `System.Security.Cryptography.ProtectedData` (the token) | EF, MediatR, MassTransit, any of the site's machinery — this is one feature, not a system |
| `PiuScoresWatcher.Tests` (`net10.0`) | `xunit` 2.9.3, `Moq` 4.20.72, `Microsoft.NET.Test.Sdk`, `coverlet.collector` | other doubling libraries; a project reference to App (App is covered by source-scan ratchets and by a human with the game open) |

Adding a package outside its allowed project is a violation. Pin versions; Dependabot moves them.

### Ports and adapters

- Every boundary crosses a **port defined in Core** and implemented by an **adapter in App**: `IClock` → `SystemClock`; `ISettingsStore` → `JsonSettingsStore`; planned `IScreenSource` (window capture · Steam screenshot folder), `IResultScreenReader`, `IPlaysClient`, `ITokenStore`, `INotifier`, `IStartupRegistration`. Naming: `I*Source`, `I*Reader`, `I*Client`, `I*Store`. **One implementation per port**, except `IScreenSource`, which has one per capture mode.
- DI is the generic host in `App.xaml.cs`; registrations are explicit, there is no reflection scan.
- **The clock is `IClock`.** `DateTime*.Now`, `UtcNow` and `Today` appear in `SystemClock.cs` and nowhere else under `src/` (arch-test enforced, `ClockSeamTests`). A play's `playedAt`, the dedupe window and every log line read the port.

### Domain rules

- Value types validate at construction: `static From(...)` factories throwing a `Core/Exceptions` type; every such exception derives `WatcherException`. **A `WatcherException` message is written to be shown to the player as-is**; any other exception is logged and reported as a generic sentence, never printed.
- **The server is the checksum authority, and the watcher reconciles first.** A screen posts only when the five judgments recompute to the score it shows; the server recomputes again and refuses what does not reconcile (`judgments-do-not-reconcile`). Never bypass either half. The three scoring rules (score from judgments, accuracy truncated to two decimals, award from judgments) are **copied** from PIU Scores with the site file cited in the comment, pinned by the owner's verified result screens — never a reference to the site's code, never a shared package.
- **The recognizer fails loud.** An unreadable screen is saved under `failed/` and surfaced; nothing is guessed, nothing partial is posted.
- **Which mix** is decided by the layout: a Warm Up / Division screen posts to `rise`, an Arcade Station screen to `riseArcade`; a Challenge result (a division badge where the song title goes) is skipped — it is an aggregate, not a play.
- **Settings are a JSON file; the token is never in it.** The token is DPAPI-encrypted on its own under `%LOCALAPPDATA%\PiuScoresWatcher\`.
- `--replay` and `--base-url` are dev seams. Nothing in code defaults to a non-production URL; an unknown switch is refused, not ignored.
- **The site's API contract is read, never guessed**: PumpItUpScoreTracker `docs/API.md` and its Swagger are shape truth for `GET api/v2/players/me` and `POST api/v2/players/me/plays`. Auth is `Authorization: Basic base64("anything:<token>")`.

## UI conventions

- WPF with the built-in Fluent theme (`ThemeMode="System"` on the Application); no third-party control library.
- **The tray icon is the app.** `ShutdownMode="OnExplicitShutdown"`: windows come and go, only Quit exits. Startup lives in `Program.Main` (the Velopack hook, then the single-instance mutex, then WPF), so `App.xaml` compiles as a Page.
- **Feedback is a toast** — never a window that takes focus from the game, and **never an overlay** of any kind (design D3).
- **Player-facing strings are the owner's copy.** Ship placeholders and say so in the PR; never invent final copy. English only in v1 (D14).
- No color literals in XAML beyond what the theme provides; a brand color, when one arrives, is a resource with a name.

## Test conventions

See [docs/HOW-TO-TEST.md](docs/HOW-TO-TEST.md) for the philosophy; the agent-facing specifics:

- **xUnit 2.9.3 + Moq 4.20.72.** No `FakeItEasy`, `NSubstitute`, `AutoFixture`. Talk about doubles by role (stub / mock / fake), not by library type.
- **Folder-as-tag**: `StartupTests/` and `RecognitionTests/` (unit — real objects, fixtures in, records out), `ArchitectureTests/` (ratchets — rules are added, never removed). No per-test traits.
- **Naming**: `<TypeName>Tests.cs`, one class per subject; method names describe behavior (`AnUnknownSwitchIsRefusedRatherThanIgnored`), never implementation.
- **Fixtures**: result screenshots under `tests/PiuScoresWatcher.Tests/Fixtures/` (copied to output), each with the expected reading beside it. The owner's own screens are the base set; a player's screen is added only with their ok.
- **Builders** in `TestData/` (`<Type>Builder`, `WithX` returning `this`, `Build()`); **`FakeClock`** in `TestHelpers/` once `IClock` has a consumer. Never `DateTime.Now` in a test.
- **Coverage exclusions**: `[ExcludeFromCodeCoverage]` on records, DTOs and exceptions only — `GlobalUsings.cs` exposes `System.Diagnostics.CodeAnalysis` in Core and Tests. Never on the recognizer, the checksum or a parser.
- Every commit is green on `dotnet build` + `dotnet test` on its own; docs first, then code; one PR per session.

## Known divergences

- Serilog is configured in code, not from `appsettings.json` — there is no appsettings, and a tray app has one logging policy.
- `.editorconfig` says `utf-8` where PIU Scores says `utf-8-bom`; the site's files carry no BOM either, and a BOM in a workflow or `global.json` is a hazard for nothing.
- `PiuScoresWatcher.Tests` does not reference App, so the settings store, the update service and the windows have no automated test; the ratchets scan their source and the checklist in HOW-TO-TEST covers their behavior. Revisit if App grows logic that is not an adapter.
