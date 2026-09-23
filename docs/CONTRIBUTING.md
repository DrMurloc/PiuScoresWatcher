# Contributing

Contributions are welcome. This is a solo-maintained project (DrMurloc), so the process is lightweight — but the review bar is real. Read the policies below before you open a PR; they are enforced liberally, not aspirationally.

## The flow

1. Branch from `main`.
2. Open a PR. A descriptive title and a few sentences on what changed and why.
3. Ping **DrMurloc** in [Discord](https://discord.gg/AvS5PxnvSN) when it's ready for review.

## Contribution policies

### 1. AI contributions are allowed — cohesion is not optional

AI-assisted and AI-generated contributions are welcome. However, **large-scale contributions will be declined liberally if the cohesive target isn't immediately understandable.** If I have to reverse-engineer what your PR is *for*, it's getting closed, not studied.

### 2. Human in the loop is a must

I expect AI contributors to be using AI to **explore and understand** their changes — not to single-prompt a diff and ship it. If I even suspect a contributor doesn't understand the code they changed, I will decline the PR liberally. AI automates low-level decisions; it does not replace human decision making. You are the author of record: be able to explain every line, why it's there, and what breaks without it.

### 3. One PR, one actionable

Pull requests must be well scoped to a **single actionable**: a single bug fix, a single refactor, a single feature add. A small amount of scope creep (a low-hanging-fruit bug fix you tripped over, for instance) is acceptable, but it **must be called out explicitly** in a PR comment by the contributor — undeclared drive-by changes read as noise and count against the PR.

### 4. Testing is mandatory

Untested code is highly subject to declined PRs. See [HOW-TO-TEST.md](HOW-TO-TEST.md) for the rungs and where a change of your shape should be tested. The default expectation: a regression test at the lowest layer that would have caught the bug, or behavioral coverage for the feature you added. A change to the reader comes with the fixture screenshot that motivated it.

### 5. Talk to me *before* large refactors

Architectural changes and large refactors must be communicated to me **before** the PR exists. [ARCHITECTURE.md](ARCHITECTURE.md) describes the shape the codebase already follows — two halves, ports and adapters, a headless core — start there. Unannounced architecture PRs get closed regardless of quality.

### 6. Player-facing text is mine

Every string a player reads — a notification, a menu item, the settings window, the privacy page — is written by me. They all live in one file, `src/PiuScoresWatcher.App/Copy.cs`; a change that needs a new one adds a placeholder there and says so in the PR. Don't write final copy.

## Build and test

```sh
dotnet build PiuScoresWatcher.sln -c Release
dotnet test tests/PiuScoresWatcher.Tests/PiuScoresWatcher.Tests.csproj
```

Run both locally before opening a PR. CI runs the same on [GitHub Actions](../.github/workflows/ci.yml). A change to capture, the reader or posting also goes through the checklist in [HOW-TO-TEST.md](HOW-TO-TEST.md) with the game open.

## Getting a working environment

[HOW-TO-RUN.md](HOW-TO-RUN.md) — prerequisites, running from source, the dev switches, pointing the watcher at a local PIU Scores.

## Conventions

- [ARCHITECTURE.md](ARCHITECTURE.md) — the shape. Read before any non-trivial PR.
- [CLAUDE.md](../CLAUDE.md) — machine-readable conventions for AI coding agents (package allowlists, the ratchets, test patterns). If you're contributing with an AI agent, point it here.
- [DOMAIN.md](DOMAIN.md) — what a RISE result screen says and how it is scored.

## Community

Discord is where changes get discussed: <https://discord.gg/AvS5PxnvSN>. Security issues: email <joneccker@gmail.com> rather than opening a public issue.
