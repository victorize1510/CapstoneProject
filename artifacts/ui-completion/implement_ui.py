from pathlib import Path
import re, uuid, shutil

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'Capstone/Assets'
UI = ASSETS / 'ThangTest/UIThang/Inventory/UI'
BACKUP = ROOT / 'artifacts/ui-completion/before'

def write(path, text):
    if path.exists():
        backup = BACKUP / path.relative_to(ROOT)
        if not backup.exists():
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, backup)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8', newline='\n')

def replace(path, old, new):
    text = path.read_text(encoding='utf-8-sig')
    if old not in text:
        raise RuntimeError(f'Missing expected source: {path.name}: {old[:70]}')
    write(path, text.replace(old, new))

def panel():
    cards = '\n'.join('                    <ui:Instance template="CodexCard" />' for _ in range(25))
    categories = '\n'.join('                    <ui:Instance template="CodexCategory" />' for _ in range(12))
    pages = '\n'.join('                <ui:Instance template="CodexPageButton" />' for _ in range(9))
    write(UI / 'CodexPanel.uxml', f'''<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <ui:Style src="MenuTheme.uss" />
    <ui:Style src="MenuCommon.uss" />
    <ui:Style src="CodexPanel.uss" />
    <ui:Style src="MenuScreenHeader.uss" />
    <ui:Template name="CodexCard" src="CodexCard.uxml" />
    <ui:Template name="CodexCategory" src="CodexCategory.uxml" />
    <ui:Template name="CodexTypeBadge" src="CodexTypeBadge.uxml" />
    <ui:Template name="CodexPageButton" src="CodexPageButton.uxml" />
    <ui:VisualElement name="codex-panel" class="codex-panel menu-theme">
        <ui:VisualElement class="screen-header">
            <ui:VisualElement class="screen-heading">
                <ui:VisualElement class="screen-heading-mark" picking-mode="Ignore" focusable="false" />
                <ui:Label text="CẨM NANG PET" class="screen-title" />
            </ui:VisualElement>
            <ui:Label text="Khám phá và ghi nhận muôn loài sinh vật" class="codex-subtitle" />
            <ui:Button name="codex-back" text="←  QUAY LẠI" class="screen-back" />
        </ui:VisualElement>
        <ui:VisualElement class="codex-body">
            <ui:ScrollView class="codex-column codex-left" mode="Vertical" horizontal-scroller-visibility="Hidden" vertical-scroller-visibility="Auto">
                <ui:VisualElement class="codex-progress">
                    <ui:Label text="Tiến độ thu thập" class="codex-section-title" />
                    <ui:Label name="codex-progress-number" text="—" class="codex-progress-number" />
                    <ui:ProgressBar name="codex-progress-bar" title="Chưa có thông tin" low-value="0" high-value="100" />
                </ui:VisualElement>
                <ui:VisualElement class="codex-search-row">
                    <ui:TextField name="codex-search" placeholder-text="Tìm tên hoặc số thứ tự..." class="codex-search" />
                    <ui:Button name="codex-clear" text="×" tooltip="Xóa tìm kiếm" class="codex-clear" />
                </ui:VisualElement>
                <ui:VisualElement name="codex-categories">
{categories}
                </ui:VisualElement>
            </ui:ScrollView>
            <ui:VisualElement class="codex-column codex-center">
                <ui:VisualElement class="codex-toolbar">
                    <ui:Button text="Theo số thứ tự" class="codex-sort" />
                    <ui:Button text="Theo hệ" class="codex-sort" />
                    <ui:Button text="Theo khu vực" class="codex-sort" />
                    <ui:Button text="Độ hiếm" class="codex-sort" />
                </ui:VisualElement>
                <ui:Button name="codex-filter-toggle" text="Bộ lọc và sắp xếp" class="codex-filter-toggle" />
                <ui:VisualElement name="codex-filters" class="codex-filters">
                    <ui:DropdownField name="codex-region" tooltip="Lọc khu vực" />
                    <ui:DropdownField name="codex-rarity" tooltip="Lọc độ hiếm" />
                    <ui:DropdownField name="codex-discovery" tooltip="Lọc trạng thái khám phá" />
                    <ui:DropdownField name="codex-extra-sort" tooltip="Sắp xếp danh sách" />
                    <ui:Button name="codex-reset" text="Đặt lại bộ lọc" />
                </ui:VisualElement>
                <ui:ScrollView class="codex-grid-scroll" mode="Vertical" horizontal-scroller-visibility="Hidden" vertical-scroller-visibility="Auto">
                    <ui:VisualElement name="codex-grid" class="codex-grid">
{cards}
                    </ui:VisualElement>
                    <ui:Label name="codex-empty" text="Cẩm nang chưa có thông tin sinh vật." class="codex-empty" />
                </ui:ScrollView>
                <ui:VisualElement class="codex-pagination">
{pages}
                </ui:VisualElement>
            </ui:VisualElement>
            <ui:ScrollView class="codex-column codex-right" mode="Vertical" horizontal-scroller-visibility="Hidden" vertical-scroller-visibility="Auto">
                <ui:Label name="codex-detail-empty" text="Chọn sinh vật để xem thông tin." class="codex-empty" />
                <ui:VisualElement name="codex-details">
                    <ui:Label name="codex-number" />
                    <ui:Label name="codex-name" class="codex-detail-name" />
                    <ui:Button name="codex-favorite" text="Đánh dấu yêu thích" class="codex-favorite-action" />
                    <ui:VisualElement class="codex-types">
                        <ui:Instance template="CodexTypeBadge" />
                        <ui:Instance template="CodexTypeBadge" />
                    </ui:VisualElement>
                    <ui:VisualElement name="codex-detail-portrait" class="codex-detail-portrait" />
                    <ui:Label name="codex-description" class="codex-detail-text" />
                    <ui:Label text="Thông tin cơ bản" class="codex-section-title" />
                    <ui:Label name="codex-basic" class="codex-detail-text" />
                    <ui:Label text="Tiến hóa" class="codex-section-title" />
                    <ui:VisualElement name="codex-evolution" class="codex-evolution" />
                </ui:VisualElement>
            </ui:ScrollView>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
''')
    replace(UI / 'MonsterInventory.uxml', '    <ui:Template name="BoxPanel"', '    <ui:Template name="CodexPanel" src="CodexPanel.uxml" />\n    <ui:Template name="BoxPanel"')
    replace(UI / 'MonsterInventory.uxml', '        <ui:Instance template="BoxPanel"', '        <ui:Instance template="CodexPanel" class="panel-template-host" picking-mode="Ignore" />\n        <ui:Instance template="BoxPanel"')
    replace(UI / 'MenuScreenHeader.uss', '.inventory-panel .screen-header,', '.codex-panel .screen-header,\n.inventory-panel .screen-header,')

def integration():
    p = UI / 'MonsterInventoryController.cs'
    replace(p, '        VisualElement boxPanel;', '        VisualElement boxPanel;\n        VisualElement codexPanel;\n        Capstone.Game.CodexSystem.PetCodexPanelController codexController;\n        public bool IsCodexOpen => IsOpen && activeSection == MenuSection.Codex;\n        public event Action CodexBackRequested;')
    replace(p, '            EnsurePetReleasePanelController();\n            RegisterCategoryButtons();', '''            EnsurePetReleasePanelController();
            codexController = GetComponent<Capstone.Game.CodexSystem.PetCodexPanelController>();
            if (codexController == null) codexController = gameObject.AddComponent<Capstone.Game.CodexSystem.PetCodexPanelController>();
            codexController.Bind(document);
            codexController.BackRequested -= HandleCodexBack;
            codexController.BackRequested += HandleCodexBack;
            RegisterCategoryButtons();''')
    replace(p, '        void OnDisable() {\n            Close();', '''        void OnDisable() {
            if (codexController != null) codexController.BackRequested -= HandleCodexBack;
            Close();''')
    replace(p, '            boxPanel = rootElement.Q<VisualElement>("box-panel");', '            boxPanel = rootElement.Q<VisualElement>("box-panel");\n            codexPanel = rootElement.Q<VisualElement>("codex-panel");')
    replace(p, '        void OpenContentSection(MenuSection section) {', '''        public void OpenCodexPanel() => OpenContentSection(MenuSection.Codex);
        void HandleCodexBack() { Close(); CodexBackRequested?.Invoke(); }

        void OpenContentSection(MenuSection section) {''')
    replace(p, '            root.style.display = DisplayStyle.None;\n            VisibilityChanged?.Invoke(false);', '            codexController?.Close();\n            root.style.display = DisplayStyle.None;\n            VisibilityChanged?.Invoke(false);')
    replace(p, '            bool showPetBox = section == MenuSection.Box;', '            bool showPetBox = section == MenuSection.Box;\n            bool showCodex = section == MenuSection.Codex;\n            if (showCodex) codexController?.Open(); else codexController?.Close();\n            SetVisible(codexPanel, showCodex);')
    replace(p, '!showInventoryContent && !showPetParty && !showPetBox', '!showInventoryContent && !showPetParty && !showPetBox && !showCodex')
    replace(p, '            Journal,\n            Bag,', '            Codex,\n            Journal,\n            Bag,')
    p = ASSETS / 'Game/UISystem/GameMenuController.cs'
    replace(p, '        public void OpenQuestDirect() {', '''        UiReturnTarget codexReturnTarget;
        public void OpenCodexDirect() {
            if (!PlayerActionGate.TryAccept(this, "codex")) return;
            ResolveReferences();
            codexReturnTarget = IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay;
            CloseProfile(false); CloseMenu(); CloseMap();
            if (inventory == null) { OpenFeaturePlaceholder("Codex", codexReturnTarget); return; }
            inventory.CodexBackRequested -= HandleCodexBack;
            inventory.CodexBackRequested += HandleCodexBack;
            inventory.OpenCodexPanel();
            SetInputMode(GameInputMode.Inventory);
        }
        void HandleCodexBack() {
            if (codexReturnTarget == UiReturnTarget.Menu) OpenMenu();
            else RefreshInputModeFromOpenUi();
        }

        public void OpenQuestDirect() {''')
    replace(p, '() => OpenFeaturePlaceholder("Codex")', 'OpenCodexDirect')
    replace(p, '            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, IsMenuOpen ? "menu-visibility" : "ui-back")) return;', '''            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, IsMenuOpen ? "menu-visibility" : "ui-back")) return;
            if (inventory != null && inventory.IsCodexOpen) { inventory.Close(); HandleCodexBack(); return; }''')
    replace(p, '        void OnDisable() {\n            UnsubscribeProfileProvider();', '        void OnDisable() {\n            if (inventory != null) inventory.CodexBackRequested -= HandleCodexBack;\n            UnsubscribeProfileProvider();')
    replace(p, 'CreateText(button.transform, "Label", title, 33', 'CreateText(button.transform, "Label", VietnameseUI.Screen(title), 28')
    replace(p, 'CreateText(placeholderPanel, "Title", title,', 'CreateText(placeholderPanel, "Title", VietnameseUI.Screen(title),') if 'CreateText(placeholderPanel, "Title", title,' in p.read_text(encoding='utf8') else None

def metas():
    for folder in [ASSETS / 'Game/CodexSystem']:
        if not folder.with_suffix('.meta').exists():
            write(folder.with_suffix('.meta'), f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    for file in [*UI.glob('Codex*'), * (ASSETS / 'Game/CodexSystem').rglob('*.cs'), ASSETS / 'Game/UISystem/VietnameseUI.cs']:
        if file.suffix == '.meta' or not file.is_file() or file.with_suffix(file.suffix+'.meta').exists(): continue
        importer = 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n' if file.suffix == '.cs' else 'ScriptedImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 2\n  script: {fileID: 12388, guid: 0000000000000000e000000000000000, type: 0}\n' if file.suffix == '.uxml' else 'ScriptedImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 2\n  script: {fileID: 12385, guid: 0000000000000000e000000000000000, type: 0}\n'
        write(file.with_suffix(file.suffix+'.meta'), f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n'+importer+'  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

if __name__ == '__main__':
    panel()
    integration()
    metas()
    print('Created Codex layout and connected menu routing.')
