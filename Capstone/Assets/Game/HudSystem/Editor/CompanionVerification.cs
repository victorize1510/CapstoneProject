using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.CreatureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;
namespace Capstone.Game.HudSystem.Editor {
    public static class CompanionVerification {
        public const string Output=Capstone.Game.UISystem.QaArtifactPaths.Root + "companion-ui/";
        const string Request="Temp/VerifyCompanion.request",OwnPlay="CompanionVerification.OwnPlay";
        static bool running;
        const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        [MenuItem("Tools/Capstone/Advanced/Companion UI/Capture three tabs")]
        static void RequestCapture()=>Queue("after");
        [MenuItem("Tools/Capstone/Advanced/Companion UI/Validate interactions")]
        static void RequestValidation()=>Queue("test");

        const string VerifySwitch = "Tools/Capstone/Advanced/Companion UI/Verify switching pets in Skills (isolated)";
        static string SwitchReportFolder => Path.Combine(QaArtifactPaths.Root, "CompanionItems");
        [MenuItem(VerifySwitch, true)]
        static bool CanVerifyViewedPetSwitch() => !EditorApplication.isPlayingOrWillChangePlaymode && !running
            && SceneManager.GetActiveScene().name == DemoSceneOneSaveIsolation.CreatureQaSceneName
            && !SceneManager.GetActiveScene().isDirty && PrefabStageUtility.GetCurrentPrefabStage() == null;

        [MenuItem(VerifySwitch)]
        static void StartViewedPetSwitch() {
            var isolation = Object.FindFirstObjectByType<DemoSceneOneSaveIsolation>();
            if (isolation == null) throw new InvalidOperationException("Missing Creature QA save isolation.");
            string profile = Path.Combine(SwitchReportFolder, "profile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(profile);
            string slot = DemoSceneOneSaveIsolation.CreatureQaSlot;
            string original = new PlayerJsonFileSaveStore(isolation.developmentProfile).GetPath(slot);
            string copy = new PlayerJsonFileSaveStore(profile).GetPath(slot);
            foreach (string suffix in new[] { "", ".bak" })
                if (File.Exists(original + suffix)) File.Copy(original + suffix, copy + suffix);
            File.WriteAllText(Path.Combine(SwitchReportFolder, "companion-playmode.txt"), "Entering isolated companion verification.");
            SessionState.SetString(DemoSceneOneSaveIsolation.CompanionVerificationProfileKey, profile);
            EditorApplication.EnterPlaymode();
        }

        static void ViewedPetSwitchPlayMode(PlayModeStateChange state) {
            const string key = DemoSceneOneSaveIsolation.CompanionVerificationProfileKey;
            string profile = SessionState.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(profile)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) VerifyViewedPetSwitch();
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.EraseString(key);
            foreach (string file in Directory.GetFiles(profile)) File.Delete(file);
            Directory.Delete(profile);
        }

        static async void VerifyViewedPetSwitch() {
            running = true;
            checks.Clear();
            var inventory = Object.FindFirstObjectByType<MonsterInventoryController>();
            var menu = Object.FindFirstObjectByType<GameMenuController>();
            var provider = Object.FindFirstObjectByType<PetCommandHudProvider>();
            bool openedHere = false;
            try {
                var save = Object.FindFirstObjectByType<PlayerSaveController>();
                for (int i = 0; i < 50 && save != null && !save.IsCurrentSlotLoaded; i++) await Task.Delay(100);
                string activeProfile = SessionState.GetString(DemoSceneOneSaveIsolation.CompanionVerificationProfileKey, string.Empty);
                Check(save != null && string.Equals(Path.GetDirectoryName(save.SavePath), Path.GetFullPath(activeProfile),
                    StringComparison.OrdinalIgnoreCase) && save.IsCurrentSlotLoaded
                    && !save.IsSaveBlocked, "isolated QA profile loaded");
                Check(inventory != null && menu != null && provider != null, "scene has live companion UI");
                var party = inventory.GetComponent<PetPartyPanelController>();
                var skills = inventory.GetComponent<PetSkillsPanelController>();
                var root = inventory.GetComponent<UIDocument>().rootVisualElement;
                var candidates = Enumerable.Range(0, 6).Select(index => (index, pet: provider.GetPetControllerAt(index)))
                    .Where(entry => entry.pet != null)
                    .Select(entry => (entry.index, stats: entry.pet.GetComponentInChildren<PetHudRuntimeStats>(true)))
                    .Where(entry => entry.stats != null && entry.stats.GetSkills().Any(skill => skill.unlocked)).ToList();
                Check(candidates.Count >= 2, "party has two pets with usable skills");
                var first = candidates[0];
                var second = candidates.FirstOrDefault(entry => !entry.stats.GetSkills().Where(skill => skill.unlocked)
                    .Select(skill => skill.skillId).SequenceEqual(first.stats.GetSkills().Where(skill => skill.unlocked).Select(skill => skill.skillId)));
                Check(second.stats != null, "party has two different sets of equipped skills");
                var combatPet = provider.GetSelectedPetController();
                openedHere = !inventory.IsPetsSectionOpen;
                if (openedHere) menu.OpenPetsDirect();
                await Task.Delay(200);
                var cards = root.Q("pets-panel").Query<Button>(className: "pet-party-card").ToList();
                void View(int index) {
                    using var evt = ClickEvent.GetPooled();
                    evt.target = cards[index];
                    cards[index].SendEvent(evt);
                }
                void VerifySkills(PetHudRuntimeStats stats) {
                    var equipped = stats.GetSkills();
                    for (int i = 0; i < stats.EquippedSkillSlotCount; i++) {
                        string expected = i < equipped.Count && equipped[i].unlocked ? VietnameseUI.Skill(equipped[i].displayName) : "Trống";
                        Check(root.Q<Button>("skills-equipped-slot-" + (i + 1)).Q<Label>(className: "skills-equipped-name").text == expected,
                            stats.DisplayName + " equipped slot " + (i + 1));
                    }
                    var learned = stats.GetLearnedSkills().Where(skill => skill.unlocked).Select(skill => VietnameseUI.Skill(skill.displayName));
                    var rows = root.Query<SkillListRow>().ToList().Where(row => row.style.display.value != DisplayStyle.None);
                    Check(rows.Select(row => row.NameLabel.tooltip).SequenceEqual(learned), stats.DisplayName + " learned skills and order");
                }
                View(first.index);
                party.ShowCompanionTab(1);
                await Task.Delay(220);
                Check(skills.IsEmbedded, "Kỹ năng uses the embedded dialog");
                VerifySkills(first.stats);
                var edit = root.Q<Button>("skills-change-mode");
                var caption = edit.Q<Label>(className: "angular-button-caption");
                Click(edit);
                Click(root.Q<Button>("skills-equipped-slot-1"));
                Check(caption.text == "Hủy đổi", "first pet has a pending edit");
                View(second.index);
                VerifySkills(second.stats);
                Check(caption.text == "Đổi kỹ năng"
                    && root.Q<Label>("skills-tooltip-name").text == "Chi tiết kỹ năng",
                    "switch resets the previous pet's edit and inspector");
                View(first.index);
                VerifySkills(first.stats);
                Check(provider.GetSelectedPetController() == combatPet && party.CompanionTab == 1,
                    "switching viewed pets leaves combat selection and open tab intact");
                await Task.Delay(170);
                var displayed = first.stats.GetSkills()[0];
                Click(root.Q<Button>("skills-equipped-slot-1"));
                Check(root.Q("skills-tooltip-level") == null
                    && root.Q<Label>("skills-tooltip-power").text == SkillPresentation.Power(displayed)
                    && root.Q<Label>("skills-tooltip-mana").text == SkillPresentation.Mana(displayed)
                    && root.Q<Label>("skills-tooltip-cooldown").text == (displayed.cooldownSeconds > 0f ? $"{displayed.cooldownSeconds:0.#} giây" : "-")
                    && root.Q<Label>("skills-tooltip-description").text == VietnameseUI.SkillDescription(displayed.description),
                    "skill tooltip shows authored power, MP, cooldown and description without a skill level");
                party.ShowCompanionTab(2);
                await Task.Delay(220);
                string before = root.Q<Label>("pets-growth-level-summary").text;
                View(second.index);
                var preview = inventory.GetComponent<PetLevelUpService>()?.CreatePreview(provider.GetPetControllerAt(second.index));
                Check(preview != null && root.Q<Label>("pets-growth-level-summary").text.Contains("Cấp " + preview.CurrentLevel + " → "),
                    "growth panel refreshes for the newly viewed pet");
                if (first.stats.Level != second.stats.Level)
                    Check(root.Q<Label>("pets-growth-level-summary").text != before, "growth preview no longer shows the previous level");
                var levelService = inventory.GetComponent<PetLevelUpService>();
                var pet = provider.GetPetControllerAt(first.index);
                var growth = levelService.CreatePreview(pet);
                var beforeLevelUp = first.stats.CreateSaveData();
                Check(levelService.GrantBattleExperience(pet, growth.ExperienceNeeded, out var gained)
                    && gained.CurrentLevel == growth.CurrentLevel + 1
                    && first.stats.CreateSaveData().equippedSkillIds.SequenceEqual(beforeLevelUp.equippedSkillIds),
                    "existing Pet EXP advances the pet level and preserves equipped skill IDs");
                inventory.Close();
                await VerifySkillStorageAndEvolution(save, inventory);
            } catch (Exception error) {
                checks.Add(error.ToString());
                Debug.LogException(error);
            } finally {
                if (openedHere) inventory.Close();
                File.WriteAllLines(Path.Combine(SwitchReportFolder, "companion-playmode.txt"), checks);
                running = false;
                EditorApplication.ExitPlaymode();
            }
        }

        static async Task VerifySkillStorageAndEvolution(PlayerSaveController save, MonsterInventoryController inventory) {
            var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Game/CreatureSystem/Data/CreatureLibrary.asset");
            var bats = library.families.Single(family => family.definitionId == "bat");
            var roster = Object.FindFirstObjectByType<PetBoxRuntimeProvider>();
            var coordinator = Object.FindFirstObjectByType<PetCaptureCoordinator>();
            var bag = (MonsterInventoryAdapter)typeof(PlayerSaveController).GetField("inventoryAdapter", Any).GetValue(save);
            var ball = CaptureAssets.LoadBall();
            Check(roster != null && coordinator != null && roster.FindFirstEmptyBoxSlot() >= 0
                && ball != null && bag != null && bag.TryAddItemAtomic(ball, 1), "isolated capture and BOX fixtures are ready");
            int boxIndex = roster.FindFirstEmptyBoxSlot();
            var fixture = Object.Instantiate(bats.forms[0].wildPrefab, roster.PartyInput.transform.position + Vector3.forward * 20, Quaternion.identity);
            if (fixture.TryGetComponent<BasicCreatureEnemy>(out var brain)) brain.enabled = false;
            var target = fixture.GetComponent<WildPetCaptureTarget>();
            await Task.Delay(120);
            var stats = target.Stats;
            stats.SetIdentity(stats.DisplayName, 12);
            stats.UnlockSkillsUpToLevel(stats.Level, out _);
            string id = target.Metadata.PersistentId;
            var previous = stats.CreateSaveData();
            Check(previous.learnedSkillProgress.Any(skill => skill.skillId == "pulse.dark" && skill.unlocked),
                "Dơi Nhỏ learns Xung bóng tối at Pet level 12 before capture");
            var random = typeof(PetCaptureCoordinator).GetField("RandomSource", Any);
            var oldRandom = random.GetValue(coordinator);
            var parameters = new object[] { ball, bag, target, null, null };
            try {
                random.SetValue(coordinator, new AuditCaptureRandom());
                bool executed = (bool)typeof(PetCaptureCoordinator).GetMethod("TryCaptureTarget", Any).Invoke(coordinator, parameters);
                Check(executed && roster.Contains(target.Pet) && !target.IsWild, "captured the same Wild individual through the real transaction: " + parameters[4]);
            } finally {
                if (coordinator != null) random.SetValue(coordinator, oldRandom);
            }
            int partyIndex = Enumerable.Range(0, 6).Where(index => roster.GetPartyPet(index) == target.Pet).DefaultIfEmpty(-1).First();
            if (partyIndex >= 0) {
                bool moved = roster.TryMovePartyToBox(partyIndex, out string error);
                Check(moved, "captured pet moves from Party to BOX: " + error);
            }
            Check(roster.GetStoredPet(boxIndex) == target.Pet && save.SaveNow(), "same captured individual is saved in BOX");
            Check(save.LoadNow(), "reload isolated save containing the captured BOX pet");
            var stored = roster.GetStoredPet(boxIndex);
            var metadata = stored?.GetComponentInChildren<PetCollectionMetadata>(true);
            var storedStats = stored?.GetComponentInChildren<PetHudRuntimeStats>(true);
            var reloaded = storedStats?.CreateSaveData();
            Check(metadata != null && metadata.PersistentId == id && reloaded != null
                && previous.equippedSkillIds.SequenceEqual(reloaded.equippedSkillIds)
                && previous.learnedSkillProgress.Where(skill => skill.unlocked).All(old =>
                    reloaded.learnedSkillProgress.Any(skill => skill.skillId == old.skillId && skill.unlocked)),
                "Capture, BOX and reload preserve learned and equipped IDs");

            var evolution = Object.FindFirstObjectByType<PetEvolutionService>() ?? inventory.gameObject.AddComponent<PetEvolutionService>();
            evolution.Bind(bag);
            var preview = evolution.CreatePreview(stored);
            storedStats.SetIdentity(storedStats.DisplayName, Mathf.Max(storedStats.Level, preview.RequiredLevel));
            var beforeEvolution = storedStats.CreateSaveData();
            bool evolved = evolution.TryEvolve(stored, out string evolutionError);
            Check(evolved && metadata.CurrentFormId == bats.forms[1].formId, "captured pet evolves through current gameplay service: " + evolutionError);
            var evolvedSkills = storedStats.CreateSaveData();
            Check(beforeEvolution.equippedSkillIds.SequenceEqual(evolvedSkills.equippedSkillIds)
                && beforeEvolution.learnedSkillProgress.Where(skill => skill.unlocked).All(old =>
                    evolvedSkills.learnedSkillProgress.Any(skill => skill.skillId == old.skillId && skill.unlocked)),
                "evolution keeps each learned skill and equipped slot");
            var disk = PlayerSaveMigration.Read(File.ReadAllText(save.SavePath), out _);
            var saved = disk.pets.petStates.Single(state => state.petId == id);
            Check(saved.customization.currentFormId == bats.forms[1].formId
                && saved.runtimeStats.equippedSkillIds.SequenceEqual(evolvedSkills.equippedSkillIds)
                && saved.runtimeStats.learnedSkillProgress.Any(skill => skill.skillId == "pulse.dark" && skill.unlocked),
                "evolved form and learned/equipped skill IDs persist to the isolated save");
        }

        sealed class AuditCaptureRandom : ICaptureRandom {
            public double NextFloat01() => 0;
        }

        static void Queue(string command){
            if(running||File.Exists(Request)||File.Exists("Temp/ProfileCompanion.request")){Debug.LogWarning("Companion QA is already queued or running.");return;}
            Directory.CreateDirectory("Temp");File.WriteAllText(Request,command);
        }
        [InitializeOnLoadMethod] static void Init(){
            EditorApplication.update-=Poll;EditorApplication.update+=Poll;
            EditorApplication.playModeStateChanged-=ViewedPetSwitchPlayMode;
            EditorApplication.playModeStateChanged+=ViewedPetSwitchPlayMode;
        }
        static void Poll(){
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            if(!EditorApplication.isPlaying){if(EditorApplication.isPlayingOrWillChangePlaymode)return;running=true;try{EnsureAssets();SessionState.SetBool(OwnPlay,true);EditorApplication.EnterPlaymode();}finally{running=false;}return;}
            if(Object.FindFirstObjectByType<GameMenuController>()==null)return;
            string command=File.ReadAllText(Request).Trim();File.Delete(Request);if(command=="test")Test();else Capture(command);
        }
        static readonly List<string> checks=new List<string>();
        static void Check(bool condition,string message){checks.Add((condition?"PASS ":"FAIL ")+message);if(!condition)throw new Exception(message);}
        static void Click(Button button){Check(button!=null&&button.enabledInHierarchy,"enabled button "+button?.name);typeof(Clickable).GetMethod("Invoke",Any).Invoke(button.clickable,new object[]{null});}
        [Serializable]sealed class BoundsEntry{public string id;public float x,y,width,height;}
        [Serializable]sealed class BoundsDocument{public int width,height;public BoundsEntry[] boxes;}
        static void SaveBounds(UIDocument document,string suffix){
            var root=document.rootVisualElement;var panel=root.Q("pets-panel");float sx=Screen.width/root.worldBound.width,sy=Screen.height/root.worldBound.height;
            string[] names={"companion-header","companion-hero","companion-tabs","companion-inspector","companion-dock","pets-swap-button","companion-resources","companion-stats","companion-skill-summary","companion-actions"};
            var entries=new List<BoundsEntry>();foreach(var id in names){var e=panel.Q(id);if(e==null)continue;var r=e.worldBound;entries.Add(new BoundsEntry{id=id,x=(r.x-root.worldBound.x)*sx,y=(r.y-root.worldBound.y)*sy,width=r.width*sx,height=r.height*sy});}
            foreach(var e in panel.Query<Button>(className:"pet-party-card").ToList()){var r=e.worldBound;entries.Add(new BoundsEntry{id=e.name,x=r.x*sx,y=r.y*sy,width=r.width*sx,height=r.height*sy});}
            File.WriteAllText(Output+"bounds-"+suffix+".json",JsonUtility.ToJson(new BoundsDocument{width=Screen.width,height=Screen.height,boxes=entries.ToArray()},true));
        }
        static async void Test(){
            running=true;checks.Clear();Directory.CreateDirectory(Output);
            var menu=Object.FindFirstObjectByType<GameMenuController>();var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();
            var party=inventory.GetComponent<PetPartyPanelController>();var provider=Object.FindFirstObjectByType<PetCommandHudProvider>();var input=Object.FindFirstObjectByType<PetCommandInput>();
            var doc=inventory.GetComponent<UIDocument>();var ui=doc.rootVisualElement;
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",Any);int previous=(int)selected.GetValue(view);
            var originalPet=provider.GetSelectedPetController();var labelObject=new GameObject("CompanionHUDVisibilityFixture",typeof(TextMesh));var worldRenderer=labelObject.GetComponent<Renderer>();
            try{
                await Task.Delay(1200);menu.CloseMenu();inventory.Close();await Task.Delay(300);
                bool priorCursor=Cursor.visible;var priorLock=Cursor.lockState;
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,selected,size);view.Show();view.Focus();await Task.Delay(600);menu.OpenPetsDirect();await Task.Delay(500);
                    Check(inventory.IsPetsSectionOpen&&InventoryInputController.GameplayInputBlocked,"party owns input at "+size);
                    Check(!party.IsPreview3D&&!party.HasPreviewTexture,"default 2D without renderer "+size);
                    Check(ui.Q<Label>("pets-selected-portrait-fallback").resolvedStyle.display==DisplayStyle.Flex,"missing large art stays 2D "+size);
                    Check(worldRenderer.forceRenderingOff,"world HUD labels suppressed "+size);
                    var panel=ui.Q("pets-panel");var cards=panel.Query<Button>(className:"pet-party-card").ToList();Check(cards.Count==6,"six dock slots "+size);
                    SaveBounds(doc,size.x+"x"+size.y);
                    foreach(var id in new[]{"pets-tab-overview","pets-tab-skills","pets-tab-growth","pets-back-button","pets-level-up-button","pets-heal-button","pets-swap-button"}){
                        var button=ui.Q<Button>(id);var bounds=button.worldBound;
                        Check(bounds.width>0&&bounds.height>0&&ui.worldBound.Contains(bounds.min)&&ui.worldBound.Contains(bounds.max),"visible hitbox "+id+" "+size);
                        var picked=button.panel.Pick(bounds.center);Check(picked==button||picked?.GetFirstAncestorOfType<Button>()==button,"native panel Pick reaches "+id+" "+size);
                    }
                    var stats=originalPet.GetComponentInChildren<PetCollectionMetadata>(true).CurrentStats;
                    Check(ui.Q<Label>("pets-stat-health").text==stats.Hp.ToString()&&ui.Q<Label>("pets-stat-magic-attack").text==stats.MagicAttack.ToString()&&ui.Q<Label>("pets-stat-magic-defense").text==stats.MagicDefense.ToString(),"real stat values, including HP stat not current HP "+size);
                    foreach(var key in new[]{"health","attack","magic-attack","defense","magic-defense","speed","critical-rate","critical-damage"})Check(ui.Q<Label>("pets-stat-"+key)!=null,"stat binding "+key+" "+size);
                    Click(ui.Q<Button>("pets-tab-skills"));await Task.Delay(250);
                    var skills=inventory.GetComponent<PetSkillsPanelController>();Check(party.CompanionTab==1&&skills.IsEmbedded&&!skills.IsOpen,"single embedded skill controller "+size);
                    int count=provider.GetEquippedSkillSlotCount();for(int i=0;i<4;i++){var slot=ui.Q<Button>("skills-equipped-slot-"+(i+1));Check(slot.resolvedStyle.display==DisplayStyle.Flex&&slot.enabledInHierarchy==(i<count),"provider-authorized equipped slot "+i+" "+size);}
                    var learned=ui.Query<Button>(className:"skills-learned-card").ToList();if(learned.Count>0){Click(learned[0]);await Task.Delay(200);Check(ui.Q<Label>("skills-tooltip-name").text!="Chi tiết kỹ năng","learned selection populates inspector "+size);}
                    Click(ui.Q<Button>("pets-tab-growth"));await Task.Delay(250);Check(party.CompanionTab==2&&!skills.IsEmbedded,"growth detaches skill dialog "+size);
                    Check(ui.Q<Label>("pets-growth-level-summary").text.Contains("Lv."),"growth is service-backed preview "+size);
                    foreach(var pair in new[]{new[]{"pets-growth-level","level-up-modal-overlay"},new[]{"pets-growth-heal","heal-modal-overlay"}}){
                        Click(ui.Q<Button>(pair[0]));await Task.Delay(300);Check(ui.Q(pair[1]).resolvedStyle.display!=DisplayStyle.None,"existing preview popup "+pair[1]+" "+size);
                        ScreenCapture.CaptureScreenshot(Output+"popup-"+pair[1]+"-"+size.x+"x"+size.y+".png");await Task.Delay(200);menu.HandleShortcut(KeyCode.Escape);await Task.Delay(250);Check(inventory.IsPetsSectionOpen&&party.CompanionTab==2,"Esc cancels popup and keeps tab/pet "+size);
                    }
                    Click(ui.Q<Button>("pets-tab-overview"));await Task.Delay(200);Click(ui.Q<Button>("pets-rename-button"));await Task.Delay(220);Check(party.IsRenameOpen,"rename uses existing modal "+size);
                    var field=ui.Q<TextField>("pets-rename-input");Check(field.maxLength==16,"rename 16-character validation "+size);field.value="   ";await Task.Delay(180);Check(!ui.Q<Button>("pets-rename-save").enabledInHierarchy,"invalid rename cannot confirm "+size);menu.HandleShortcut(KeyCode.Escape);await Task.Delay(250);Check(!party.IsRenameOpen&&inventory.IsPetsSectionOpen,"rename cancel restores party "+size);
                    Click(ui.Q<Button>("pets-swap-button"));await Task.Delay(200);Check(panel.Query<Button>(className:"is-swap-source").ToList().Count==1,"swap source is distinct marker "+size);Click(ui.Q<Button>("pets-swap-button"));await Task.Delay(200);Check(panel.Query<Button>(className:"is-swap-source").ToList().Count==0,"swap cancel "+size);
                    var summonBefore=provider.GetPetSlots().Select(s=>s.summoned).ToArray();var other=provider.GetPetControllerAt(1);
                    if(other!=null){input.SetActivePet(other);provider.NotifyHudDataChanged();await Task.Delay(250);Check(provider.GetPetSlots()[1].selected&&summonBefore.SequenceEqual(provider.GetPetSlots().Select(s=>s.summoned)),"viewing different pet preserves summoned flags (read-only input/provider notification) "+size);input.SetActivePet(originalPet);provider.NotifyHudDataChanged();await Task.Delay(200);}
                    for(int cycle=0;cycle<3;cycle++){Click(ui.Q<Button>("pets-mode-3d"));await Task.Delay(350);Check(party.IsPreview3D&&party.HasPreviewTexture,"3D stage allocated only on request "+cycle+" "+size);if(cycle==0){ScreenCapture.CaptureScreenshot(Output+"preview-3d-"+size.x+"x"+size.y+".png");await Task.Delay(180);}Click(ui.Q<Button>("pets-mode-2d"));await Task.Delay(250);Check(!party.HasPreviewTexture&&Resources.FindObjectsOfTypeAll<GameObject>().Count(g=>g.name=="PetPortraitRenderStage"&&g.activeInHierarchy)==0,"3D stage released "+cycle+" "+size);}
                    Click(ui.Q<Button>("pets-details-button"));await Task.Delay(200);Check(party.IsDetailsOpen,"existing details popup "+size);menu.HandleShortcut(KeyCode.Escape);await Task.Delay(200);
                    var name=ui.Q<Label>("pets-selected-name");string originalName=name.text;name.text="Tên thú cực kỳ dài kiểm tra ellipsis và vùng thao tác";await Task.Delay(100);Check(name.resolvedStyle.textOverflow==TextOverflow.Ellipsis,"long name ellipsis "+size);ScreenCapture.CaptureScreenshot(Output+"long-name-"+size.x+"x"+size.y+".png");await Task.Delay(200);name.text=originalName;
                    var providerField=typeof(PetPartyPanelController).GetField("petHudProvider",Any);var autoField=typeof(PetPartyPanelController).GetField("autoFindProvider",Any);
                    var savedProvider=providerField.GetValue(party);var savedAuto=autoField.GetValue(party);GameObject fixture=null;
                    try{
                        autoField.SetValue(party,false);providerField.SetValue(party,null);party.Refresh();await Task.Delay(180);
                        Check(ui.Q<Label>("pets-stat-health").text=="—"&&ui.Q<Label>("pets-health-value").text=="— / —","missing provider shows unknown values "+size);
                        foreach(var id in new[]{"pets-level-up-button","pets-heal-button","pets-mode-3d","pets-swap-button"})Check(!ui.Q<Button>(id).enabledInHierarchy,"missing provider disables "+id+" "+size);
                        fixture=new GameObject("CompanionEmptyRosterFixture");var placeholder=fixture.AddComponent<PlaceholderPetHudProvider>();placeholder.enabled=false;
                        typeof(PlaceholderPetHudProvider).GetField("petSlots",Any).SetValue(placeholder,new List<PetSlotHudData>(new PetSlotHudData[6]));
                        providerField.SetValue(party,placeholder);party.Refresh();await Task.Delay(180);
                        Check(panel.Query<Button>(className:"pet-party-card").ToList().Count==6&&panel.Query<Button>(className:"is-selected").ToList().All(b=>!b.ClassListContains("pet-party-card")),"empty roster keeps six unselected dock cells "+size);
                        Check(ui.Q<Label>("pets-selected-name").text=="Chọn một thú","empty roster has readable empty state "+size);
                        Check(ui.Q("pets-health-fill").resolvedStyle.width<1&&panel.Query<VisualElement>(className:"pet-skill-card").ToList().All(e=>e.resolvedStyle.display==DisplayStyle.None),"empty roster clears HP and skill summary even if provider supplies residual values "+size);
                        ScreenCapture.CaptureScreenshot(Output+"empty-roster-"+size.x+"x"+size.y+".png");await Task.Delay(180);
                    }finally{providerField.SetValue(party,savedProvider);autoField.SetValue(party,savedAuto);party.Refresh();if(fixture!=null)Object.Destroy(fixture);}
                    Click(ui.Q<Button>("pets-back-button"));await Task.Delay(300);Check(!inventory.IsPetsSectionOpen&&!party.HasPreviewTexture&&!worldRenderer.forceRenderingOff,"Back releases preview/HUD "+size);Check(Cursor.visible==priorCursor&&Cursor.lockState==priorLock,"cursor restored "+size);
                }
                checks.Add("NOTE Positive resource-consuming confirmations, persistent rename/favorite/swap and equip save were not executed on user data.");
            }catch(Exception e){checks.Add("ERROR "+e);Debug.LogException(e);}
            finally{if(input!=null&&originalPet!=null){input.SetActivePet(originalPet);provider.NotifyHudDataChanged();}inventory.Close();Object.Destroy(labelObject);selected.SetValue(view,previous);File.WriteAllLines(Output+"validation.txt",checks);running=false;if(SessionState.GetBool(OwnPlay,false)){SessionState.SetBool(OwnPlay,false);EditorApplication.ExitPlaymode();}}
        }
        static void EnsureAssets(){
            foreach(string path in Directory.GetFiles(CompanionAssetImporter.Root,"*.png",SearchOption.AllDirectories))if(((TextureImporter)AssetImporter.GetAtPath(path)).textureType!=TextureImporterType.Sprite)AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            string catalog=CompanionAssetImporter.Root+"Portraits.asset";
            if(!File.Exists(catalog))AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PetCompanionArtCatalog>(),catalog);
        }
        static async void Capture(string stage){
            running=true;Directory.CreateDirectory(Output);var log=new List<string>();
            var menu=Object.FindFirstObjectByType<GameMenuController>();
            var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);
            var selected=type.GetProperty("selectedSizeIndex",Any);int previous=(int)selected.GetValue(view);
            try{
                await Task.Delay(1200); menu.CloseMenu(); inventory.Close();
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,selected,size);view.Show();view.Focus();await Task.Delay(700);
                    menu.OpenPetsDirect();await Task.Delay(700);
                    if(Screen.width!=size.x||Screen.height!=size.y)throw new Exception("Wrong Game view size "+Screen.width+"x"+Screen.height);
                    ScreenCapture.CaptureScreenshot(Output+stage+"-overview-"+size.x+"x"+size.y+".png");await Task.Delay(400);
                    if(stage!="before"){
                        var party=inventory.GetComponent<PetPartyPanelController>();
                        foreach(int tab in new[]{1,2}){party.ShowCompanionTab(tab);await Task.Delay(500);ScreenCapture.CaptureScreenshot(Output+stage+"-"+(tab==1?"skills":"growth")+"-"+size.x+"x"+size.y+".png");await Task.Delay(400);}
                    }
                    log.Add("PASS actual Game view "+size+" stage="+stage);inventory.Close();await Task.Delay(200);
                }
            }catch(Exception e){log.Add("ERROR "+e);Debug.LogException(e);}
            finally{inventory?.Close();selected.SetValue(view,previous);File.WriteAllLines(Output+stage+"-capture.txt",log);running=false;
                if(SessionState.GetBool(OwnPlay,false)){SessionState.SetBool(OwnPlay,false);EditorApplication.ExitPlaymode();}}
        }
    }
}
