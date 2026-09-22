# Technologies

What the watcher is built with, and why each piece. "Planned" means the seam is designed and the package arrives with its feature.

| Piece | Role | Why this one |
|---|---|---|
| **.NET 10** (LTS) | the runtime | The current LTS (support to November 2028). PIU Scores is on it, the owner's machine has only its SDK, and .NET 8 leaves support in November 2026. WPF ships in every .NET release. |
| **WPF** with the Fluent theme | the tray icon's menu and the settings window | Mature, no third-party control library needed; `ThemeMode="System"` (new in .NET 9/10, still marked experimental — `WPF0001` is suppressed) gives a current look and follows the OS light/dark setting. WinUI 3 would add the Windows App SDK for a window nobody looks at twice. |
| **`net10.0-windows10.0.19041.0`** target | the Windows API projections | `Windows.Graphics.Capture`, `Windows.Media.Ocr` and toasts resolve from the SDK's `Microsoft.Windows.SDK.NET.Ref` reference; nothing to install. 19041 = Windows 10 version 2004, the floor. |
| **Microsoft.Extensions.Hosting** | the generic host: DI, logging, background services | The one-page composition root; `BackgroundService` for the update check and, later, the capture loop. |
| **Serilog** (`Serilog.Extensions.Hosting`, `Serilog.Sinks.File`) | rolling file logs | Seven daily files under the data folder; configured in code — there is no appsettings. |
| **H.NotifyIcon.Wpf** | the tray icon | The maintained fork of Hardcodet's `TaskbarIcon`; declared in `App.xaml`, context menu and click events included. |
| **Velopack** (`Velopack` + the `vpk` tool) | installer, delta updates, the runtime bootstrap | One-click per-user install with no admin prompt; installs the .NET Desktop Runtime when missing; delta packages; `UpdateManager` checks GitHub Releases at start-up, downloads in the background and applies on the next launch. The successor to Squirrel.Windows, which is what the Warcraft Logs uploader's generation of tools used. |
| **GitHub Releases** | hosting the installer and the update feed | Free, versioned, and `releases/latest/download/…` is a stable URL the site's download button can hardcode. |
| **GitHub Actions** | CI and releases | The PR gate builds Release and runs the tests on `windows-latest` (WPF builds only on Windows); a `vX.Y.Z` tag packages, signs and publishes. Azure Pipelines runs the site because the site deploys to Azure; nothing here does. |
| **Azure Trusted Signing** | code signing, so SmartScreen does not warn | Microsoft's managed signing service (about $10/month), signing in through GitHub's OIDC token — no certificate file, no secret in the repo. Wired in `release.yml`, gated on the account existing. |
| **Windows DPAPI** (`System.Security.Cryptography.ProtectedData`) — planned | the token at rest | Encrypted for the Windows account, no key to manage. |
| **Windows.Graphics.Capture** — planned | game-window mode | The OS capture path Game Bar and OBS use: the GPU hands over the frame, no CPU decode, works on fullscreen games. |
| **Windows.Media.Ocr** — planned | reading the song title | Built into Windows 10/11, offline, free, good on clean UI text. The digits are read by template matching against the game's own font instead — more reliable than general OCR on a fixed layout. |
| **SkiaSharp** — planned | decoding screenshots and pixel access | The same library PIU Scores renders its share cards with; MIT; no OpenCV-sized native dependency for a few template matches. |
| **Windows Community Toolkit notifications** — planned | toasts | The "Recorded · Gargoyle S18 · 975,429 SS" confirmation; works from an unpackaged WPF app. |
| **xUnit 2.9.3 + Moq 4.20.72** | tests | PIU Scores' pair; no other doubling library. |
| **Dependabot** | freshness bumps | Weekly, nuget grouped minor/patch, github-actions. |
| **CodeQL** | static analysis | GitHub's default setup on the public repo (a repo setting, not a file). |
