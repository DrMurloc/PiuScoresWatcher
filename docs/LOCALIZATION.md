# Localization

The watcher speaks PIU Scores' languages, less Murloc: English, Español (México), Español (España), Português,
한국어, 日本語, Français and Italiano (design D58–D62). A player picks one under Settings → Language; Machine Default,
where everyone starts, follows Windows.

## How a line is translated

- **English lives in `src/PiuScoresWatcher.App/Copy.cs` and nowhere else.** Every line with words in it goes
  through `L("…")` there, and the English sentence is the key — the convention PIU Scores' own resx files use.
- **Every other language is `src/PiuScoresWatcher.App/Resources/Strings.<code>.resx`**: one `<data>` per line,
  its `name` the English exactly, its `value` the translation. There is no English resx.
- **Insert in alphabetical position** (ignoring case), never at the end: two branches that both append meet on
  the same last lines, and a resx conflict resolves badly.
- **A changed English line is a new key.** Add the new entry to every file and delete the old one in the same
  change; `TranslationTests` fails until every language matches Copy.cs exactly.
- **Two keys never differ only by case.** MSBuild's resource compiler keeps one and drops the other without a
  word, and that language quietly shows English. A section label is its word upper-cased in code, so `ACCOUNT`
  is the key `Account`.
- **Keep every `{0}` hole and every `{KEY}` key cap** (`{A}`, `{TAB}`) exactly as the English has them, in
  whatever order the language wants. A key cap is drawn as a key, so it stays in capitals and never moves inside
  a word.
- **A count has two keys**, the one form and the other (`1 screen couldn't be read`,
  `{0} screens couldn't be read`). Translate both, even in a language with one form; French and Portuguese use the
  one form for 0 as well.
- **`MMM d` is a date pattern** ("Sep 24"): translate it into the language's own short month-and-day pattern, in
  .NET's custom format, with literal letters in quotes.
- **`Verbatim("…")` marks what the game prints as-is** — the chart labels. It is never a key.

## Vocabulary and register

The site's glossary for each language is the vocabulary and the voice. Read it before translating; a term a
player meets on the site and in the watcher is the same word in both.

| Language | Code | Site glossary | Register |
|---|---|---|---|
| Español (México) | es-MX | [LOCALIZATION-es-MX.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/LOCALIZATION-es-MX.md) | `usted`, the glossary's documented register (its tú/usted fork is the site's to settle); `puntaje` |
| Español (España) | es-ES | [LOCALIZATION-es-ES.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/LOCALIZATION-es-ES.md) | `tú`; buttons in the infinitive; `score` and `chart` stay English |
| Português | pt-BR | [LOCALIZATION-pt-BR.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/LOCALIZATION-pt-BR.md) | `você`; sentence case |
| 한국어 | ko-KR | [LOCALIZATION-ko-KR.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/LOCALIZATION-ko-KR.md) | formal 합쇼체 (`-습니다`); ASCII punctuation; 채보 for a chart |
| 日本語 | ja-JP | [LOCALIZATION-ja-JP.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/LOCALIZATION-ja-JP.md) | です/ます; full-width punctuation, half-width numerals; 譜面 for a chart |
| Français | fr-FR | [LOCALIZATION-fr-FR.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/LOCALIZATION-fr-FR.md) | `vous`; a no-break space before `?`, `!`, `:` and `;` |
| Italiano | it-IT | [LOCALIZATION-it-IT.md](https://github.com/DrMurloc/PumpItUpScoreTracker/blob/main/docs/LOCALIZATION-it-IT.md) | `tu`; straight apostrophes; `chart` invariable |

### Never translated

PIU Scores and PIU Scores Watcher; RISE, Warm Up and Arcade Station (the game's names); Steam, Windows and F12;
the tabs 5K SINGLE and 6K DOUBLE; key caps; grade letters (SSS+, AA…); the chart labels (5K S18, 6K HD23,
Arcade 5K S20); and "API tokens" in the path to the site's token page, which the site shows in English. Plate
and mark names follow the site: English everywhere but Korean (퍼펙트 게임).
