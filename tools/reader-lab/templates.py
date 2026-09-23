"""Sprite-derived templates from the install, evaluated against the screenshots, then the combined
template set written for the C# generator."""
import os, json
import numpy as np
from PIL import Image
import reader as P

SPR = os.path.join(P.HERE, "sprites")

def sprite_mask(name, fn):
    im = Image.open(os.path.join(SPR, name)).convert("RGBA")
    arr = np.asarray(im).astype(np.float32)
    a = arr[..., 3:4] / 255.0
    rgb = (arr[..., :3] * a).astype(np.int16)  # composited over black
    return fn(rgb)

FAMILIES = {
    "dgscore": [(f"DanceGrade_Text_MainScore_{d}.png", str(d), P.gold_mask) for d in range(10)],
    "arscore": [(f"Arcade_Score_Number_{d}.png", str(d), P.white_mask) for d in range(10)],
    "arlevel": [(f"Arcade_BigNumber_{k}_{d}.png", str(d), P.white_mask) for k in "SD" for d in range(10)],
}
sprites = P.Templates()
for fam, items in FAMILIES.items():
    for fn, ch, mfn in items:
        mask = sprite_mask(fn, mfn)
        gl = P.segments(mask, keep_dots=False)
        g = max(gl, key=lambda g: g["h"])
        if len(gl) != 1:
            print(f"  note: {fn} segmented into {len(gl)} pieces, using the tallest {g['w']}x{g['h']}")
        sprites.add(fam, ch, P.normalize(mask, g), g["w"] / g["h"])

def fields(exp, lay):
    fs = [("score", lay["score"], P.gold_mask if exp[0] == "dance" else P.white_mask, lay["score_family"], str(exp[10])),
          ("level", lay["badge_digits"], P.white_mask, lay["level_family"], str(exp[3]))]
    return fs

print("=== sprite-only templates reading the screenshots' score and level fields ===")
bad = 0; lowest = 1.0; margins = []
for name, exp in P.EXPECTED.items():
    if exp[0] not in ("dance", "arcade"):
        continue
    img = P.load(name)
    lay = P.LAYOUTS[exp[0]]
    diffs = {}
    for key, r, mfn, fam, s in fields(exp, lay):
        if fam not in sprites.t:
            continue
        gl, mask = P.read_field(P.crop(img, r), mfn, fam, sprites, keep_dots=False)
        got = "".join(c or "?" for c, sc, g in gl)
        for c, sc, g in gl:
            if c is not None:
                lowest = min(lowest, sc)
                arr = P.normalize(mask, g); aspect = g["w"] / max(g["h"], 1)
                second = 0.0
                for ch2, items in sprites.t[fam].items():
                    if ch2 == c: continue
                    for tarr, tasp in items:
                        if abs(tasp - aspect) > 0.45 * max(tasp, aspect): continue
                        second = max(second, P.similarity(arr, tarr))
                margins.append(sc - second)
        if got != s:
            diffs[key] = (s, got)
    if diffs:
        bad += 1
        print(f"{name}: {diffs}")
m = sorted(margins)
print(f"sprite-only: {bad} screens misread; lowest accepted similarity {lowest:.3f}; margin min {m[0]:.3f} p5 {m[len(m)//20]:.3f} median {m[len(m)//2]:.3f}")

# combined set: everything the screenshots taught plus the sprites, deduped, 12x20
combined = P.Templates()
for fam, chars in sprites.t.items():
    for ch, items in chars.items():
        for arr, asp in items:
            combined.add(fam, ch, arr, asp)
for name, exp in P.EXPECTED.items():
    if exp[0] not in ("dance", "arcade"):
        continue
    img = P.load(name)
    lay = P.LAYOUTS[exp[0]]
    fs = []
    for key, v in zip(["perfect", "great", "good", "bad", "miss", "combo"], exp[4:10]):
        fs.append((lay["values"][key], P.white_mask, lay["digit_family"], str(v)))
    fs.append((lay["accuracy"], P.white_mask, lay["digit_family"], exp[11] + "%"))
    fs += [(r, mfn, fam, s) for key, r, mfn, fam, s in fields(exp, lay)]
    for r, mfn, fam, s in fs:
        dots = "%" in s
        gl, mask = P.read_field(P.crop(img, r), mfn, fam, combined, s, train=True, keep_dots=dots)
        if len(gl) == len(s):
            for item, ch in zip(gl, s):
                if item[0] == ".": continue
                combined.add(fam, ch, item[3], item[4])
for fam, chars in combined.t.items():
    for ch, items in chars.items():
        seen = {}
        for arr, asp in items:
            seen[arr.tobytes()] = (arr, asp)
        chars[ch] = list(seen.values())
counts = {fam: {ch: len(v) for ch, v in sorted(chars.items())} for fam, chars in combined.t.items()}
print("combined template counts:", counts)
dump = {fam: {ch: [{"bits": "".join("1" if x else "0" for x in arr.flatten()), "aspect": round(float(asp), 3)} for arr, asp in items]
              for ch, items in chars.items()} for fam, chars in combined.t.items()}
with open(P.TEMPLATES_OUT, "w") as f:
    json.dump({"w": P.TW, "h": P.TH, "families": dump}, f)
print("wrote", P.TEMPLATES_OUT, P.TW, "x", P.TH)
