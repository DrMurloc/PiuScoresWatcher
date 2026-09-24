"""Copy the labeled screenshots into the repo's fixtures with the player card blacked out, and
write expected.json from the prototype's labels."""
import json, os
from PIL import Image, ImageDraw
import sys
import reader as P

DEST = os.path.join(P.REPO, "tests", "PiuScoresWatcher.Tests", "Fixtures", "screens")
if len(sys.argv) > 1:
    P.SHOTS = sys.argv[1]
    P.EXPECTED = {n: e for n, e in P.ALL_EXPECTED.items() if P.shot_path(n)}
os.makedirs(DEST, exist_ok=True)
DUPES = {"20260922184110", "20260921070650", "20260921071208", "20260921073533", "20260921200229",
         "20260921201014", "20260921201739", "20260921202355", "20260922192119", "20260922192121"}
# player-card rectangles at 1080p, by what the screen is
CARDS = {
    "dance": [(1340, 872, 1880, 985)], "arcade": [(215, 10, 755, 130)],
    "challenge": [(1340, 872, 1880, 985)], "empty": [(1340, 872, 1880, 985)],
    None: [(1340, 872, 1880, 985), (1340, 20, 1880, 125), (1360, 955, 1880, 1075)],
}
TYPES = {"S": "Single", "HD": "HalfDouble", "D": "Double"}
# frames whose score was still counting up when F12 fired: the reading is complete, the checksum refuses it
UNSETTLED = {"20260922192124"}
expected = {}
for name, exp in sorted(P.EXPECTED.items()):
    if name in DUPES:
        continue
    im = Image.open(P.shot_path(name)).convert("RGB")
    sx, sy = im.width / 1920, im.height / 1080
    d = ImageDraw.Draw(im)
    for x0, y0, x1, y1 in CARDS[exp[0]]:
        d.rectangle((x0 * sx, y0 * sy, x1 * sx, y1 * sy), fill=(0, 0, 0))
    im.save(os.path.join(DEST, name + ".jpg"), quality=88)
    if exp[0] in ("dance", "arcade"):
        expected[name] = {
            "kind": "result", "layout": "DanceGrade" if exp[0] == "dance" else "Arcade",
            "mix": "rise" if exp[0] == "dance" else "riseArcade",
            "title": exp[1], "chartType": TYPES[exp[2]], "level": exp[3],
            "perfects": exp[4], "greats": exp[5], "goods": exp[6], "bads": exp[7], "misses": exp[8],
            "maxCombo": exp[9], "score": exp[10], "accuracy": exp[11], "broken": exp[12],
        }
        if name in UNSETTLED:
            expected[name]["kind"] = "unsettled"
            expected[name]["note"] = "taken two seconds after the numbers landed at 1280x720: the judgments are final, the score was still counting up (971,789 of 976,610)"
    elif exp[0] == "challenge":
        expected[name] = {"kind": "aggregate", "layout": "DanceGrade"}
    elif exp[0] == "empty":
        expected[name] = {"kind": "empty", "layout": "DanceGrade"}
    else:
        expected[name] = {"kind": "none"}
with open(os.path.join(DEST, "expected.json"), "w") as f:
    json.dump(expected, f, indent=2)
# the song lists (songlist.py) add their own entries, and relabel the song wheel frame above
import songlist
songlist.write_fixtures()
sizes = sum(os.path.getsize(os.path.join(DEST, f)) for f in os.listdir(DEST))
print(len(expected), "fixtures,", round(sizes / 1e6, 1), "MB")
