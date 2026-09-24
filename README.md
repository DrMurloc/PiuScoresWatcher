# PIU Scores Watcher

A Windows desktop app that watches for scores in PUMP IT UP RISE and records them on
[PIU Scores](https://piuscores.arroweclip.se).

Status: **the MVP is being built on one pull request, #8.** The reader, posting and capture are in: the tray
app captures the RISE window once a second while the game runs and reads every F12 screenshot, posting each
reconciled play once. The first-run and settings windows and the toasts are next; then one test session with
the game, then v0.1.0. The plan is [docs/design/watcher.md](docs/design/watcher.md) §6.

## What it will do

Install → paste your PIU Scores token → pick how it watches (the game window, your Steam F12
screenshots, or both) → it sits in the tray. RISE starts, it wakes; a result screen appears, it
reads it and posts the play; a toast confirms it; RISE closes, it sleeps. The server recomputes
every score from the judgments and refuses anything that does not reconcile, so a misread never
becomes a record.

Download, once released:
`https://github.com/DrMurloc/PiuScoresWatcher/releases/latest/download/PiuScoresWatcher-win-Setup.exe`

## Documentation

- [docs/HOW-TO-RUN.md](docs/HOW-TO-RUN.md) — prerequisites, running from source, the dev switches, packaging and releasing
- [docs/HOW-TO-TEST.md](docs/HOW-TO-TEST.md) — the test rungs and the checklist with the game open
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — the two halves, the pipeline, the ports, what is deliberately absent
- [docs/DOMAIN.md](docs/DOMAIN.md) — what a RISE result screen says and what the watcher makes of it
- [docs/PRIVACY.md](docs/PRIVACY.md) — what it looks at, what it sends, what it keeps
- [docs/TECHNOLOGIES.md](docs/TECHNOLOGIES.md) — the stack, and why each piece
- [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) — the contribution policies
- [docs/LOCALIZATION.md](docs/LOCALIZATION.md) — the eight languages and how a line is translated
- [docs/design/watcher.md](docs/design/watcher.md) — the design of record: decisions, phases, open questions
- [CLAUDE.md](CLAUDE.md) — machine-readable conventions for AI coding agents

## License

[MIT](LICENSE).
