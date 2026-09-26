# How to Test

## Philosophy

The same rule as PIU Scores: **use the lowest-level test that would catch the regression**, and move up a rung only when the lower one would mock away the thing that might break. The watcher has three rungs, and the top one is a person with the game open — no automation drives RISE.

1. **Unit tests** (`tests/PiuScoresWatcher.Tests/StartupTests/`, `RecognitionTests/`, `ScoringTests/`, `DomainTests/`) — Core's pure logic. The launch-option parser; the detector and the reader over **fixture screenshots** (a JPEG in, a `ResultScreenReading` out, compared field by field against `expected.json`, then the checksum); the Phoenix formula and the checksum against the owner's verified screens. Real objects, no mocks where avoidable, no clock calls (`FakeClock` once `IClock` has a consumer).
2. **Architecture ratchets** (`ArchitectureTests/`) — Core stays headless (no OS target, no UI/Windows/Velopack reference); the wall clock is read only in `SystemClock.cs`; nothing is written into Velopack's install folder; no window or text property spells its own words (they live in `Copy.cs`). Rules are added, never removed. A third arrives with the first player-facing error string: no raw exception text reaches a toast or a window.
3. **The checklist with the game** — for what only RISE can show. App's adapters (capture, the folder watcher, toasts, the token store) are thin by design and are exercised here, not mocked into meaninglessness.

Tests are classified by **folder-as-tag**; no per-test traits.

Two hard rules on dependency realism:

- **A fixture screenshot is the real thing.** The reader is tested on actual result screens at actual resolutions, never on synthesized images. When a player sends a screen the reader could not read, it becomes a fixture (with their ok) *before* the fix is written.
- **Never test the capture API with a fake window.** If the question is "does `PrintWindow` hand us the RISE frame", the answer comes from the checklist.

## Running the tests

```sh
dotnet test tests/PiuScoresWatcher.Tests/PiuScoresWatcher.Tests.csproj
```

No Windows API, no game, no network; it runs wherever the SDK does. This is the PR gate, together with the Release build ([`ci.yml`](../.github/workflows/ci.yml)).

## Fixtures

`tests/PiuScoresWatcher.Tests/Fixtures/screens/` (copied to the test output): the owner's Steam screenshots and the first tester's kept song lists (4K, from his `failed\` folder, with his ok), each with the player card blacked out, and `expected.json` saying what each one is. Every screen is one of five kinds:

| Kind | What the reader must do with it |
|---|---|
| `result` | read every number, and the checksum must reconcile — Warm Up singles and half-doubles, the Arcade Station's singles and doubles, coloured and grey grades, 1080p and 720p |
| `unsettled` | read every number, and the checksum must **refuse** it: a frame caught while the score was still counting up (the judgments are final a beat before the score is) |
| `empty` | `NumbersNotShown` — the grade sticker is up, the number bars are still blank |
| `aggregate` | `NotAPlay` — a Challenge result, four songs summed under a division badge where the song title goes |
| `none` | not detected at all — WorldMax's mission summary, the title screen, the song wheel, a loading frame |
| `songlist` | Warm Up's song list: the lit tab — 5K SINGLE lit orange, 6K DOUBLE lit blue (D71) — the lit level, the best score, and a grade badge that agrees with it (D47) |
| `songlist-empty` | Warm Up's song list on a chart with no best: nothing to capture |
| `arcadelist` | the Arcade Station's song list: not read in v1 (D45) |

Adding a screen is a lab job, not a hand edit: [tools/reader-lab/README.md](../tools/reader-lab/README.md) labels it, masks it, writes `expected.json` and regenerates the templates when a glyph is new. A player's screen is added only with their ok; the Arcade Station is the thin side of the set (four distinct results), so an Arcade result that fails to read is the first thing to ask for.

Fixtures are decoded with SkiaSharp, a test-only dependency; Core never decodes a file.

The song title is the one part the suite cannot read: Windows OCR is an App adapter. `python tools/reader-lab/titles.py` replays every labeled screen through the built watcher and scores the titles it reads, exactly and as the chart list would match them — the check after touching `TitleInk` (D53). On a song list it scores the first place that names the song, the lit row or the panel (D66). What the suite does pin is everything around the OCR: which boxes a title is read from and in what order, the pages, a title that runs off its box (D68), and the chart list's answer to the readings the tester's screens gave.

## The checklist with the game

This is the MVP's one loop (watcher.md §6, D33) — the installed build, a token, one session with the game — and afterwards the check before any release that touches capture, the reader or posting. `--replay` on a fixture first proves the build reads; then, live:

- [ ] Warm Up result → posted to `rise`, toast names the song, chart and score; the site's journal shows the play.
- [ ] Arcade Station result → posted to `riseArcade`, plate and plus grade read correctly.
- [ ] A Challenge result (division badge in the header) → skipped, nothing posted, one log line.
- [ ] A grey (broken) grade → `isBroken` true.
- [ ] Sit on a result screen for a minute → one play, not sixty.
- [ ] A play held for review whose screen reads right to you → keep its F12: another case like Aragami S19, whose score fits more notes than it judged (watcher.md §9).
- [ ] F12 on a result screen with the game window mode off → the file is picked up and posted once.
- [ ] Both modes on, F12 pressed on a screen the grab already read → still one play.
- [ ] A screen that can't be trusted (F12 in Steam-screenshot mode before the numbers finish counting) → refused locally, saved under `failed\`, surfaced, not posted. Covering the screen with another window does nothing: both modes read the game's own frame, never what's on top of it.
- [ ] Wrong token → the settings window says so; nothing posts.
- [ ] Quit RISE → the watcher sleeps (no capture, no CPU); start RISE → it wakes.
- [ ] Install fresh → the first-run window opens on How it works; Set it up → the three steps; a bad token says so; a good one says who it is.
- [ ] Notifications all off → no notification for a recorded play, and the settings window still lists it; one kind off → only that kind goes quiet.
- [ ] A recorded play → the chime, and its notification makes no sound of its own; F12 before the score finishes counting → the low tone; a play with the token disconnected → the tick; Sounds while playing off → silence.
- [ ] No banner at all → Windows' Do Not Disturb is on; the notifications are still in the notification center.
- [ ] A notification's Review → the review window on that screen; Open settings → the settings window.
- [ ] Review window → Show the file opens Explorer on it; Delete moves to the next; Delete all empties the list and the amber row goes.
- [ ] Plays before any token is pasted → kept as "not connected", one notification for the lot, and they show in Recent.
- [ ] Pause from the tray → nothing posts; resume → it does.
- [ ] Launch the watcher again while it runs → the running one's settings open.
- [ ] F5 a dev build while the installed watcher runs → both run; the dev one's title and tooltip name the local site; connecting it leaves the installed one connected.
- [ ] Sign out and back in → it is in the tray (Start with Windows); switched off → it isn't.
- [ ] Bulk capture: start it from the tray → the window says how many Warm Up bests PIU Scores has; Start.
- [ ] Move through a dozen charts on Warm Up's song list, some 6K DOUBLE via TAB → a chime for each best above the site's, a tick for the rest, and the site shows the chimed ones as plays — the 6K DOUBLE ones as half-doubles (D71).
- [ ] Flip quickly between two charts → neither is captured with the other's score.
- [ ] Two songs in a row showing the same level and score (two 1,000,000s at one level) → both chime.
- [ ] Titles the panel scrolls (wanna go to the moon palace, Conflict -NOMA CONCEiVER REMiX-) and short ones (B2, D, N, Dr. M, 8 6) → each chimes or ticks within two seconds; none is kept (D66–D69).
- [ ] The Quick Brown Fox Jumps Over The Lazy Dog, the one title that scrolls in the list too → captured if you wait on it for two seconds; kept at most once if you don't.
- [ ] A chart PIU Scores doesn't list → the low tone, and the review window says PIU Scores doesn't list this chart (D70).
- [ ] RISE at 1280×720: F12 a handful of Warm Up song lists with bests on them → they become the 720p fixtures, and the reader is fixed against them before the tag (watcher.md §9).
- [ ] Start a song → the run ends; one summary notification; one line in Recent.
- [ ] Run it again over the same charts → ticks only, nothing sent.
- [ ] The sounds are clear over the song list's music at your usual volume.
- [ ] Sounds switched off → a silent run; the summary still arrives.
- [ ] Settings → Language → 한국어 → the settings window redraws in Korean at once, the tray menu follows, and the next notification is in Korean; Machine Default → back to Windows' language.
- [ ] Every window in Français and in 日本語 → nothing cut off, and Japanese in Japanese type.
- [ ] Uninstall → the tray icon, the Start menu entry and the sign-in entry are gone; reinstall → still connected, settings kept.

## What is not tested automatically, and why

`PiuScoresWatcher.Tests` references Core only, so the settings store, the update service and the windows have no automated test. They are adapters and thin by intent; the ratchets scan their source, and the checklist covers their behavior. If App ever grows logic that is not an adapter, that logic moves to Core and gets a unit test — the reference stays where it is.
