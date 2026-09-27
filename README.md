# PIU Scores Watcher

A Windows desktop app that watches for scores in PUMP IT UP RISE and records them on
[PIU Scores](https://piuscores.arroweclip.se).

Status: **v1.0.0, released 2026-09-27**, after v0.1.0 (the MVP, 2026-09-24) and v0.2.0 (2026-09-26). The design
of record, and which decisions each release carried, is [docs/design/watcher.md](docs/design/watcher.md).

## What it does

Install → paste your PIU Scores token → pick how it watches (the game window, your Steam F12
screenshots, or both) → it sits in the tray. RISE starts, it wakes; a result screen appears, it
reads it and posts the play; a toast confirms it; RISE closes, it ends the session and sleeps. The
server recomputes every score from the judgments and refuses anything that does not reconcile, so a
misread never becomes a record.

Download: <https://github.com/DrMurloc/PiuScoresWatcher/releases/latest/download/PiuScoresWatcher-win-Setup.exe>
— Windows 10 (version 2004 or later) or 11. The installer adds the .NET 10 Desktop Runtime if it is missing, and
the watcher updates itself from then on.

## Documentation

- [docs/HOW-TO-RUN.md](docs/HOW-TO-RUN.md) — prerequisites, running from source, the dev switches, packaging and releasing
- [docs/HOW-TO-TEST.md](docs/HOW-TO-TEST.md) — the test rungs and the checklist with the game open
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — the two halves, the pipeline, the ports, what is deliberately absent
- [docs/DOMAIN.md](docs/DOMAIN.md) — what a RISE result screen says and what the watcher makes of it
- [docs/PRIVACY.md](docs/PRIVACY.md) — the privacy policy: what it looks at, what it sends, what it keeps
- [docs/TECHNOLOGIES.md](docs/TECHNOLOGIES.md) — the stack, and why each piece
- [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) — the contribution policies
- [docs/LOCALIZATION.md](docs/LOCALIZATION.md) — the eight languages and how a line is translated
- [docs/design/watcher.md](docs/design/watcher.md) — the design of record: decisions, phases, open questions
- [CLAUDE.md](CLAUDE.md) — machine-readable conventions for AI coding agents

## License

[MIT](LICENSE).
