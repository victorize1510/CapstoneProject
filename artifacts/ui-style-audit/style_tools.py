"""Validate UI assets; retain the initial migration recipe for audit history.

The migration recipe describes the first transformation only. Later reviewed
theme, focus and visibility changes are maintained in the Unity source files.
"""
import re
from pathlib import Path
from collections import Counter
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / 'Capstone/Assets/ThangTest/UIThang/Inventory/UI'
BASE = Path(__file__).parent / 'baseline'

def rules(text):
    text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
    result = []
    for match in re.finditer(r'([^{}]+)\{([^{}]*)\}', text):
        selectors = [re.sub(r'\s+', ' ', s.strip()) for s in match[1].split(',')]
        declarations = []
        for item in match[2].split(';'):
            if item.strip():
                prop, value = item.split(':', 1)
                declarations.append((prop.strip(),value.strip()))
        result.append((selectors,declarations))
    return result

def render(items):
    def compact(declarations):
        values=dict(declarations); replacements={}; skip=set()
        for prop in ('margin','padding','border-width','border-color','border-radius'):
            keys=[p for p,_ in expand(prop,'1px')]
            if all(key in values for key in keys) and len({values[key] for key in keys})==1:
                first=next(key for key,_ in declarations if key in keys)
                replacements[first]=(prop,values[first]); skip.update(set(keys)-{first})
        return [replacements.get(p,(p,v)) for p,v in declarations if p not in skip]
    return '\n\n'.join(',\n'.join(selectors)+' {\n'+''.join('    '+p+': '+v+';\n' for p,v in compact(declarations))+'}' for selectors,declarations in items)+'\n'

def expand(prop,value):
    if prop in ('margin','padding','border-width','border-color','border-radius'):
        values=re.findall(r'(?:[^\s(]+\([^)]*\)|[^\s]+)',value)
        if not 1<=len(values)<=4 or 'var(' in value:
            return [(prop,value)]
        a=values[0]; b=values[1] if len(values)>1 else a
        c=values[2] if len(values)>2 else a; d=values[3] if len(values)>3 else b
        if prop=='border-radius':
            keys=['border-top-left-radius','border-top-right-radius','border-bottom-right-radius','border-bottom-left-radius']
        elif prop.startswith('border-'):
            keys=['border-'+edge+'-'+prop[7:] for edge in ('top','right','bottom','left')]
        else:
            keys=[prop+'-'+edge for edge in ('top','right','bottom','left')]
        return list(zip(keys,(a,b,c,d)))
    return [(prop,value)]

def prune(items):
    # Only remove declarations overwritten by the SAME selector. Never reorder
    # unrelated selectors: equal-specificity cascade still depends on source order.
    expanded=[]
    for selectors,declarations in items:
        for selector in selectors:
            expanded.append(([selector],[entry for prop,value in declarations for entry in expand(prop,value)]))
    seen=set(); result=[]; removed=0
    for selectors,declarations in reversed(expanded):
        kept=[]
        for prop,value in reversed(declarations):
            key=(selectors[0],prop)
            if key in seen:
                removed+=1
            else:
                seen.add(key); kept.append((prop,value))
        if kept: result.append((selectors,list(reversed(kept))))
    result.reverse()
    # Rejoin adjacent rules with identical declarations; specificity/order unchanged.
    joined=[]
    for selectors,declarations in result:
        if joined and joined[-1][1]==declarations:
            joined[-1][0].extend(selectors)
        else: joined.append((selectors,declarations))
    return joined,removed

def owner(selector):
    if selector.startswith('.inventory-root.keyboard-navigation .item-row'): return 'InventoryPanel'
    if selector.startswith(('.inventory-root','.quest-inventory-shell','.menu-backdrop','.bottom-hint','.bottom-keycap','.hint-divider','.hint-text','.bag-evolution')):
        return 'MonsterInventory'
    if selector.startswith('.box-'): return 'BoxPanel'
    if selector.startswith(('.pets-','.pet-party-','.pet-card-','.pet-skill-','.pet-empty-')): return 'PetsPanel'
    if selector.startswith(('.quest-','.objective-','.reward-','.tracked-quest-','.hidden-state-','.side-keycap','.sort-label','.small-square-button')): return 'QuestPanel'
    if selector.startswith(('.primary-quest-','.danger-quest-')): return 'QuestPanel'
    if selector.startswith(('.inventory-','.category-','.capacity-','.list-panel','.item-','.no-items-','.detail-','.effect-')): return 'InventoryPanel'
    if selector.startswith('.menu-placeholder-'): return 'MenuPlaceholderPanel'
    if selector.startswith('.quantity-'): return 'InventoryQuantityPopup'
    return 'MenuCommon'

def migrate():
    modules={name:[] for name in ('MonsterInventory','MenuCommon','BoxPanel','PetsPanel','QuestPanel','InventoryPanel','MenuPlaceholderPanel','InventoryQuantityPopup')}
    for selectors,declarations in rules((BASE/'MonsterInventory.uss').read_text(encoding='utf-8-sig')):
        groups={}
        for selector in selectors: groups.setdefault(owner(selector),[]).append(selector)
        for name,group in groups.items(): modules[name].append((group,declarations))
    summaries=[]
    for path in sorted(BASE.glob('*.uss')):
        name=path.stem
        if name=='MonsterInventory': continue
        modules[name]=modules.get(name,[])+rules(path.read_text(encoding='utf-8-sig'))
    # Header geometry is shared; screen/body geometry belongs to each screen.
    headers=[]
    for selectors,declarations in modules['MenuScreenHeader']:
        if selectors==['.pets-panel'] or selectors==['.pets-panel .screen-header']: modules['PetsPanel'].append((selectors,declarations))
        elif selectors==['.box-panel'] or selectors==['.box-panel .screen-header .box-subtitle']: modules['BoxPanel'].append((selectors,declarations))
        elif selectors==['.quest-panel']: modules['QuestPanel'].append((selectors,declarations))
        elif selectors==['.inventory-panel .inventory-shell']: modules['InventoryPanel'].append((selectors,declarations))
        else: headers.append((selectors,declarations))
    modules['MenuScreenHeader']=headers
    for name,items in modules.items():
        cleaned,removed=prune(items)
        summaries.append(f'{name}: {len(items)} input rules -> {len(cleaned)} output rules; {removed} overwritten declarations removed')
        intro={
            'MonsterInventory':'Composed document only: screen routing, template stacking and Bag evolution confirmation.',
            'MenuCommon':'Shared primitives only. Screen-specific selectors belong to their panel stylesheet.',
            'MenuScreenHeader':'Shared full-screen header. Keep title, back button and viewport geometry consistent.',
            'InventoryQuantityPopup':'Bag quantity dialog, attached to the composed root because the popup is created there.',
        }.get(name,name+' owns its layout, typography and interaction states. Runtime controllers own data and visibility.')
        (UI/(name+'.uss')).write_text('/* '+intro+' */\n\n'+render(cleaned),encoding='utf-8')
    ns={'ui':'UnityEngine.UIElements'}
    for source in BASE.glob('*.uxml'):
        tree=ET.parse(source); root=tree.getroot()
        style='{UnityEngine.UIElements}Style'
        for element in list(root):
            if element.tag==style and element.get('src')=='MonsterInventory.uss' and source.stem!='MonsterInventory': element.set('src','MenuCommon.uss')
        if source.stem=='MonsterInventory':
            root.insert(0,ET.Element(style,src='MenuCommon.uss'))
            root.insert(2,ET.Element(style,src='InventoryQuantityPopup.uss'))
        elif not any(e.tag==style and e.get('src')=='MenuCommon.uss' for e in root):
            root.insert(0,ET.Element(style,src='MenuCommon.uss'))
        if source.stem=='InventoryPanel':
            root.insert(2,ET.Element(style,src='InventoryQuantityPopup.uss'))
        if source.stem=='MonsterItemRow': root.append(ET.Element(style,src='InventoryPanel.uss'))
        # Move presentation defaults out of markup; C# visibility overrides remain supported.
        for element in root.iter():
            if element.get('style')=='display: none;':
                element.attrib.pop('style')
                element.set('class',element.get('class','')+' inventory-initially-hidden')
        ET.register_namespace('ui','UnityEngine.UIElements'); ET.indent(tree,space='    ')
        tree.write(UI/source.name,encoding='unicode',xml_declaration=False)
        with (UI/source.name).open('a',encoding='utf-8') as stream: stream.write('\n')
    (Path(__file__).parent/'migration.txt').write_text('\n'.join(summaries),encoding='utf-8')

def validate():
    issues=[]
    for path in UI.glob('*.uxml'):
        root=ET.parse(path).getroot(); names=[]
        for element in root.iter():
            if element.get('name'): names.append(element.get('name'))
            if element.get('style'): issues.append(path.name+': inline style remains')
            if element.tag.endswith(('Style','Template')) and element.get('src'):
                if not (UI/element.get('src')).exists(): issues.append(path.name+': missing '+element.get('src'))
        duplicates=[k for k,v in Counter(names).items() if v>1]
        if duplicates: issues.append(path.name+': duplicate names '+str(duplicates))
        if (BASE/path.name).exists():
            old=[e.get('name') for e in ET.parse(BASE/path.name).getroot().iter() if e.get('name')]
            # New controls and layout reordering are expected; existing bindings must survive.
            missing=set(old)-set(names)
            if missing: issues.append(path.name+': controller query names removed '+str(sorted(missing)))
    for path in UI.glob('*.uss'):
        text=path.read_text(encoding='utf-8-sig'); rules(text)
        if '!important' in text or '@media' in text: issues.append(path.name+': unsupported web CSS')
        if text.count('{') != text.count('}'): issues.append(path.name+': unbalanced rule braces')
        for asset in re.findall(r'url\("([^"]+)"\)', text):
            if '://' not in asset and not (path.parent/asset).resolve().exists():
                issues.append(path.name+': missing asset '+asset)
    if issues: raise AssertionError('\n'.join(issues))
    print('PASS XML, stylesheet/template/asset references, unique local query names, binding-name preservation, no inline presentation or unsupported CSS directives')

if __name__ == '__main__':
    import sys
    if '--migrate' in sys.argv:
        raise SystemExit('Historical migration only: do not overwrite reviewed source assets. Use --validate.')
    if '--validate' in sys.argv: validate(); raise SystemExit()
    for path in sorted(UI.glob('*.uss')):
        items=rules(path.read_text(encoding='utf-8-sig'))
        counts=Counter(s for selectors,_ in items for s in selectors)
        print(path.name, 'rules=',len(items),'selectors repeated=',sum(v-1 for v in counts.values()))
    for selectors,declarations in rules((UI/'MonsterInventory.uss').read_text(encoding='utf-8-sig')):
        print(', '.join(selectors))
