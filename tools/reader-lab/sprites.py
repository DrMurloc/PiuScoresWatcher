import re, os
import UnityPy
DATA = r"C:\Program Files (x86)\Steam\steamapps\common\PUMP IT UP RISE\PUMP IT UP RISE_Data"
pat = re.compile(r"Number|Score|Digit|Percent|Combo|Accuracy", re.I)
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sprites")
os.makedirs(out, exist_ok=True)
seen = set()
for fn in ("resources.assets", "sharedassets0.assets"):
    env = UnityPy.load(os.path.join(DATA, fn))
    for obj in env.objects:
        if obj.type.name != "Sprite":
            continue
        d = obj.read()
        name = d.m_Name
        if not pat.search(name) or name in seen:
            continue
        seen.add(name)
        try:
            img = d.image
            print(f"{name:45} {img.size[0]}x{img.size[1]}")
            if re.search(r"Arcade_(Big)?Number|ExtraScore|Score_|Arcade.*Score|DanceGrade_Text", name):
                img.save(os.path.join(out, name + ".png"))
        except Exception as e:
            print(f"{name:45} ERR {e}")
