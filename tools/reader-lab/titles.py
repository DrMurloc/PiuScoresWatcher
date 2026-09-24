"""How well the watcher reads song titles: every labeled screen with a title goes through the built
watcher's --replay (Windows OCR included, nothing posted) and the title it read is scored against the
label — exactly, and the way the chart list would still match it (SongCatalog: folded letters, a few
edits allowed). Run after changing TitleInk or anything the title passes through; Windows only.

    python titles.py [path to PiuScoresWatcher.exe]

The default is the Release build, then the Debug one. The replays run under a local dev site so
nothing touches the installed watcher's folder (D44).
"""
import json
import os
import subprocess
import sys
import unicodedata

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
SCREENS = os.path.join(REPO, "tests", "PiuScoresWatcher.Tests", "Fixtures", "screens")
BUILDS = [os.path.join(REPO, "src", "PiuScoresWatcher.App", "bin", c, "net10.0-windows10.0.19041.0", "PiuScoresWatcher.exe")
          for c in ("Release", "Debug")]
FOLD = {"0": "o", "1": "l", "i": "l", "|": "l", "!": "l", "5": "s"}


def key(title):
    """SongCatalog.Key: compatibility-normalized, lower case, look-alikes folded, letters and digits only."""
    folded = (FOLD.get(c, c) for c in unicodedata.normalize("NFKC", title).lower())
    return "".join(c for c in folded if c.isalnum())


def distance(a, b):
    previous = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        current = [i]
        for j, cb in enumerate(b, 1):
            current.append(min(previous[j] + 1, current[j - 1] + 1, previous[j - 1] + (ca != cb)))
        previous = current
    return previous[-1]


def main():
    exe = sys.argv[1] if len(sys.argv) > 1 else next((b for b in BUILDS if os.path.exists(b)), None)
    if exe is None:
        sys.exit("build the app first: dotnet build src/PiuScoresWatcher.App")
    env = {k: v for k, v in os.environ.items() if k not in ("PIUSCORESWATCHER_TOKEN", "PIUSCORESWATCHER_BASE_URL")}
    expected = json.load(open(os.path.join(SCREENS, "expected.json"), encoding="utf-8"))
    exact = matched = total = 0
    for stem, label in sorted(expected.items()):
        title = label.get("title")
        path = os.path.join(SCREENS, stem + ".jpg")
        if not title or not os.path.exists(path):
            continue
        run = subprocess.run([exe, "--replay", path, "--dry-run", "--base-url", "http://127.0.0.1:5998/"],
                             capture_output=True, text=True, encoding="utf-8", env=env, timeout=120)
        read = (json.loads(run.stdout).get("title") or "") if run.stdout.strip() else ""
        total += 1
        is_exact = read == title
        is_matched = is_exact or (key(read) != "" and distance(key(read), key(title)) <= max(1, len(key(read)) // 8))
        exact += is_exact
        matched += is_matched
        if not is_exact:
            verdict = "catalog still matches" if is_matched else "MISSED"
            print(f"{stem} {label['kind']:15} {title!r:28} read {read!r:28} {verdict}")
    print(f"\n{exact} of {total} titles read exactly; {matched} of {total} the chart list would match")


if __name__ == "__main__":
    main()
