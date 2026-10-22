from pathlib import Path
root=Path('Capstone/Assets')
paths=list((root/'ThangTest/UIThang/Inventory/UI').glob('*.uxml'))
paths += [root/'Game/UISystem/VietnameseUI.cs', root/'Game/UISystem/GameMenuController.cs', root/'Game/ProfileSystem/ProfilePanelLayout.cs',root/'Game/MapSystem/Editor/WorldMapUISetup.cs']
for path in paths:
    old=path.read_text(encoding='utf-8-sig')
    new=old.replace('QUAY LẠI','TRỞ LẠI').replace('Quay lại','Trở lại')
    if new != old: path.write_text(new,encoding='utf-8')
