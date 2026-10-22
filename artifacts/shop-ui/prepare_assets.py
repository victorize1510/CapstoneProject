from pathlib import Path
import uuid

root = Path(__file__).resolve().parents[2] / 'Capstone/Assets'
ui = root / 'ThangTest/UIThang/Inventory/UI'
# Move existing currency primitives to the shared sheet, byte-for-byte in behavior.
style = ui / 'InventoryPanel.uss'
text = style.read_text(encoding='utf-8-sig')
start = text.index('.inventory-currency {')
end = text.index('.inventory-body {', start)
currency = text[start:end]
common = ui / 'MenuCommon.uss'
common_text = common.read_text(encoding='utf-8-sig')
if '.inventory-currency {' not in common_text:
    common.write_text(common_text + '\n/* Shared wallet chip: Bag and Shop. */\n' + currency, encoding='utf-8')
    style.write_text(text[:start] + text[end:], encoding='utf-8')

script = root / 'Game/ShopSystem/ShopCatalog.cs'
meta = Path(str(script) + '.meta')
if not meta.exists():
    meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n', encoding='utf-8')
guid = next(line.split(': ')[1] for line in meta.read_text().splitlines() if line.startswith('guid:'))
catalog = root / 'Game/ShopSystem/Resources/Shop/DefaultShopCatalog.asset'
catalog.parent.mkdir(parents=True, exist_ok=True)
catalog.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: SCRIPT_GUID, type: 3}
  m_Name: DefaultShopCatalog
  m_EditorClassIdentifier: Assembly-CSharp::Capstone.Game.ShopSystem.ShopCatalog
  offers:
  - Id: potion
    Item: {fileID: 11400000, guid: b9d83c99a0cb41a59482463840d3581c, type: 2}
    Category: -1
    GoldPrice: -1
    Stock: -1
    MaxPerPurchase: 99
    Unlocked: 1
    LockedReason: ''
  - Id: dusk-stone
    Item: {fileID: 11400000, guid: 5bd730739fc04e47a6f9f66bc2a2bf4d, type: 2}
    Category: 4
    GoldPrice: -1
    Stock: -1
    MaxPerPurchase: 99
    Unlocked: 1
    LockedReason: ''
'''.replace('SCRIPT_GUID', guid), encoding='utf-8')
print('Shared currency chip moved; real-item catalog created with prices unset.')
