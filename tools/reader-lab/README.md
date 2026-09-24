# The reader lab

The Python prototype the result-screen reader was designed on, kept so the reader can be re-measured and
its templates regenerated when RISE changes a screen. Not part of the solution; nothing here ships.

Needs Python 3.12+ with `pillow` and `numpy`; `sprites.py` additionally needs `UnityPy` and a local RISE
install.

| Script | What |
|---|---|
| `reader.py` | The prototype: layout detection by the five judgment labels, digit segmentation, glyph templates learned from the labeled screens, a leave-one-screen-out evaluation, and a scan of the folder for result screens that are not labeled yet. Reads the repo's fixture screens by default; `SHOTS=<folder>` points it at a Steam screenshots folder. Its label table (`EXPECTED`) is the source `expected.json` is written from. |
| `sprites.py` | Exports the game's number sprites (the DANCE GRADE score, the Arcade Station score and stepball digits) from the install into `./sprites/` — game art, ignored by git. |
| `templates.py` | Builds the template set the reader ships: the sprites plus every glyph the labeled screens teach, deduplicated, written to `src/PiuScoresWatcher.Core/Recognition/templates.json`. Prints how the sprite-only set reads the screens first, so a sprite that no longer matches the game shows up. |
| `songlist.py` | Warm Up's song list (watcher.md D45-D48): detection by the yellow banner, the lit tab and the lit level box; the level box and best-score digit families (`wllevel`, `wlvalue`); the grade-badge features. `python songlist.py` reports on the labeled song-list screens; `--write` merges the two families into `templates.json` (the result screens' families untouched) and writes `grades.json`; `--fixtures` adds the song-list screens to the fixtures. The nine grade sprites it reads are `sprites/Rise_Grade_<G>.png`, the game's own art as PIU Scores serves it under `letters/Rise` (ignored by git like the rest). |
| `fixtures.py` | Copies labeled screens from a Steam screenshots folder into `tests/PiuScoresWatcher.Tests/Fixtures/screens/` with the player card blacked out, and writes `expected.json`. `python fixtures.py <steam screenshots folder>`. |

## Adding a screen

1. Label it in `reader.py`'s `EXPECTED` (the numbers as the screen shows them; the checksum will tell you if you
   mistyped one).
2. `python fixtures.py "<Steam>\userdata\<id>\760\remote\2756930\screenshots"` — the masked copy and `expected.json`.
3. `python reader.py` — the leave-one-out report says whether the reader already reads it.
4. If a glyph is new (an Arcade Station digit never seen before, a changed font): `python sprites.py` once, then
   `python templates.py`, and `dotnet test` from the repo root proves every fixture still reads.

`templates.json` is generated output. Never edit it by hand.
