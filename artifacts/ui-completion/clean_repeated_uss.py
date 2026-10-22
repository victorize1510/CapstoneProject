"""Remove only identical superseded declarations; preserve the cascade and GUIDs."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
folder = root / "Capstone/Assets/ThangTest/UIThang/Inventory/UI"
backup = root / "artifacts/ui-completion/before-light-uss"
backup.mkdir(exist_ok=True)

def rules(source):
    plain = re.sub(r"/\*.*?\*/", lambda m: re.sub(r"[^\n]", " ", m.group()), source, flags=re.S)
    return [(m, [(p.group(1), p.group(2).strip(), m.start(2)+p.start(), m.start(2)+p.end())
                 for p in re.finditer(r"(?m)^[ \t]*([-\w]+)[ \t]*:[ \t]*([^;]+);[ \t]*\n?", m.group(2))])
            for m in re.finditer(r"([^{}]+)\{([^{}]*)\}", plain)]

def final_values(source):
    values = {}
    for rule, props in rules(source):
        selector = rule.group(1).strip()
        for prop, value, *_ in props:
            values[selector, prop] = value
    return values

report = []
for filename in ("PetsPanel.uss", "BoxPanel.uss", "InventoryPanel.uss", "QuestPanel.uss"):
    path = folder / filename
    source = path.read_text(encoding="utf-8-sig")
    (backup / filename).write_text(source, encoding="utf-8")
    latest, removals = {}, []
    removed = 0
    for rule, props in reversed(rules(source)):
        selector = rule.group(1).strip()
        dead = []
        for prop, value, start, end in reversed(props):
            key = selector, prop
            if key in latest and latest[key] == value:
                dead.append((start, end)); removed += 1
            elif key not in latest:
                latest[key] = value
        if props and len(dead) == len(props):
            removals.append((rule.start(1), rule.end()))
        else:
            removals.extend(dead)
    cleaned = source
    for start, end in sorted(removals, reverse=True):
        cleaned = cleaned[:start] + cleaned[end:]
    assert final_values(source) == final_values(cleaned), filename + " final declarations changed"
    if cleaned != source:
        path.write_text(cleaned, encoding="utf-8")
    report.append(f"{filename}: removed {removed} identical superseded declarations; saved {len(source)-len(cleaned)} characters; final selector/property values unchanged")
(root / "artifacts/ui-completion/uss-cleanup.txt").write_text("\n".join(report), encoding="utf-8")
print("\n".join(report))
