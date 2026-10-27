"""Summarize real Unity evidence; output stays on E. No gameplay/source mutation."""
from pathlib import Path
from collections import defaultdict
import csv, json, math, statistics
from PIL import Image, ImageDraw

ROOT=Path('E:/UnityWork/CapstoneProjectData/Artifacts/ui-polish-2026-10-22')
OUT=ROOT/'comparison';OUT.mkdir(exist_ok=True)
def shot(path):
    return Image.open(path if path.exists() else path.with_suffix('.jpg')).convert('RGB')
geometry=[];profiles=[];motion={}
for size,height in ((1920,1080),(1366,768)):
    name=f'01-menu-{size}x{height}.json'
    before={b['id']:b for b in json.loads((ROOT/'before'/name).read_text(encoding='utf-8-sig'))['boxes']}
    after={b['id']:b for b in json.loads((ROOT/'after'/name).read_text(encoding='utf-8-sig'))['boxes']}
    for key in ('PetsButton','InventoryButton','ProfileButton','QuestButton','MapButton','CodexButton'):
        geometry.append(dict(resolution=f'{size}x{height}',id=key,max_edge_delta_px=max(abs(after[key][p]-before[key][p]) for p in ('x','y','width','height'))))
    for stem,keys in [('01-menu',('PetsButton','InventoryButton','ProfileButton','QuestButton','MapButton','CodexButton')),('02-tab',('companion-inspector','companion-actions','pets-details-button'))]:
        b=json.loads((ROOT/'before'/f'{stem}-{size}x{height}.json').read_text(encoding='utf-8-sig'))['boxes'];a=json.loads((ROOT/'after'/f'{stem}-{size}x{height}.json').read_text(encoding='utf-8-sig'))['boxes']
        original=shot(ROOT/'before'/f'{stem}-{size}x{height}.png');current=shot(ROOT/'after'/f'{stem}-{size}x{height}.png')
        overlay=Image.blend(original,current,.5);draw=ImageDraw.Draw(overlay)
        for data,color in ((b,'#ffbf40'),(a,'#00e5dd')):
            for box in data:
                if box['id'] in keys:draw.rectangle((box['x'],box['y'],box['x']+box['width'],box['y']+box['height']),outline=color,width=2)
        draw.text((12,12),'BEFORE amber / AFTER cyan; geometry comparison, real data may differ',fill='white',stroke_width=1,stroke_fill='black')
        overlay.save(OUT/f'{stem}-{size}-overlay.jpg',quality=88)
    for stage in ('before','after'):
        sheet=Image.new('RGB',(1280,4*384),(29,37,53));d=ImageDraw.Draw(sheet)
        for i,path in enumerate(sorted((ROOT/stage).glob(f'0*-{size}x{height}.png')) or sorted((ROOT/stage).glob(f'0*-{size}x{height}.jpg'))):
            im=Image.open(path).convert('RGB');im.thumbnail((640,360));x=(i%2)*640;y=(i//2)*384;sheet.paste(im,(x,y+24));d.text((x+8,y+6),path.stem,fill='white')
        sheet.save(OUT/f'{stage}-{size}-gallery.jpg',quality=90)
    for phase in ('closed','menu','companion'):
        for stage in ('before','after'):
            rows=list(csv.DictReader((ROOT/stage/f'profile-{size}-{phase}.csv').open()))
            cpu=sorted(int(r['cpu_ns'])/1e6 for r in rows if int(r['cpu_ns'])>=0);gc=[int(r['gc_bytes']) for r in rows if int(r['gc_bytes'])>=0]
            profiles.append(dict(stage=stage,resolution=size,phase=phase,samples=len(rows),cpu_median_ms=statistics.median(cpu),cpu_p95_ms=cpu[math.ceil(len(cpu)*.95)-1],gc_median_bytes=statistics.median(gc)))
for stage in ('native1366','native1920'):
    path=ROOT/stage/'native-bounds.csv'
    if not path.exists():continue
    groups=defaultdict(list)
    for row in csv.DictReader(path.open()):
        if row['id'] in ('pets-tab-overview','pets-tab-skills','pets-tab-growth','pets-rename-button','pets-favorite-button') and float(row['width'])>0:
            groups[(row['id'],row['part'])].append(row)
    motion[stage]={f'{key[0]}/{key[1]}':dict(samples=len(rows),range={p:max(float(x[p]) for x in rows)-min(float(x[p]) for x in rows) for p in ('x','y','width','height')}) for key,rows in groups.items()}
for name,rows in [('geometry',geometry),('profiler',profiles)]:
    with (OUT/f'{name}.csv').open('w',newline='',encoding='utf-8') as f:
        w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
(OUT/'native-motion.json').write_text(json.dumps(motion,ensure_ascii=False,indent=2),encoding='utf-8')
print('Updated geometry/profiler CSV, native-motion JSON and 8 comparison images on E.')
