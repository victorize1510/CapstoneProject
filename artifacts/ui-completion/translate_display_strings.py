"""Localize Vietnamese UI copy only. Preserve C# interpolation expressions and identifiers."""
from pathlib import Path
import re
project = Path(__file__).resolve().parents[2]
assets = project / 'Capstone/Assets'
ui = assets / 'ThangTest/UIThang/Inventory/UI'
terms = {'pet': 'thú', 'slot': 'ô', 'skill': 'kỹ năng', 'item': 'vật phẩm', 'bag': 'Túi đồ', 'box': 'Kho thú', 'pets': 'Thú đồng hành', 'double-click': 'nhấp đúp', 'Cooldown': 'Hồi chiêu'}
exact = {'Item Name':'Tên vật phẩm', 'HP (PREVIEW)':'HP TRƯỚC / SAU', 'CD -':'Hồi chiêu: —', **{f'Skill {i}': f'Kỹ năng {i}' for i in range(1,5)}}
def translate(text):
    if text in exact: return exact[text]
    if not any(c.isalpha() and ord(c)>127 for c in text): return text
    for term, value in terms.items():
        if term == 'pet' and 'cẩm nang' in text.lower(): continue
        text = re.sub(r'\b'+re.escape(term)+r'\b', value, text, flags=re.I)
    if text.startswith('thú '): text = ('THÚ ' if text[4:].isupper() else 'Thú ') + text[4:]
    return text

def csharp(text):
    # The scanner copies interpolation expressions verbatim, including nested quotes.
    out=[]; pos=0
    while pos<len(text):
        if text.startswith('//',pos):
            end=text.find('\n',pos); end=len(text) if end<0 else end
            out.append(text[pos:end]); pos=end; continue
        if text.startswith('/*',pos):
            end=text.find('*/',pos+2); end=len(text) if end<0 else end+2
            out.append(text[pos:end]); pos=end; continue
        if text[pos]!='"': out.append(text[pos]); pos+=1; continue
        interpolated=pos>0 and text[pos-1]=='$'
        # Verbatim strings in this project are paths; leave unchanged.
        verbatim=pos>0 and text[pos-1]=='@'
        out.append('"'); pos+=1; start=pos
        while pos<len(text):
            if text[pos]=='\\' and not verbatim: pos+=2; continue
            if verbatim and text.startswith('""',pos): pos+=2; continue
            if text[pos]=='"': out.append(translate(text[start:pos])); out.append('"'); pos+=1; break
            if interpolated and text[pos]=='{' and not text.startswith('{{',pos):
                out.append(translate(text[start:pos])); begin=pos; depth=1; pos+=1
                quoted=False; escaped=False
                while pos<len(text) and depth:
                    c=text[pos]
                    if escaped: escaped=False
                    elif quoted and c=='\\': escaped=True
                    elif c=='"': quoted=not quoted
                    elif not quoted and c=='{': depth+=1
                    elif not quoted and c=='}': depth-=1
                    pos+=1
                out.append(text[begin:pos]); start=pos; continue
            if interpolated and text.startswith('{{',pos): pos+=2; continue
            pos+=1
    return ''.join(out)

paths=list((assets/'Game/HudSystem').glob('*PanelController.cs'))+list((assets/'Game/HudSystem').glob('*Service.cs'))
paths += [assets/'Game/UISystem/GameMenuController.cs', assets/'Game/QuestSystem/UI/QuestPanelController.cs', ui/'MonsterInventoryController.cs', ui/'ItemQuantityPopup.cs']
for path in paths:
    old=path.read_text(encoding='utf-8-sig'); new=csharp(old)
    if path.name == 'PetSkillsPanelController.cs':
        new=new.replace('SafeName(skill.displayName)', 'SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(skill.displayName))')
        new=new.replace('SafeName(learnedSnapshot[selectedLearnedIndex].displayName)', 'SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(learnedSnapshot[selectedLearnedIndex].displayName))')
        new=new.replace('SafeName(learnedSnapshot[learnedSkillIndex].displayName)', 'SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(learnedSnapshot[learnedSkillIndex].displayName))')
        new=new.replace('"EMPTY"', '"Trống"').replace('"Skill"', '"Kỹ năng"')
        new=new.replace('$"CD {skill.cooldownSeconds:0.#}s"', '$"Hồi chiêu: {skill.cooldownSeconds:0.#} giây"').replace('"CD -"','"Hồi chiêu: —"')
    if path.name == 'PetPartyPanelController.cs':
        new=new.replace('DisplayOrDash(metadata.Gender)', 'Capstone.Game.UISystem.VietnameseUI.Gender(metadata.Gender)')
        new=new.replace('$"Cooldown: {skill.cooldownSeconds:0.#}s"', '$"Hồi chiêu: {skill.cooldownSeconds:0.#} giây"').replace('"Cooldown: -"','"Hồi chiêu: —"')
    if new!=old:
        backup=project/'artifacts/ui-completion/before-translation-finish'/path.relative_to(assets); backup.parent.mkdir(parents=True,exist_ok=True)
        if not backup.exists(): backup.write_text(old,encoding='utf-8')
        path.write_text(new,encoding='utf-8')
for path in ui.glob('*.uxml'):
    old=path.read_text(encoding='utf-8-sig')
    new=re.sub(r'(text|tooltip)="([^"]*)"', lambda m:m[1]+'="'+translate(m[2])+'"',old)
    new=re.sub(r'(<ui:Label[^>]*text="HP"[^>]*)(/>)',r'\1 tooltip="Sinh lực hiện tại và tối đa." \2',new)
    new=re.sub(r'(<ui:Label[^>]*text="EXP"[^>]*)(/>)',r'\1 tooltip="Kinh nghiệm để đạt cấp tiếp theo." \2',new)
    if new!=old: path.write_text(new,encoding='utf-8')
print('Translated UI text without altering interpolation expressions or control names.')
