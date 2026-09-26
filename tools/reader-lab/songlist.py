"""The Warm Up song list (watcher.md D45-D48): where the lit chart and its best sit, how they read, and the
grade-badge check; and every other row's marks and Perfect Games (D84, D85). Trains the two digit families the list
prints in and writes the grade features for templates.py; run it on its own for a report over the labeled song-list
screens, or with --rows for the rows alone."""
import json, os
import numpy as np
from PIL import Image
import reader as P

GRADES_OUT = os.path.join(P.REPO, "src", "PiuScoresWatcher.Core", "Recognition", "grades.json")
GRADES = ["SSS", "SS", "S", "AA", "A", "B", "C", "D", "F"]
BG = np.array([34, 34, 36], dtype=np.float64)   # the panel behind the badge
FEATURE = 16                                     # the badge is compared as 16x16 RGB

# 1080p rectangles; the reader keeps them as fractions
BANNER = P.R(140, 30, 630, 110)                  # the yellow WARM UP banner
TABS = {"S": P.R(174, 492, 383, 540), "HD": P.R(398, 492, 607, 540)}   # 5K SINGLE, 6K DOUBLE
# the lit tab's border and caption, as (hue lo, hue hi, saturation, value): 5K SINGLE's orange (hue about 23),
# 6K DOUBLE's blue (about 201); an unlit tab is grey (watcher.md D71)
TAB_LIT = {"S": (15, 40, 0.6, 0.6), "HD": (185, 230, 0.6, 0.6)}
BOX_X0, BOX_STRIDE, BOX_W, BOX_Y0, BOX_Y1, BOXES = 83, 90, 76, 562, 630, 6
SCORE = P.R(320, 636, 452, 668)
COMBO = P.R(320, 716, 452, 748)
BADGE = P.R(470, 640, 600, 750)
# the lit song's title, where the game cuts it off: the list's lit row first, then the panel short of the card's
# white edge (watcher.md D66, D68)
ROW_TITLE = P.R(872, 576, 1433, 618)
TITLE = P.R(80, 388, 606, 440)

def box(i):
    x0 = BOX_X0 + BOX_STRIDE * i
    return P.R(x0 + 4, BOX_Y0, x0 + BOX_W - 4, BOX_Y1)

def box_digits(i):
    """Inside the box, right of the H stamp that overlaps its corner and above the three bars."""
    x0 = BOX_X0 + BOX_STRIDE * i
    return P.R(x0 + 21, 574, x0 + 66, 604)

# The list's seven rows, the lit song in the fourth (watcher.md D66). Under each level in a row three bars light gold from
# the left for the chart's best mark: one No Miss, two Full Combo, three a Perfect Game (D84). Measured on the first
# tester's 4K lists, halved: the bars' centre line, the row pitch, the six places for a level (right-aligned), and the
# inside of each bar clear of its dark edges.
ROWS, LIT_ROW = 7, 4
ROW_BARS_Y, ROW_PITCH = 300.8, 108.36
PLACE_X0, PLACE_PITCH, PLACES = 1454, 62, 6
BARS = ((1, 11), (15, 25), (29, 40))
BAR_HALF = 1.5
# a bar lit gold, a bar unlit grey (a colourless dark grey: the row behind is navy), and the lit row's yellow, which
# hides empty places behind the same gold
BAR_LIT = (35, 55, 0.7, 0.75)
BAR_UNLIT_SAT, BAR_UNLIT_VAL = 0.3, (0.18, 0.45)
ROW_LIT = (30, 60, 0.6, 0.7)
BAR_SHARE, ROW_LIT_SHARE = 0.6, 0.1

def bars_y(row):
    return ROW_BARS_Y + ROW_PITCH * (row - 1)

def bar(row, place, i):
    x0 = PLACE_X0 + PLACE_PITCH * place
    y = bars_y(row)
    return P.R(x0 + BARS[i][0], y - BAR_HALF, x0 + BARS[i][1], y + BAR_HALF)

def row_digits(row, place):
    """A place's level, above its bars; the H stamp sits over its top left, as it does on the panel's boxes."""
    x0 = PLACE_X0 + PLACE_PITCH * place
    y = bars_y(row)
    return P.R(x0 - 6, y - 40, x0 + 48, y - 6)

def row_lit_patch(row):
    """Right of the row's last place, where nothing but the row itself is drawn: yellow on the lit row, navy on the rest."""
    y = bars_y(row)
    return P.R(1812, y - 14, 1880, y + 14)

def row_title(row):
    """The lit row's title box (ROW_TITLE), moved to the row."""
    dy = bars_y(row) - bars_y(LIT_ROW)
    return P.R(872, 576 + dy, 1433, 618 + dy)

def row_jacket(row):
    dy = bars_y(row) - bars_y(LIT_ROW)
    return P.R(792, 572 + dy, 858, 648 + dy)

def bar_state(img, r):
    """'L' lit, 'u' unlit, '.' neither."""
    h, s, v = P.hsv(P.crop(img, r))
    lo, hi, smin, vmin = BAR_LIT
    if float(((h >= lo) & (h <= hi) & (s >= smin) & (v >= vmin)).mean()) >= BAR_SHARE:
        return "L"
    if float(((s <= BAR_UNLIT_SAT) & (v >= BAR_UNLIT_VAL[0]) & (v <= BAR_UNLIT_VAL[1])).mean()) >= BAR_SHARE:
        return "u"
    return "."

def read_rows(img, templates):
    """Every row but the lit one, as the watcher reads it (D85): per row, None for the lit row, '?' for one that can't be
    read on this frame, else [(level or None, lit bars)] left to right — the level read only where all three are lit."""
    out = {}
    for row in range(1, ROWS + 1):
        if share(img, row_lit_patch(row), *ROW_LIT) >= ROW_LIT_SHARE:
            out[row] = None
            continue
        charts, seen_chart, readable = [], False, True
        for place in range(PLACES):
            states = "".join(bar_state(img, bar(row, place, i)) for i in range(3))
            if states == "...":
                if seen_chart:  # a chart, then an empty place: nothing right-aligned looks like that
                    readable = False
                continue
            if states not in ("uuu", "Luu", "LLu", "LLL"):
                readable = False
                continue
            seen_chart = True
            lit = states.count("L")
            level = None
            if lit == 3:
                gl, _ = P.read_field(P.crop(img, row_digits(row, place)), light_mask, "wllevel", templates, keep_dots=False)
                text = "".join(c or "?" for c, s, g in gl)
                level = int(text) if text.isdigit() else None
                readable = readable and level is not None
            charts.append((level, lit))
        out[row] = charts if readable else "?"
    return out

def describe_rows(rows):
    """The label's form: each chart's level (or '-' where it isn't read) and lit bars, per row."""
    def one(charts):
        if charts is None or charts == "?":
            return charts
        return " ".join(f"{'-' if level is None else level}:{lit}" for level, lit in charts)
    return [one(rows[row]) for row in range(1, ROWS + 1)]

# name -> (kind, title, type, level, best, max combo, grade); arcade-list screens must never read as Warm Up
EXPECTED_LIST = {
    "20260923193118": ("warmup", "Aragami", "S", 19, 971789, 392, "SS"),
    "20260923193120": ("warmup-empty", "Aragami", "S", 17, None, None, None),
    "20260923193123": ("warmup", "Destr0yer", "S", 22, 971998, 507, "SS"),
    "20260923193126": ("warmup", "L (PIU Edit)", "S", 21, 956496, 290, "S"),
    "20260923193144": ("warmup", "Vacuum Cleaner", "S", 20, 956984, 241, "S"),
    "20260923193156": ("warmup", "Morrighan", "S", 20, 945403, 170, "AA"),
    # the song wheel frame from the first batch, labeled "not a result" then: the list with no best on the lit chart
    "20260922072011": ("warmup-empty", "Fantasia Sonata Destiny", "S", 20, None, None, None),
    "20260923193215": ("arcade-list",), "20260923193219": ("arcade-list",), "20260923193249": ("arcade-list",),
    "20260923193303": ("arcade-list",), "20260923194347": ("arcade-list",), "20260923194349": ("arcade-list",),
}
# every level box on each Warm Up screen, left to right: the lit one and the grey ones teach the same font
BOX_LEVELS = {
    "20260923193118": [15, 17, 19, 22, 24], "20260923193120": [15, 17, 19, 22, 24],
    "20260923193123": [7, 13, 18, 22], "20260923193126": [10, 17, 21, 24],
    "20260923193144": [16, 20, 25], "20260923193156": [16, 18, 20, 22], "20260922072011": [15, 18, 20, 23],
}

# The first tester's song lists (watcher.md D66-D70): 3840x2160, kept by his watcher on 2026-09-24/25, added with his
# ok. Each name is its failed\ file's stamp without the dash; the note says what the screen pins.
TESTER = {
    "20260924233850": (("warmup", "wanna go to the moon palace", "S", 21, 995844, 1111, "SSS"), [21],
                       "the panel shows the start of the title cut off at its edge; the list's row holds all of it (D66, D68)"),
    "20260925003656": (("warmup", "The People didn't know \"Pumping up\"", "S", 8, 1000000, 546, "SSS"), [8],
                       "the panel shows only the tail, 'Pumping up', which is another song's whole title (D68)"),
    "20260924234156": (("warmup", "Blaze emotion (Band version)", "S", 9, 1000000, 465, "SSS"), [2, 9, 17],
                       "the title has scrolled off the panel entirely; the row holds it (D66)"),
    "20260924235245": (("warmup", "Conflict -NOMA CONCEiVER REMiX-", "S", 16, 996047, 886, "SSS"), [7, 11, 16, 18, 22],
                       "the panel shows 'Conflict -', the start of the title and another song's whole name (D68)"),
    "20260925003645": (("warmup", "The Quick Brown Fox Jumps Over The Lazy Dog", "S", 11, 1000000, 586, "SSS"), [11],
                       "too long for the row as well: both places show its start, cut off at the edge (D68)"),
    "20260925003727": (("warmup", "The Quick Brown Fox Jumps Over The Lazy Dog", "S", 19, 995422, 591, "SSS"), [11, 16, 19, 23],
                       "the row shows the tail scrolling out, the panel the start coming back in (D68, D69)"),
    "20260924233951": (("warmup", "B2", "S", 7, 1000000, 514, "SSS"), [4, 7, 10, 16, 18],
                       "a short title Windows OCR reads only three times over (D67)"),
    "20260924235401": (("warmup", "D", "S", 4, 1000000, 185, "SSS"), [4, 7, 11, 18],
                       "a one-letter title (D67)"),
    "20260925002550": (("warmup", "N", "S", 16, 954989, 289, "S"), [5, 16],
                       "a one-letter title (D67)"),
    "20260924235732": (("warmup", "Dr. M", "S", 6, 1000000, 298, "SSS"), [3, 6, 9, 11, 14, 16],
                       "a short title (D67)"),
    "20260925002824": (("warmup", "8 6", "S", 12, 1000000, 550, "SSS"), [12],
                       "the panel's box used to take in the card's white edge, and 8 6 read as nothing (D66)"),
    "20260925000000": (("warmup", "Elysium", "S", 4, 1000000, 272, "SSS"), [4, 9, 14],
                       "a title that reads, at a level PIU Scores' Rise list had wrong (D70)"),
}
# The owner's F12s of the list with 6K DOUBLE lit (watcher.md D71): the tab is blue, not orange
SIX_K = {
    "20260926111534": (("warmup", "Curiosity Overdrive", "HD", 16, 972054, 200, "SS"), [16, 21, 24],
                       "6K DOUBLE lit: its tab is blue where 5K SINGLE's is orange (D71)"),
    "20260926111537": (("warmup-empty", "SONIC BOOM", "HD", 14, None, None, None), [14, 18, 23, 25],
                       "6K DOUBLE lit, a chart with no best (D71)"),
    "20260926111606": (("warmup-empty", "ULTRA SYNERGY MATRIX", "HD", 20, None, None, None), [12, 20, 23],
                       "6K DOUBLE lit, caught as the highlight reached the second box: its digits still grey (D71)"),
}
EXPECTED_LIST.update({name: exp for name, (exp, _, _) in (TESTER | SIX_K).items()})
BOX_LEVELS.update({name: boxes for name, (_, boxes, _) in (TESTER | SIX_K).items()})
NOTES = {name: note for name, (_, _, note) in (TESTER | SIX_K).items()}
# every row but the lit one, per place 1-7, as the owner's and the testers' screens show them, read by eye (D84): each
# chart's level and lit bars; None for the lit row, and "" for one with no chart (the bottom row under the lab's old
# player-card mask)
ROW_LABELS = {
    "20260922072011": ["11:0 14:0 17:0 20:0 22:0", "15:0 19:0 22:0 24:0", "7:0 11:0 16:0 18:0 22:0", None, "7:0 13:0 18:0 22:0", "12:0 18:0 20:0 22:0", ""],
    "20260923193118": ["15:0 18:0 20:0 23:0", "7:0 13:0 18:0 22:0", "12:0 18:0 20:0 22:0", None, "14:0 17:0 20:0 23:0", "12:0 16:0 18:0 20:0 22:0", "11:0 14:0 17:0 19:0 21:0 23:0"],
    "20260923193120": ["15:0 18:0 20:0 23:0", "7:0 13:0 18:0 22:0", "12:0 18:0 20:0 22:0", None, "14:0 17:0 20:0 23:0", "12:0 16:0 18:0 20:0 22:0", "11:0 14:0 17:0 19:0 21:0 23:0"],
    "20260923193123": ["15:0 19:0 22:0 24:0", "7:0 11:0 16:0 18:0 22:0", "15:0 18:0 20:0 23:0", None, "12:0 18:0 20:0 22:0", "15:0 17:0 19:0 22:0 24:0", "14:0 17:0 20:0 23:0"],
    "20260923193126": ["13:0 16:0 20:0 22:0", "16:0 18:0 21:0 23:0", "12:0 20:0 23:0", None, "12:0 15:0 18:0 21:0", "11:0 14:0 17:0 20:0 22:0", "15:0 19:0 22:0 24:0"],
    "20260923193144": ["18:0 21:0", "18:0", "21:0", None, "21:0", "18:0", "13:0 18:0"],
    "20260923193156": ["4:0 7:0 11:0 16:0 19:0", "17:0 19:0 24:0", "4:0 7:0 11:0 16:0 18:0 20:0", None, "12:0 15:0 18:0 21:0", "4:0 7:0 11:0 16:0 19:0 21:0", "12:0 16:0 19:0 21:0"],
    "20260924233850": ["21:0", "21:0", "21:0", None, "21:0", "21:0", "21:0"],
    "20260924233951": ["7:3 10:3 14:3 17:2 19:2 21:0", "14:3 18:1", "13:3 18:2", None, "16:1 18:1 21:0 23:0", "13:3 19:0", "6:3 8:3"],
    "20260924234156": ["1:3 5:3 9:3 13:3 16:2", "4:3 6:3 12:3", "3:3 6:3 7:3 9:3 16:1 17:1", None, "4:3 7:3 9:3 17:1 20:1", "3:3 5:3 8:3 12:3 16:0 20:0", "7:3 12:3 15:0 17:2 20:1"],
    "20260924235245": ["10:3 15:2 18:2 21:0", "4:3 6:3 11:0 13:3 17:1", "6:3 11:0 15:0 17:2 19:2 22:0", None, "13:3 16:2 19:0 22:0", "7:3 11:0 15:0 18:1 22:0", "4:3 7:3 9:3 12:3 15:2"],
    "20260924235401": ["10:0 15:1 18:2 20:0 22:0", "4:3 7:3 11:0 17:2 19:3 21:0", "8:3 12:3 16:3 18:3 21:0", None, "5:3 6:3 11:0 18:1", "2:3 4:3 6:3 15:3", "8:3 12:3 16:2 19:1 21:1"],
    "20260924235732": ["4:3 7:3 10:0 17:3 19:1", "5:3 13:3 18:1", "4:3 6:3 18:1", None, "12:0 18:0", "16:3 19:0 22:0 24:0", "10:0 13:3 16:1 18:1 21:0"],
    "20260925000000": ["15:0 17:0 19:0 21:1", "4:3 7:3 11:0 16:2 18:1 22:0", "11:3 14:3 17:2 21:0", None, "2:3 8:3 12:2 16:2", "7:3 11:0 16:0 18:1 20:1", "13:3 16:2 20:0 22:0"],
    "20260925002550": ["4:3 10:2 18:0 19:1", "4:3 6:3 8:0 15:2", "15:0 18:1 21:0 23:0", None, "5:3 8:3 12:2 20:0", "9:0 16:0 19:1", "11:0 15:0 18:2"],
    "20260925002824": ["15:3", "9:3", "12:3", None, "3:3", "6:3", "14:3"],
    "20260925003645": ["12:3", "16:3", "8:3", None, "3:3", "4:3", "11:3"],
    "20260925003656": ["4:3 6:3 9:3 12:3 19:1", "4:3 7:3 11:3 16:2 19:0 21:0", "3:3 5:3 12:3 16:3", None, "11:3 16:0 19:1 23:0", "8:0 11:0 15:0 17:0 19:0 21:0", "3:3 4:3 11:3 13:3 17:1"],
    "20260925003727": ["4:3 7:3 11:3 16:2 19:0 21:0", "3:3 5:3 12:3 16:3", "8:3", None, "8:0 11:0 15:0 17:0 19:0 21:0", "3:3 4:3 11:3 13:3 17:1", "12:3 16:3 19:0 21:0"],
    "20260926111534": ["18:0 21:0 24:0", "13:0 17:0 23:0 25:0", "14:0 18:0 23:0 25:0", None, "18:0 24:0 25:0", "21:0 25:0 26:0", "21:0 24:0 27:0"],
    "20260926111537": ["13:0 18:0 21:0 24:0", "18:0 21:0 24:0", "13:0 17:0 23:0 25:0", None, "16:0 21:0 24:0", "18:0 24:0 25:0", "21:0 25:0 26:0"],
    "20260926111606": ["14:0 20:0 23:0", "17:0 20:0 23:0 25:0", "17:0 21:0 24:0 26:0", None, "16:0 18:0 24:0", "17:0 21:0 24:0", "11:0 20:0 24:0"],
}
KEPT = None  # a watcher's failed\ folder the tester's screens are copied from (--fixtures <folder>)


def source(name):
    """The screenshot, the fixture already made from it, or a kept screen named the watcher's way
    (20260924-233850-watcher-grab.png) — a tester's screen is in no Steam folder of the owner's."""
    fixture = os.path.join(P.REPO, "tests", "PiuScoresWatcher.Tests", "Fixtures", "screens", name + ".jpg")
    kept = os.path.join(KEPT, f"{name[:8]}-{name[8:]}-watcher-grab.png") if KEPT else None
    return (P.shot_path(name) or (fixture if os.path.exists(fixture) else None)
            or (kept if kept and os.path.exists(kept) else None))


def load(name):
    return np.asarray(Image.open(source(name)).convert("RGB")).astype(np.int16)


AVAILABLE = {n: e for n, e in EXPECTED_LIST.items() if source(n)}

def share(img, r, lo, hi, smin, vmin):
    h, s, v = P.hsv(P.crop(img, r))
    return float(((h >= lo) & (h <= hi) & (s >= smin) & (v >= vmin)).mean())

def light_mask(region):
    """Light and colourless, relative to the field's brightest such pixel: the values white or mid-fade grey, the
    level digits white or grey, never the yellow box. A fixed bar low enough for the grey let the white digits'
    anti-aliasing bridge the gaps between them (Destr0yer's 971998 read as four glyphs)."""
    spread = region.max(axis=2) - region.min(axis=2)
    lum = P.lum(region)
    colourless = spread < 60
    brightest = float(lum[colourless].max()) if colourless.any() else 0.0
    return colourless & (lum > max(100.0, 0.6 * brightest))

def detect(img):
    """(chart type, lit box) on a Warm Up song list, else None."""
    if share(img, BANNER, 35, 60, 0.6, 0.7) < 0.2:
        return None
    lit_tabs = [t for t, r in TABS.items() if share(img, r, *TAB_LIT[t]) >= 0.08]
    lit_boxes = [i for i in range(BOXES) if share(img, box(i), 40, 60, 0.6, 0.7) >= 0.3]
    if len(lit_tabs) != 1 or len(lit_boxes) != 1:
        return None
    return lit_tabs[0], lit_boxes[0]

def grade_feature(rgb, fg):
    """Mean colour of 16x16 cells over the badge's bounding box, centred and unit length — the C# reader bins the same way."""
    ys, xs = np.nonzero(fg)
    if ys.size == 0:
        return None
    y0, y1, x0, x1 = int(ys.min()), int(ys.max()) + 1, int(xs.min()), int(xs.max()) + 1
    h, w = y1 - y0, x1 - x0
    out = np.zeros((FEATURE, FEATURE, 3))
    for cy in range(FEATURE):
        ya = y0 + (cy * h) // FEATURE
        yb = max(ya + 1, y0 + ((cy + 1) * h) // FEATURE)
        for cx in range(FEATURE):
            xa = x0 + (cx * w) // FEATURE
            xb = max(xa + 1, x0 + ((cx + 1) * w) // FEATURE)
            out[cy, cx] = rgb[ya:yb, xa:xb].reshape(-1, 3).mean(axis=0)
    v = out.flatten()
    v = v - v.mean()
    return v / (np.linalg.norm(v) + 1e-9)

def sprite_feature(grade):
    im = np.asarray(Image.open(os.path.join(P.HERE, "sprites", f"Rise_Grade_{grade}.png")).convert("RGBA")).astype(np.float64)
    a = im[..., 3:4] / 255.0
    return grade_feature(im[..., :3] * a + BG * (1 - a), im[..., 3] > 40)

def badge_feature(img):
    reg = P.crop(img, BADGE).astype(np.float64)
    return grade_feature(reg, np.abs(reg - BG).sum(axis=2) > 60)

def classify_grade(feature, feats):
    if feature is None:
        return None, 0.0, 0.0
    ranked = sorted(((float(feature @ f), g) for g, f in feats.items()), reverse=True)
    return ranked[0][1], ranked[0][0], ranked[0][0] - ranked[1][0]

def train(templates):
    """Adds the song list's two digit families to a template set: the level boxes, and the best score and max combo."""
    added = 0
    for name, exp in AVAILABLE.items():
        if exp[0] not in ("warmup", "warmup-empty"):
            continue
        img = load(name)
        fields = [(box_digits(i), "wllevel", str(level)) for i, level in enumerate(BOX_LEVELS[name])]
        if exp[0] == "warmup":
            fields += [(SCORE, "wlvalue", str(exp[4])), (COMBO, "wlvalue", str(exp[5]))]
        for r, fam, s in fields:
            gl, mask = P.read_field(P.crop(img, r), light_mask, fam, templates, s, train=True, keep_dots=False)
            if len(gl) != len(s):
                print(f"  train skip {name} {fam} '{s}': {len(gl)} glyphs")
                continue
            for item, ch in zip(gl, s):
                templates.add(fam, ch, item[3], item[4])
                added += 1
    return added

def write_grades():
    feats = {g: sprite_feature(g) for g in GRADES}
    with open(GRADES_OUT, "w") as f:
        json.dump({"size": FEATURE, "background": [int(c) for c in BG], "grades": {g: [round(float(x), 5) for x in v] for g, v in feats.items()}}, f)
    print("wrote", GRADES_OUT)
    return feats

def read(img, templates, feats):
    found = detect(img)
    if found is None:
        return None
    chart, lit = found
    def field(r, fam):
        gl, _ = P.read_field(P.crop(img, r), light_mask, fam, templates, keep_dots=False)
        return "".join(c or "?" for c, s, g in gl), min((s for c, s, g in gl), default=0.0)
    level, level_sim = field(box_digits(lit), "wllevel")
    score, score_sim = field(SCORE, "wlvalue")
    grade, grade_sim, margin = classify_grade(badge_feature(img), feats) if score else (None, 0.0, 0.0)
    return dict(chart=chart, box=lit, level=level, score=score, grade=grade, level_sim=level_sim, score_sim=score_sim,
                grade_sim=grade_sim, grade_margin=margin)

def main():
    templates = P.Templates()
    print("trained", train(templates), "glyphs")
    feats = write_grades()
    bad = 0
    for name, exp in AVAILABLE.items():
        got = read(load(name), templates, feats)
        if exp[0] == "arcade-list":
            ok = got is None
            print(name, "arcade list ->", "not a Warm Up list" if ok else got)
        else:
            want = dict(chart=exp[2], level=str(exp[3]), score=str(exp[4]) if exp[4] else "", grade=exp[6])
            ok = got is not None and all(got[k] == v for k, v in want.items())
            print(name, exp[1], "->", {k: (round(v, 3) if isinstance(v, float) else v) for k, v in (got or {}).items()})
        bad += 0 if ok else 1
    print("song list:", bad, "misread")
    rows_report(templates)

def rows_agree(label, got):
    """A row as read against its label: the same lit bars for every chart, and the level wherever all three are lit."""
    if label is None or label == "?" or got is None or got == "?":
        return label == got
    want = [(int(level), int(lit)) for level, lit in (chart.split(":") for chart in label.split())]
    return len(want) == len(got) and all(lit == got_lit and (lit < 3 or level == got_level)
                                         for (level, lit), (got_level, got_lit) in zip(want, got))

def rows_report(templates):
    """Every labeled song list's rows through read_rows (D84, D85): what disagrees with the label, and the totals."""
    charts = perfect = unread = bad = 0
    for name in sorted(ROW_LABELS):
        if not source(name):
            continue
        rows = read_rows(load(name), templates)
        label = ROW_LABELS[name]
        for row in range(1, ROWS + 1):
            got = rows[row]
            if got == "?":
                unread += 1
            elif got is not None:
                charts += len(got)
                perfect += sum(1 for _, lit in got if lit == 3)
            if not rows_agree(label[row - 1], got):
                bad += 1
                print(f"  {name} row {row}: read {describe_rows(rows)[row - 1]!r}, labeled {label[row - 1]!r}")
    print(f"rows: {charts} charts, {perfect} Perfect Games, {unread} rows not read, {bad} disagree with their labels")

FIXTURES = os.path.join(P.REPO, "tests", "PiuScoresWatcher.Tests", "Fixtures", "screens")
# the player card: top right on Warm Up's song list, top left on the Arcade Station's
LIST_CARDS = {"warmup": (1340, 20, 1875, 135), "warmup-empty": (1340, 20, 1875, 135), "arcade-list": (215, 10, 755, 130)}
TYPE_NAMES = {"S": "Single", "HD": "HalfDouble"}

def write_fixtures():
    """Adds the song-list screens to the fixtures, player card blacked out, merging their entries into expected.json."""
    from PIL import ImageDraw
    path = os.path.join(FIXTURES, "expected.json")
    with open(path) as f:
        expected = json.load(f)
    for name, exp in sorted(AVAILABLE.items()):
        target = os.path.join(FIXTURES, name + ".jpg")
        if not os.path.exists(target):  # an existing fixture keeps its masking and its bytes
            im = Image.open(source(name)).convert("RGB")
            sx, sy = im.width / 1920, im.height / 1080
            x0, y0, x1, y1 = LIST_CARDS[exp[0]]
            ImageDraw.Draw(im).rectangle((x0 * sx, y0 * sy, x1 * sx, y1 * sy), fill=(0, 0, 0))
            im.save(target, quality=88)
        if exp[0] == "warmup":
            expected[name] = {"kind": "songlist", "mix": "rise", "title": exp[1], "chartType": TYPE_NAMES[exp[2]],
                              "level": exp[3], "score": exp[4], "maxCombo": exp[5], "grade": exp[6]}
            if name in NOTES:
                expected[name]["note"] = NOTES[name]
            if name in ROW_LABELS:
                expected[name]["rows"] = ROW_LABELS[name]
        elif exp[0] == "warmup-empty":
            expected[name] = {"kind": "songlist-empty", "mix": "rise", "title": exp[1], "chartType": TYPE_NAMES[exp[2]], "level": exp[3]}
            if name in NOTES:
                expected[name]["note"] = NOTES[name]
            if name in ROW_LABELS:
                expected[name]["rows"] = ROW_LABELS[name]
        else:
            expected[name] = {"kind": "arcadelist", "note": "the Arcade Station's song list: not read in v1 (D45)"}
    with open(path, "w") as f:
        json.dump(dict(sorted(expected.items())), f, indent=2)
    print(len(AVAILABLE), "song-list fixtures written;", len(expected), "fixtures in expected.json")

def merge_into_templates():
    """Replaces the song list's two families in the shipped templates.json, leaving the result screens' untouched."""
    templates = P.Templates()
    train(templates)
    with open(P.TEMPLATES_OUT) as f:
        shipped = json.load(f)
    assert shipped["w"] == P.TW and shipped["h"] == P.TH
    for fam, chars in templates.t.items():
        unique = {}
        for ch, items in chars.items():
            seen = {}
            for arr, asp in items:
                seen[arr.tobytes()] = (arr, asp)
            unique[ch] = [{"bits": "".join("1" if x else "0" for x in arr.flatten()), "aspect": round(float(asp), 3)}
                          for arr, asp in seen.values()]
        shipped["families"][fam] = unique
        print(fam, {ch: len(v) for ch, v in sorted(unique.items())})
    with open(P.TEMPLATES_OUT, "w") as f:
        json.dump(shipped, f)
    print("merged into", P.TEMPLATES_OUT)

if __name__ == "__main__":
    import sys
    if "--rows" in sys.argv:  # the rows alone: no grade sprites needed, nothing written
        rows_templates = P.Templates()
        train(rows_templates)
        rows_report(rows_templates)
    elif "--write" in sys.argv:
        merge_into_templates()
        write_grades()
    elif "--fixtures" in sys.argv:
        at = sys.argv.index("--fixtures")
        if len(sys.argv) > at + 1:  # a watcher's failed\ folder the tester's screens come from
            KEPT = sys.argv[at + 1]
            AVAILABLE = {n: e for n, e in EXPECTED_LIST.items() if source(n)}
        write_fixtures()
    else:
        main()
