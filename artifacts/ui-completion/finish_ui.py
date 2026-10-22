"""One-time finishing edits; not part of Unity runtime or setup tooling."""
from pathlib import Path
import re

project = Path(__file__).resolve().parents[2]
assets = project / 'Capstone/Assets'
def edit(relative, change):
    path = assets / relative
    original = path.read_text(encoding='utf-8-sig')
    updated = change(original)
    if updated == original: return
    backup = project / 'artifacts/ui-completion/before-finish' / relative
    backup.parent.mkdir(parents=True, exist_ok=True)
    if not backup.exists(): backup.write_text(original, encoding='utf-8')
    path.write_text(updated, encoding='utf-8')

for panel in ['PetHeal', 'PetLevelUp', 'PetRelease', 'PetSkills']:
    def focus(text):
        text = text.replace('            dialog?.Focus();', '            Capstone.Game.UISystem.PanelFocusUtility.Open(overlay);')
        return text.replace('            if (overlay != null) overlay.style.display = DisplayStyle.None;', '            if (overlay != null) overlay.style.display = DisplayStyle.None;\n            Capstone.Game.UISystem.PanelFocusUtility.Close(overlay);')
    edit(f'Game/HudSystem/{panel}PanelController.cs', focus)

def party(text):
    text = text.replace('            detailsOverlay.BringToFront();', '            detailsOverlay.BringToFront();\n            Capstone.Game.UISystem.PanelFocusUtility.Open(detailsOverlay);')
    text = text.replace('            if (detailsOverlay != null) detailsOverlay.style.display = DisplayStyle.None;', '            if (detailsOverlay != null) detailsOverlay.style.display = DisplayStyle.None;\n            Capstone.Game.UISystem.PanelFocusUtility.Close(detailsOverlay);')
    text = text.replace('            renameInput?.Focus();', '            Capstone.Game.UISystem.PanelFocusUtility.Open(renameOverlay);')
    text = text.replace('            renameOriginalName = string.Empty;\n        }', '            renameOriginalName = string.Empty;\n            Capstone.Game.UISystem.PanelFocusUtility.Close(renameOverlay);\n        }')
    text = text.replace('SafeName(skill.displayName)', 'SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(skill.displayName))')
    exact = {'Locked':'Chưa mở', 'Heal':'Hồi phục', 'Hồi HP cho pet đang chọn bằng Potion.':'Hồi HP cho thú đang chọn bằng Potion.', 'Không tìm thấy MonsterInventoryController để mở Heal Popup.':'Không thể mở màn hồi phục lúc này.', 'Chưa chọn pet để đổi tên.':'Chọn thú đồng hành để đổi tên.', 'Chưa chọn pet để xem chi tiết.':'Chọn thú đồng hành để xem chi tiết.'}
    for old, new in exact.items(): text = text.replace('"'+old+'"', '"'+new+'"')
    return text
edit('Game/HudSystem/PetPartyPanelController.cs', party)

def quantity(text):
    for old, new in {'Action':'Thao tác', 'Item':'Vật phẩm', 'Half':'Một nửa', 'All':'Tất cả', 'Confirm':'Xác nhận', 'Cancel':'Hủy'}.items():
        text = text.replace('"'+old+'"', '"'+new+'"')
    text = text.replace('            quantityField.Focus();', '            Capstone.Game.UISystem.PanelFocusUtility.Open(overlay);')
    text = text.replace('            overlay.style.display = DisplayStyle.None;', '            overlay.style.display = DisplayStyle.None;\n            Capstone.Game.UISystem.PanelFocusUtility.Close(overlay);')
    return text
edit('ThangTest/UIThang/Inventory/UI/ItemQuantityPopup.cs', quantity)

def profile(text):
    for name, dialog in [('Name','nameDialog'), ('Avatar','avatarDialog')]:
        text = text.replace(f'        void Hide{name}Dialog() {{\n            Capstone.Game.UISystem.MenuKeyboardNavigation.Restore(popupReturnFocus);\n            if ({dialog} != null) {dialog}.gameObject.SetActive(false);\n        }}', f'        void Hide{name}Dialog() {{\n            bool wasOpen = {dialog} != null && {dialog}.gameObject.activeSelf;\n            if ({dialog} != null) {dialog}.gameObject.SetActive(false);\n            if (wasOpen) {{ Capstone.Game.UISystem.MenuKeyboardNavigation.Restore(popupReturnFocus); popupReturnFocus = null; }}\n        }}')
    text = text.replace('            if (avatarScroll != null) avatarScroll.verticalNormalizedPosition = 1f;\n        }', '            if (avatarScroll != null) avatarScroll.verticalNormalizedPosition = 1f;\n            Capstone.Game.UISystem.MenuKeyboardNavigation.Advance(avatarDialog, false);\n        }')
    return text
edit('Game/ProfileSystem/ProfilePanelController.cs', profile)

ui = assets / 'ThangTest/UIThang/Inventory/UI'
literal_map = {'SKILL ICON':'Kỹ năng', 'Skill Name':'Tên kỹ năng', 'Cooldown':'Hồi chiêu', 'LOCKED':'Chưa mở', 'Locked':'Chưa mở', 'HP':'HP', 'MP':'MP', 'EXP':'EXP'}
for path in ui.glob('*.uxml'):
    def labels(text):
        return re.sub(r'text="([^"]*)"', lambda match: 'text="'+literal_map.get(match[1], match[1])+'"', text)
    edit(str(path.relative_to(assets)), labels)

edit('Game/HudSystem/PetBoxPanelController.cs', lambda text: text.replace('"HOÀN TẤT BẮT PET"','"HOÀN TẤT THU PHỤC"').replace('"BOX ĐÃ ĐẦY"','"KHO THÚ ĐÃ ĐẦY"').replace('"MUA THÊM SLOT"','"MỞ RỘNG KHO"').replace('"thú đang chờ được lưu vào Kho thú"','"Thú đang chờ được lưu vào kho"'))
print('Finished focus wiring and explicit display translations.')
