# How to Run

## Prerequisites

- **Windows 10 version 2004 (build 19041) or Windows 11.** The capture API the watcher will use (`Windows.Graphics.Capture`) needs it; the manifest and the target framework say so.
- **.NET 10 SDK** — the version in [`global.json`](../global.json) (`10.0.401`, rolling forward to later 10.0 feature bands). `dotnet --list-sdks` to check.
- Optional: **Visual Studio 2022 17.14+** with the ".NET desktop development" workload for the XAML designer, or Rider. Nothing here needs an IDE — the CLI builds and runs everything.
- No Windows SDK install: the Windows API projections arrive through the `net10.0-windows10.0.19041.0` target framework as a NuGet reference.
- No Docker, no database, no secrets.

Once, after cloning:

```sh
dotnet tool restore
```

That installs `vpk` (the Velopack packager) locally from `.config/dotnet-tools.json`.

## Running from source

```sh
dotnet run --project src/PiuScoresWatcher.App
```

A tray icon appears (a yellow W on navy — placeholder art). Left-click or **Open settings** opens the window; **Quit** exits. Closing the window does not exit; the tray icon is the app.

A dev run from `bin/` is not an "installed" copy, so the update check logs "skipping" and does nothing — Velopack only updates what it installed.

### Dev switches

None of these is needed to run the watcher as a player does; an unknown switch is refused rather than ignored.

| Switch | What |
|---|---|
| `--replay <screenshot>` | Push one result screenshot through the whole pipeline without the game open. The way the reader gets developed, and the way a player's failed screen gets reproduced. (Logged as "not built yet" until the pipeline lands.) |
| `--dry-run` | Read and report; never post. Pairs with `--replay`. |
| `--base-url <url>` | Where PIU Scores is. Absolute `http(s)` only. |
| `PIUSCORESWATCHER_BASE_URL` | The same as `--base-url`, as an environment variable, for a shell you keep pointed at a local site. The switch wins when both are set. |

Pass switches through `dotnet run` after `--`:

```sh
dotnet run --project src/PiuScoresWatcher.App -- --replay C:\shots\result.png --dry-run
```

### Pointing at a local PIU Scores

Run the site locally (its `docs/HOW-TO-RUN.md`: Aspire, Docker), note the Web app's `https://localhost:<port>`, and start the watcher with `--base-url https://localhost:<port>`. The token comes from that local site's `/Account` page (the dev-auth backdoor gives you an account). Never commit a base URL or a token anywhere.

### Where it keeps things

`%LOCALAPPDATA%\PiuScoresWatcher\` — `settings.json`, `logs\` (rolling daily, seven kept), `failed\` (screens the reader could not read). The settings window's **Open logs folder** button goes straight there. The token, when it exists, sits beside them DPAPI-encrypted, never inside the settings file.

### A Windows 10 wrinkle

While a window is being captured, Windows 10 draws a thin yellow border around it. Windows 11 lets an app turn that off; Windows 10 does not. It is cosmetic, and F12 mode does not capture at all.

## Packaging locally

Reproduces what the release workflow does, into `Releases/` (ignored by git):

```sh
dotnet publish src/PiuScoresWatcher.App/PiuScoresWatcher.App.csproj -c Release -r win-x64 --self-contained false -o publish
dotnet vpk pack --packId PiuScoresWatcher --packVersion 0.1.0 --packDir publish --mainExe PiuScoresWatcher.exe --packTitle "PIU Scores Watcher" --packAuthors DrMurloc --icon src/PiuScoresWatcher.App/Assets/app.ico --framework net10.0-x64-desktop --outputDir Releases
```

`Releases\PiuScoresWatcher-win-Setup.exe` installs to the user's profile (no admin prompt), adds a Start menu shortcut and, because it is framework-dependent, installs the .NET 10 Desktop Runtime first when the machine lacks it. A local package is unsigned; SmartScreen says so on first run (**More info → Run anyway**).

## Releasing

Tag and push:

```sh
git tag v0.1.0
git push origin v0.1.0
```

[`release.yml`](../.github/workflows/release.yml) builds, tests, publishes, packages with Velopack (the installer, the full package and a delta from the previous release) and publishes the GitHub release. Installed copies download it in the background on their next launch and apply it on the one after. The download URL never changes: `https://github.com/DrMurloc/PiuScoresWatcher/releases/latest/download/PiuScoresWatcher-win-Setup.exe`.

### Signing (Azure Trusted Signing)

Releases are unsigned until four **repository variables** exist, and signed from then on — no workflow edit:

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | a Microsoft Entra app registration with a **federated credential** for this repository (`repo:DrMurloc/PiuScoresWatcher:ref:refs/tags/*`), granted the *Trusted Signing Certificate Profile Signer* role on the account |
| `TRUSTED_SIGNING_ENDPOINT` | the account's regional endpoint, e.g. `https://eus.codesigning.azure.net` |
| `TRUSTED_SIGNING_ACCOUNT` | the Trusted Signing account name |
| `TRUSTED_SIGNING_PROFILE` | the certificate profile name (a *Public Trust* profile) |

None of them is a secret: the identity has no password, GitHub's OIDC token is the credential. Setting up the account is a one-time job in the Azure portal — create a Trusted Signing account, complete identity validation (an individual developer's takes a form and a review), create a Public Trust certificate profile — and it is the owner's.
