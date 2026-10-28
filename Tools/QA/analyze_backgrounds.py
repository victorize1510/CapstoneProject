"""Analyze actual Editor captures; never manufacture a runtime screen from a mockup."""
import csv
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageChops

ROOT = Path('E:/UnityWork/CapstoneProjectData/Artifacts/bag-pet-backgrounds-unity-2026-10-23')
NAMES = ['01-bag-grid', '02-bag-list', '03-pet-overview', '03-pet-skills', '03-pet-growth']
OUT = ROOT / 'comparison'
OUT.mkdir(exist_ok=True)
rows, summary = [], []

for resolution in ['1920x1080', '1366x768']:
    board = Image.new('RGB', (1280, 5*390+40), '#202B45')
    draw = ImageDraw.Draw(board)
    draw.text((12, 12), f'ACTUAL UNITY PLAY MODE | {resolution} | BEFORE (left) / AFTER (right)', fill='white')
    for i, name in enumerate(NAMES):
        stem = f'{name}-{resolution}'
        before = json.loads((ROOT/'before'/f'{stem}.json').read_text(encoding='utf-8-sig'))
        after = json.loads((ROOT/'after'/f'{stem}.json').read_text(encoding='utf-8-sig'))
        old = {e['path']:e for e in before['elements']}
        new = {e['path']:e for e in after['elements']}
        deltas, changed_text, changed_opacity = [], [], []
        for path in old.keys() & new.keys():
            delta = max(abs(old[path][k]-new[path][k]) for k in ['x','y','w','h'])
            deltas.append(delta)
            rows.append([stem,path,delta])
            if old[path].get('text') != new[path].get('text'): changed_text.append(path)
            if abs(old[path]['opacity']-new[path]['opacity']) > .0001: changed_opacity.append(path)
        missing = sorted(old.keys()-new.keys())
        required = ['inventory-shell','bag-browser','bag-inspector','inventory-view-button','inventory-actions'] if name.startswith(('01','02')) else ['companion-dock','pets-party-row','pets-tab-overview','pets-tab-skills','pets-tab-growth','pets-selected-name']
        absent = [id for id in required if not any(p.endswith('/'+id) for p in new)]
        summary.append(dict(screen=stem, matched=len(deltas), max_bounds_delta_px=max(deltas,default=0),
                            missing=missing, changed_text=changed_text, changed_opacity=changed_opacity, missing_required=absent,
                            added=sorted(new.keys()-old.keys())))
        draw.text((12, 40+i*390), name, fill='#CCAD83')
        for col, stage in enumerate(['before','after']):
            image = Image.open(ROOT/stage/f'{stem}.png').convert('RGB')
            assert image.size == (int(resolution.split('x')[0]),int(resolution.split('x')[1]))
            image.thumbnail((632,356), Image.Resampling.LANCZOS)
            board.paste(image, (4+col*640,64+i*390))
        # Full-resolution pair images of the two primary examples at the smaller target size.
        if resolution == '1366x768' and name in ['01-bag-grid','03-pet-overview']:
            pair = Image.new('RGB',(2732,800),'#202B45')
            p = ImageDraw.Draw(pair)
            p.text((12,10),'BEFORE - actual Editor capture',fill='white')
            p.text((1378,10),'AFTER - actual Editor capture',fill='white')
            pair.paste(Image.open(ROOT/'before'/f'{stem}.png').convert('RGB'),(0,32))
            pair.paste(Image.open(ROOT/'after'/f'{stem}.png').convert('RGB'),(1366,32))
            pair.save(OUT/f'{name}-before-after.jpg',quality=90)
            if name == '03-pet-overview':
                # Compare only aligned UI: the world camera can settle differently across two Play sessions.
                import math
                rect=next(e for p,e in new.items() if p.endswith('/green-inspector-union'))
                crop=(math.floor(rect['x']),math.floor(rect['y']),math.ceil(rect['x']+rect['w']),math.ceil(rect['y']+rect['h']))
                panels=[Image.open(ROOT/s/f'{stem}.png').convert('RGB').crop(crop) for s in ['before','after']]
                panel_pair=Image.new('RGB',(panels[0].width*2,panels[0].height+32),'#202B45')
                panel_draw=ImageDraw.Draw(panel_pair)
                for col,img in enumerate(panels):
                    panel_pair.paste(img,(col*img.width,32))
                    panel_draw.text((col*img.width+12,10),['BEFORE - runtime UI','AFTER - runtime UI'][col],fill='white')
                panel_pair.save(OUT/'pet-panel-before-after.jpg',quality=92)
    board.save(OUT/f'all-five-{resolution}.jpg',quality=88)

(OUT/'bounds-results.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
with (OUT/'bounds-deltas.csv').open('w',newline='',encoding='utf-8') as f:
    writer=csv.writer(f);writer.writerow(['screen','element_path','max_bounds_delta_px']);writer.writerows(rows)
print(json.dumps(dict(screens=len(summary),matched_bounds=len(rows),max_bounds_delta_px=max(s['max_bounds_delta_px'] for s in summary),
    missing_total=sum(len(s['missing']) for s in summary),required_missing=sum(len(s['missing_required']) for s in summary),
    changed_opacity=sum(len(s['changed_opacity']) for s in summary),changed_text=sum(len(s['changed_text']) for s in summary)),indent=2))
