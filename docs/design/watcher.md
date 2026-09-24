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
- **D4 (owner, 2026-09-22). Signed with Azure Artifact Signing** (Trusted Signing until Microsoft renamed
  it). The release workflow signs when the account's variables exist and releases unsigned until then
  (HOW-TO-RUN §Signing); it runs in the `release` environment, the one exact subject the Azure sign-in
  trusts. The account is the owner's: an individual validation, US and Canada only, with his legal name on
  the certificate; he lifted the Azure read-only rule for the setup on 2026-09-24.
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
  leaves the machine, within the point the game's own arithmetic can land off the formula (D57); the server
  recomputes again, with the same point, and answers `400 judgments-do-not-reconcile` otherwise. The
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
- **D14. ~~English only in v1.~~ Superseded by D58** (owner, 2026-09-24): the watcher speaks the site's
  languages. Every English string is the owner's copy (CONTRIBUTING §6).
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
  five results, and its one `5` came from the owner's first test loop (§9).
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
- **D36. Every string a player reads lives in one file, `src/PiuScoresWatcher.App/Copy.cs`**, in English. XAML
  binds to it (`{x:Static}`) and code formats through it, so the owner changes the copy in one place. **The mocks'
  wording is approved copy** (owner, 2026-09-24: approving the mocks approved their words, D37); a line written
  since is called out in its PR. Ratcheted: no literal text in XAML, and every line has its translations (D60).
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
- **D44 (owner, 2026-09-23). A dev run points at the local site and keeps its own data.** The launch profile makes
  F5 and `dotnet run` talk to the site's local Aspire run (`https://localhost:7144`, the site's own launch
  settings), with a second profile for production; the app itself still defaults to production, and an installed
  copy never reads a launch profile. A run aimed at any site other than production keeps its settings, token, logs
  and kept screens under `%APPDATA%\PiuScoresWatcher\dev\<host>-<port>\`, takes its own one-copy lock, and names the
  site in its title and tray tooltip — so it runs beside the installed copy and never touches its token.

**Bulk capture** — every best a player already has, from RISE's song list, without replaying anything (owner,
2026-09-23; mocks: https://claude.ai/artifact/GNA8brvdiEB3aEbnFoXVzf, approved with "get it in").

- **D45 (owner, 2026-09-23). Warm Up's song list only, in v1.** The Warm Up song list shows the selected chart's
  BEST SCORE, ACCURACY, MAX COMBO and grade. The Arcade Station's list shows the best score, grade and plate, but
  its plate is a game bug — stale or blank as the selection moves (Ugly Dee D18 at 988,166 showed two plates two
  seconds apart; the owner has reported it) — so the Arcade Station waits.
- **D46 (owner, 2026-09-23). A capture is a play, the way a Phoenix first import is**: the same write, the same
  Discord cards, highlights and sessions. It carries no judgments; "judgements should never be mandatory", so the
  site makes them optional on the plays write (a site change on its own PR). One capture per request, like a live
  play — no batching: a first import's card truncates, and the site's sittings group a run. A capture is always a
  pass; the owner has never seen a failed best on the list.
- **D47. What a capture reads.** The song (Windows OCR, matched against the site's chart list, D49), the chart
  type (the lit 5K SINGLE or 6K DOUBLE tab), the level (the lit box) and the best score. The grade badge is the
  check: it must be the grade the score earns, compared against the nine RISE grade sprites, or the frame goes to
  review. Accuracy and max combo are neither sent nor used: the list's figures do not always come from the
  best-score play (Aragami S19 shows 971,789 beside 97.95%, which no one play can score), so they check nothing.
- **D48. A capture is taken once the panel has read the same for half a second**, sampled five times a second
  while a run is on. The wait rides out the panel changing between charts; a panel that lingers on the previous
  chart's numbers longer than that is the one risk the owner's test pass watches for (the Arcade plate bug says
  the game can lag).
- **D49. A run starts by fetching the mix's charts and the player's bests** (`GET api/v2/charts`, `GET
  api/v2/players/{id}/scores`). Only a capture above the stored best is sent; the rest are "already there". The
  title is matched against the chart list — case, spacing and punctuation ignored, and O/0, I/l/1 and S/5 misreads
  forgiven, among the songs that have the lit chart — so what is posted is the catalog's own spelling. Live result
  screens use the same match whenever the chart list is loaded; without one they post what the OCR read, as before.
- **D50. Feedback is sound**: a chime for sent, a tick for already there, a low tone for a panel that could not be
  read or a capture PIU Scores did not take. The tones are synthesized, no audio files; one switch turns them off.
  The first play test (2026-09-24) could not hear the mock's soft beeps over Warm Up's song previews, so they
  were remade sharp-edged and bright (a snap and a ding, a click, a buzz) and as loud as a WAV goes without
  distorting — 11 to 23 dB louder; the watcher's own slider in Windows' volume mixer turns them down.
  No notification during a run; one summary when it ends, with its own switch (the seventh kind, D35); one line in
  Recent per run.
- **D51. A run starts from the tray or settings, through a window** that explains the keys every time and says
  how many bests PIU Scores already has. It ends when the song list has been gone for thirty seconds (a song
  started), when a result screen appears, when RISE closes, when the list never shows in ten minutes, or from the
  tray or settings. A run looks at the game window whatever the watching mode, and overrides a pause while it is on:
  starting one is the player asking for exactly that.
- **D52. A capture's `source` is `watcher-songlist`**, so the journal and the Undo page name it; it is dated when
  it is read, like a first import. F12 works during a run too: a Steam screenshot of the song list is read the same
  way, without the half-second wait — a file is already still.

**Reading titles** — found by the bulk capture smoke, 2026-09-23: Morrighan's title on the song list read as nothing.

- **D53. Windows OCR reads the title from a soft white-and-colourless mask at its 1080p size** (`TitleInk`). A
  title is white wherever it sits — the yellow bar, the Arcade Station's blue band, a jacket on the song list — so
  a pixel's ink is how bright it is times how colourless, graded rather than cut, and the region is scaled to what
  it measures on a 1080p screen. The old preparation (anything brighter than 200 is ink, then doubled) let bright
  colours in the art through, and at double size Windows OCR returned nothing for short titles (4NT, 86, 1948,
  KUGUTSU) even on a clean page. Over the fixtures, through `--replay` (`tools/reader-lab/titles.py`): 13 of 38
  titles read exactly and 26 of 38 matched the chart list before; 33 and 36 after. Cynical and one Aragami still
  read as nothing and go to review.

**The second loop evening** (owner, 2026-09-23, "definitely seeing some scores missing"): six of a dozen plays
never reached the site, and two stepballs were read at the wrong level.

- **D54 (owner's go). A window result that never reconciles is kept.** The game window used to call such a
  screen "not yet" for as long as it stood there, so a misread number — the Arcade Station's first 5, read as a
  6 — vanished without a word unless the player also pressed F12. Now it is kept for review, with the same
  notification an unreadable screenshot gets, once its numbers have stood still for three seconds and still
  disagree, or when the screen goes away (or the next play arrives) before they ever agreed. A score still
  counting up is never kept: its judgments are final a beat before the score, so a frame of the same play that
  reconciles later clears it. A play a screenshot already kept is not kept twice.
- **D55 (owner's go, "the 8 6 review/fix"). The title is tried up to four ways, and the chart list decides.**
  Windows OCR returned nothing for VANISH, D and 8 6 on clean pages. The page now carries a margin of paper
  (`TitleInk.Pages`), and the reader tries, in order, the page at the 1080p size, at twice that, with wide gaps
  between letters closed up (8 6 is two lone characters to Windows until it is one word), and closed up with
  the strokes thinned (Warm Up's song list sets 8 6 in heavy outlined type) — stopping at the first reading
  that names a chart. The chart list also accepts a reading with a piece missing or extra when it fits one song
  alone: "• Alice In Wonderworld" is *K.O.A : Alice In Wonderworld*, "O/ox (Percent X)" is *%X (Percent X)*.
  Over the 50 fixture titles through `--replay`: 43 read exactly at the first attempt (33 before), 49 match the
  chart list at some attempt. **D** — a one-letter title — is the one no attempt reads; it goes to review.
- **D56. On the Arcade Station the note count decides the chart.** The stepball read 12 as 18 (8 6) and 18 as
  13 (%X) — once from a screenshot, once from the window — and the level is outside the checksum, so a misread
  could have filed a score on a chart the player never played. A full play's judgments add up to the chart's
  note count, which PIU Scores knows for 1,969 of the Arcade Station's 2,460 charts: the chart must have that
  many notes; when the level read has not, the song's chart that has is the one posted; when the title matches
  no chart at the level read, the charts with that many notes are searched instead; and a play no chart of its
  song adds up to is kept for review (`ChartDisagrees`), never posted. A broken play is not checked — it may not
  add up. Warm Up's charts carry no note count on PIU Scores yet (0 of 2,992), so a Warm Up level is taken as
  read; its level digits have not been misread. A play is also recognised as the same play from both sources
  whatever level each read, so one play is never posted twice.
- **D57. The checksum allows a point.** VECTOR's 528/44/13/2/29 with a combo of 122 makes 901,012.99 by the
  formula, and the game prints 901,013. PIU Scores already allows the point (its formula matched 2,268 of 2,277
  real judgment-carrying records exactly); the watcher refused the play. A misread count moves the score by
  far more than a point, so the checksum still catches every one.

**Languages** (owner, 2026-09-24: "we want to support all the languages we support there", a picker "with
'Machine Default' as default", "don't worry about murloc", then "get them all localized").

- **D58 (owner, 2026-09-24). The watcher speaks PIU Scores' languages, less Murloc**: English, Español (México),
  Español (España), Português, 한국어, 日本語, Français and Italiano — the site's `SupportedCultures`, in its
  order, each named in its own language. Replaces D14.
- **D59 (owner, 2026-09-24). Language is the last section of settings, and Machine Default comes first.** Machine
  Default is where every player starts, and it is stored as no choice at all. It follows Windows' preferred
  languages in order, each placed the way the site places a browser's: an exact match, else the language's
  default region — Spanish from anywhere but Mexico is Spain's (the site's own ruling, 2026-08-03), French is
  France's, Portuguese is Brazil's — and English when none places. Picking a language redraws the settings
  window on the spot and relabels the tray menu, and every notification after it speaks it; nothing restarts
  and watching never stops. Other windows follow the next time they open; first run follows Windows and has no
  picker.
- **D60. English is the key, as on the site.** A translation is an entry in `Resources/Strings.<code>.resx` keyed
  by the English line verbatim, so a rewritten line is a new key and the tests fail until every language has
  it. Copy.cs is the only English — there is no English resx; the ratchets read the keys out of Copy.cs. The
  site's rules come with it: keys in alphabetical order (`OrdinalIgnoreCase`, so two branches' new keys never
  meet at the end of a file), and no two keys that differ only by case (MSBuild's resource compiler keeps one
  and drops the other without a word). One rule is the watcher's own: a translation keeps exactly the `{0}` holes
  and `{KEY}` key caps its English has. A section label is its word upper-cased in code (`ACCOUNT` is
  `Account`), which also keeps it from twinning with the same word elsewhere. The game's own labels (`5K S18`,
  `Arcade 10K D23`) are marked `Verbatim` and never translated; grade letters come from Core and are not copy.
  Arch-test enforced, `TranslationTests`.
- **D61. Numbers, dates and plurals follow the language; the log stays English.** A score, a time and a date are
  written in the chosen language's formats, as on the site, and a count picks its form by the language's rule
  (French and Portuguese count 0 as one). A log line is written in English whatever the player reads, so a log a
  player sends is readable. Reading titles never changes: Windows' English OCR, whatever the language (D55).
- **D62. The site's glossaries are the vocabulary — linked, not copied** ([LOCALIZATION.md](../LOCALIZATION.md)).
  The site's repository is public, each glossary runs 250 to 880 lines of mostly site words, and a term a player
  meets on both must not drift apart: 채보 for a chart, 퍼펙트 게임 on a Korean plate, and each language's
  register — Korean's formal 합쇼체, Japanese です/ます, `tú` in Spain, `usted` in Mexico (the site glossary's
  documented register; its open tú/usted fork is the site's to settle), `você`, `vous`, `tu`. The watcher's own
  words — capture, screenshot, the game window, the tray, the token, review, the three sounds, Machine Default —
  are LOCALIZATION.md's table. The translations are Claude's, as the site's are.

**Sounds while playing** (owner, 2026-09-24: "By default, we should play the sound used in the bulk-capture flow
for 'your score was submitted' when a score is captured during normal play. Make that toggleable off. Also include
the invalid vs failed noises").

- **D63 (owner, 2026-09-24). A play makes a sound too, on by default**, with one switch in settings (Watching,
  "Sounds while playing") apart from bulk capture's own (D50). The sound plays whatever the notification switches
  say: Windows' do-not-disturb rules often hold notifications back while a game runs full screen, so the sound is
  the feedback that reaches a player mid-session.
- **D64. Bulk capture's three sounds, one meaning each.** The chime: the play is on PIU Scores. The low tone: the
  screen couldn't be read — in time to press F12 again while it is still up. The tick: it was read, and PIU Scores
  didn't take it (refused, unreachable, the token). A play's notification goes silent when its sound plays, so a
  play makes one sound, not two, and a screen seen twice makes none the second time. A bulk capture keeps its own
  meanings (D50).

## 3. The pipeline

`IScreenSource` (one adapter per mode) → `ResultScreenDetector` (pixel anchors; which station) →
`ResultScreenReader` (digit templates from the game's own font at layout positions that scale with the window;
the title by Windows OCR) → `PlayChecksum` (D10) → `Deduplicator` (D11) → `IPlaysClient` → `INotifier`.
ARCHITECTURE.md draws it. A bulk capture run (D45–D52) branches off the same sources: `SongListDetector` →
`SongListReader` (the lit tab, the lit level box, the best score, the grade badge) → `BulkCaptureRun` (the
half-second wait, the chart-list match, the stored bests) → `IPlaysClient`, with sounds instead of notifications.

The reader was built on the owner's 81 screenshots of 2026-09-21/22 (39 kept as fixtures: 30 results across
both layouts, coloured and grey, 1080p and 720p; one mid-count frame; two blank-number frames; a Challenge
aggregate; the WorldMax summary, the title screen, the song wheel and a loading frame). D18–D24 record what
they taught. Other resolutions come from the alpha testers, and every screen the reader cannot read becomes a
fixture before the fix.

The title alone is OCR'd: Core's `TitleInk` turns the title's region into dark letters on white with paper
around them (D53), in up to four pages the App's `WindowsOcrTitleReader` hands to Windows one at a time until a
reading names a chart (D55). The name is matched against the chart list when it is loaded (D49) — on the Arcade
Station at the level the chart's note count says was played (D56); PIU Scores resolves what is posted and answers
404 for a song it does not know.

## 4. The API

`POST api/v2/players/me/plays` (rise.md §6.3 D14; API.md), personal token, `Authorization: Basic
base64("anything:<token>")`. One request per play:

| Field | The watcher sends |
|---|---|
| `mix` | `rise` or `riseArcade` (D15) |
| `source` | `watcher-grab` or `watcher-f12` (D16) |
| `plays[]` | one play: `songName`, `chartType`, `level` (the server resolves the chart; `404` for an unknown title), `perfects`, `greats`, `goods`, `bads`, `misses`, `maxCombo`, `score`, `isBroken`, `playedAt` (the clock, ISO-8601 with offset) |
| `award` | omitted — the server derives it from the judgments and would refuse a wrong claim anyway |
| a bulk capture | `source` `watcher-songlist`; the play carries `songName`, `chartType`, `level`, `score`, `isBroken` false and `playedAt` (when it was read), and no judgments or max combo (D46) |
| `recordBrokenAsBest` | omitted — the mix's default |

`200` returns `recorded`, `mix`, `scoringModel`. `400` problem types the toast must turn into sentences:
`judgments-do-not-reconcile` (a misread — should not reach the server past D10), `judgments-invalid`,
`score-invalid`, `played-at-invalid`, `legacy-mix`, `source-required`, `plays-required`. `401` — the token.
`404` — the song. `429` carries `Retry-After` (600 requests a minute per token; a session is nowhere near).

`GET api/v2/players/me` verifies the token on the settings window and greets the player by name. A bulk capture
run also reads `GET api/v2/charts?mix=rise` (the chart list the titles are matched against) and `GET
api/v2/players/{id}/scores?mix=rise` (the player's bests), following each page's `next` link (D49).

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
| Every player-facing string | the mocks' wording, approved, all in `App/Copy.cs` (D36) |
| A play's sound | built: on by default with its own switch; the chime, the low tone and the tick (D63, D64); smoke-tested — a play's notification goes silent when its sound plays, and keeps Windows' sound when the switch is off |
| Languages | built: the site's eight less Murloc, the Language section with Machine Default (D58–D62); the English unchanged over 370 compared lines; smoke-tested in every language — the windows, the notifications, a live switch |
| Bulk capture from Warm Up's song list | built to the iteration-2 mocks (D45–D52) and smoke-tested end to end on the fixtures against a stand-in site (start window, sounds, counts, Stop, the summary, Recent); PIU Scores takes captures since it made judgments optional (its PR 358, merged 2026-09-24); one card per run waits on its sittings (PR 357), which ship before the watcher's first release |
| Reading titles | a soft white-and-colourless mask at the 1080p size (D53): 33 of 38 fixture titles exact, 36 matched |

**The one loop.** The UI is in. Install the build made on the owner's PC, paste a token, and play one
session — a Warm Up result, an Arcade Station result, F12 on one, one left up for a minute, a Division result if
convenient, and a dozen Arcade Station plays for the missing `5` (§9) — then check the site's journal. HOW-TO-TEST's
checklist is the script. Anything it turns up is fixed on #8.

**Then** merge, tag `v0.1.0`, and the release workflow publishes the installer — signed once the Artifact Signing
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

- **The Arcade Station's fonts are thin on samples.** The first 5 came from VANISH (2026-09-23) and the second
  evening added four more Arcade results, but held out of training the stepball still misreads (18 as 12, 17 as
  18, 19 as 12) and so, once, does the score. The checksum catches the numbers; D56 catches the level wherever
  PIU Scores knows the chart's note count. More Arcade results with F12 keep making both sturdier.
- **One-letter titles.** No attempt reads **D** (D55). A Warm Up note count on PIU Scores would let the chart be
  found by its notes the way the Arcade Station's are (D56); until then such a play goes to review.
- **Steam Deck.** RISE runs on Deck; the watcher is Windows-only and F12 screenshots on a Deck stay on the
  Deck. Park until a Deck player asks; Avalonia is the route (D7).
- **The name.** "PIU Scores Watcher" is a placeholder; the owner names it before v0.1.0 (the icon is settled,
  D34).
- **Steam's "uncompressed copy" folder.** Steam can save a lossless PNG beside the JPEG; worth watching both?
