# How to Run

## Prerequisites

- **Windows 10 version 2004 (build 19041) or Windows 11.** The target framework and the built-in OCR the title is read with expect it; the manifest says so.
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

A tray icon appears — the PIU Scores arrow. With no token stored, the first-run window opens as well (paste a token from the site's `/Account`, pick how it watches, Start with Windows); afterwards the watcher starts in the tray alone. Left-click or **Open settings** opens the settings window; **Quit** exits. Closing a window does not exit; the tray icon is the app. Launching it again while it runs opens the running one's settings.

From that moment it watches, in the mode `settings.json` says (both by default): the RISE window once a second while `PUMP IT UP RISE.exe` has one, and every JPEG Steam writes into RISE's screenshots folders. Notifications confirm each play (each kind can be switched off in settings), and the log says it all regardless — `logs\watcher-<date>.log` says "RISE started; watching its window", then per result screen either `Recorded: <song> <type> <level> — <score>` or why not (`Not recorded: … no chart matched`, `kept for review — …`, `The PIU Scores token was rejected`). A screen the reader could not turn into a play is under `failed\` as a PNG with a JSON note beside it. Nothing posts without a token: connect in the first-run or settings window, or set `PIUSCORESWATCHER_TOKEN` for a dev run. Start with Windows only takes effect for an installed copy — a dev run from `bin/` never registers itself.

A dev run from `bin/` is not an "installed" copy, so the update check logs "skipping" and does nothing — Velopack only updates what it installed.

### Dev switches

None of these is needed to run the watcher as a player does; an unknown switch is refused rather than ignored.

| Switch | What |
|---|---|
| `--replay <screenshot>` | One result screenshot through the whole pipeline without the game open — detection, the reading, the checksum, the title by Windows OCR — reported as JSON on the console that launched it (or, with no console, to `logs\replay.json`). The way the reader gets developed, and the way a player's failed screen gets reproduced. Exit 0: a play that reconciles; 1: a result screen that is not one (numbers not landed, a Challenge aggregate, unreadable, refused by the checksum); 3: not a result screen. |
| `--dry-run` | Read and report; never post. Pairs with `--replay`. Without it, a replay that reconciles and has a title **posts the play** whenever a token is available. |
| `PIUSCORESWATCHER_TOKEN` | A personal token in the environment stands in for the stored one — the way a replay posts to a local site before the settings window exists. Never persisted. |
| `--base-url <url>` | Where PIU Scores is. Absolute `http(s)` only. |
| `PIUSCORESWATCHER_BASE_URL` | The same as `--base-url`, as an environment variable, for a shell you keep pointed at a local site. The switch wins when both are set. |

Pass switches through `dotnet run` after `--`:

```sh
dotnet run --project src/PiuScoresWatcher.App -- --replay C:\shots\result.png --dry-run
```

The title comes from the OCR built into Windows, which needs an English language pack with OCR installed
(Settings → Time & language → Language; the default English install has it). Without one the report carries
no title and says why in the log.

### Pointing at a local PIU Scores

A dev run already does. The launch profile in `src/PiuScoresWatcher.App/Properties/launchSettings.json` points F5 and `dotnet run` at the site's local Aspire run, `https://localhost:7144` (the port is the site's own launch settings). Run the site locally (its `docs/HOW-TO-RUN.md`: Aspire, Docker) and paste a token from that local site's `/Account` page (the dev-auth backdoor gives you an account) into the dev copy's first-run window. For the real site from a dev build, pick the **Production** profile (`dotnet run --launch-profile Production`); `--base-url` still wins over either.

A run against any site other than production keeps its own everything under `%APPDATA%\PiuScoresWatcher\dev\<host>-<port>\` — settings, token, logs, kept screens — and its own one-copy lock, so it runs beside an installed watcher without touching its token. Its window titles and tray tooltip name the site, which is how the two tray arrows tell apart. Never commit a token anywhere.

The whole loop from a shell, with a real screen:

```sh
$env:PIUSCORESWATCHER_TOKEN = "<a token from the local site's /Account>"
dotnet run --project src/PiuScoresWatcher.App -- --replay "<Steam>\userdata\<id>\760\remote\2756930\screenshots\<shot>.jpg"
```

The report's `connection` says who the token is (`connected`, `unauthorized`, or why it failed), `posting` says what the site did with the play (`Recorded`, `Refused` with the problem slug, `SongUnknown`, `RateLimited`, `Failed`), and the play then shows in that account's journal on the site. Add `--dry-run` to see the reading without posting.

### Where it keeps things

`%APPDATA%\PiuScoresWatcher\` (a dev run against another site: `dev\<host>-<port>\` beneath it) — `settings.json`, `logs\` (rolling daily, seven kept), `failed\` (screens that couldn't be read, and plays PIU Scores didn't record, each with a note saying why). The settings window's **Open logs folder** button goes straight there. The token, when it exists, sits beside them DPAPI-encrypted, never inside the settings file.

### If the game comes out black

Game-window mode copies the window through Windows' own compositor (`PrintWindow`), which works for the borderless fullscreen Unity uses by default. A setup that returns black frames — exclusive fullscreen, an unusual overlay — shows up as a watcher that never sees a result screen while F12 mode still works; say so, and the Graphics Capture path gets added for it.

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

### Signing (Azure Artifact Signing)

Artifact Signing is what Microsoft called Trusted Signing until 2026; the variable names and Velopack's
`--azureTrustedSignFile` keep the old name. Releases are unsigned until six **repository variables** exist,
and signed from then on — no workflow edit:

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | a Microsoft Entra app registration with a **federated credential** for the release environment (`repo:DrMurloc/PiuScoresWatcher:environment:release`), granted the *Artifact Signing Certificate Profile Signer* role on the certificate profile |
| `TRUSTED_SIGNING_ENDPOINT` | the account's regional endpoint, e.g. `https://cus.codesigning.azure.net` for Central US |
| `TRUSTED_SIGNING_ACCOUNT` | the Artifact Signing account name |
| `TRUSTED_SIGNING_PROFILE` | the certificate profile name (a *Public Trust* profile) |

None of them is a secret: the identity has no password, GitHub's OIDC token is the credential. The release
job runs in the `release` environment because a federated credential trusts one exact token subject — a
pattern such as `ref:refs/tags/*` is refused — and the environment gives every tag the same subject.

Setting it up is a one-time job and the owner's, since the account is billed and the certificate carries his
legal name. Everything but the identity check has an Azure CLI command (`az extension add --name
artifact-signing`): register the `Microsoft.CodeSigning` provider, create the account (`az artifact-signing
create --sku Basic`), give yourself the *Artifact Signing Identity Verifier* role on it, then — in the portal
only — complete an **individual** identity validation (the name and address come from the Azure billing
account and must match a government ID; the check runs on a phone through AU10TIX and Microsoft
Authenticator), then create the Public Trust certificate profile (`az artifact-signing certificate-profile
create --profile-type PublicTrust --identity-validation-id …`), the app registration and its federated
credential, and the role assignment. Signing does not make SmartScreen's warning disappear on day one: the
reputation builds as signed downloads accumulate.
