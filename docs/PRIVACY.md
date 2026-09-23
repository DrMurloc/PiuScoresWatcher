# Privacy

> Player-facing. This is a factual draft for the owner to rewrite in his own voice before the download page links it; the facts are binding on the code either way.

**What it looks at.** In game-window mode, the watcher captures the PUMP IT UP RISE window — only that window, never the desktop or any other window — once a second while RISE is running, and looks at nothing when it is not. In Steam-screenshot mode it reads new files in RISE's Steam screenshots folder and nothing else. Frames that are not a result screen are discarded on the spot; they are never stored or sent.

**What it sends.** One request per play, to PIU Scores, containing exactly: which mix, the song title, chart type and level, the five judgment counts, max combo, the score, whether the run was broken, when it was played, and which mode captured it. No screenshot is ever uploaded. Nothing is sent anywhere else, and nothing is sent about the machine, the player or the session.

**What it keeps.** Under `%APPDATA%\PiuScoresWatcher\`: the settings (mode, Start with Windows, the screenshots folder), seven days of logs, and the result screens it could not read — those stay on the machine, and nothing sends them anywhere unless the player chooses to. The PIU Scores token is stored encrypted for the Windows account it was entered under; it is never written in clear and never leaves the machine except as the credential on the requests above.

**No telemetry.** The watcher does not phone home. Its only outbound traffic is the plays it posts to PIU Scores and its check of GitHub for a newer version at start-up.

**It reads nothing from the game itself.** No game files, no memory, no saves, no Steam account data. It looks at the screen the way a player does.

**Uninstalling** removes the app; the folder above stays until deleted, so a reinstall keeps the settings. Deleting that folder removes everything the watcher ever kept.
