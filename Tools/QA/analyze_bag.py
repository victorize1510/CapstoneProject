"""Compare actual Game-view bounds/captures to the BAG package; all outputs stay on E."""
from pathlib import Path
import json, csv, statistics
from PIL import Image, ImageChops, ImageEnhance, ImageDraw

ROOT=Path('E:/UnityWork/CapstoneProjectData/Artifacts/bag-implementation-2026-10-22')
DESIGN=Path('E:/AIWork/Capstone/BAG_2026-10-22')
layout=json.loads((DESIGN/'Specs/layout-grid.json').read_text(encoding='utf-8-sig'))
mapping={'inventory-shell':layout['shell'],'category-panel':layout['category'],'bag-browser':layout['browser'],'bag-inspector':layout['inspector'],'item-scroll':layout['items']['viewport'],'detail-panel':layout['detail']['body'],'inventory-actions':layout['detail']['actions']}
for key,rect in layout['toolbar'].items(): mapping[{'search':'inventory-search-field','clear':'inventory-search-field-clear','sort':'inventory-sort-field','filter':'inventory-filter-field','view':'inventory-view-button'}[key]]=rect
rows=[]; visual=[]
for width,height in [(1920,1080),(1366,768)]:
    bounds=json.loads((ROOT/f'after/01-grid-{width}x{height}.json').read_text())
    actual={e['id']:e for e in bounds['controls']}
    s=min(width/1600,height/900); ox=(width-1600*s)/2;oy=(height-900*s)/2
    for name,rect in mapping.items():
        if name not in actual: continue
        target=[ox+rect[0]*s,oy+rect[1]*s,rect[2]*s,rect[3]*s]
        raster=[rect[0]*width/1600,rect[1]*height/900,rect[2]*width/1600,rect[3]*height/900]
        box=[actual[name][k] for k in ['x','y','w','h']]
        rows.append([width,height,name,*box,*target,max(abs(a-b) for a,b in zip(box,target)),max(abs(a-b) for a,b in zip(box,raster))])
    for mode,mock in [('grid','01_BAG_Grid'),('list','02_BAG_List')]:
        current=Image.open(ROOT/f'after/0{1 if mode=="grid" else 2}-{mode}-{width}x{height}.png').convert('RGB')
        reference=Image.open(DESIGN/f'Screens/{mock}.png').convert('RGB').resize((width,height),Image.Resampling.LANCZOS)
        crop=(int(24*s+ox),int(112*s+oy),int(1576*s+ox),int(860*s+oy))
        a,b=current.crop(crop),reference.crop(crop)
        difference=ImageChops.difference(a,b)
        hist=difference.histogram(); mae=sum((i%256)*n for i,n in enumerate(hist))/(a.width*a.height*3)
        visual.append({'resolution':f'{width}x{height}','mode':mode,'ui_rgb_mae_0_255':round(mae,3),'note':'Includes different real item names/counts/bindings/font and material; not a pass threshold.'})
        # Keep only 1366 overlays/diffs; full final captures already cover both resolutions.
        if width==1366:
            Image.blend(a,b,.5).save(ROOT/f'overlay-{mode}.jpg',quality=90)
            ImageEnhance.Contrast(difference).enhance(3).save(ROOT/f'diff-{mode}.jpg',quality=90)
with (ROOT/'geometry.csv').open('w',newline='',encoding='utf-8') as f:
    writer=csv.writer(f);writer.writerow(['width','height','control','actual_x','actual_y','actual_w','actual_h','expected_fit_x','expected_fit_y','expected_fit_w','expected_fit_h','max_fit_delta_px','max_nonuniform_mockup_delta_px']);writer.writerows(rows)
(ROOT/'visual-diff.json').write_text(json.dumps(visual,ensure_ascii=False,indent=2),encoding='utf-8')
state=ROOT/'after/state-bounds.csv'
if state.exists():
    groups={}
    for r in csv.DictReader(state.open()):groups.setdefault((r['control'],r['part']),[]).append([float(r[k]) for k in ['x','y','w','h']])
    deltas=[max(max(v[i] for v in g)-min(v[i] for v in g) for i in range(4)) for g in groups.values()]
    (ROOT/'bounds-summary.json').write_text(json.dumps({'automated_state_groups':len(groups),'max_state_geometry_delta_px':max(deltas,default=0),'max_layout_fit_delta_px':max((r[-2] for r in rows),default=0),'max_mockup_nonuniform_delta_px':max((r[-1] for r in rows),default=0)},indent=2))
profile=ROOT/'after/editor-profiler.csv'
if profile.exists():
    samples=list(csv.DictReader(profile.open())); out={'environment':'Unity Editor Play Mode, not standalone','samples':len(samples),'before_baseline':'not recorded; no performance improvement claim'}
    for key in ['cpu_ns','gc_bytes']:
        vals=sorted(int(r[key]) for r in samples);out[key]={'median':statistics.median(vals),'p95':vals[int(.95*(len(vals)-1))],'max':max(vals)}
    (ROOT/'profiler-summary.json').write_text(json.dumps(out,indent=2))
native=ROOT/'native/native-bounds.csv'
if native.exists():
    groups={};states=set();occurrences={}
    for r in csv.DictReader(native.open()):
        key=(r['sample'],r['button'],r['part']);index=occurrences.get(key,0);occurrences[key]=index+1
        states.add(r['state']);groups.setdefault((r['button'],r['part'],index),[]).append([float(r[k]) for k in ['x','y','w','h']])
    deltas=[max(max(v[i] for v in g)-min(v[i] for v in g) for i in range(4)) for g in groups.values()]
    (ROOT/'native-bounds-summary.json').write_text(json.dumps({'observed_states_in_retained_tail':sorted(states),'groups':len(groups),'max_geometry_delta_panel_units':max(deltas,default=0),'scope':'Bounded observer tail from second Grid pass. Unnamed labels distinguished by occurrence; this is not full native hover/press/focus coverage.'},indent=2))
# Derived comparison board from actual captures, not a runtime replacement.
board=Image.new('RGB',(1366,420),(32,43,69));draw=ImageDraw.Draw(board)
for x,folder,title in [(0,'before','BEFORE - actual Unity capture'),(683,'after','AFTER - actual Unity capture')]:
    photo=Image.open(ROOT/f'{folder}/01-grid-1366x768.png').convert('RGB').resize((683,384),Image.Resampling.LANCZOS)
    board.paste(photo,(x,36));draw.text((x+16,12),title,fill=(237,233,225))
board.save(ROOT/'before-after.jpg',quality=93)
print(json.dumps({'geometry_controls':len(rows),'fit_max_px':max(r[-2] for r in rows),'mockup_max_px':max(r[-1] for r in rows),'visual':visual},ensure_ascii=False))
