# Domain

What a PUMP IT UP RISE result screen says, and what the watcher makes of it. The full glossary of Pump It Up terms is PIU Scores' [DOMAIN.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/DOMAIN.md); the RISE research it draws on is its [design/rise.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/design/rise.md).

## RISE

PUMP IT UP RISE (Steam app 2756930) is the official PC port, played with hands — keyboard, gamepad, Steam Deck. **One game holds two separate ecosystems, and records never cross between them**, so PIU Scores tracks it as two mixes:

| | **RISE mode** — Warm Up · Division · Challenge · WorldMax | **Arcade Station** |
|---|---|---|
| PIU Scores mix | `rise` | `riseArcade` |
| Chart types | 5K SINGLE, 6K H.DOUBLE | 5K SINGLE, 10K DOUBLE |
| Grades | **nine**: SSS SS S AA A B C D F — no plus tiers, no AAA | the full Phoenix ladder with plus tiers, on the Phoenix 2 floors |
| Awards | three **marks**: PERFECT GAME · FULL COMBO · NO MISS | the eight Phoenix plates |
| Lifebar | its own HP model; Warm Up is "break off" — HP 0 does not end the song, but the grade turns **grey** | "break on": HP 0 ends the song, like the arcade |
| Holds | the head of every hold is a judged tap; pre-holding scores a MISS | arcade behavior |

Nothing exports: saves are encrypted, the game server has no web surface, Steam publishes no leaderboards. The result screen is the only place a play is written down, which is why this app exists.

## The result screen

Every station's result screen carries the same fields, in the game's own font at fixed positions that scale with the window:

- the **song title** and the **chart** — type and level (`5K S16`, `6K HD23`; `10K D20` in the Arcade Station);
- the five **judgments** — PERFECT, GREAT, GOOD, BAD, MISS;
- **MAX COMBO**;
- the **score**, seven digits with leading zeros (`0960836`);
- **accuracy**, a percentage **truncated** (never rounded) to two decimals;
- the **grade**, and the **mark** (RISE mode) or **plate** (Arcade Station) when one was earned.

Two things the watcher must tell apart before it reads anything:

- **Which station.** An Arcade Station screen shows a plate and can show a plus grade; a Warm Up / Division screen shows a mark and never a plus. The layout decides the mix.
- **A Challenge result is not a play.** Challenge shows a *division badge* where the song title goes and its numbers are sums across several songs (accuracy is the per-song mean). The watcher skips it.

Division result screens look identical to Warm Up's and post the same way.

## Scoring — the three rules the watcher copies

All three are PIU Scores' rules, verified there against the owner's result screens to the point; the watcher copies them (with the site file cited) so it can refuse a misread before posting.

1. **Score** = ⌊ 995,000 × accuracy + 5,000 × maxCombo ⁄ notes ⌋, where notes = P + G + Gd + B + M.
2. **Accuracy** = (P + 0.6 G + 0.2 Gd + 0.1 B) ⁄ notes. The screen shows it truncated to two decimals: 93.8073 displays as **93.80**.
3. **The award** follows from the judgments: PERFECT GAME = all perfects; FULL COMBO = no good, bad or miss; NO MISS = no miss. PIU Scores stores RISE's marks in its Phoenix vocabulary — PG, UG (Ultimate Game) and SG (Superb Game) — and shows them as PG / FC / NM on the RISE mix. The Arcade Station's plates are the Phoenix ones (PG, UG, EG, SG, MG, TG, FG, RG). The server derives the award from the judgments itself and refuses a claimed award they do not earn, so the watcher may simply not claim one.

A score that does not recompute from the judgments means a digit was misread. That is the whole reason the rules are here.

### The RISE grade ladder (mix `rise`)

| Grade | From |
|---|---|
| SSS | 990,000 |
| SS | 970,000 |
| S | 950,000 |
| AA | 900,000 |
| A | 750,000 |
| B | 650,000 |
| C | 550,000 |
| D | 450,000 |
| F | 0 |

SSS to AA are measured; A and below are placeholders that PIU Scores corrects from recorded plays. The watcher posts the grade it read and the server records a disagreement rather than failing on one.

### Grey grade

In Warm Up, if the gauge empties even once the grade turns grey and the run counts as broken (no EXP). The watcher posts `isBroken: true`; the Arcade Station cannot show a grey grade because HP 0 ends the song there.

## Note counts

RISE-mode judgment totals do **not** equal the arcade note counts — the game re-cut hold sections and judges hold heads — so each RISE chart's note count is learned from captures on the site (`ChartMix.NoteCount`). The watcher does nothing special: it posts the five counts, and the sum is the note count.

## What a play becomes on PIU Scores

`POST api/v2/players/me/plays` with a personal token. The server recomputes the score from the judgments (`400 judgments-do-not-reconcile` when it does not match), derives the award, writes the play to the score journal and, when it beats the record, makes it the personal best. The mix must be Phoenix-scored (`rise` and `riseArcade` both are). The wire shape is PIU Scores' [API.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/API.md) and its Swagger; [design/watcher.md](design/watcher.md) §API lists the fields the watcher sends.
