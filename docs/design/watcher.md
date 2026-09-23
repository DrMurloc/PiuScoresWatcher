# PIU Scores Watcher — the capture app for PUMP IT UP RISE

Status: **the MVP, on one pull request — #8** (2026-09-23, D33). Built so far: the reader reads every one of the
owner's result screens and reconciles them; the client posts; the tray app captures the game window once a
second while RISE runs and reads every F12 screenshot Steam writes, posting each reconciled play once. Iteration 1 of
the UI is being built on the same PR (§6, D34–D42). Not yet tried
against the running game — that is the one loop at the end. Phase 2 of PIU
Scores' RISE plan
([rise.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/design/rise.md) §8): phase 1 added
RISE as two mixes with the v2 plays write this app posts to; phase 3 (boards and PUMBILITY) is the site's.

Owner decisions are marked **(owner, date)**; the rest are decided unless he objects. Open questions are §9.

---

## 1. What it is

A Windows tray app. Install, paste the PIU Scores token, pick how it watches; then it sits in the tray, wakes
when RISE starts, reads each result screen, posts the play, and a toast confirms it. The bar is the Warcraft
Logs uploader: install → sign in → forget it.

RISE writes a play down in exactly one place, the result screen (saves encrypted, no API, no Steam
leaderboards — rise.md §1), so the screen is what gets read. Two ways to see it:

- **Game-window mode** — capture the RISE window once a second while the game runs; a few-pixel check says
  whether it is a result screen; only then is it read.
- **Steam-screenshot mode (F12)** — watch RISE's Steam screenshots folder and read each new file. Costs
  nothing during play; the player presses F12 on the result screen.

## 2. Decisions

- **D1 (owner, 2026-09-22). Its own public repo**, `DrMurloc/PiuScoresWatcher`, MIT. It shares PIU Scores'
  conventions and none of its code: a partner tool that talks to the public API.
- **D2 (owner, 2026-09-22). Auto-post with a toast.** No review queue in v1; a "hold for review" setting is
  cheap to add later if players want it. The checksum (D10) is what makes auto-post safe.
- **D3 (owner, 2026-09-22). No overlay.** Not an injected one (hooks the game's renderer, costs GPU every
  frame, anti-cheat-adjacent, fragile across patches) and not a transparent always-on-top window (borderless
  only). Feedback is a toast, a sound, and the site. A tiny always-on-top status *pill* — a plain window in a
  corner — is a later option for people who want in-game confirmation; it is not an overlay.
- **D4 (owner, 2026-09-22). Signed with Azure Trusted Signing.** The release workflow signs when the
  account's variables exist and releases unsigned until then (HOW-TO-RUN §Signing). The account is the
  owner's to create.
- **D5. Two modes, both on by default.** Game-window and F12; a screen seen by both is one play (D11).
  Low-power machines use F12 alone.
- **D6. A tray app that starts with Windows** — default on, shown on the first-run screen, a toggle in
  settings. Default-off's failure mode is an hour of unrecorded plays. Asleep, it checks every few seconds
  whether a RISE window exists; it wakes when one does and sleeps when it goes. No Steam launch-option trick.
- **D7. .NET 10 + WPF, Windows only.** .NET 10 is the current LTS, the site is on it, .NET 8 leaves support
  in November 2026. WPF's Fluent theme makes a settings window presentable with no UI library. Avalonia is the
  cross-platform route if Steam Deck demand appears (§9).
- **D8. Velopack + GitHub Releases.** One-click per-user install, delta updates, the .NET Desktop Runtime
  bootstrapped by the installer (framework-dependent build). The app checks for a newer release at start-up,
  downloads it in the background and applies it on the next launch — RISE patches monthly and a moved layout
  must be fixable without a support thread.
- **D9. The token is pasted from `/Account` in v1.** A "link this device" flow (the app opens the browser,
  the player clicks Approve) is a site change for later.
- **D10. Two checksums, one authority.** The reader's output must recompute to the score on screen before it
  leaves the machine; the server recomputes again and answers `400 judgments-do-not-reconcile` otherwise. The
  three scoring rules are **copied** from PIU Scores with the site file cited, pinned by the owner's verified
  screens — never a shared package, never a reference to the site's code.
- **D11. Dedupe by content and time.** A result screen stays up as long as the player leaves it; the same
  reading within a window is one play. Both modes feed one deduplicator. The server is idempotent on play
  time as a second net.
- **D12. Everything on disk lives under `%APPDATA%\PiuScoresWatcher\`**: settings (never the token), the token
  DPAPI-encrypted on its own, seven days of logs, and the screens the reader could not read, kept for review. Not
  `%LOCALAPPDATA%\PiuScoresWatcher\`: that is Velopack's install folder, replaced whole on every install and
  removed on uninstall, so a reinstall would sign the player out and lose their settings (found 2026-09-23,
  ratcheted). No telemetry (PRIVACY.md).
- **D13. The reader fails loud.** An unreadable screen is saved and surfaced; nothing is guessed, nothing
  partial is posted. A failed screen becomes a fixture (with the player's ok) before the fix is written.
- **D14. English only in v1.** A settings window has a dozen strings; the site's nine locales came with a
  community behind them. Every string is the owner's copy (CONTRIBUTING §6).
- **D15. The layout decides the mix.** Warm Up / Division screens post to `rise`, Arcade Station screens to
  `riseArcade`; a Challenge result — a division badge where the song title goes — is an aggregate and is
  skipped.
- **D16. `source` names the mode**: `watcher-grab` or `watcher-f12`, so the site can weigh the two if their
  readings ever differ (F12 files are JPEG; captures are lossless).
- **D17. Core is headless** (no OS target, no UI/Windows/Velopack reference — ratcheted); every Windows API
  sits in App behind a Core port. A fixture screenshot is a unit test on any machine.
- **D18. A result screen is recognised by its five judgment labels.** PERFECT blue, GREAT green, GOOD yellow,
  BAD magenta, MISS red, each at a fixed place on each layout; a frame is a result screen of that layout when
  every label shows (the fixtures score 0.36 and up per label, everything else scores zero). The two layouts —
  "DANCE GRADE" for Warm Up and Division, the arcade's own screen for the Arcade Station — are told apart the
  same way and decide the mix (D15).
- **D19. Numbers are read by template matching, never OCR.** Each field sits at a fraction of the frame
  (measured at 1080p, proven at 720p); its ink is cut into glyphs by column projection; a glyph reduced to a
  12×20 bitmap is the character of its most similar template of a compatible shape, or unread. The templates
  are the owner's screens plus the game's own number sprites (the DANCE GRADE score, the Arcade Station's score
  and stepball digits), generated by `tools/reader-lab` into `templates.json`. Held-out evaluation: every
  DANCE GRADE screen reads with any screen removed from training; the Arcade Station's judgment font has seen
  four results and lacks a `5` until one is played (§9).
- **D20. Chart type comes from colour, level from digits.** The DANCE GRADE badge is red for 5K SINGLE and blue
  for 6K DOUBLE (the game's caption for the half-double); the stepball is red SINGLE or green DOUBLE. A screen
  with no badge is a Challenge aggregate and not a play. The grade is not read: a grey sticker (no saturated
  bright pixels) is the broken marker, and the grade for the toast follows from the score.
- **D21. The checksum is exact on the score and a hundredth loose on accuracy.** The score recomputes in
  integers from the five counts and max combo; the accuracy the screen shows must land within 0.01 of the one
  the judgments make, because the game truncates a floating-point value and prints 96.83 for an exact 96.84.
  Accuracy is compared by its digits (the '.' is too small to segment, the '%' sometimes splits).
- **D22. A frame that reads but does not reconcile is "not yet", not "wrong".** The numbers land in two beats —
  judgments, then a score that counts up for about two seconds — so the watcher keeps reading until a frame
  reconciles (the window source, D28), and a fixture of that kind (`unsettled`) pins the refusal.
- **D23. Fixtures carry the player card blacked out**, always, by the lab (owner, 2026-09-22). The card is the
  only personal thing on a result screen and no field the reader uses is near it.
- **D25. One play per request, posted as read.** `ObservedPlay` is a complete, reconciled reading plus the
  title the OCR read plus the clock; the award is never claimed (the server derives it), `recordBrokenAsBest`
  is never sent (the mix's default). `source` is `watcher-grab`, `watcher-f12` or `watcher-replay`. The chart
  type goes as the enum name (`Single`, `HalfDouble`, `Double`) and the title as read: the server matches the
  song name exactly, case-insensitively, on the mix — a title the OCR garbles is a 404, never a wrong chart.
- **D26. Every answer the server can give is an outcome, not an exception.** Recorded, Refused (a 400 with its
  problem slug — `judgments-do-not-reconcile`, `played-at-invalid`, …), SongUnknown (404), Unauthorized (401),
  RateLimited (429 with `Retry-After`), Failed (anything else, including no network). Each carries what a toast
  needs; the toast copy itself is the owner's and arrives with the settings window.
- **D27. The token is DPAPI at rest, and an environment variable is the only other way in.** `token.bin` is
  encrypted for the Windows account; `PIUSCORESWATCHER_TOKEN` stands in for it during development (never
  persisted), which is how `--replay` posts to a local site before the settings window exists. The token is
  sent as the Basic password with a throwaway username, exactly as the site documents.
- **D28. Game-window mode copies the window with `PrintWindow`, not the Graphics Capture API.** Once a
  second while a `PUMP IT UP RISE` process has a window, its client area is rendered through DWM
  (`PW_RENDERFULLCONTENT`) into a bitmap — a CPU copy of one frame, a few milliseconds, no border drawn
  on Windows 10, no Direct3D interop, and nothing but that one window ever captured. A frame identical
  to the last (a sparse pixel fingerprint) is skipped before the detector sees it. If a player's setup
  comes out black (exclusive fullscreen), Graphics Capture is the fallback to add then, not now.
- **D29. Handled once per ten minutes, keyed by the numbers.** A play is remembered when it is posted or
  kept for review — never when it was merely "not yet" — so the window's once-a-second view of the same
  result screen costs a detect and a read and nothing more, and OCR runs once per play. The key is mix,
  type, level, judgments, max combo, score and the broken flag; the title is left out because OCR may
  spell it differently between frames.
- **D30. A window frame that does not reconcile waits; a screenshot that does not is kept.** The window
  brings a new frame every second, so "not yet" is right there; an F12 file is final, so the same
  frame is saved under `failed\` with why. A title the OCR cannot read is kept either way.
- **D31. Failed screens are a PNG and a JSON note** — the frame as captured, and why it failed with what
  was read — under `failed\`, until the player deals with them in the review dialog.
- **D32. F12 folders are found, not asked for.** `HKCU\Software\Valve\Steam\SteamPath` gives the Steam
  root, every numeric folder under `userdata` is an account, and RISE's screenshots sit at
  `760\remote\2756930\screenshots` under each; the settings' folder override replaces the lot. A file is
  read once its size has held still and it opens, because Steam writes the JPEG in steps.
- **D24. WorldMax is skipped, and no station has a stage-break result screen.** WorldMax ends on a mission
  summary without the five counts, so it cannot be posted and never detects; a broken run in the Arcade Station
  never reaches a result, and in Warm Up the grey grade is the whole story. `isBroken` is the grey sticker.

- **D33 (owner, 2026-09-23). One pull request for the MVP.** Everything through `v0.1.0` lands on #8 and the
  owner tests it once, installed, at the end (§6) — not a PR or a loop per piece. Dependabot follows suit: one
  combined pull request a month for NuGet and the workflow actions together, every update type, CI as the check.

- **D34 (owner, 2026-09-23). The icon is the PIU Scores arrow** — the site's favicon. The only copy is 77×77, so
  the small sizes (16–64, the tray's among them) are cut from it directly and the large ones (128, 256) upscaled;
  a bigger original would sharpen those.
- **D35 (owner, 2026-09-23). Every notification can be turned off** — all at once, or kind by kind: every
  recorded play, a play PIU Scores didn't take, a screen that couldn't be read, the token stopping working, an
  update. All on by default (D2). With them off, the tray menu's status line and the settings window still say
  when something is wrong.
- **D36. Every string a player reads lives in one file, `src/PiuScoresWatcher.App/Copy.cs`.** XAML binds to it
  (`{x:Static}`) and code formats through it, so the owner rewrites the copy in one place; the text there is the
  mocks' placeholder copy until he does. Ratcheted: no literal text in XAML.
- **D37 (owner, 2026-09-23). The mocks are iteration 1's contract**, with D34, D35 and one cut: the review
  dialog shows the file rather than sending it (there is nowhere to send one yet), so the couldn't-read toast's
  buttons are Review and Ignore.
- **D38. A play the site didn't record is kept for review, whatever the reason** — refused, unknown song, the
  token, a rate limit, no network — so a failure never loses a play silently; the review dialog shows each with
  its reason. A retry button comes after the MVP.
- **D39. First run opens whenever no token is stored**; after that the watcher starts in the tray. A second
  launch asks the running one to open its settings (a named event), and a notification's click reaches the
  running one too. A replay is a command and runs beside a running watcher.
- **D40. Start with Windows is the per-user Run key**, written only for an installed copy (never a dev build),
  following the setting (default on, D6), and removed with the notification registration by Velopack's
  uninstall hook.
- **D41. A notification's grade and award follow from the numbers**: RISE's nine-grade ladder and three marks on
  `rise`, the Phoenix 2 ladder and the eight plates on `riseArcade`, copied from PIU Scores (`PhoenixLetterGrade`,
  the `PhoenixPlate` tolerances, `AwardSets`) with the site file cited. A broken play shows its grade and no award.
- **D42. "Updated" comes from the version the settings last saw**, not from Velopack's update hook, which runs
  before any window can exist. Pause lives in memory: a relaunch watches again.
- **D43. Why a frame was kept is a closed list** (`KeptBecause`: four ways a screen can't be read, six ways a read
  play isn't recorded, "not connected" among them), so the review window's sentence for each is copy (D36) and the
  free-form detail stays in the note and the log. The pipeline reports every unrecorded play with the play; the App
  decides what that shows — a token outcome is the token's notification and its switch, the play still lands in
  Recent. With no token stored nothing is sent: the play is kept as "not connected", not as a rejected token.
  Notifications sit under Recent in the settings window, so the list a player opens it for stays in view.

## 3. The pipeline

`IScreenSource` (one adapter per mode) → `ResultScreenDetector` (pixel anchors; which station) →
`ResultScreenReader` (digit templates from the game's own font at layout positions that scale with the window;
the title by Windows OCR) → `PlayChecksum` (D10) → `Deduplicator` (D11) → `IPlaysClient` → `INotifier`.
ARCHITECTURE.md draws it.

The reader was built on the owner's 81 screenshots of 2026-09-21/22 (39 kept as fixtures: 30 results across
both layouts, coloured and grey, 1080p and 720p; one mid-count frame; two blank-number frames; a Challenge
aggregate; the WorldMax summary, the title screen, the song wheel and a loading frame). D18–D24 record what
they taught. Other resolutions come from the alpha testers, and every screen the reader cannot read becomes a
fixture before the fix.

The title alone is OCR'd: the App's `WindowsOcrTitleReader` turns the title bar into black text on white,
doubles it, and hands it to Windows; PIU Scores resolves the name and answers 404 for one it does not know.

## 4. The API

`POST api/v2/players/me/plays` (rise.md §6.3 D14; API.md), personal token, `Authorization: Basic
base64("anything:<token>")`. One request per play:

| Field | The watcher sends |
|---|---|
| `mix` | `rise` or `riseArcade` (D15) |
| `source` | `watcher-grab` or `watcher-f12` (D16) |
| `plays[]` | one play: `songName`, `chartType`, `level` (the server resolves the chart; `404` for an unknown title), `perfects`, `greats`, `goods`, `bads`, `misses`, `maxCombo`, `score`, `isBroken`, `playedAt` (the clock, ISO-8601 with offset) |
| `award` | omitted — the server derives it from the judgments and would refuse a wrong claim anyway |
| `recordBrokenAsBest` | omitted — the mix's default |

`200` returns `recorded`, `mix`, `scoringModel`. `400` problem types the toast must turn into sentences:
`judgments-do-not-reconcile` (a misread — should not reach the server past D10), `judgments-invalid`,
`score-invalid`, `played-at-invalid`, `legacy-mix`, `source-required`, `plays-required`. `401` — the token.
`404` — the song. `429` carries `Retry-After` (600 requests a minute per token; a session is nowhere near).

`GET api/v2/players/me` verifies the token on the settings window and greets the player by name.

## 5. Player experience

1. **Install.** One download, one click, no wizard; a tray icon and a Start menu entry. Signed (D4), so no
   SmartScreen warning once the account exists.
2. **First run.** Paste the token (checked on the spot: "Connected as …"); choose the mode; Start with
   Windows (on). Done.
3. **Then nothing.** RISE starts, it wakes. A result screen → a notification: "Recorded · Gargoyle · 5K S18 ·
   975,429 · SS · Full Combo". RISE closes, it sleeps. A screen it cannot read → a notification that says so, and
   the frame kept for review. Every kind of notification can be switched off (D35).

## 6. The MVP — one pull request (D33)

Everything from the reader to the installer lands on **one pull request, #8**, and is tested in **one loop at
the end**. Commits inside it stay small — docs first, each green on its own — but nothing ships as a separate
PR. (Commit 1, the repository structure, went straight to `main`: an empty repository has nothing to open a PR
against.)

The MVP is the Warcraft Logs bar of §1: download, paste a token, play — and every result screen becomes a play
on the site with nothing else to do.

| Piece | State on #8 |
|---|---|
| Reading a result screen; the checksum | done — every fixture reads and reconciles (D18–D24) |
| Posting a play; the token at rest | done (D25–D27) |
| Watching the game window once a second; the F12 folders | done, not yet tried against the running game (D28–D32) |
| Updates | done — checked at start-up, applied on the next launch (D8) |
| The installer | wired in `release.yml`; built on the owner's PC for the loop |
| First run, settings, the tray menu, notifications, the review dialog | built — iteration 1 of the mocks, with the arrow icon and switchable notifications (D34–D38, D43); smoke-tested on dropped F12 screenshots, not yet with the game |
| Start with Windows; a second launch opens the running one's settings | built (D39, D40); the second launch smoke-tested, the Run key waits for an installed copy |
| Every player-facing string | the mocks' placeholder copy, all in `App/Copy.cs` until the owner rewrites it (D36) |

**The one loop.** The UI is in. Install the build made on the owner's PC, paste a token, and play one
session — a Warm Up result, an Arcade Station result, F12 on one, one left up for a minute, a Division result if
convenient, and a dozen Arcade Station plays for the missing `5` (§9) — then check the site's journal. HOW-TO-TEST's
checklist is the script. Anything it turns up is fixed on #8.

**Then** merge, tag `v0.1.0`, and the release workflow publishes the installer — signed once the Trusted Signing
variables exist (D4).

**After the MVP**, each on its own schedule: the site's download card (§7, the owner's copy), the alpha with two
RISE players, the Graphics Capture fallback if a setup comes out black (D28), and sending a failed screen to the
developer — in the MVP the review dialog shows the file, because there is nowhere to send one yet (decided unless
the owner objects).

## 7. Site side

Small: a "Capture app" card on the RISE upload page — what it does, the download button
(`releases/latest/download/PiuScoresWatcher-win-Setup.exe`), three setup steps — and a pointer from `/Account`
beside the tokens. Owner's copy. The v2 plays write exists already (rise.md D14).

## 8. Evidence

- rise.md §1, §5 and §10 — the two stations, scoring verified to the point on 20 screens, the nine-grade
  ladder, the marks, the grey grade, Challenge aggregates, the 40 screenshots.
- The owner's Steam screenshots folder (`Steam\userdata\<id>\760\remote\2756930\screenshots`) — the F12
  source and the base fixture set (81 shots on 2026-09-22, every result reconciling to the point once two of
  my own transcriptions were corrected).
- The RISE install's `resources.assets` — the number sprites (`DanceGrade_Text_MainScore_*`,
  `Arcade_Score_Number_*`, `Arcade_BigNumber_*`) the template set is completed from.

## 9. Open questions

- **The Arcade Station's judgment font has never shown a `5`.** Four distinct Arcade results exist; every other
  digit is covered (the score and stepball fonts are complete from the game's sprites). A dozen Arcade Station
  plays from the owner close it; until then an Arcade result with a 5 in a judgment count reads as unreadable
  and is kept for the developer, exactly as D13 says.
- **Steam Deck.** RISE runs on Deck; the watcher is Windows-only and F12 screenshots on a Deck stay on the
  Deck. Park until a Deck player asks; Avalonia is the route (D7).
- **The name.** "PIU Scores Watcher" is a placeholder; the owner names it before v0.1.0 (the icon is settled,
  D34).
- **Steam's "uncompressed copy" folder.** Steam can save a lossless PNG beside the JPEG; worth watching both?
