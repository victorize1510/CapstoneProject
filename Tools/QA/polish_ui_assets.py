"""Build independent UI art, without mockup screenshots or baked text. Requires Pillow."""
from pathlib import Path
import math
from PIL import Image, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parents[2] / 'Capstone/Assets/Game/UISystem'
SOURCE = ROOT / 'ArtSource/Polish20261022'
OUTPUT = ROOT / 'Resources/UI/Polish20261022'
SOURCE.mkdir(parents=True, exist_ok=True)
OUTPUT.mkdir(parents=True, exist_ok=True)
for name in ('profile', 'quest', 'map', 'book'):
    original = ROOT / f'ArtSource/Latest20261020/Assets/Art/scene-{name}-r2.png'
    ImageOps.fit(Image.open(original).convert('RGBA'), (760, 240), Image.Resampling.LANCZOS).save(OUTPUT / f'scene-{name}.png')

points = []
for tooth in range(8):
    for offset, radius in ((-.5, 23), (-.28, 23), (-.20, 30), (.20, 30), (.28, 23)):
        angle = (tooth + offset) * math.tau / 8
        points.append((32 + radius * math.cos(angle), 32 + radius * math.sin(angle)))
coords = ' '.join(f'{x:.4f},{y:.4f}' for x, y in points)
(SOURCE / 'settings.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="128" height="128"><defs><mask id="hole"><rect width="64" height="64" fill="white"/><circle cx="32" cy="32" r="10" fill="black"/></mask></defs><polygon points="{coords}" fill="white" mask="url(#hole)"/></svg>\n', encoding='utf-8')
gear = Image.new('RGBA', (512, 512)); draw = ImageDraw.Draw(gear)
draw.polygon([(x*8, y*8) for x, y in points], fill='white')
draw.ellipse((176, 176, 336, 336), fill=(0,0,0,0))
gear.resize((128,128), Image.Resampling.LANCZOS).save(OUTPUT / 'settings.png')

# One contour, transparent full-bleed art window, material caption and discreet bottom/right rails.
polygon = [(15,3),(365,3),(377,15),(377,217),(365,229),(15,229),(3,217),(3,15)]
path = 'M' + 'L'.join(f'{x},{y}' for x,y in polygon) + 'Z'
(SOURCE / 'major-front.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 380 232"><defs><linearGradient id="caption" x1="0" y1="0" x2="0" y2="1"><stop offset=".68" stop-color="#AEB8CD" stop-opacity="0"/><stop offset=".79" stop-color="#AEB8CD" stop-opacity=".96"/><stop offset="1" stop-color="#9FAFC7"/></linearGradient></defs><path d="{path}" fill="url(#caption)" stroke="#8996B0" stroke-width="1.5"/><path d="M373,28V215L363,225H20" fill="none" stroke="#CCAD83" stroke-width="1" opacity=".55"/></svg>\n', encoding='utf-8')
front = Image.new('RGBA', (760,464)); mask=Image.new('L',front.size);d=ImageDraw.Draw(mask);d.polygon([(x*2,y*2) for x,y in polygon],fill=255)
grain=Image.open(ROOT/'Resources/UI/Latest20261020/Assets/Art/steel-enamel-r2.png').convert('RGB').resize(front.size,Image.Resampling.LANCZOS)
px=front.load(); noise=grain.load()
for y in range(464):
    t=max(0,min(1,(y/2-154)/29)); t=t*t*(3-2*t)
    for x in range(760):
        n=(sum(noise[x,y])/3-175)*.07
        px[x,y]=(round(174+n),round(184+n),round(205+n),round(t*248))
front.putalpha(Image.frombytes('L',front.size,bytes(min(a,b) for a,b in zip(front.getchannel('A').tobytes(),mask.tobytes()))))
d=ImageDraw.Draw(front); d.line([(x*2,y*2) for x,y in polygon+[polygon[0]]],fill='#8996B0',width=3)
d.line([(746,56),(746,430),(726,450),(40,450)],fill=(204,173,131,140),width=2)
front.save(OUTPUT/'major-front.png')
base=grain.copy().convert('RGBA'); pixels=base.load()
for y in range(base.height):
    for x in range(base.width):
        n=(sum(noise[x,y])/3-175)*.065
        pixels[x,y]=(round(174+n),round(184+n),round(205+n),255)
base=base.resize((760,240),Image.Resampling.LANCZOS)
d=ImageDraw.Draw(base)
for offset in range(0,48,12):d.arc((360-offset,-70-offset,790+offset,280+offset),0,360,fill=(137,150,176,45),width=1)
base.save(OUTPUT/'secondary-base.png')
(SOURCE/'secondary-base.svg').write_text('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 380 120"><rect width="380" height="120" fill="#AEB8CD"/><image href="../Latest20261020/Assets/Art/steel-enamel-r2.png" width="380" height="120" opacity=".065"/><path d="M190,-35A108,87 0 1 1 395,140M184,-41A114,93 0 1 1 401,146" fill="none" stroke="#8996B0" opacity=".18"/></svg>\n',encoding='utf-8')
print('Generated 4 independent artwork textures, symmetrical settings icon, and single-contour caption frame.')
