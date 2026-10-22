from pathlib import Path
import re, uuid, sys
sys.path.insert(0, str(Path(__file__).parent))
from implement_ui import ROOT, ASSETS, UI, write, replace

def display_words(value):
    # These transformations only apply to display attributes and text literals containing spaces.
    for a,b in [('Double-click','Nhấp đúp'),('click','nhấp'),('Slot','Ô'),('slot','ô'),('Party','Đội hình'),('BOX','KHO THÚ'),('Box','Kho thú'),('Gold','Vàng'),('Quest','Nhiệm vụ'),('SKILLS','KỸ NĂNG'),('Level','Cấp'),('Lv.','Cấp'),('Thảo','Cỏ')]:
        value = re.sub(r'(?<![\w-])'+re.escape(a)+r'(?![\w-])',b,value)
    value = re.sub(r'\bpets?\b','thú',value,flags=re.I)
    return value

def run():
    for p in UI.glob('*.uxml'):
        text=p.read_text(encoding='utf8')
        text=re.sub(r'((?:text|tooltip)=)"([^"]*)"',lambda m:m[1]+'"'+display_words(m[2])+'"',text)
        write(p,text)
    for p in [*(ASSETS/'Game/HudSystem').glob('*PanelController.cs'),*(ASSETS/'Game/ProfileSystem').glob('ProfilePanel*.cs'), UI/'MonsterInventoryController.cs']:
        text=p.read_text(encoding='utf8')
        def literal(m):
            content=m[0][1:-1]
            # Keep hierarchy paths, element names, enum lookups, keys and proper names.
            return '"'+display_words(content)+'"' if ' ' in content and not content.startswith(('Assets/','Game/','Game Tools/')) and not ('Pet' in content and re.search(r'Pet[A-Z]',content)) else m[0]
        text=re.sub(r'"(?:\\.|[^"\\])*"',literal,text)
        write(p,text)

    menu=ASSETS/'Game/UISystem/GameMenuController.cs'
    text=menu.read_text(encoding='utf8')
    for a,b in {'TRAINER MENU':'HUẤN LUYỆN VIÊN','PLAYER':'BẠN','Current Area':'Khu vực','Play Time':'Thời gian chơi','Party Pets':'Đội thú','<   Back':'←  QUAY LẠI','Back to gameplay':'Trở về trò chơi','Feature':'Tính năng','Back':'Quay lại','YOU':'HS','PAW':'THÚ','BAG':'TÚI','LOG':'NV','CFG':'CĐ','MAP':'BĐ','BOOK':'CN'}.items():
        text=text.replace('"'+a+'"','"'+b+'"')
    text=text.replace('AddButtonHint(settings, "Settings")','AddButtonHint(settings, "Cài đặt")').replace('"Label", "Settings", 14','"Label", "Cài đặt", 14')
    text=text.replace('placeholderTitle.text = title;', 'placeholderTitle.text = VietnameseUI.Screen(title);')
    text=text.replace('title + "\\n\\nPanel nay chua co backend that. Khi co system rieng, minh chi can noi nut menu nay sang controller cua system do."','"Tính năng này chưa sẵn sàng. Hãy quay lại sau."')
    text=text.replace('title + " is a placeholder panel."','"Tính năng đang được hoàn thiện."')
    text=text.replace('            if (CurrentMode != GameInputMode.Gameplay) {', '''            if (CurrentMode != GameInputMode.Gameplay) {
                if (Input.GetKeyDown(KeyCode.Tab) && (IsMenuOpen || IsProfileOpen())) {
                    Transform navigationRoot = IsProfileOpen() ? profile.NavigationRoot : panel;
                    MenuKeyboardNavigation.Advance(navigationRoot, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                    return;
                }''')
    text=text.replace('            SetInputMode(GameInputMode.Menu);\n        }\n\n        public void CloseMenu()', '            SetInputMode(GameInputMode.Menu);\n            MenuKeyboardNavigation.Advance(panel, false);\n        }\n\n        public void CloseMenu()')
    # Menu labels may be longer after translation; preserve row bounds and fit text.
    text=text.replace('            SetTopStretch(label.rectTransform, 112f, 62f, 12f, 44f);', '            SetTopStretch(label.rectTransform, 112f, 62f, 12f, 62f);\n            label.resizeTextForBestFit = true; label.resizeTextMinSize = 17; label.resizeTextMaxSize = 28;')
    write(menu,text)
    p=ASSETS/'Game/ProfileSystem/ProfilePanelController.cs'
    text=p.read_text(encoding='utf8').replace('        const string RootName', '        public Transform NavigationRoot => nameDialog != null && nameDialog.gameObject.activeSelf ? nameDialog : avatarDialog != null && avatarDialog.gameObject.activeSelf ? avatarDialog : root;\n        GameObject popupReturnFocus;\n        const string RootName')
    for method in ['ShowNameDialog','ShowAvatarDialog']:
        text=text.replace('        void '+method+'() {', '        void '+method+'() {\n            popupReturnFocus = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;')
    for method in ['HideNameDialog','HideAvatarDialog']:
        text=text.replace('        void '+method+'() {', '        void '+method+'() {\n            Capstone.Game.UISystem.MenuKeyboardNavigation.Restore(popupReturnFocus);')
    text=text.replace('value.ToString() : "-"','value.ToString() : "Chưa có thông tin"')
    write(p,text)

    # Shared search affordance. Existing named TextFields and callbacks stay intact.
    for name,field in [('InventoryPanel','inventory-search-field'),('BoxPanel','box-search-field')]:
        p=UI/(name+'.uxml'); text=p.read_text(encoding='utf8')
        text=re.sub(r'(<ui:TextField name="'+field+r'"[^>]*/>)',r'\1\n                        <ui:Button name="'+field+'-clear" text="×" tooltip="Xóa tìm kiếm" class="menu-search-clear" />',text)
        write(p,text)
    p=UI/'MenuCommon.uss'; write(p,p.read_text(encoding='utf8')+'''
/* Search and accessibility states shared by feature pages. */
.menu-search-clear { width: 34px; min-width: 34px; height: 36px; margin-right: 8px; font-size: 22px; }
.menu-panel Button:focus, .pets-panel Button:focus, .box-panel Button:focus, .quest-panel Button:focus { border-color: var(--menu-focus-border, #c6a34d); }
.menu-detail-empty { flex-grow: 1; min-height: 100px; white-space: normal; -unity-text-align: middle-center; color: #5c7158; font-size: 18px; }
''')
    p=UI/'MonsterInventoryController.cs'
    replace(p,'            sortField = rootElement.Q<DropdownField>("inventory-sort-field");','            sortField = rootElement.Q<DropdownField>("inventory-sort-field");\n            RegisterControl(rootElement.Q<Button>("inventory-search-field-clear"), () => { searchField.value = string.Empty; searchField.Focus(); });') if False else None
    # Use existing guarded registration infrastructure.
    text=p.read_text(encoding='utf8')
    anchor='        void RegisterActionButtons() {'
    text=text.replace(anchor,anchor+'\n            RegisterButton(document.rootVisualElement.Q<Button>("inventory-search-field-clear"), () => { searchField.value = string.Empty; searchField.Focus(); });')
    # Exact helper name is verified below before source is written.
    helper=re.search(r'void (\w+)\(Button button, Action callback\)',text)
    if helper: text=text.replace('            RegisterButton(document.rootVisualElement.Q<Button>("inventory-search-field-clear")','            '+helper[1]+'(document.rootVisualElement.Q<Button>("inventory-search-field-clear")')
    write(p,text)
    p=ASSETS/'Game/HudSystem/PetBoxPanelController.cs'
    text=p.read_text(encoding='utf8')
    # Bind clear once through the controller's existing callback registry.
    anchor='            searchField = docRoot.Q<TextField>("box-search-field");'
    if anchor in text: text=text.replace(anchor,anchor+'\n            RegisterButton(docRoot.Q<Button>("box-search-field-clear"), () => { searchField.value = string.Empty; searchField.Focus(); });')
    else:
        anchor='        void HandleSearchChanged(ChangeEvent<string> _) {'
        text=text.replace(anchor,anchor) # registration below uses Bind root instead
        text=text.replace('            selectedGender = docRoot.Q<Label>("box-selected-gender");','            selectedGender = docRoot.Q<Label>("box-selected-gender");\n            RegisterButton(docRoot.Q<Button>("box-search-field-clear"), () => { searchField.value = string.Empty; searchField.Focus(); });')
    text=text.replace('            if (emptyLabel != null) emptyLabel.style.display = DisplayStyle.None;', '''            if (emptyLabel != null) {
                bool filtering = !string.IsNullOrWhiteSpace(query) || selectedElements.Count > 0;
                bool anyMatch = orderedSlots.Exists(entry => entry.snapshot.pet != null && MatchesSearch(entry.snapshot, query) && MatchesElement(entry.snapshot.element, entry.snapshot.secondaryElement));
                emptyLabel.text = filtering ? "Không tìm thấy thú phù hợp. Hãy thử bộ lọc khác." : "Kho chưa có thú.";
                emptyLabel.style.display = anyMatch ? DisplayStyle.None : DisplayStyle.Flex;
            }''')
    write(p,text)
    # Quest detail placeholder lives outside the hidden detail-content block.
    p=UI/'QuestPanel.uxml'; replace(p,'<ui:VisualElement name="quest-detail-column" class="quest-detail-column">','<ui:VisualElement name="quest-detail-column" class="quest-detail-column">\n                <ui:Label name="quest-detail-empty" text="Chọn nhiệm vụ để xem chi tiết." class="menu-detail-empty" />')
    p=ASSETS/'Game/QuestSystem/UI/QuestPanelController.cs'
    text=p.read_text(encoding='utf8').replace('        Label questPageEmpty;','        Label questPageEmpty;\n        Label detailEmpty;')
    text=text.replace('            SetVisible(detailContent, true);','            detailEmpty = rootElement?.Q<Label>("quest-detail-empty");\n            SetVisible(detailEmpty, false);\n            SetVisible(detailContent, true);')
    text=text.replace('            SetVisible(detailContent, false);','            detailEmpty = rootElement?.Q<Label>("quest-detail-empty");\n            if (detailEmpty != null) detailEmpty.text = filteredQuests.Count == 0 ? "Nhóm này chưa có nhiệm vụ." : "Chọn nhiệm vụ để xem chi tiết.";\n            SetVisible(detailEmpty, true);\n            SetVisible(detailContent, false);')
    write(p,text)

    # Dedicated pet portrait lifecycle: bake only while PETS is actually visible.
    p=ASSETS/'Game/HudSystem/PetPartyPanelController.cs'
    text=p.read_text(encoding='utf8').replace('        VisualElement selectedPortrait;', '        readonly PetPortraitPreview portraitPreview = new PetPortraitPreview();\n        VisualElement selectedPortrait;')
    text=text.replace('        void OnDisable() {','        void OnDisable() {\n            portraitPreview.Clear();')
    text=text.replace('        public void Bind(UIDocument targetDocument) {','''        void OnDestroy() => portraitPreview.Dispose();
        void LateUpdate() {
            if (inventoryOwner?.IsPetsSectionOpen != true) { portraitPreview.Clear(); return; }
            var pet = CommandProvider?.GetSelectedPetController();
            if (selectedPortrait != null && portraitPreview.Show(pet)) {
                portraitPreview.Update();
                selectedPortrait.style.backgroundImage = new StyleBackground((RenderTexture)portraitPreview.Texture);
                if (selectedPortraitFallback != null) selectedPortraitFallback.style.display = DisplayStyle.None;
            }
        }
        public void Bind(UIDocument targetDocument) {''')
    text=text.replace('            selectedPortraitFallback = document.rootVisualElement.Q<Label>("pets-selected-portrait-fallback");','''            selectedPortraitFallback = document.rootVisualElement.Q<Label>("pets-selected-portrait-fallback");
            foreach (var label in document.rootVisualElement.Query<Label>().ToList()) {
                string key = label.text.Trim().TrimStart('♥', '⚔', '✦', '◆', '◇', '➤', '★', '◉', ' ');
                string tip = Capstone.Game.UISystem.VietnameseUI.StatTooltip(key);
                if (tip.Length > 0) label.tooltip = tip;
            }''')
    text=text.replace('activeStatus.text = slot.summoned ? "ĐANG DÙNG" : string.Empty;', 'activeStatus.text = slot.summoned ? "ĐANG DÙNG" : slot.selected ? "ĐANG XEM" : string.Empty;')
    write(p,text)
    # Camera buffers are owned for the whole preview lifecycle.
    p=ASSETS/'Game/HudSystem/PetPortraitPreview.cs'
    text=p.read_text(encoding='utf8').replace('public Transform Transform; }','public Transform Transform; public readonly List<Vector3> Vertices = new List<Vector3>(); }')
    text=text.replace('        PetController source;', '        PetController source;\n        string form;')
    text=text.replace('            if (source == pet && stage != null)', '            string nextForm = pet != null ? pet.GetComponentInChildren<PetCollectionMetadata>(true)?.CurrentFormId : null;\n            if (source == pet && form == nextForm && stage != null)')
    text=text.replace('            source = pet;', '            source = pet; form = nextForm;')
    text=text.replace('var vertices = new List<Vector3>(); mesh.GetVertices(vertices);','var vertices = part.Vertices; mesh.GetVertices(vertices);')
    write(p,text)

    p=ASSETS/'Game/MapSystem/UI/WorldMapUIController.cs'
    text=p.read_text(encoding='utf8').replace('        float nextMarkerRefresh;', '        MapMarkerClusters clusters;\n        readonly List<MapMarkerView> clusterViews = new List<MapMarkerView>();\n        float nextMarkerRefresh;')
    text=text.replace('            RestoreIcons();\n        }','            RestoreIcons();\n            clusters?.Close();\n        }')
    text=text.replace('            markerContainer.gameObject.AddComponent<RectMask2D>();','            markerContainer.gameObject.AddComponent<RectMask2D>();\n            clusters = new MapMarkerClusters(markerContainer, theme, SelectMarker);')
    text=text.replace('group.ToString(), 76, 0, 96, 40, 21','FilterName(group), 76, 0, 96, 40, 17')
    text=text.replace('        public void SetFilter(MapFilterGroup group, bool visible) {','''        static string FilterName(MapFilterGroup group) {
            switch (group) {
                case MapFilterGroup.NPC: return "Nhân vật";
                case MapFilterGroup.Pet: return "Thú";
                case MapFilterGroup.Enemy: return "Kẻ địch";
                case MapFilterGroup.Boss: return "Trùm";
                case MapFilterGroup.Quest: return "Nhiệm vụ";
                case MapFilterGroup.Shop: return "Cửa hàng";
                default: return "Dịch chuyển";
            }
        }
        public bool TryCloseMarkerGroup() { if (clusters?.IsPopupOpen != true) return false; clusters.Close(); return true; }
        public void SetFilter(MapFilterGroup group, bool visible) {
            clusters?.Close();''')
    text=text.replace('            if (worldMap == null || !worldMap.IsOpen) return;', '            if (worldMap == null || !worldMap.IsOpen) { clusters?.Close(); return; }')
    text=text.replace('            selectedLabel.transform.parent.gameObject.SetActive(selectedVisible);','''            selectedLabel.transform.parent.gameObject.SetActive(selectedVisible);
            clusterViews.Clear();
            foreach (var view in sourceViews.Values) clusterViews.Add(view);
            foreach (var view in dataViews.Values) { UpdateDataVisibility(view); clusterViews.Add(view); }
            clusters?.Update(clusterViews);''')
    text=text.replace('"Region: X 0, Z 0"','"Tọa độ: X 0, Z 0"')
    write(p,text)
    p=ASSETS/'Game/MapSystem/UI/RegionDisplayPanel.cs'; write(p,p.read_text(encoding='utf8').replace('Region: X','Tọa độ: X'))
    p=ASSETS/'Game/UISystem/GameMenuController.cs'; replace(p,'                case GameInputMode.WorldMap:\n                    HandleMapBack();','                case GameInputMode.WorldMap:\n                    if (mapSystem != null && mapSystem.GetComponent<Capstone.Game.MapSystem.UI.WorldMapUIController>()?.TryCloseMarkerGroup() == true) return;\n                    HandleMapBack();')

    # New file metas (existing GUIDs are untouched).
    new_files=[* (ASSETS/'Game/CodexSystem').rglob('*.cs'), ASSETS/'Game/HudSystem/PetPortraitPreview.cs',ASSETS/'Game/UISystem/MenuKeyboardNavigation.cs',ASSETS/'Game/MapSystem/UI/MapMarkerClusters.cs']
    for p in new_files:
        meta=p.with_suffix('.cs.meta')
        if not meta.exists(): write(meta,f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    print('Added panel affordances, isolated portraits, map grouping and navigation.')

if __name__=='__main__': run()
