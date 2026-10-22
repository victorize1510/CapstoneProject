from pathlib import Path
p=Path(__file__).resolve().parents[2]/'Capstone/Assets/Game/UISystem/GameMenuController.cs'
s=p.read_text(encoding='utf-8-sig')
for method in ['OpenInventoryDirect','OpenQuestDirect','OpenPetsDirect','OpenBoxDirect','OpenBoxFromWorld']:
    start=s.index('        public void '+method+'() {')
    end=s.index('\n        }',start)
    segment=s[start:end]
    if 'SetInventoryReturnTarget(' in segment: continue
    # Keep I toggle closing before changing the return target.
    marker='            CloseProfile(false);'
    target='UiReturnTarget.Gameplay' if method=='OpenBoxFromWorld' else 'IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay'
    segment=segment.replace(marker,'            SetInventoryReturnTarget('+target+');\n'+marker,1)
    if method=='OpenInventoryDirect': segment=segment.replace('                CloseInventory();\n                RefreshInputModeFromOpenUi();','                inventory.RequestClose();')
    s=s[:start]+segment+s[end:]
p.write_text(s,encoding='utf-8')
