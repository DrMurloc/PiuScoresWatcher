# How to Test

## Philosophy

The same rule as PIU Scores: **use the lowest-level test that would catch the regression**, and move up a rung only when the lower one would mock away the thing that might break. The watcher has three rungs, and the top one is a person with the game open — no automation drives RISE.

1. **Unit tests** (`tests/PiuScoresWatcher.Tests/StartupTests/`, `RecognitionTests/`) — Core's pure logic. The launch-option parser; the detector and the reader over **fixture screenshots** (a PNG in, a `ResultScreen` record out, compared field by field against the expected reading stored beside it); the checksum against the owner's verified screens. Real objects, no mocks where avoidable, no clock calls (`FakeClock` once `IClock` has a consumer).
2. **Architecture ratchets** (`ArchitectureTests/`) — Core stays headless (no OS target, no UI/Windows/Velopack reference); the wall clock is read only in `SystemClock.cs`. Rules are added, never removed. A third arrives with the first player-facing error string: no raw exception text reaches a toast or a window.
3. **The checklist with the game** — for what only RISE can show. App's adapters (capture, the folder watcher, toasts, the token store) are thin by design and are exercised here, not mocked into meaninglessness.

Tests are classified by **folder-as-tag**; no per-test traits.

Two hard rules on dependency realism:

- **A fixture screenshot is the real thing.** The reader is tested on actual result screens at actual resolutions, never on synthesized images. When a player sends a screen the reader could not read, it becomes a fixture (with their ok) *before* the fix is written.
- **Never test the capture API with a fake window.** If the question is "does `Windows.Graphics.Capture` hand us the RISE frame", the answer comes from the checklist.

## Running the tests

```sh
dotnet test tests/PiuScoresWatcher.Tests/PiuScoresWatcher.Tests.csproj
```

No Windows API, no game, no network; it runs wherever the SDK does. This is the PR gate, together with the Release build ([`ci.yml`](../.github/workflows/ci.yml)).

## Fixtures

`tests/PiuScoresWatcher.Tests/Fixtures/` (copied to the test output). One folder per result-screen kind — Warm Up, Division, Arcade Station, Challenge (must be skipped), grey grade, each at the resolutions players actually use — holding the screenshot and its expected reading. The owner's own screens are the base set. Steam F12 screenshots are JPEG; window captures are lossless: keep one of each per kind, because the reader must survive both.

## The checklist with the game

Run before a release that touches capture, the reader or posting. Use `--replay` on the fixtures first; then, live, with `--base-url` at a local PIU Scores:

- [ ] Warm Up result → posted to `rise`, toast names the song, chart and score; the site's journal shows the play.
- [ ] Arcade Station result → posted to `riseArcade`, plate and plus grade read correctly.
- [ ] A Challenge result (division badge in the header) → skipped, nothing posted, one log line.
- [ ] A grey (broken) grade → `isBroken` true.
- [ ] Sit on a result screen for a minute → one play, not sixty.
- [ ] F12 on a result screen with the game window mode off → the file is picked up and posted once.
- [ ] Both modes on, F12 pressed on a screen the grab already read → still one play.
- [ ] A misread (cover part of the judgments with another window) → refused locally, saved under `failed\`, surfaced, not posted.
- [ ] Wrong token → the settings window says so; nothing posts.
- [ ] Quit RISE → the watcher sleeps (no capture, no CPU); start RISE → it wakes.

## What is not tested automatically, and why

`PiuScoresWatcher.Tests` references Core only, so the settings store, the update service and the windows have no automated test. They are adapters and thin by intent; the ratchets scan their source, and the checklist covers their behavior. If App ever grows logic that is not an adapter, that logic moves to Core and gets a unit test — the reference stays where it is.
