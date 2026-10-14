from pathlib import Path
import re, sys
sys.path.insert(0, str(Path(__file__).parent))
from implement_ui import ROOT, ASSETS, UI, write

VI = {
    'INVENTORY':'TÚI ĐỒ', 'PETS':'THÚ ĐỒNG HÀNH', 'BOX':'KHO THÚ', 'QUEST JOURNAL':'NHIỆM VỤ',
    'ALL':'Tất cả', 'USE':'SỬ DỤNG', 'GIVE':'CHO THÚ', 'DROP':'BỎ', 'ASSIGN':'GÁN',
    'EVOLVE':'TIẾN HÓA', 'ASSIGN QUICK':'GÁN PHÍM TẮT', 'Close':'Đóng', 'ACTIVE':'ĐANG DÙNG',
    'LEAD':'DẪN ĐẦU', 'PET PREVIEW':'XEM TRƯỚC', 'SKILLS':'KỸ NĂNG',
    'Level':'Cấp độ', 'Rarity':'Độ hiếm', 'Give Item':'CHO VẬT PHẨM',
    'No items found.':'Không tìm thấy vật phẩm phù hợp.', 'Select an item':'Chọn vật phẩm để xem thông tin.',
    'Player overview':'Thông tin người chơi', 'Manage party pets':'Quản lý đội thú',
    'Open bag':'Mở túi đồ', 'Quest journal':'Theo dõi nhiệm vụ', 'Open world map':'Xem bản đồ',
    'Creature records':'Tra cứu sinh vật', 'Select a menu item.':'Chọn một mục trong menu.',
    'Trainer':'Người huấn luyện', 'Unknown Area':'Chưa rõ khu vực',
    'Vật phẩm Quest':'Vật phẩm nhiệm vụ', 'Gold đang sở hữu':'Vàng đang sở hữu',
    'Boss đã đánh bại':'Trùm đã đánh bại', 'Player ID':'Mã người chơi',
}

def localize():
    # UXML display attributes only: query names and template identifiers stay stable.
    for p in UI.glob('*.uxml'):
        text = p.read_text(encoding='utf-8-sig')
        def attribute(match):
            value = match.group(2)
            value = VI.get(value, value)
            for a,b in [('Party (','Đội hình ('), ('Double-click pet nguồn, sau đó click ô đích để đổi vị trí. Slot 1–6 tương ứng phím 1–6.', 'Nhấp đúp thú nguồn, rồi chọn ô đích để đổi vị trí. Ô 1–6 tương ứng phím 1–6.'),
                        ('ACTIVE là pet đang sử dụng • Viền cyan là pet đang xem', 'Đang dùng: thú đã gọi ra • Đang xem: thú được chọn'),
                        ('Chọn một pet','Chọn một thú'), ('Chọn pet','Chọn thú'), ('Level Up','Tăng cấp'), ('Healing','Hồi phục'), ('Quest','Nhiệm vụ')]: value=value.replace(a,b)
            return match.group(1)+'"'+value+'"'
        updated = re.sub(r'((?:text|tooltip)=)"([^"]*)"', attribute, text)
        if updated != text: write(p,updated)
    files = [* (ASSETS/'Game/HudSystem').glob('*PanelController.cs'), ASSETS/'Game/UISystem/GameMenuController.cs',
             * (ASSETS/'Game/ProfileSystem').glob('ProfilePanel*.cs'), ASSETS/'Game/QuestSystem/UI/QuestPanelController.cs', UI/'MonsterInventoryController.cs', UI/'ItemQuantityPopup.cs']
    for p in files:
        if not p.exists(): continue
        text=p.read_text(encoding='utf-8-sig')
        for old,new in VI.items(): text=text.replace('"'+old+'"','"'+new+'"')
        # Replace duplicated formatters, keeping all their callers and typed contracts.
        for method,typ,helper in [('FormatElement','PetElement','Element'),('FormatRarity','PetRarity','Rarity'),('FormatRarity','InventoryItemRarity','Rarity')]:
            pattern = rf'        static string {method}\({typ} (\w+)\)\s*\{{'
            m=re.search(pattern,text)
            if not m: continue
            start=m.start(); end=m.end(); depth=1
            while depth:
                depth += (text[end]=='{') - (text[end]=='}'); end+=1
            arg=m.group(1)+('.ToString()' if typ=='InventoryItemRarity' else '')
            text=text[:start]+f'        static string {method}({typ} {m.group(1)}) => Capstone.Game.UISystem.VietnameseUI.{helper}({arg});'+text[end:]
        # User-facing mixed-language messages, without touching action IDs or proper names.
        text=text.replace('ĐỘI HÌNH  |  Party (','ĐỘI HÌNH  |  Đội hình (').replace('ĐỘI HÌNH | Party (','ĐỘI HÌNH | Đội hình (')
        text=text.replace(' trong Bag.', ' trong túi đồ.').replace(' khỏi Bag.', ' khỏi túi đồ.').replace('từ Bag', 'từ túi đồ')
        text=text.replace(' / Box', ' / Kho thú').replace('"Box / ', '"Kho thú / ')
        text=text.replace('"Vật phẩm Quest"','"Vật phẩm nhiệm vụ"')
        if p.name=='ProfilePanelLayout.cs': text=text.replace('Icon.Book, "Codex"','Icon.Book, "Cẩm nang"')
        if p.name=='MonsterInventoryController.cs':
            text=text.replace('title + " panel is a placeholder. Connect the real gameplay backend later."','"Tính năng này chưa sẵn sàng."')
            for key in ['Party','Map','Settings','Quest Journal','Box','Bag']:
                text=text.replace('return "'+key+'";', 'return Capstone.Game.UISystem.VietnameseUI.Screen("'+key+'");')
        if text!=p.read_text(encoding='utf-8-sig'): write(p,text)
    # Unity's built-in UXML importer ID must match the installed editor.
    for p in UI.glob('Codex*.uxml.meta'):
        text=p.read_text(encoding='utf-8').replace('fileID: 12388','fileID: 13804'); write(p,text)

if __name__=='__main__':
    localize()
    print('Localized UI display strings and consolidated enum formatters.')
