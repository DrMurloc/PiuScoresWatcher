# Privacy Policy

**PIU Scores Watcher** · Last updated September 27, 2026

This privacy policy explains how PIU Scores Watcher ("the app") handles information: what it looks at on your computer, what it sends and to whom, what it stores, and the choices you have. The app is free, open-source software published by DrMurloc ("we", "us", "our"). Its source code is public at [github.com/DrMurloc/PiuScoresWatcher](https://github.com/DrMurloc/PiuScoresWatcher), so anything this policy says can be checked against the code.

The app records your PUMP IT UP RISE scores on [PIU Scores](https://piuscores.arroweclip.se), a website with its own accounts and its own [privacy notice](https://piuscores.arroweclip.se/Privacy). This policy covers the app. What happens to your scores once they reach PIU Scores is covered by the PIU Scores privacy notice.

If you do not agree with this policy, please do not install or use the app.

## Summary of key points

- **No tracking.** The app has no analytics, telemetry, advertising or crash reporting.
- **It looks only at PUMP IT UP RISE.** Depending on the mode you choose, it captures the RISE window (never your desktop or any other window), reads the screenshots you take of RISE with Steam, or both (the default). Pictures it doesn't need are discarded immediately.
- **Your scores go to PIU Scores and nowhere else.** The app sends each play to your PIU Scores account, using the token you give it. It never uploads a screenshot or any other image.
- **It checks GitHub for updates** each time it starts.
- **What it stores stays on your computer.** Your token is stored encrypted for your Windows account.
- **You're in control.** You can pause the app, disconnect it from PIU Scores, or delete everything it has stored at any time.

## Contents

1. [What does the app look at on my computer?](#1-what-does-the-app-look-at-on-my-computer)
2. [What does the app send, and to whom?](#2-what-does-the-app-send-and-to-whom)
3. [How is the information used?](#3-how-is-the-information-used)
4. [What does the app store on my computer?](#4-what-does-the-app-store-on-my-computer)
5. [Does the app share or sell my information?](#5-does-the-app-share-or-sell-my-information)
6. [How long is information kept?](#6-how-long-is-information-kept)
7. [How is my information kept safe?](#7-how-is-my-information-kept-safe)
8. [What choices and rights do I have?](#8-what-choices-and-rights-do-i-have)
9. [Children's privacy](#9-childrens-privacy)
10. [Changes to this policy](#10-changes-to-this-policy)
11. [How to contact us](#11-how-to-contact-us)

## 1. What does the app look at on my computer?

*In short: the RISE window and your Steam screenshots of RISE, depending on the mode you choose, and little else.*

- **The game window**, if you choose this mode, and whatever the mode while a bulk capture you start is running. While PUMP IT UP RISE is running, the app copies the picture in the RISE window about once a second, or about five times a second during a bulk capture. It copies RISE's window only, never your desktop or another program's window. A picture that isn't a result screen (or, during a bulk capture, RISE's song list) is discarded immediately. A result screen is read, and its picture is then discarded too, unless the app couldn't turn it into a recorded play (see [section 4](#4-what-does-the-app-store-on-my-computer)).
- **Your Steam screenshots of RISE**, if you choose this mode. The app reads the screenshots Steam saves when F12 is pressed in RISE, from RISE's screenshot folder for every Steam account on the computer, or from the one folder you choose in settings. On a computer shared by several Steam accounts, a RISE result screenshot taken on any of them is recorded on the PIU Scores account the app is connected to. To find these folders, the app looks up where Steam is installed and which Steam accounts on the computer have a folder there. Apart from that, it reads nothing from Steam.
- **Whether RISE is running.** Every few seconds, the app checks whether a program named PUMP IT UP RISE is running. It doesn't record or send anything about the other programs on your computer.
- **Song titles** are read off the screen with the text recognition built into Windows, which runs on your computer. No image is sent to a recognition service.
- **Your Windows language settings**, to show the app in your language unless you choose one in settings.

The app does not read RISE's game files, memory or save data.

## 2. What does the app send, and to whom?

*In short: your plays to PIU Scores, a version check to GitHub, and, only if your computer needs it, a download of .NET from Microsoft.*

### PIU Scores

The app talks to PIU Scores ([piuscores.arroweclip.se](https://piuscores.arroweclip.se)) over an encrypted HTTPS connection, using the PIU Scores token you give it:

- **Checking your token.** When you enter a token, and afterwards when needed (for example, each time the app starts), the app asks PIU Scores which account the token belongs to, to confirm it works and to show you the account name. A token you enter is kept only if PIU Scores accepts it.
- **The chart list.** Once your token is accepted, and regularly after that, the app reads PIU Scores' list of RISE charts, to match the song titles it reads to the way PIU Scores spells them.
- **Your plays.** For each result screen it reads, the app sends: the station (RISE mode or the Arcade Station), the song title, the chart type and level, the five judgment counts (Perfect, Great, Good, Bad, Miss), the max combo, the score, whether the run was broken, the date and time of the play (from your computer's clock, including its time zone offset), and how the app saw the screen (the game window or a Steam screenshot).
- **Bulk captures.** When you start a bulk capture of RISE's song list, the app also reads your best scores from PIU Scores, so it can skip bests PIU Scores already has. For each best it reads off the song list, it sends the station, the song title, the chart type and level, the score, when it was read, and that it came from the song list, as a pass and with no judgment counts or max combo. A Perfect Game shown on the list is sent as a score of 1,000,000.
- **The end of a session.** When a session ends (for example, when RISE closes, after a break as long as you chose in settings, or when you quit the app), the app tells PIU Scores, once for each station you played on. That request says which station and nothing else.

Every request to PIU Scores carries your token, so PIU Scores knows which account it is for, and the app's name and version. Like any connection over the internet, it also shows PIU Scores your IP address. PIU Scores' hosting may set a load-balancing cookie, which the app keeps in memory while it runs and never saves. The app never uploads a screenshot or any other image, and it sends nothing about you or your computer beyond what is listed here.

### GitHub

Each time an installed copy of the app starts, it asks GitHub, where the app is published, whether a newer version exists. If one does, the app downloads it in the background and installs it the next time it starts. GitHub receives what any web request carries, such as your IP address; the [GitHub General Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement) applies.

### Microsoft

The app runs on Microsoft's .NET Desktop Runtime. If your computer doesn't have it, the installer downloads it from Microsoft, as can the updater if a later version needs a newer .NET; the [Microsoft Privacy Statement](https://www.microsoft.com/en-us/privacy/privacystatement) applies. The app itself sends nothing to Microsoft.

### Links you click

The links in the app (for example, to PIU Scores, to this policy or to a version's release notes) open in your web browser. The websites they lead to have their own privacy policies.

## 3. How is the information used?

*In short: only to record your scores on PIU Scores and to keep the app working.*

The information described above is used only to:

- recognize RISE result screens (and the song list, during a bulk capture) and read your scores from them;
- record your plays on your PIU Scores account without sending the same play twice, and tell PIU Scores when a session ends;
- show you what was recorded, in notifications and in the app;
- keep screens the app couldn't record, so you can review them;
- keep the app up to date.

It is not used for advertising, profiling or any other purpose.

## 4. What does the app store on my computer?

*In short: its settings, your encrypted token, its logs, and screens it couldn't record, in one folder on your computer.*

Everything the app keeps about you and your plays is in `%APPDATA%\PiuScoresWatcher\` (usually `C:\Users\<your name>\AppData\Roaming\PiuScoresWatcher`):

| What | Details |
|---|---|
| Settings (`settings.json`) | Your choices: how the app watches, Start with Windows, the screenshots folder if you set one, which notifications and sounds are on, when a session ends, your language, and the last version you ran. Never your token. |
| Your PIU Scores token (`token.bin`) | Encrypted with Windows Data Protection (DPAPI), so only your Windows account on this computer can read it. It is never written anywhere in readable form. |
| Sessions (`session.json`) | Which stations have a session open on PIU Scores, when each last had a play, and any session end not yet delivered, so a session can still be ended after a restart. |
| Logs (`logs\`) | A record of what the app did, for troubleshooting: for example, when RISE started and closed, the plays it sent and how PIU Scores answered. They include file paths on your computer, which contain your Windows user name, and in screenshot mode the path of the screenshot folder, which contains your Steam account number. They never contain your token. The seven most recent daily logs are kept; older ones are deleted automatically. |
| Screens it couldn't record (`failed\`) | When a screen can't be read, or a play isn't recorded on PIU Scores (including whenever the app isn't connected to PIU Scores), the app generally keeps the picture it saw, with a short note of what it read and where the picture came from, so you can review it. A picture can show your in-game profile and anything else shown in the game window at the time, such as an overlay notification. The app doesn't send them anywhere. You can view and delete them in the app. |

Elsewhere on your computer:

- The app itself, and any update it has downloaded, is in its install folder, `%LOCALAPPDATA%\PiuScoresWatcher\`.
- Its installer and updater keep a small diagnostic log, with versions, update checks and file paths, in `%LOCALAPPDATA%\velopack\`.
- Start with Windows is on unless you turn it off; while it's on, the app is in your Windows account's startup programs.
- Windows keeps a registration for the app's notifications, and keeps the notifications themselves, which show the plays recorded, in its notification center until you clear them.
- The installer adds shortcuts to the app, such as in the Start menu.

Uninstalling the app removes its install folder, its startup entry, its notification registration and its shortcuts.

## 5. Does the app share or sell my information?

*In short: no. It sends your plays to PIU Scores because that is what you set it up to do.*

The app sends information only as described in [section 2](#2-what-does-the-app-send-and-to-whom). We don't sell or rent your information, and the app contains no advertising, analytics, tracking cookies or tracking of any kind.

Once your plays reach PIU Scores, the PIU Scores privacy notice covers them, including who can see them on the site.

## 6. How long is information kept?

*In short: on your computer, until you delete it; on PIU Scores, as its privacy notice says.*

- **Settings, token and sessions** stay until you change them, disconnect, or delete the app's folder.
- **Logs:** the seven most recent daily logs are kept; older ones are deleted automatically. The updater's log is rotated automatically when it reaches 1 MB.
- **Screens the app couldn't record** stay until you delete them.
- **Plays sent to PIU Scores** are kept as described in the PIU Scores privacy notice.

Uninstalling the app does not delete `%APPDATA%\PiuScoresWatcher\`, so a reinstall keeps your settings and token. Deleting that folder removes everything the app keeps about you and your plays.

## 7. How is my information kept safe?

*In short: your token is encrypted on your computer and only ever sent to PIU Scores, over HTTPS.*

The app connects to PIU Scores and GitHub only over encrypted HTTPS connections. Your token is encrypted for your Windows account, is never written anywhere in readable form, and is only ever sent to PIU Scores. However, no method of storing or sending information is completely secure, and we cannot guarantee absolute security. Anyone who can sign in to your Windows account can use the app as you, so keep that account secure.

## 8. What choices and rights do I have?

*In short: you decide what the app watches, and you can stop it, disconnect it or delete its data at any time.*

- **Choose how it watches** (the game window, your Steam screenshots, or both) in settings.
- **Pause it** from the tray menu. While paused, the app doesn't capture the RISE window or read screenshots unless you start a bulk capture, though it still checks whether RISE is running and can still contact PIU Scores and GitHub as described in section 2. Pausing lasts until you resume or the app restarts.
- **Turn off Start with Windows**, so the app only runs when you open it.
- **Disconnect** in settings to delete your token from your computer. The app keeps watching while disconnected, keeping each result screen it sees for review instead of sending it; pause or quit the app to stop that.
- **Replace your token.** Making a new token on your PIU Scores account page stops the old one working. If PIU Scores stops accepting the token the app has stored, connecting a new one in settings replaces it.
- **Delete kept screens** in the app, or **delete the app's folder** to remove everything it keeps about you and your plays.
- **Uninstall** the app at any time from Windows settings.

Depending on where you live (for example, the European Economic Area, the United Kingdom or California), you may have the right to access, correct or delete your personal information, or to object to how it is used. Everything the app stores is on your own computer, where you can see and delete all of it. For your plays and your account on PIU Scores, use the options in the PIU Scores privacy notice. For anything else, [contact us](#11-how-to-contact-us).

## 9. Children's privacy

*In short: the app doesn't knowingly collect information from children.*

The app is not directed at children under 13 (or the minimum age where you live), and we don't knowingly collect personal information from children through it. Using the app requires a PIU Scores account; the PIU Scores privacy notice says who may hold one. If you believe a child's information has been sent through the app, [contact us](#11-how-to-contact-us).

## 10. Changes to this policy

We may update this policy, for example when the app changes what it sends or stores. The "Last updated" date at the top shows when it last changed, and every change is visible in [this file's history](https://github.com/DrMurloc/PiuScoresWatcher/commits/main/docs/PRIVACY.md) on GitHub. The privacy link in the app always opens the current version. If we make a material change, we may also point it out in the release notes of the version that makes it.

## 11. How to contact us

For questions or requests about this policy or your privacy, email [joneccker@gmail.com](mailto:joneccker@gmail.com). For questions about your PIU Scores account or the plays on it, see the contact details in the [PIU Scores privacy notice](https://piuscores.arroweclip.se/Privacy).
