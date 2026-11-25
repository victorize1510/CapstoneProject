using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.CaptureSystem;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.QuestSystem {
    [DisallowMultipleComponent]
    internal sealed class ChapterQuestFlow : MonoBehaviour {
        internal const string GameplayScene = "DemoScene 3";
        internal const string ChapterId = "chapter_01";
        const string ElderId = "npc_village_elder";
        const string FinalQuestId = "main_quest_09";

        [Header("Nút hội thoại Trưởng làng")]
        [SerializeField] string questLabel = "Nhiệm vụ";
        [SerializeField] string viewQuestLabel = "Xem nhiệm vụ";
        [SerializeField] string acceptLabel = "Nhận nhiệm vụ";
        [SerializeField] string turnInLabel = "Trả nhiệm vụ";
        [SerializeField] string leaveLabel = "Rời đi";

        QuestManager quests;
        MonsterInventoryAdapter inventory;
        PlayerSaveController saves;
        QuestRewardService rewards;
        PetBoxRuntimeProvider roster;
        PetCommandInput player;
        ChapterWorldLayout world;
        VillageChiefCutscene chief;
        DialogueBoxUI dialogue;
        TextMesh grassMarker;
        bool conversation, refreshing;
        float nextWorldCheck;
        static ChapterQuestFlow live;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => live = null;

        void Start() {
            quests = GetComponent<QuestManager>();
            inventory = FindFirstObjectByType<MonsterInventoryAdapter>(FindObjectsInactive.Include);
            roster = FindFirstObjectByType<PetBoxRuntimeProvider>(FindObjectsInactive.Include);
            saves = FindFirstObjectByType<PlayerSaveController>(FindObjectsInactive.Include);
            player = roster != null ? roster.PartyInput : FindFirstObjectByType<PetCommandInput>();
            rewards = FindFirstObjectByType<QuestRewardService>(FindObjectsInactive.Include);
            chief = FindFirstObjectByType<VillageChiefCutscene>(FindObjectsInactive.Include);
            world = FindFirstObjectByType<ChapterWorldLayout>();
            live = this;
            if (inventory != null) inventory.InventoryChanged += Refresh;
            if (roster != null) roster.Changed += Refresh;
            quests.QuestsChanged += Refresh;
            if (world != null) {
                grassMarker = CreateElderMarker(world.GrassElder);
            }
            Refresh();
        }

        static TextMesh CreateElderMarker(Transform elder) {
            if (elder == null) return null;
            var marker = new GameObject("Chapter I NPC Indicator").AddComponent<TextMesh>();
            marker.transform.SetParent(elder, false);
            marker.transform.position = elder.position + Vector3.up * 2.15f;
            marker.characterSize = .13f;
            marker.fontSize = 64;
            marker.anchor = TextAnchor.MiddleCenter;
            Capstone.Game.WorldFeedback.CameraActorOccluder.Ensure(elder.gameObject);
            return marker;
        }

        void OnDestroy() {
            if (inventory != null) inventory.InventoryChanged -= Refresh;
            if (roster != null) roster.Changed -= Refresh;
            if (quests != null) quests.QuestsChanged -= Refresh;
            if (live == this) live = null;
            ReleaseDialogue();
        }

        void OnDisable() {
            StopAllCoroutines();
            DoorPrompt.Hide(this);
            ReleaseDialogue();
        }

        static QuestProgressEvent CreatureEvent(QuestObjectiveType type, DummyEnemy enemy, PetCollectionMetadata pet) =>
            new QuestProgressEvent(type, type == QuestObjectiveType.DefeatBoss ? "chapter_01_boss" : "",
                targetTypeId: pet != null ? pet.DefinitionId : "", regionId: enemy.QuestZoneId,
                formId: pet != null ? pet.CurrentFormId : "", sceneId: enemy.gameObject.scene.name,
                receiptId: type == QuestObjectiveType.CaptureCreature ? pet.PersistentId : enemy.EncounterId,
                startedAtNight: enemy.QuestEncounterStartedAtNight == true);

        internal static void ReportCapture(QuestManager manager, DummyEnemy enemy, PetCollectionMetadata pet) {
            if (manager?.Chapter == null || enemy == null || pet == null || string.IsNullOrEmpty(enemy.QuestZoneId)) return;
            manager.ReportProgress(CreatureEvent(QuestObjectiveType.CaptureCreature, enemy, pet));
        }

        internal static void ReportDefeat(DummyEnemy enemy, GameObject source) {
            if (live == null || enemy == null || source == null || enemy.IsTrainingDummy ||
                !source.TryGetComponent<PetController>(out var pet) || pet.IsWildEncounter ||
                live.player == null || pet.owner != live.player.transform) return;
            if (enemy.IsBoss) {
                if (live.world != null && live.world.Boss == enemy)
                    live.quests.ReportProgress(CreatureEvent(QuestObjectiveType.DefeatBoss, enemy, null));
                return;
            }
            if (!string.IsNullOrEmpty(enemy.QuestZoneId) && enemy.TryGetComponent<PetCollectionMetadata>(out var metadata))
                live.quests.ReportProgress(CreatureEvent(QuestObjectiveType.DefeatCreature, enemy, metadata));
        }

        bool IsChapter(QuestDefinition definition) => definition != null && definition.ChapterId == ChapterId
            && definition.QuestGiverId == ElderId;

        QuestRuntimeState Active() => quests.GetActiveQuests().FirstOrDefault(state => IsChapter(state.Definition));

        QuestDefinition Available() => quests.GetQuestDefinitions()
            .FirstOrDefault(def => IsChapter(def) && quests.GetQuestAvailability(def.QuestId) == QuestAvailabilityState.Available);

        void Refresh() {
            if (refreshing || quests == null) return;
            refreshing = true;
            try {
                var state = Active();
                if (state != null && inventory != null)
                    foreach (var objective in state.Definition.Objectives)
                        if (objective.ObjectiveType == QuestObjectiveType.CollectItem)
                            quests.RefreshChapterObjective(state, objective, Quantity(objective.TargetId));
                if (grassMarker != null) {
                    bool ready = state != null && IsReady(state);
                    string symbol = state != null ? "?" : Available() != null ? "!" : "";
                    Color color = state == null || ready ? new Color(1f, .79f, .29f) : new Color(.62f, .66f, .72f);
                    SetMarker(grassMarker, symbol, color);
                }
            } finally { refreshing = false; }
        }

        static void SetMarker(TextMesh marker, string symbol, Color color) {
            if (marker == null) return;
            marker.text = symbol;
            marker.color = color;
        }

        int Quantity(string id) {
            var item = InventoryDefinitionCatalog.Find(id);
            return item != null && inventory != null ? inventory.GetQuantity(item) : 0;
        }

        internal bool IsReady(QuestRuntimeState state) {
            foreach (var objective in state.Definition.Objectives) {
                switch (objective.ObjectiveType) {
                    case QuestObjectiveType.GiveItem:
                        if (!HasMaterials(objective)) return false;
                        break;
                    case QuestObjectiveType.DeliverPet:
                        if (EligiblePets(state, objective).Count < objective.RequiredAmount) return false;
                        break;
                    default:
                        if (state.GetObjective(objective.ObjectiveId)?.IsComplete != true) return false;
                        break;
                }
            }
            return true;
        }

        static IEnumerable<(string id, int count)> Materials(QuestObjectiveDefinition objective) {
            int count = objective.RequiredAmount / (objective.AlternateItemIds.Count + 1);
            yield return (objective.TargetId, count);
            foreach (var id in objective.AlternateItemIds) yield return (id, count);
        }

        bool HasMaterials(QuestObjectiveDefinition objective) => Materials(objective).All(item => Quantity(item.id) >= item.count);

        List<PetController> EligiblePets(QuestRuntimeState state, QuestObjectiveDefinition delivery) {
            if (roster == null || player == null) return new List<PetController>();
            var receipts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var objective in state.Definition.Objectives)
                if (objective.ObjectiveType == QuestObjectiveType.CaptureCreature)
                    receipts.UnionWith(state.GetObjective(objective.ObjectiveId).Receipts);
            return roster.StoredPets.Concat(player.petSlots).Where(pet => pet != null && !pet.IsSummoned &&
                !pet.IsDefeatPresentation && !pet.HasCombatTarget && pet.TryGetComponent<PetCollectionMetadata>(out var info) &&
                !info.IsReleased && !info.IsFavorite && receipts.Contains(info.PersistentId) &&
                delivery.MatchesCreature(info.DefinitionId, info.CurrentFormId)).Distinct().ToList();
        }

        internal bool TryGetTarget(QuestRuntimeState state, out QuestTargetInfo target) {
            target = default;
            if (player == null || world == null) return false;
            var elder = world.GrassElder;
            if (IsReady(state) && elder != null && elder.gameObject.activeInHierarchy) {
                return world.PointForMap(player.transform.position, elder.position, false, state.QuestId, "",
                    "Gặp Trưởng làng để trả nhiệm vụ", out target);
            }
            foreach (var objective in state.Definition.Objectives) {
                if (objective.IsHandover || state.GetObjective(objective.ObjectiveId)?.IsComplete == true) continue;
                if (world.TryGetPoint(objective, player.transform.position, out var position, out bool cave, out string zone))
                    return world.PointForMap(player.transform.position, position, cave, state.QuestId, objective.ObjectiveId,
                        zone != null ? $"{zone} · {objective.Title}" : objective.Title, out target);
            }
            if (world.TryGetHuntingPoint(state.Definition, player.transform.position, out var hunt, out bool inCave, out string huntingZone))
                return world.PointForMap(player.transform.position, hunt, inCave, state.QuestId, "",
                    huntingZone != null ? $"{huntingZone} · Tìm thêm thú để giao" : "Tìm thêm thú để giao", out target);
            return false;
        }

        void Update() {
            if (quests == null || player == null) return;
            var camera = Camera.main;
            if (camera != null) {
                FaceMarker(grassMarker, camera.transform.position);
            }
            if (Time.unscaledTime >= nextWorldCheck) {
                nextWorldCheck = Time.unscaledTime + .25f;
                var active = Active();
                if (active != null && world != null) world.ReportLocations(quests, active, player.transform.position);
            }
            var elder = world != null ? world.GrassElder : null;
            var guide = world != null ? world.TravelGuideFor(player.transform.position) : null;
            bool nearGuide = WorldInteractionView.CanInteract(player.transform, guide, ChapterWorldLayout.ElderTalkDistance);
            bool nearChief = WorldInteractionView.CanInteract(player.transform, elder, ChapterWorldLayout.ElderTalkDistance);
            bool guideSelected = nearGuide && (!nearChief ||
                (guide.position - player.transform.position).sqrMagnitude < (elder.position - player.transform.position).sqrMagnitude);
            var actor = guideSelected ? guide : elder;
            bool near = !conversation && !InventoryInputController.GameplayInputBlocked &&
                (nearGuide || nearChief);
            if (near && Capstone.Game.SettingsSystem.GameSettingsService.InteractionPromptsVisible)
                DoorPrompt.Show(this, actor.position + Vector3.up * 1.6f,
                    guideSelected ? "[E] Nói chuyện với người dẫn đường" : "[E] Nói chuyện với Trưởng làng");
            else DoorPrompt.Hide(this);
            if (near && GameplayKeyboard.GetKeyDown(KeyCode.E) && WorldInteractionView.TryConsumePress())
                StartCoroutine(Talk(actor, guideSelected));
        }

        static void FaceMarker(TextMesh marker, Vector3 cameraPosition) {
            if (marker == null || marker.text.Length == 0) return;
            Vector3 facing = marker.transform.position - cameraPosition;
            if (facing.sqrMagnitude > .001f) marker.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }

        DialogueBoxUI GetDialogue() {
            if (dialogue != null) return dialogue;
            dialogue = FindFirstObjectByType<DialogueBoxUI>(FindObjectsInactive.Include);
            if (dialogue != null && dialogue.IsBuilt) {
                dialogue.Build(dialogue.transform);
                return dialogue;
            }
            if (dialogue != null && dialogue.GetComponentInParent<Canvas>() is Canvas existing && existing.gameObject.activeInHierarchy) {
                dialogue.Build(existing.transform);
                return dialogue;
            }
            var canvas = new GameObject("Chapter I Dialogue", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            var output = canvas.GetComponent<Canvas>();
            output.renderMode = RenderMode.ScreenSpaceOverlay;
            output.sortingOrder = 110;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            dialogue = canvas.AddComponent<DialogueBoxUI>();
            dialogue.Build(canvas.transform);
            return dialogue;
        }

        IEnumerator Talk(Transform elder, bool isGuide) {
            conversation = true;
            DoorPrompt.Hide(this);
            InventoryInputController.SetExternalGameplayInputBlocked(this, true);
            bool travel = false;
            try {
                var box = GetDialogue();
                Refresh();
                yield return box.Show(isGuide ? "Người dẫn đường" : "Trưởng làng",
                    !isGuide && chief != null ? chief.ChiefPortrait : null);
                bool fromCave = elder == world.CaveElder;
                if (isGuide && world.CanTravelFrom(elder)) {
                    yield return box.TypeLine(fromCave ? "Bạn muốn trở về đồng cỏ?" : "Bạn muốn vào hang động?", 58, false);
                    yield return box.ChooseCaveTravel(fromCave, accepted => travel = accepted);
                } else if (isGuide) {
                    yield return box.TypeLine("Lối đi đang tạm đóng.", 58);
                } else {
                    yield return box.TypeLine("Cháu cần ta giúp gì?", 58, false);
                    int choice = -1;
                    yield return box.Choose(new[] { QuestOption(), leaveLabel }, result => choice = result);
                    if (choice == 0) yield return TalkAboutQuest(box);
                }
                yield return box.Hide();
            } finally { ReleaseDialogue(); }
            if (travel && !world.TryTravelFromElder(elder)) StartCoroutine(ShowTravelUnavailable());
        }

        IEnumerator ShowTravelUnavailable() {
            conversation = true;
            InventoryInputController.SetExternalGameplayInputBlocked(this, true);
            try {
                var box = GetDialogue();
                yield return box.Show("Người dẫn đường", null);
                yield return box.TypeLine("Lối đi đang bị chặn. Cháu hãy quay lại sau nhé.", 52);
                yield return box.Hide();
            } finally { ReleaseDialogue(); }
        }

        string QuestOption() {
            var state = Active();
            if (state != null) return IsReady(state) ? turnInLabel : viewQuestLabel;
            return quests.TutorialComplete && Available() != null ? acceptLabel : questLabel;
        }

        IEnumerator TalkAboutQuest(DialogueBoxUI box) {
            if (!quests.TutorialComplete) {
                yield return box.TypeLine("Hãy hoàn thành phần hướng dẫn và chọn thú đồng hành đầu tiên. Sau đó ta sẽ giao việc cho cháu.", 48);
                yield break;
            }
            var state = Active();
            if (state != null) {
                if (IsReady(state)) yield return TurnIn(box, state);
                else yield return Lines(box, state.Definition.ProgressDialogue);
            } else if (Available() is QuestDefinition next) yield return OfferQuest(box, next);
            else {
                string text = quests.IsQuestCompleted(FinalQuestId)
                    ? "Nhờ có cháu mà ngôi làng đã trở lại bình yên. Ta hy vọng một ngày nào đó sẽ được nghe thêm những câu chuyện về hành trình của cháu."
                    : "Hãy luôn cẩn thận khi khám phá thế giới bên ngoài ngôi làng.";
                yield return box.TypeLine(text, 42);
            }
        }

        IEnumerator OfferQuest(DialogueBoxUI box, QuestDefinition definition) {
            yield return box.TypeLine(definition.Title, 65);
            yield return Lines(box, definition.AcceptDialogue, false);
            int choice = -1;
            yield return box.Choose(new[] { acceptLabel, "Để sau" }, result => choice = result);
            if (choice != 0) yield return box.TypeLine("Không sao. Khi nào chuẩn bị xong, hãy quay lại gặp ta.", 42);
            else if (!TryAccept(definition)) yield return box.TypeLine("Chưa thể nhận nhiệm vụ. Hãy kiểm tra chỗ trống trong túi và bản lưu.", 42);
        }

        static IEnumerator Lines(DialogueBoxUI box, string[] lines, bool waitForLast = true) {
            if (lines == null) yield break;
            for (int i = 0; i < lines.Length; i++)
                yield return box.TypeLine(lines[i], 42, waitForLast || i < lines.Length - 1);
        }

        void ReleaseDialogue() {
            if (!conversation) return;
            conversation = false;
            if (dialogue != null) dialogue.HideImmediate();
            InventoryInputController.SetExternalGameplayInputBlocked(this, false);
        }

        IEnumerator TurnIn(DialogueBoxUI box, QuestRuntimeState active) {
            var selected = new List<PetController>();
            var delivery = active.Definition.Objectives.FirstOrDefault(objective => objective.ObjectiveType == QuestObjectiveType.DeliverPet);
            if (delivery != null && roster != null) {
                var candidates = EligiblePets(active, delivery);
                int page = 0;
                while (selected.Count < delivery.RequiredAmount) {
                    const int pageSize = 3;
                    var remaining = candidates.Except(selected).ToList();
                    if (remaining.Count == 0) yield break;
                    page = Mathf.Clamp(page, 0, (remaining.Count - 1) / pageSize);
                    var displayed = remaining.Skip(page * pageSize).Take(pageSize).ToArray();
                    var options = displayed.Select(PetLabel).ToList();
                    int next = options.Count;
                    if (remaining.Count > pageSize) options.Add($"Trang kế · {page + 1}/{(remaining.Count + pageSize - 1) / pageSize}");
                    options.Add("Hủy giao thú");
                    yield return box.TypeLine($"{active.Definition.Title}\n{delivery.Title} ({selected.Count}/{delivery.RequiredAmount})", 110, false);
                    int choice = -1;
                    yield return box.Choose(options.ToArray(), value => choice = value);
                    if (choice < 0 || choice == options.Count - 1) yield break;
                    if (choice == next) { page = (page + 1) % ((remaining.Count + pageSize - 1) / pageSize); continue; }
                    selected.Add(displayed[choice]);
                }
            }
            string confirmation = selected.Count > 0 ? $"Giao {selected.Count} thú đã chọn cho Trưởng làng. Các thú này sẽ rời bộ sưu tập."
                : "Cháu đã hoàn thành các mục tiêu.";
            yield return box.TypeLine($"{active.Definition.Title}\n{confirmation}", 75, false);
            int confirm = -1;
            yield return box.Choose(new[] { turnInLabel, "Để sau" }, result => confirm = result, 1);
            if (confirm != 0) yield break;
            PetElement starterChoice = PetElement.Unknown;
            if (active.QuestId == "main_quest_06" && quests.StarterElement == PetElement.Unknown) {
                yield return box.TypeLine("Thú khởi đầu của cháu thuộc hệ chính nào?", 75, false);
                int choice = -1;
                yield return box.Choose(new[] { "Lửa", "Nước", "Cỏ", "Để sau" }, value => choice = value, 3);
                starterChoice = choice switch { 0 => PetElement.Fire, 1 => PetElement.Water, 2 => PetElement.Nature, _ => PetElement.Unknown };
                if (starterChoice == PetElement.Unknown) yield break;
            }
            if (!TryTurnInWithStarter(active, selected, starterChoice, out string error)) { yield return box.TypeLine(error, 42); yield break; }
            yield return Lines(box, active.Definition.CompleteDialogue);
            if (active.QuestId == FinalQuestId)
                yield return box.TypeLine("HOÀN THÀNH CHƯƠNG I\nBí ẩn hang động", 34);
            else if (Available() is QuestDefinition next) {
                yield return box.TypeLine("Ta còn một việc muốn nhờ cháu.", 55);
                yield return OfferQuest(box, next);
            }
        }

        string PetLabel(PetController pet) {
            var stats = pet.GetComponent<PetHudRuntimeStats>();
            int partySlot = Array.IndexOf(player.petSlots, pet);
            string location = partySlot >= 0 ? $"Đội ô {partySlot + 1}" : "Kho";
            if (partySlot < 0)
                for (int i = 0; i < roster.StoredPets.Count; i++) {
                    if (roster.StoredPets[i] != pet) continue;
                    location = $"Kho ô {i + 1}";
                    break;
                }
            return stats != null ? $"{stats.DisplayName} · Cấp {stats.Level} · {location}" : $"{pet.name} · {location}";
        }

        internal bool TryAccept(QuestDefinition definition) {
            if (!IsChapter(definition) || Active() != null || saves == null || inventory == null ||
                !quests.CanAcceptQuest(definition) || CaptureEventBatch.IsActive) return false;
            var bag = inventory.CaptureSlots();
            var journal = quests.CreateSaveData();
            using var batch = new CaptureEventBatch();
            bool committed = false;
            try {
                foreach (var grant in definition.StartItems) {
                    var item = InventoryDefinitionCatalog.Find(grant.TargetId);
                    if (item == null || !inventory.TryAddItemAtomic(item, grant.Amount, false)) return false;
                }
                if (!quests.AcceptChapterQuest(definition)) return false;
                Refresh();
                if (!saves.SaveNow()) return false;
                committed = true;
                if (definition.StartItems.Count > 0) inventory.NotifyAtomicAddition();
                batch.Commit();
                return true;
            } finally {
                if (!committed) {
                    inventory.RestoreCaptureSlots(bag);
                    quests.RestoreFromSaveData(journal);
                }
            }
        }

        internal bool TryTurnIn(QuestRuntimeState state, IReadOnlyList<PetController> selected, out string error) =>
            TryTurnInWithStarter(state, selected, PetElement.Unknown, out error);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool TryDeveloperComplete(QuestDefinition definition, PetElement starter, out string error) {
            error = "Không thể nhận nhiệm vụ qua luồng Chương I.";
            if (!Capstone.Game.UISystem.DevCheatService.Active || !IsChapter(definition)) return false;
            var state = quests.GetQuestState(definition.QuestId);
            if (state?.Status == QuestStatus.Completed) {
                if (state.RewardsClaimed) return true;
                rewards ??= FindFirstObjectByType<QuestRewardService>(FindObjectsInactive.Include);
                if (rewards != null) return rewards.TryGrantChapterTurnIn(state, out error);
                return false;
            }
            if (state?.Status != QuestStatus.Active && !TryAccept(definition)) return false;
            return TryTurnInWithStarter(quests.GetQuestState(definition.QuestId), Array.Empty<PetController>(), starter,
                out error, true);
        }
#endif

        bool TryTurnInWithStarter(QuestRuntimeState state, IReadOnlyList<PetController> selected, PetElement starterChoice,
            out string error, bool simulateHandover = false) {
            error = "Chưa đủ điều kiện trả nhiệm vụ.";
            if (state == null || Active() != state || (!simulateHandover && !IsReady(state)) || CaptureEventBatch.IsActive ||
                inventory == null || saves == null || roster == null || player == null) return false;
            if (rewards == null) rewards = FindFirstObjectByType<QuestRewardService>(FindObjectsInactive.Include);
            if (rewards == null) { error = "Hệ thống phần thưởng chưa sẵn sàng."; return false; }
            var delivery = state.Definition.Objectives.FirstOrDefault(objective => objective.ObjectiveType == QuestObjectiveType.DeliverPet);
            if (delivery != null && !simulateHandover) {
                var eligible = EligiblePets(state, delivery);
                if (selected.Count != delivery.RequiredAmount || selected.Distinct().Count() != selected.Count ||
                    selected.Any(pet => !eligible.Contains(pet))) {
                    error = "Danh sách thú đã thay đổi. Hãy chọn lại đúng cá thể, thu thú về và bỏ Yêu thích trước khi giao.";
                    return false;
                }
                if (!player.petSlots.Any(pet => pet != null && !selected.Contains(pet) &&
                    pet.GetComponent<PetHudRuntimeStats>() is PetHudRuntimeStats stats && stats.Health > 0)) {
                    error = "Đội hình phải giữ ít nhất một thú còn khả năng chiến đấu.";
                    return false;
                }
            } else if (selected.Count != 0) return false;
            var journal = quests.CreateSaveData();
            var bag = inventory.CaptureSlots();
            var detached = new List<PetRosterReleaseToken>();
            using var batch = new CaptureEventBatch();
            bool committed = false;
            try {
                if (state.QuestId == "main_quest_06" && quests.StarterElement == PetElement.Unknown)
                    quests.StarterElement = starterChoice;
                foreach (var pet in selected) {
                    if (!roster.TryDetachPetForRelease(pet, out var token, out error)) return false;
                    detached.Add(token);
                    pet.GetComponent<PetCollectionMetadata>().SetReleased(true);
                }
                foreach (var objective in state.Definition.Objectives) {
                    if (simulateHandover) {
                        quests.RefreshChapterObjective(state, objective, objective.RequiredAmount);
                        continue;
                    }
                    if (!objective.IsHandover) continue;
                    if (objective.ObjectiveType == QuestObjectiveType.GiveItem)
                        foreach (var item in Materials(objective))
                            if (!inventory.TryRemoveItemAtomic(InventoryDefinitionCatalog.Find(item.id), item.count)) {
                                error = "Số lượng vật phẩm đã thay đổi.";
                                return false;
                            }
                    quests.RefreshChapterObjective(state, objective, objective.RequiredAmount);
                }
                if (!quests.CompleteChapterQuest(state.QuestId) || !rewards.TryGrantChapterTurnIn(state, out error)) return false;
                committed = true;
                foreach (var token in detached) {
                    token.Pet.gameObject.SetActive(false);
                    Destroy(token.Pet.gameObject);
                }
                inventory.NotifyAtomicAddition();
                batch.Commit();
                return true;
            } finally {
                if (!committed) {
                    for (int i = detached.Count - 1; i >= 0; i--) {
                        detached[i].Pet.GetComponent<PetCollectionMetadata>().SetReleased(false);
                        roster.RestoreDetachedPet(detached[i], out _);
                    }
                    inventory.RestoreCaptureSlots(bag);
                    quests.RestoreFromSaveData(journal);
                }
            }
        }
    }
}
