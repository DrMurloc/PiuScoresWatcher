"""The reader lab: the prototype of the result-screen reader over the owner's screenshots: layout detection by the
five colored judgment labels, digit segmentation by column projection, glyph templates learned
from the labeled screens, and the Phoenix checksum. Prints a per-screen report; writes the
templates as JSON for the C# generator."""
import json, os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
# a folder of RISE screenshots: the Steam screenshots folder (SHOTS=...), else the repo's fixture screens
SHOTS = os.environ.get("SHOTS", os.path.join(REPO, "tests", "PiuScoresWatcher.Tests", "Fixtures", "screens"))
TEMPLATES_OUT = os.path.join(REPO, "src", "PiuScoresWatcher.Core", "Recognition", "templates.json")
W, H = 1920, 1080

def R(x0, y0, x1, y1):
    return (x0 / W, y0 / H, x1 / W, y1 / H)

LAYOUTS = {
    "dance": dict(
        labels={"perfect": R(940, 198, 1090, 236), "great": R(940, 296, 1060, 332), "good": R(940, 393, 1050, 430),
                "bad": R(940, 490, 1020, 528), "miss": R(940, 588, 1035, 626)},
        values={"perfect": R(1690, 191, 1850, 239), "great": R(1690, 289, 1850, 337), "good": R(1690, 386, 1850, 434),
                "bad": R(1690, 484, 1850, 532), "miss": R(1690, 582, 1850, 630), "combo": R(1690, 681, 1850, 729)},
        accuracy=R(1690, 779, 1850, 827),
        score=R(300, 645, 740, 735),
        badge=R(1795, 32, 1868, 120), badge_digits=R(1805, 40, 1859, 90),
        title=R(845, 38, 1785, 82),
        sticker=R(280, 240, 640, 530),
        card=R(1340, 872, 1880, 985),
        digit_family="dg", score_family="dgscore", level_family="dg",
    ),
    "arcade": dict(
        labels={"perfect": R(868, 442, 1052, 478), "great": R(890, 490, 1032, 524), "good": R(895, 537, 1022, 571),
                "bad": R(915, 585, 1008, 619), "miss": R(905, 632, 1018, 666)},
        values={"perfect": R(596, 438, 700, 482), "great": R(596, 485, 700, 529), "good": R(596, 532, 700, 576),
                "bad": R(596, 579, 700, 623), "miss": R(596, 626, 700, 670), "combo": R(596, 673, 700, 717)},
        accuracy=R(596, 721, 720, 765),
        score=R(145, 352, 565, 428),
        badge=R(588, 285, 718, 402), badge_digits=R(598, 322, 708, 388),
        title=R(560, 190, 1360, 248),
        sticker=None,
        card=R(215, 10, 755, 130),
        digit_family="ar", score_family="arscore", level_family="arlevel",
    ),
}

# name -> (layout, title, type, level, P, G, Gd, B, M, combo, score, acc, broken)   None layout = not a result
EXPECTED = {
    "20260921073527": ("dance", "Curiosity Overdrive", "S", 20, 1047, 35, 8, 2, 9, 414, 968683, "97.16", False),
    "20260921195451": ("arcade", "4NT", "S", 22, 946, 110, 14, 4, 26, 343, 919853, "92.29", False),
    "20260921195952": ("dance", "Darkside Of The Mind", "S", 23, 985, 175, 72, 28, 73, 101, 826833, "83.06", True),
    "20260921200226": ("dance", "PRiMA MATERiA", "S", 21, 1289, 30, 9, 3, 19, 245, 965762, "96.97", False),
    "20260921200554": ("dance", "4NT", "S", 22, 985, 68, 8, 3, 14, 272, 949834, "95.33", False),
    "20260921201011": ("dance", "King's Tomb", "S", 22, 1025, 118, 31, 10, 16, 181, 915325, "91.91", False),
    "20260921201328": ("dance", "Morrighan", "S", 20, 911, 59, 13, 3, 14, 170, 945403, "94.93", False),
    "20260921201730": ("dance", "Cynical", "S", 16, 833, 19, 0, 0, 0, 852, 991124, "99.10", False),
    "20260921202034": ("dance", "Love Code", "S", 19, 889, 68, 6, 6, 10, 263, 948168, "95.15", False),
    "20260921202351": ("dance", None, "S", 21, 1432, 32, 6, 2, 28, 563, 965434, "96.83", False),
    "20260921202708": ("dance", None, "S", 20, 1133, 66, 7, 1, 15, 241, 956984, "96.08", False),
    "20260921202931": ("dance", "Nakakapagpabagabag", "S", 18, 1263, 16, 1, 0, 0, 1279, 994399, "99.43", False),
    "20260921203154": ("dance", "86", "S", 20, 2224, 40, 11, 3, 22, 848, 975429, "97.84", False),
    "20260921203505": ("dance", "Aragami", "S", 22, 1231, 62, 20, 14, 29, 288, 935598, "93.92", True),
    "20260921203746": ("dance", "Darkside Of The Mind", "S", 23, 1023, 157, 77, 30, 46, 158, 848246, "85.19", True),
    "20260921204006": ("dance", None, "S", 22, 1168, 41, 6, 1, 14, 365, 967280, "97.06", False),
    "20260921205242": ("challenge",),
    "20260921205925": ("dance", None, "S", 18, 1085, 14, 2, 0, 5, 398, 985823, "98.89", False),
    "20260922183006": ("arcade", "Ugly Dee", "D", 18, 990, 0, 0, 0, 11, 821, 988166, "98.90", False),
    "20260922184107": ("dance", "4NT", "HD", 13, 507, 18, 0, 0, 2, 433, 981738, "98.25", False),
    "20260922184110": ("dance", "4NT", "HD", 13, 507, 18, 0, 0, 2, 433, 981738, "98.25", False),
    "20260922184333": ("dance", "Curiosity Overdrive", "HD", 16, 724, 22, 2, 3, 38, 231, 932022, "93.52", True),
    "20260922184844": (None,), "20260922184847": (None,),
    "20260922185707": ("arcade", "Ugly Dee", "D", 17, 71, 0, 0, 0, 0, 71, 1000000, "100.00", False),
    "20260922190046": ("dance", "Neo Catharsis", "S", 25, 1071, 226, 160, 100, 43, 286, 777366, "78.03", True),
    "20260922190321": ("dance", "KUGUTSU", "S", 25, 877, 309, 206, 176, 77, 77, 678406, "68.15", True),
    "20260922190544": ("dance", "1948", "S", 26, 899, 310, 285, 199, 207, 54, 608610, "61.15", True),
    "20260922190818": ("dance", "1948", "S", 26, 637, 333, 216, 156, 558, 21, 469066, "47.13", True),
    "20260922191107": ("dance", "1948", "S", 26, 820, 310, 259, 178, 333, 19, 563324, "56.61", True),
    "20260922191728": (None,), "20260922191729": ("empty",),
    "20260922191732": ("dance", "QUATTUORUX", "S", 19, 1167, 51, 7, 3, 10, 383, 965443, "96.87", False),
    "20260922191749": ("dance", "QUATTUORUX", "S", 19, 1167, 51, 7, 3, 10, 383, 965443, "96.87", False),
    "20260922192119": (None,), "20260922192121": (None,), "20260922192122": ("empty",),
    "20260922192124": ("dance", "Aragami", "S", 19, 974, 21, 3, 2, 8, 392, 971789, "97.95", False),
    "20260921223218": (None,), "20260922072011": (None,),
    # the older pair from the 09-21 morning and the second frames (with the footer) of labeled results
    "20260921070648": ("dance", "Pop & Pump & DIVE!!", "S", 16, 734, 81, 16, 1, 13, 181, 926479, "93.00", False),
    "20260921070650": ("dance", "Pop & Pump & DIVE!!", "S", 16, 734, 81, 16, 1, 13, 181, 926479, "93.00", False),
    "20260921071206": ("arcade", "Curiosity Overdrive", "S", 20, 1038, 36, 9, 4, 14, 278, 960836, "96.43", False),
    "20260921071208": ("arcade", "Curiosity Overdrive", "S", 20, 1038, 36, 9, 4, 14, 278, 960836, "96.43", False),
    "20260921073533": ("dance", "Curiosity Overdrive", "S", 20, 1047, 35, 8, 2, 9, 414, 968683, "97.16", False),
    "20260921200229": ("dance", "PRiMA MATERiA", "S", 21, 1289, 30, 9, 3, 19, 245, 965762, "96.97", False),
    "20260921201014": ("dance", "King's Tomb", "S", 22, 1025, 118, 31, 10, 16, 181, 915325, "91.91", False),
    "20260921201739": ("dance", "Cynical", "S", 16, 833, 19, 0, 0, 0, 852, 991124, "99.10", False),
    "20260921202355": ("dance", "PARADOXX", "S", 21, 1432, 32, 6, 2, 28, 563, 965434, "96.83", False),
}
EXPECTED["20260921202351"] = ("dance", "PARADOXX", "S", 21, 1432, 32, 6, 2, 28, 563, 965434, "96.83", False)
EXPECTED["20260921202708"] = ("dance", "Vacuum Cleaner", "S", 20, 1133, 66, 7, 1, 15, 241, 956984, "96.08", False)
EXPECTED["20260921204006"] = ("dance", "Solve My Hurt", "S", 22, 1168, 41, 6, 1, 14, 365, 967280, "97.06", False)
EXPECTED["20260921205925"] = ("dance", "PRiMA MATERiA", "S", 18, 1085, 14, 2, 0, 5, 398, 985823, "98.89", False)
# the owner's test loop, 09-23: the first Arcade Station 5 in the judgment font (max combo 0500, read as 600)
EXPECTED["20260923214956"] = ("arcade", "VANISH", "S", 20, 1188, 23, 2, 1, 8, 500, 981005, "98.38", False)
# the second evening of the loop: short titles Windows OCR drops (VANISH, D, 8 6), a title it garbles (%X), a
# stepball it misread both ways (8 6 is 12, %X is 18 — the note counts settle it), and VECTOR, one point above
# the integer formula (the game's own arithmetic; PIU Scores allows the point too)
EXPECTED["20260923221151"] = ("dance", "VANISH", "S", 20, 1167, 39, 5, 2, 9, 563, 972550, "97.51", False)
EXPECTED["20260923221410"] = ("dance", "The Quick Brown Fox Jumps Over The Lazy Dog", "S", 19, 1051, 32, 6, 4, 13, 306, 965615, "96.90", False)
EXPECTED["20260923221640"] = ("dance", "D", "S", 18, 758, 23, 3, 1, 2, 585, 980384, "98.15", False)
EXPECTED["20260923221920"] = ("dance", "Mission Possible -Blow Back-", "S", 19, 969, 17, 2, 1, 2, 774, 987559, "98.85", False)
EXPECTED["20260923222133"] = ("dance", "K.O.A : Alice In Wonderworld", "S", 17, 851, 16, 2, 0, 25, 285, 959865, "96.30", True)
EXPECTED["20260923222448"] = ("dance", "CO5M1C R4ILR0AD", "S", 21, 1085, 40, 10, 6, 15, 206, 957674, "96.15", False)
EXPECTED["20260923222734"] = ("dance", "Halloween Party ~Multiverse~", "S", 20, 961, 41, 8, 4, 17, 266, 954405, "95.79", False)
EXPECTED["20260923223001"] = ("dance", "VECTOR", "HD", 15, 528, 44, 13, 2, 29, 122, 901013, "90.45", True)
EXPECTED["20260923223505"] = ("arcade", "ERRORCODE: 0", "S", 19, 1468, 18, 5, 0, 9, 879, 984530, "98.65", False)
EXPECTED["20260923224312"] = ("arcade", "8 6", "S", 12, 538, 12, 0, 0, 0, 550, 991316, "99.12", False)
EXPECTED["20260923224808"] = ("arcade", "%X (Percent X)", "S", 18, 1141, 16, 5, 3, 4, 758, 983687, "98.53", False)

def shot_path(name):
    for candidate in (name + ".jpg", name + "_1.jpg"):
        p = os.path.join(SHOTS, candidate)
        if os.path.exists(p):
            return p
    return None

def load(name):
    return np.asarray(Image.open(shot_path(name)).convert("RGB")).astype(np.int16)

# only the screens the folder actually holds; ALL_EXPECTED keeps every label for fixtures.py
ALL_EXPECTED = dict(EXPECTED)
EXPECTED = {name: exp for name, exp in EXPECTED.items() if shot_path(name)}

def crop(img, r):
    h, w = img.shape[:2]
    x0, y0, x1, y1 = int(r[0] * w), int(r[1] * h), int(r[2] * w), int(r[3] * h)
    return img[y0:y1, x0:x1]

def hsv(img):
    rgb = img.astype(np.float32) / 255.0
    mx = rgb.max(axis=2); mn = rgb.min(axis=2); d = mx - mn
    s = np.where(mx > 0, d / np.maximum(mx, 1e-6), 0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hh = np.zeros_like(mx)
    m = d > 1e-6
    rm = m & (mx == r); gm = m & (mx == g) & ~rm; bm = m & ~rm & ~gm
    hh[rm] = ((g - b)[rm] / d[rm]) % 6
    hh[gm] = ((b - r)[gm] / d[gm]) + 2
    hh[bm] = ((r - g)[bm] / d[bm]) + 4
    return hh * 60, s, mx

COLOR_CLASSES = {  # label -> (hue lo, hue hi, min sat, min val)
    "perfect": (180, 250, 0.45, 0.55), "great": (75, 150, 0.45, 0.5), "good": (35, 65, 0.5, 0.6),
    "bad": (275, 335, 0.4, 0.5), "miss": (-15, 15, 0.5, 0.5),
}

def color_fraction(region, cls):
    lo, hi, smin, vmin = COLOR_CLASSES[cls]
    h, s, v = hsv(region)
    hue = h.copy()
    if lo < 0:
        hue = np.where(hue > 180, hue - 360, hue)
    m = (hue >= lo) & (hue <= hi) & (s >= smin) & (v >= vmin)
    return float(m.mean())

def detect(img):
    best = None
    for name, lay in LAYOUTS.items():
        fr = {k: color_fraction(crop(img, r), k) for k, r in lay["labels"].items()}
        ok = all(f >= 0.06 for f in fr.values())
        if ok and (best is None or min(fr.values()) > best[1]):
            best = (name, min(fr.values()), fr)
    return best

def lum(region):
    return 0.299 * region[..., 0] + 0.587 * region[..., 1] + 0.114 * region[..., 2]

def white_mask(region):
    l = lum(region)
    spread = region.max(axis=2) - region.min(axis=2)
    return (l > 185) & (spread < 80)

def gold_mask(region):
    """The gold score digits: bright and warm. The red brush behind them is dark green-wise, the
    outline is dark, white sparkles are not warm."""
    r, g, b = region[..., 0], region[..., 1], region[..., 2]
    return (lum(region) > 140) & (r - b > 60) & (g > 120)

def row_bounds(sub):
    """The rows a glyph really occupies: a stray line of a neighbouring element is dropped."""
    counts = sub.sum(axis=1)
    keep = np.where(counts >= max(2, 0.1 * counts.max()))[0]
    if keep.size == 0:
        return 0, 0
    return int(keep[0]), int(keep[-1]) + 1

def split_wide(mask, glyphs):
    """A run much wider than its neighbours is two touching glyphs: cut at the thinnest column."""
    if len(glyphs) < 1:
        return glyphs
    widths = sorted(g["w"] for g in glyphs)
    median = widths[len(widths) // 2]
    out = []
    for g in glyphs:
        if len(glyphs) > 1 and g["w"] > 1.6 * median and g["w"] > 8:
            sub = mask[g["y0"]:g["y1"], g["x0"]:g["x1"]]
            proj = sub.sum(axis=0)
            lo, hi = int(sub.shape[1] * 0.3), int(sub.shape[1] * 0.7)
            cut = lo + int(np.argmin(proj[lo:hi]))
            for a, b in ((g["x0"], g["x0"] + cut), (g["x0"] + cut, g["x1"])):
                part = mask[:, a:b]
                if part.any():
                    y0, y1 = row_bounds(part)
                    out.append(dict(x0=a, x1=b, y0=y0, y1=y1, w=b - a, h=y1 - y0))
        else:
            out.append(g)
    return out

def segments(mask, min_h_frac=0.45, gap=1, keep_dots=True):
    cols = mask.any(axis=0)
    runs = []
    x = 0
    n = len(cols)
    while x < n:
        if cols[x]:
            s = x
            while x < n and cols[x]:
                x += 1
            runs.append([s, x])
        else:
            x += 1
    # merge runs separated by tiny gaps (glyph parts)
    merged = []
    for r in runs:
        if merged and r[0] - merged[-1][1] <= gap - 1:
            merged[-1][1] = r[1]
        else:
            merged.append(r)
    glyphs = []
    for s, e in merged:
        sub = mask[:, s:e]
        y0, y1 = row_bounds(sub)
        glyphs.append(dict(x0=s, x1=e, y0=y0, y1=y1, w=e - s, h=y1 - y0))
    if not glyphs:
        return []
    # noise: anything shorter than a third of the tallest glyph and narrower than 4px is speckle
    glyphs = [g for g in glyphs if not (g["h"] <= 3 and g["w"] <= 5)]
    if not glyphs:
        return []
    maxh = max(g["h"] for g in glyphs)
    kept = [g for g in glyphs if g["h"] >= min_h_frac * maxh or (keep_dots and g["h"] < 0.3 * maxh and g["w"] < 0.6 * maxh and g["h"] >= 3)]
    return split_wide(mask, kept)

TW, TH = int(os.environ.get("TW", 12)), int(os.environ.get("TH", 20))

def normalize(mask, g):
    sub = mask[g["y0"]:g["y1"], g["x0"]:g["x1"]].astype(np.uint8) * 255
    im = Image.fromarray(sub).resize((TW, TH), Image.BOX)
    arr = np.asarray(im) >= 128
    return arr

def similarity(a, b):
    return float((a == b).mean())

class Templates:
    def __init__(self):
        self.t = {}  # family -> char -> list of arrays
    def add(self, fam, ch, arr, aspect):
        self.t.setdefault(fam, {}).setdefault(ch, []).append((arr, aspect))
    def classify(self, fam, arr, aspect):
        best = (None, 0.0)
        for ch, items in self.t.get(fam, {}).items():
            for tarr, tasp in items:
                if abs(tasp - aspect) > 0.45 * max(tasp, aspect):
                    continue
                s = similarity(arr, tarr)
                if s > best[1]:
                    best = (ch, s)
        return best

def read_field(region, mask_fn, fam, templates, expected=None, train=False, keep_dots=True):
    mask = mask_fn(region)
    gl = segments(mask, keep_dots=keep_dots)
    out = []
    for g in gl:
        arr = normalize(mask, g)
        aspect = g["w"] / max(g["h"], 1)
        # a dot: tiny blob
        maxh = max(x["h"] for x in gl)
        if keep_dots and g["h"] < 0.3 * maxh:
            out.append((".", 1.0, g)); continue
        if train and expected is not None:
            out.append((None, 0.0, g, arr, aspect))
        else:
            ch, s = templates.classify(fam, arr, aspect)
            out.append((ch, s, g))
    return out, mask

def acc_string(expected_acc):
    return expected_acc.replace(".", ".") + "%"

def main():
    templates = Templates()
    report = []
    # pass 1: train templates from fields whose glyph count matches the expected string
    trained = 0
    for name, exp in EXPECTED.items():
        if exp[0] not in ("dance", "arcade"):
            continue
        img = load(name)
        lay = LAYOUTS[exp[0]]
        fields = []
        vals = exp[4:10]
        for key, v in zip(["perfect", "great", "good", "bad", "miss", "combo"], vals):
            fields.append((lay["values"][key], white_mask, lay["digit_family"], str(v), False))
        fields.append((lay["accuracy"], white_mask, lay["digit_family"], exp[11] + "%", True))
        score_mask = gold_mask if exp[0] == "dance" else white_mask
        fields.append((lay["score"], score_mask, lay["score_family"], str(exp[10]), False))
        fields.append((lay["badge_digits"], white_mask, lay["level_family"], str(exp[3]), False))
        for r, mfn, fam, s, dots in fields:
            gl, mask = read_field(crop(img, r), mfn, fam, templates, s, train=True, keep_dots=dots)
            chars = [c for c in s]
            if len(gl) == len(chars):
                for item, ch in zip(gl, chars):
                    if item[0] == ".":
                        continue
                    _, _, g, arr, aspect = item
                    templates.add(fam, ch, arr, aspect)
                    trained += 1
            else:
                report.append(f"TRAIN MISMATCH {name} {fam} expected '{s}' ({len(chars)}) got {len(gl)} glyphs: {[ (g['w'], g['h']) for g in (x[2] for x in gl)]}")
    print(f"trained {trained} glyphs")
    print("\n".join(report))
    # pass 2: detect + classify everything
    print("\n=== detection ===")
    for name, exp in EXPECTED.items():
        img = load(name)
        d = detect(img)
        want = exp[0]
        got = d[0] if d else None
        flag = "" if (got == want or (want in ("empty", "challenge") and got == "dance")) else "  <-- MISMATCH"
        fr = {k: round(v, 3) for k, v in d[2].items()} if d else {}
        print(f"{name} expected={want} detected={got} {fr}{flag}")
    print("\n=== reading ===")
    bad = 0
    for name, exp in EXPECTED.items():
        if exp[0] not in ("dance", "arcade"):
            continue
        img = load(name)
        lay = LAYOUTS[exp[0]]
        got = {}
        for key in ["perfect", "great", "good", "bad", "miss", "combo"]:
            gl, _ = read_field(crop(img, lay["values"][key]), white_mask, lay["digit_family"], templates, keep_dots=False)
            got[key] = "".join(c or "?" for c, s, g in gl)
        gl, _ = read_field(crop(img, lay["accuracy"]), white_mask, lay["digit_family"], templates)
        got["acc"] = "".join(c or "?" for c, s, g in gl)
        score_mask = gold_mask if exp[0] == "dance" else white_mask
        gl, _ = read_field(crop(img, lay["score"]), score_mask, lay["score_family"], templates, keep_dots=False)
        got["score"] = "".join(c or "?" for c, s, g in gl)
        gl, _ = read_field(crop(img, lay["badge_digits"]), white_mask, lay["level_family"], templates, keep_dots=False)
        got["level"] = "".join(c or "?" for c, s, g in gl)
        want = dict(zip(["perfect", "great", "good", "bad", "miss", "combo"], [str(v) for v in exp[4:10]]))
        want["acc"] = exp[11] + "%"; want["score"] = str(exp[10]); want["level"] = str(exp[3])
        diffs = {k: (want[k], got[k]) for k in want if want[k] != got[k]}
        if diffs:
            bad += 1
        print(f"{name}: {'OK' if not diffs else diffs}")
    print(f"\n{bad} screens with a misread")
    print("\n=== non-plays that detect as dance: badge and digits ===")
    for name, exp in EXPECTED.items():
        if exp[0] not in ("challenge", "empty"):
            continue
        img = load(name)
        lay = LAYOUTS["dance"]
        reg = crop(img, lay["badge"])
        h, s, v = hsv(reg)
        m = (s > 0.5) & (v > 0.4)
        hh = h[m]
        red = float(((hh < 20) | (hh > 340)).mean()) if hh.size else 0
        blue = float(((hh > 190) & (hh < 250)).mean()) if hh.size else 0
        counts = {k: len(segments(white_mask(crop(img, r)), keep_dots=False)) for k, r in lay["values"].items()}
        print(f"{name} {exp[0]}: badge red={red:.2f} blue={blue:.2f} saturated={float(m.mean()):.2f} value glyphs={counts}")
    # sticker greyness
    print("\n=== sticker saturation (dance) ===")
    for name, exp in EXPECTED.items():
        if exp[0] != "dance":
            continue
        img = load(name)
        reg = crop(img, LAYOUTS["dance"]["sticker"])
        h, s, v = hsv(reg)
        bright = v > 0.5
        sat = float((s[bright] > 0.4).mean()) if bright.any() else 0
        print(f"{name} broken={exp[12]} saturated_fraction={sat:.3f}")
    # badge color
    print("\n=== badge color ===")
    for name, exp in EXPECTED.items():
        if exp[0] not in ("dance", "arcade"):
            continue
        img = load(name)
        reg = crop(img, LAYOUTS[exp[0]]["badge"])
        h, s, v = hsv(reg)
        m = (s > 0.5) & (v > 0.4)
        hh = h[m]
        red = float(((hh < 20) | (hh > 340)).mean()) if hh.size else 0
        blue = float(((hh > 190) & (hh < 250)).mean()) if hh.size else 0
        green = float(((hh > 80) & (hh < 160)).mean()) if hh.size else 0
        print(f"{name} type={exp[2]} red={red:.2f} blue={blue:.2f} green={green:.2f}")
    # leave-one-screen-out: train on every other screen, read the held-out one
    print("\n=== leave-one-out ===")
    def fields_of(exp, lay):
        fs = []
        for key, v in zip(["perfect", "great", "good", "bad", "miss", "combo"], exp[4:10]):
            fs.append((key, lay["values"][key], white_mask, lay["digit_family"], str(v)))
        fs.append(("score", lay["score"], gold_mask if exp[0] == "dance" else white_mask, lay["score_family"], str(exp[10])))
        fs.append(("level", lay["badge_digits"], white_mask, lay["level_family"], str(exp[3])))
        return fs
    loo_bad = 0
    worst_best = 1.0
    margins = []
    for held, hexp in EXPECTED.items():
        if hexp[0] not in ("dance", "arcade"):
            continue
        t = Templates()
        for name, exp in EXPECTED.items():
            if name == held or exp[0] not in ("dance", "arcade"):
                continue
            img = load(name)
            for key, r, mfn, fam, s in fields_of(exp, LAYOUTS[exp[0]]):
                gl, mask = read_field(crop(img, r), mfn, fam, t, s, train=True, keep_dots=False)
                if len(gl) == len(s):
                    for item, ch in zip(gl, s):
                        t.add(fam, ch, item[3], item[4])
        img = load(held)
        diffs = {}
        for key, r, mfn, fam, s in fields_of(hexp, LAYOUTS[hexp[0]]):
            gl, mask = read_field(crop(img, r), mfn, fam, t, keep_dots=False)
            got = "".join(c or "?" for c, sc, g in gl)
            for c, sc, g in gl:
                if c is not None:
                    worst_best = min(worst_best, sc)
                    # margin to the best other char
                    arr = normalize(mask, g); aspect = g["w"] / max(g["h"], 1)
                    second = 0.0
                    for ch2, items in t.t.get(fam, {}).items():
                        if ch2 == c:
                            continue
                        for tarr, tasp in items:
                            if abs(tasp - aspect) > 0.45 * max(tasp, aspect):
                                continue
                            second = max(second, similarity(arr, tarr))
                    margins.append(sc - second)
            if got != s:
                diffs[key] = (s, got)
        if diffs:
            loo_bad += 1
        print(f"{held}: {'OK' if not diffs else diffs}")
    print(f"leave-one-out: {loo_bad} screens with a misread; lowest accepted similarity {worst_best:.3f}; "
          f"margin min {min(margins):.3f} p5 {sorted(margins)[len(margins)//20]:.3f} median {sorted(margins)[len(margins)//2]:.3f}")
    # scan every screenshot in the folder for result screens I have not labeled
    print("\n=== unlabeled screenshots that look like results ===")
    for fn in sorted(os.listdir(SHOTS)):
        if not fn.endswith(".jpg"):
            continue
        name = fn[:-6] if fn.endswith("_1.jpg") else fn[:-4]
        if name in EXPECTED:
            continue
        d = detect(load(name))
        if d:
            print(f"{name}: {d[0]}")
    # dedupe templates
    for fam, chars in templates.t.items():
        for ch, items in chars.items():
            seen = {}
            for arr, asp in items:
                seen[arr.tobytes()] = (arr, asp)
            chars[ch] = list(seen.values())
    print("deduped template counts:", {fam: {ch: len(v) for ch, v in chars.items()} for fam, chars in templates.t.items()})
    # dump templates
    dump = {fam: {ch: [[int(x) for x in arr.flatten()] for arr, asp in items] for ch, items in chars.items()} for fam, chars in templates.t.items()}
    asp = {fam: {ch: [round(float(asp), 3) for arr, asp in items] for ch, items in chars.items()} for fam, chars in templates.t.items()}
    with open(os.path.join(HERE, "templates-screens-only.json"), "w") as f:
        json.dump({"w": TW, "h": TH, "templates": dump, "aspects": asp}, f)
    print("\ntemplate counts:", {fam: {ch: len(v) for ch, v in chars.items()} for fam, chars in templates.t.items()})

if __name__ == "__main__":
    main()
