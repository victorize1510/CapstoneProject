"""Reproduce the BAG-only visual tree from the supplied logical layout. No gameplay/fixture data."""
from pathlib import Path
import xml.etree.ElementTree as E

ROOT = Path(__file__).resolve().parents[2]
NS = {'ui': 'UnityEngine.UIElements', 'bag': 'Capstone.Game.Inventory.Bag', 'ga': 'Capstone.Game.UISystem'}
for key, value in NS.items(): E.register_namespace(key, value)
def add(parent, tag, **attrs):
    prefix, tag = tag.split(':')
    return E.SubElement(parent, '{'+NS[prefix]+'}'+tag, {k.replace('_','-'):str(v) for k,v in attrs.items()})
def pos(x,y,w,h): return f'position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{h}px;'
def label(parent, text='', name=None, cls=None, rect=None, style=''):
    attrs={'text':text,'picking_mode':'Ignore'}
    if name: attrs['name']=name
    if cls: attrs['class']=cls
    if rect: style=pos(*rect)+style
    if style: attrs['style']=style
    return add(parent,'ui:Label',**attrs)
def surface(parent,kind,rect,name=None):
    attrs={'kind':kind,'style':pos(*rect)}
    if name: attrs['name']=name
    return add(parent,'bag:BagSurface',**attrs)
def button(parent,name,text,kind,rect,cls=''):
    return add(parent,'bag:BagButton',name=name,text=text,kind=kind,style=pos(*rect),**{'class':cls})

root=E.Element('{'+NS['ui']+'}UXML',{'editor-extension-mode':'False'})
for src in ['MenuTheme.uss','MenuCommon.uss','../../../../Game/Inventory/Bag/Resources/UI/BAG20261022/Bag.uss']:
    add(root,'ui:Style',src=src)
panel=add(root,'ui:VisualElement',name='inventory-panel',**{'class':'inventory-panel menu-panel menu-theme bag-design'})
canvas=add(panel,'ui:VisualElement',name='bag-canvas',**{'class':'bag-canvas'})
add(canvas,'ga:AngularSurface',role='panel',shape='parallel',style=pos(28,24,644,64))
add(canvas,'ui:VisualElement',name='bag-title-emblem',picking_mode='Ignore',**{'class':'bag-title-emblem'})
label(canvas,'TÚI ĐỒ',cls='bag-title',rect=(144,24,480,64))
surface(canvas,'category-row',(1176,36,176,48))
label(canvas,'G',rect=(1196,42,24,36),style='color:#CCAD83;font-size:24px;-unity-font-style:bold;')
label(canvas,'0','inventory-gold-label',rect=(1224,42,112,36))
button(canvas,'cancel-button','←  Trở lại','action-primary',(1372,36,196,48),'bag-dark')
surface(canvas,'shell',(24,112,1552,748),'inventory-shell')
body=add(canvas,'ui:VisualElement',name='inventory-body',style='position:absolute;left:0;right:0;top:0;bottom:0;')
surface(body,'category-panel',(48,136,240,628),'category-panel')
label(body,'DANH MỤC',cls='bag-category-heading',rect=(68,160,172,28))
categories=[('all','Tất cả','grid'),('capture-ball','Dụng cụ bắt','capture'),('medicine','Hồi phục','heal'),('food','Thức ăn','food'),('material','Nguyên liệu','material'),('key-item','Vật phẩm chính','keyitem'),('quest-item','Vật phẩm nhiệm vụ','questitem'),('equipment','Trang bị','defense')]
for i,(key,title,icon) in enumerate(categories):
    b=button(body,'category-'+key,'','category-row',(64,194+i*65,212,56),'category-button bag-dark'+(' is-selected' if i==0 else ''))
    add(b,'ui:VisualElement',name='bag-category-'+key+'-icon',picking_mode='Ignore',**{'class':'category-icon'})
    label(b,title,cls='category-label');label(b,'0',cls='category-count')
surface(body,'browser',(312,136,784,628),'bag-browser')
add(body,'ui:VisualElement',name='bag-browser-art',picking_mode='Ignore',style=pos(516,410,560,280)+'background-size:cover;opacity:0.10;')
for kind,rect in [('search',(328,154,292,48)),('sort',(680,154,176,48)),('filter',(868,154,148,48))]: surface(body,kind,rect)
add(body,'ui:TextField',name='inventory-search-field',tooltip='Tìm tên, mã hoặc danh mục vật phẩm',style=pos(328,154,292,48))
add(body,'ui:VisualElement',name='bag-search-icon',picking_mode='Ignore',style=pos(340,168,20,20)+'background-size:contain;')
button(body,'inventory-search-field-clear','×','action-small',(628,154,40,48))
for key,rect in [('sort',(680,154,176,48)),('filter',(868,154,148,48))]: add(body,'ui:DropdownField',name='inventory-'+key+'-field',style=pos(*rect))
b=button(body,'inventory-view-button','','view-toggle',(1028,154,52,48),'bag-dark')
add(b,'ui:VisualElement',name='bag-view-icon',picking_mode='Ignore',style=pos(14,12,24,24)+'background-size:contain;-unity-background-image-tint-color:#EDE9E1;')
label(body,'','bag-type-count','bag-type-count',(328,212,440,30));label(body,'Ô vuông','bag-mode-label','bag-mode-label',(852,212,220,30))
scroll=add(body,'ui:ScrollView',name='item-scroll',mode='Vertical',horizontal_scroller_visibility='Hidden',vertical_scroller_visibility='Auto')
add(scroll,'ui:VisualElement',name='item-grid')
empty=add(body,'ui:VisualElement',name='bag-empty-state',picking_mode='Ignore',**{'class':'bag-empty'})
surface(empty,'browser',(0,0,596,260));add(empty,'ui:VisualElement',name='bag-empty-icon',picking_mode='Ignore',**{'class':'bag-empty-icon'})
label(empty,'Túi đồ đang trống','no-items-label')
label(body,'Số trên ô là tổng vật phẩm cùng loại.','bag-grid-helper',rect=(328,704,744,32),style='-unity-text-align:middle-center;')
surface(body,'inspector',(1112,136,440,628),'bag-inspector');surface(body,'detail-art',(1136,156,392,172),'bag-detail-art')
icon=add(body,'ui:VisualElement',name='detail-icon',picking_mode='Ignore');label(icon,'◇','detail-icon-placeholder')
add(body,'ui:VisualElement',name='bag-detail-empty-art',picking_mode='Ignore',style=pos(1284,208,96,96)+'background-size:contain;')
label(body,'Chọn vật phẩm','inventory-detail-empty')
label(body,'Thông tin và thao tác hiện tại đây.','bag-detail-empty-hint',style='position:absolute;left:1136px;top:422px;width:392px;height:56px;-unity-text-align:middle-center;')
detail=add(body,'ui:ScrollView',name='detail-panel',mode='Vertical',horizontal_scroller_visibility='Hidden',vertical_scroller_visibility='Auto')
label(detail,'','detail-name')
badges=add(detail,'ui:VisualElement',**{'class':'detail-badges'});label(badges,'','detail-category');label(badges,'','detail-rarity')
for key in ['quantity','description','effect']: label(detail,'','detail-'+key)
source=add(detail,'ui:VisualElement',name='detail-source-block');label(source,'NGUỒN THU THẬP',cls='detail-section-title');label(source,'','detail-source');label(detail,'','detail-flavor')
label(body,'','bag-action-feedback','bag-feedback')
actions=add(body,'ui:VisualElement',name='inventory-actions')
button(actions,'use-button','Sử dụng','action-primary',(0,0,392,56),'bag-dark')
secondary=add(actions,'ui:VisualElement',**{'class':'inventory-secondary-actions'})
button(secondary,'equip-button','Gán R/T','action-small',(0,0,190,44));button(secondary,'drop-button','Bỏ','action-small',(202,0,190,44))
surface(canvas,'category-row',(48,780,1504,56))
label(canvas,'','bag-footer-count','bag-footer-count',(68,788,200,40))
label(canvas,'Q/E Danh mục · ↑/↓ Chọn · Enter Thao tác · Esc Quay lại',cls='bag-footer-hint',rect=(288,788,900,40))
label(canvas,'R / T Vật phẩm nhanh',cls='bag-footer-quick',rect=(1224,788,300,40))
E.indent(root,space='  ')
(ROOT/'Capstone/Assets/ThangTest/UIThang/Inventory/UI/InventoryPanel.uxml').write_text(E.tostring(root,encoding='unicode')+'\n',encoding='utf-8')
