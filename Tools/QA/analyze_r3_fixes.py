from pathlib import Path
from collections import defaultdict, Counter
import csv, json, math, statistics, hashlib, difflib
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path('E:/UnityWork/CapstoneProjectData/Artifacts/ui-r3-fixes-2026-10-20')
PROJECT = Path('D:/Download/ProjectVTC/CapstoneProject/Capstone')
DESIGN = Path('D:/Download/ProjectVTC/Asset Capstone/ẢnhGUi/ảnh ui thô sơ/UI_Menu_Companion_Interaction_R3_2026-10-20')
OUT = ROOT / 'comparison'
OUT.mkdir(exist_ok=True)

def dump(name, data):
    (OUT/name).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')

layout = json.loads((DESIGN/'Specs/layout-grid.json').read_text(encoding='utf-8'))
rows, missing, pixel_rows = [], [], []
for jp in sorted((ROOT/'geometry/captures').glob('0*.json')):
    d = json.loads(jp.read_text(encoding='utf-8'))
    screen = jp.stem.rsplit('-',1)[0]
    if screen not in layout['screens']: continue
    w,h = d['width'], d['height']
    s=min(w/1600,h/900); ox=(w-1600*s)/2; oy=(h-900*s)/2
    expected={b['id']:dict(b) for b in layout['screens'][screen]}
    panel=expected.get('inspector-fixed')
    if panel:
        n=panel['navigation']
        expected['navigation-rail']=dict(x=panel['x']+n['x'],y=panel['y']+n['y'],width=n['width'],height=n['height'])
        for i,name in enumerate(('overview','skills','growth')):
            expected['nav-'+name]=dict(x=panel['x']+n['x']+i*(n['itemWidth']+n['gap']),y=panel['y']+n['y'],width=n['itemWidth'],height=n['height'])
    actual={b['id']:b for b in d['boxes'] if b['width']>0 and b['height']>0}
    runtime=Image.open(jp.with_suffix('.png')).convert('RGB')
    reference=Image.open(DESIGN/'Screens'/f'{screen}.png').convert('RGB').resize((round(1600*s),round(900*s)),Image.Resampling.LANCZOS)
    ref=Image.new('RGB',(w,h)); ref.paste(reference,(round(ox),round(oy)))
    overlay=Image.blend(runtime,ref,.5); draw=ImageDraw.Draw(overlay)
    mask=Image.new('1',(w,h)); md=ImageDraw.Draw(mask)
    for name,e in expected.items():
        rect=[ox+e['x']*s,oy+e['y']*s,ox+(e['x']+e['width'])*s,oy+(e['y']+e['height'])*s]
        md.rectangle(rect,fill=1); draw.rectangle(rect,outline='#ffbf40',width=2)
        a=actual.get(name)
        if a is None: missing.append(dict(screen=screen,size=f'{w}x{h}',id=name)); continue
        ar=[a['x'],a['y'],a['x']+a['width'],a['y']+a['height']]
        rows.append(dict(screen=screen,size=f'{w}x{h}',id=name,max_edge_error_px=round(max(abs(x-y) for x,y in zip(rect,ar)),4)))
        draw.rectangle(ar,outline='#00e5dd',width=1)
    draw.text((8,8),'R3 amber / runtime cyan; 50% overlay. Real game data and background differ.',fill='white',stroke_width=1,stroke_fill='black')
    overlay.save(OUT/f'{jp.stem}-overlay.png')
    delta=np.abs(np.asarray(runtime,dtype=np.float32)-np.asarray(ref,dtype=np.float32)); m=np.asarray(mask,dtype=bool)
    pixel_rows.append(dict(screen=screen,size=f'{w}x{h}',ui_rgb_mae_0_255=round(float(delta[m].mean()),3)))
    Image.fromarray(np.uint8(np.minimum(delta*3,255))).save(OUT/f'{jp.stem}-diff-x3.png')
for filename,data in [('geometry.csv',rows),('unmeasured.csv',missing),('pixels.csv',pixel_rows)]:
    if data:
        with (OUT/filename).open('w',encoding='utf-8',newline='') as f:
            writer=csv.DictWriter(f,fieldnames=list(data[0])); writer.writeheader(); writer.writerows(data)
summary={size:dict(measured=len(v:=[x for x in rows if x['size']==size]),max_edge_error_px=max(x['max_edge_error_px'] for x in v),worst=sorted(v,key=lambda x:x['max_edge_error_px'],reverse=True)[:5]) for size in sorted({x['size'] for x in rows})}
dump('geometry-summary.json',summary)

motion=defaultdict(list)
with (ROOT/'editor/native-motion.csv').open(encoding='utf-8-sig') as f:
    for row in csv.DictReader(f):
        try:
            values={k:float(row[k]) for k in ('x','y','w','h','marginLeft')}
            if values['w']>0 and values['h']>0: motion[(row['id'],row['part'])].append(values)
        except (ValueError, TypeError): pass
dump('native-icon-motion.json',{f'{k[0]}/{k[1]}':dict(samples=len(v),range_px={p:round(max(x[p] for x in v)-min(x[p] for x in v),5) for p in ('x','y','w','h','marginLeft')}) for k,v in motion.items()})
stable={}
for k,v in motion.items():
    size=Counter((round(x['w'],3),round(x['h'],3)) for x in v).most_common(1)[0][0]
    settled=[x for x in v if (round(x['w'],3),round(x['h'],3))==size]
    stable[f'{k[0]}/{k[1]}']=dict(samples=len(settled),scope='Stable resolution/layout samples; not a complete physical hover coverage claim',range_px={p:round(max(x[p] for x in settled)-min(x[p] for x in settled),5) for p in ('x','y','w','h','marginLeft')})
dump('settled-icon-motion.json',stable)
labels=defaultdict(list)
with (ROOT/'transaction-run/after/mouse/text-metrics.csv').open(encoding='utf-8-sig') as f:
    for row in csv.DictReader(f): labels[row['nav']].append(row)
dump('native-navigation-text.json',{k:dict(samples=len(v),fonts=sorted({x['font'] for x in v}),sizes=sorted({x['font_size'] for x in v}),weights=sorted({x['weight'] for x in v}),range_px={p:round(max(float(x[p]) for x in v)-min(float(x[p]) for x in v),5) for p in ('x','y','width','height')}) for k,v in labels.items()})

profiles={}
for folder in ('editor-final','after-standalone'):
    for path in (ROOT/folder).glob('profile-*.csv'):
        groups=defaultdict(list)
        with path.open(encoding='utf-8-sig') as f:
            for row in csv.DictReader(f): groups[row['phase']].append(row)
        data={}
        for phase, samples in groups.items():
            record=dict(samples=len(samples))
            for key in samples[0]:
                if key in ('phase','frame'): continue
                values=sorted(float(x[key]) for x in samples if x.get(key) is not None)
                if values: record[key]=dict(median=statistics.median(values),p95=values[math.ceil(len(values)*.95)-1])
            data[phase]=record
        profiles[f'{folder}/{path.name}']=data
dump('profiler-summary.json',profiles)

before=json.loads((ROOT/'before-state.json').read_text(encoding='utf-8'))
files={'live_save':Path('C:/Users/THẮNG/AppData/LocalLow/DefaultCompany/Capstone/Saves/player_main.json'),'scene':PROJECT/'Assets/Scenes/TestChar.unity'}
integrity={k:dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),unchanged=hashlib.sha256(p.read_bytes()).hexdigest()==before[k]['sha256']) for k,p in files.items()}
dump('integrity.json',integrity)

changes=[]
with (OUT/'fixes-only.diff').open('w',encoding='utf-8') as out:
    for p in sorted((ROOT/'before-sources').rglob('*')):
        if not p.is_file(): continue
        relative=p.relative_to(ROOT/'before-sources'); actual=PROJECT/'Assets'/relative
        if not actual.exists(): continue
        old=p.read_text(encoding='utf-8-sig'); new=actual.read_text(encoding='utf-8-sig')
        if old==new: continue
        changes.append(str(actual))
        out.writelines(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='before/'+str(relative),tofile='after/'+str(relative)))
dump('changed-production-files.json',changes)
print(json.dumps({'geometry':summary,'integrity':integrity,'production_files':len(changes)},ensure_ascii=False,indent=2))
