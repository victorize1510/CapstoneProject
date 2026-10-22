from pathlib import Path
import sys, re
from collections import Counter
import xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
ui=root/'Capstone/Assets/ThangTest/UIThang/Inventory/UI'
sys.path.insert(0,str(root/'artifacts/ui-style-audit'))
from style_tools import rules
results=[]
def check(condition,label):
    if not condition: raise AssertionError(label)
    results.append('PASS '+label)
for path in ui.glob('*.uxml'):
    tree=ET.parse(path).getroot(); names=[e.get('name') for e in tree.iter() if e.get('name')]
    check(all(v==1 for v in Counter(names).values()),path.name+' unique local names')
    check(not any(e.get('style') for e in tree.iter()),path.name+' no inline presentation')
    for e in tree.iter():
        if e.tag.endswith(('Style','Template')) and e.get('src'): check((ui/e.get('src')).exists(),path.name+' reference '+e.get('src'))
    before=root/'artifacts/ui-completion/before/Capstone/Assets/ThangTest/UIThang/Inventory/UI'/path.name
    if before.exists():
        original={e.get('name') for e in ET.parse(before).getroot().iter() if e.get('name')}
        check(original.issubset(set(names)),path.name+' existing controller names preserved')
for path in ui.glob('*.uss'):
    text=path.read_text(encoding='utf-8-sig'); rules(text)
    check(text.count('{')==text.count('}') and '!important' not in text and '@media' not in text,path.name+' Unity stylesheet structure')
    for asset in re.findall(r'url\("([^"]+)"\)',text):
        check('://' in asset or (path.parent/asset).resolve().exists(),path.name+' asset '+asset)
for path in (root/'Capstone/Assets/Game/CodexSystem').rglob('*'):
    if path.is_file() and path.suffix!='.meta': check(Path(str(path)+'.meta').exists(),path.name+' imported meta')
card=ET.parse(ui/'CodexCard.uxml').getroot()
codex=ET.parse(ui/'CodexPanel.uxml').getroot()
check(sum(e.get('template')=='CodexCard' for e in codex.iter())==25,'Codex 25 reusable card templates')
check(any(e.get('text')=='CẨM NANG PET' for e in codex.iter()),'Codex correct title')
out=root/'artifacts/ui-completion/asset-validation.txt'; out.write_text('\n'.join(results),encoding='utf-8')
print(f'{len(results)} static asset checks passed')
