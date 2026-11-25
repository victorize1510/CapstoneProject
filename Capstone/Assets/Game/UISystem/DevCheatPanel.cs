#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.UISystem {
    [DisallowMultipleComponent]
    internal sealed class DevCheatPanel : MonoBehaviour {
        static DevCheatPanel instance;
        PetCommandInput player;
        PlayerSaveController saves;
        PlayerProfileRuntimeProvider profile;
        PlayerCurrencyWallet wallet;
        QuestManager quests;
        ChapterWorldLayout world;
        bool open, busy;
        Vector2 scroll;
        CursorLockMode previousLock;
        bool previousCursor;
        string message = string.Empty;
        int starterChoice;
        static readonly string[] WaypointNames = { "Làng", "Đồng cỏ dễ", "Đồng cỏ vừa", "Đồng cỏ khó",
            "Cửa hang", "Hang giữa", "Hang sâu", "Trước đấu trường Boss" };
        static readonly PetElement[] StarterTypes = { PetElement.Fire, PetElement.Water, PetElement.Nature };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() {
            if (instance != null) return;
            instance = new GameObject("Developer Cheats").AddComponent<DevCheatPanel>();
            DontDestroyOnLoad(instance.gameObject);
            instance.Bind(SceneManager.GetActiveScene());
        }

        void OnEnable() => SceneManager.activeSceneChanged += OnSceneChanged;
        void OnDisable() {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SetOpen(false);
        }

        void OnSceneChanged(Scene previous, Scene current) {
            SetOpen(false);
            Bind(current);
        }

        static T InScene<T>(Scene scene) where T : Component =>
            scene.isLoaded ? scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<T>(true))
                .FirstOrDefault(component => component != null) : null;

        void Bind(Scene scene) {
            player = InScene<PetCommandInput>(scene);
            saves = InScene<PlayerSaveController>(scene);
            profile = InScene<PlayerProfileRuntimeProvider>(scene);
            wallet = InScene<PlayerCurrencyWallet>(scene);
            quests = InScene<QuestManager>(scene);
            world = InScene<ChapterWorldLayout>(scene);
        }

        void Update() {
            if (GameplayKeyboard.GetKeyDown(KeyCode.F10)) TogglePanel();
        }

        void TogglePanel() {
            if (open) { SetOpen(false); return; }
            if (busy || InventoryInputController.GameplayInputBlocked) return;
            Bind(SceneManager.GetActiveScene());
            if (player != null && saves != null) SetOpen(true);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Capstone/Developer Cheats/Toggle F10 panel", true)]
        static bool CanToggleFromMenu() => Application.isPlaying && instance != null;

        [UnityEditor.MenuItem("Tools/Capstone/Developer Cheats/Toggle F10 panel")]
        static void ToggleFromMenu() => instance?.TogglePanel();
#endif

        void SetOpen(bool value) {
            if (open == value) return;
            open = value;
            InventoryInputController.SetExternalGameplayInputBlocked(this, value);
            if (value) {
                previousLock = Cursor.lockState;
                previousCursor = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            } else {
                Cursor.lockState = previousLock;
                Cursor.visible = previousCursor;
            }
        }

        void Run(Func<string> action) {
            if (busy) return;
            if (!DevCheatService.EnsureSession(saves, out message)) return;
            try { message = action(); }
            catch (Exception exception) { message = exception.Message; Debug.LogException(exception); }
        }

        void OnGUI() {
            if (!open) {
                if (busy || !string.IsNullOrEmpty(message) && Time.unscaledTime < noticeUntil)
                    GUI.Box(new Rect(24f, 24f, Mathf.Min(520f, Screen.width - 48f), 49f), message);
                return;
            }
            float width = Mathf.Min(550f, Screen.width - 24f), height = Mathf.Min(700f, Screen.height - 24f);
            var frame = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            var color = GUI.color;
            GUI.color = new Color(.65f, .49f, .23f);
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            GUI.color = new Color(.045f, .072f, .13f, .99f);
            GUI.DrawTexture(new Rect(frame.x + 2, frame.y + 2, frame.width - 4, frame.height - 4), Texture2D.whiteTexture);
            GUI.color = color;
            GUILayout.BeginArea(new Rect(frame.x + 14, frame.y + 10, width - 28, height - 20));
            GUILayout.BeginHorizontal();
            GUILayout.Label("BEAST OF FANTASY  /  KIỂM THỬ", GUI.skin.box);
            if (GUILayout.Button("Đóng · F10", GUILayout.Width(92f))) SetOpen(false);
            GUILayout.EndHorizontal();
            GUILayout.Label(DevCheatService.Active ? "Phiên thử đang bật · dữ liệu tạm trong RAM" : "Cheat đang tắt");
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            bool controlsEnabled = GUI.enabled;
            GUI.enabled &= !busy;

            Section("Nhân vật");
            if (GUILayout.Button("Cấp 100")) Run(() => DevCheatService.MaxPlayer(profile));
            Section("Pet và quái");
            if (GUILayout.Button("Pet đang chọn · Cấp 100")) Run(() => PetChange(true, false, true));
            if (GUILayout.Button("Cả đội · Cấp 100")) Run(() => PetChange(true, false, false));
            if (GUILayout.Button("Pet đang chọn · Chỉ số tối đa")) Run(() => PetChange(false, true, true));
            if (GUILayout.Button("Cả đội · Chỉ số tối đa")) Run(() => PetChange(false, true, false));
            if (GUILayout.Button("Thú hoang đang chọn · Cấp 100")) Run(() => DevCheatService.MaxSelected(player.SelectedEnemy, true));
            if (GUILayout.Button("Địch đang chọn · Cấp 100")) Run(() => DevCheatService.MaxSelected(player.SelectedEnemy, false));
            Section("Vàng");
            if (GUILayout.Button("Đặt vàng thành 9.999")) Run(() => {
                if (wallet == null) return "Scene này thiếu ví vàng.";
                DevCheatService.SetGold(wallet);
                return "Vàng hiện tại là 9.999.";
            });
            Section("Chiến đấu");
            bool cooldown = GUILayout.Toggle(DevCheatService.NoCooldown, "Không hồi chiêu");
            if (cooldown != DevCheatService.NoCooldown) Run(() => {
                DevCheatService.SetNoCooldown(player, cooldown);
                return cooldown ? "Đã xóa hồi chiêu của cả đội." : "Đã khôi phục hồi chiêu.";
            });
            bool mana = GUILayout.Toggle(DevCheatService.NoMana, "Không tốn Mana");
            if (mana != DevCheatService.NoMana) Run(() => { DevCheatService.NoMana = mana; return "Đã cập nhật tiêu hao Mana."; });
            bool god = GUILayout.Toggle(DevCheatService.GodMode, "Pet đội mình miễn sát thương");
            if (god != DevCheatService.GodMode) Run(() => { DevCheatService.GodMode = god; return "Đã cập nhật miễn sát thương."; });
            if (GUILayout.Button("Hồi sinh, đầy HP/MP và giải hiệu ứng cho cả đội")) Run(() =>
                $"Đã hồi phục {DevCheatService.Heal(DevCheatService.Party(player))} pet.");
            Section("Nhiệm vụ Chương I");
            if (quests == null || quests.StarterElement == PetElement.Unknown) {
                GUILayout.Label("Hệ starter thử dùng cho thưởng NV6");
                starterChoice = GUILayout.SelectionGrid(starterChoice, new[] { "Lửa", "Nước", "Cỏ" }, 3);
            }
            for (int i = 1; i <= 8; i++) {
                int count = i;
                if (GUILayout.Button($"Hoàn thành đến nhiệm vụ {i}")) RequestChapterAction(count, -1, false);
            }
            if (GUILayout.Button("Hoàn thành toàn bộ nhiệm vụ 1–8")) RequestChapterAction(8, -1, false);
            Section("Dịch chuyển");
            for (int i = 0; i < WaypointNames.Length; i++)
                if (GUILayout.Button(WaypointNames[i])) RequestChapterAction(0, i, false);
            Section("Boss Ready");
            if (GUILayout.Button("BOSS READY – CHEAT ALL", GUILayout.Height(41f))) RequestChapterAction(8, 7, true);
            Section("Khôi phục");
            if (GUILayout.Button("Reset cấp, chỉ số và tiện ích chiến đấu")) Run(() => {
                DevCheatService.Reset(profile, player);
                return "Đã khôi phục cấp và tắt cheat chiến đấu. Tiến độ phiên thử vẫn được giữ.";
            });
            if (DevCheatService.Active && GUILayout.Button("Khôi phục toàn bộ dữ liệu lúc bắt đầu phiên thử")) Run(() => {
                if (!DevCheatService.RestoreBaseline(saves, out var error)) return error;
                world?.ClearDeveloperBossFixture();
                return "Đã khôi phục dữ liệu lúc vào phiên thử.";
            });
            GUI.enabled = controlsEnabled;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static void Section(string label) {
            GUILayout.Space(8);
            GUILayout.Label(label, GUI.skin.box);
        }

        string PetChange(bool level, bool stats, bool activeOnly) {
            var pets = activeOnly ? new[] { player.activePet } : DevCheatService.Party(player);
            int count = DevCheatService.MaxPets(pets, level, stats);
            return count == 0 ? "Chưa có pet hợp lệ trong đội." : $"Đã cập nhật {count} pet.";
        }

        float noticeUntil;
        void RequestChapterAction(int count, int waypoint, bool bossReady) {
            if (busy || !DevCheatService.EnsureSession(saves, out message)) return;
            busy = true;
            StartCoroutine(ChapterAction(count, waypoint, bossReady));
        }

        IEnumerator ChapterAction(int count, int waypoint, bool bossReady) {
            var starter = StarterTypes[starterChoice];
            if (world == null) {
                if (!saves.SaveNow()) { Finish("Không lưu được phiên thử trước khi chuyển scene."); yield break; }
                message = "Đang mở scene Chương I…";
                AsyncOperation load = null;
                if (Application.CanStreamedLevelBeLoaded(ChapterQuestFlow.GameplayScene))
                    load = SceneManager.LoadSceneAsync(ChapterQuestFlow.GameplayScene);
#if UNITY_EDITOR
                else load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    "Assets/Scenes/" + ChapterQuestFlow.GameplayScene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
                if (load == null) { Finish("Scene Chương I chưa có trong cấu hình build."); yield break; }
                yield return load;
                Bind(SceneManager.GetActiveScene());
            }
            float deadline = Time.unscaledTime + 8f;
            while (saves != null && !saves.HasCompletedStartup && Time.unscaledTime < deadline) yield return null;
            if (world == null || player == null || !DevCheatService.EnsureSession(saves, out message)) {
                Finish(string.IsNullOrEmpty(message) ? "Scene Chương I chưa khởi tạo xong." : message);
                yield break;
            }
            string error;
            try {
                var party = DevCheatService.Party(player);
                var chapter = DevCheatService.Chapter(quests);
                if (bossReady && (profile == null || wallet == null || party.Length == 0 || chapter.Length != 9)) {
                    Finish("Boss Ready cần hồ sơ nhân vật, ví, đội hình và đầy đủ Chương I.");
                    yield break;
                }
                if (bossReady && !world.TryBindDeveloperBoss(out error)) {
                    Finish(error);
                    yield break;
                }
                if (count > 0 && !DevCheatService.CompleteChapter(quests, count, starter, out error)) {
                    Finish(error);
                    yield break;
                }
                if (bossReady) {
                    if (quests.GetQuestState(chapter[8].QuestId)?.Status != QuestStatus.Active &&
                        !quests.Chapter.TryAccept(chapter[8])) {
                        Finish("Đã trả NV1–8, nhưng chưa thể nhận nhiệm vụ Boss.");
                        yield break;
                    }
                    DevCheatService.MaxPlayer(profile);
                    if (DevCheatService.MaxPets(party, true, true) != party.Length) {
                        Finish("Có pet trong đội thiếu dữ liệu cấp hoặc chỉ số.");
                        yield break;
                    }
                    DevCheatService.Heal(party);
                    DevCheatService.SetGold(wallet);
                    DevCheatService.SetNoCooldown(player, true);
                    if (!saves.SaveNow()) { Finish(saves.SaveBlockedReason); yield break; }
                }
                if (waypoint >= 0 && !DevCheatService.TryTeleport(world, player, waypoint, out error)) {
                    Finish(error);
                    yield break;
                }
                if (waypoint >= 0) SetOpen(false);
                Finish(bossReady ? "Boss Ready hoàn tất." : count > 0 ? $"Đã hoàn thành và nhận thưởng NV1–{count}." :
                    $"Đã dịch chuyển đến {WaypointNames[waypoint]}.");
            } catch (Exception exception) { Debug.LogException(exception); Finish(exception.Message); }
        }

        void Finish(string result) {
            message = result;
            busy = false;
            noticeUntil = Time.unscaledTime + 6f;
        }
    }
}
#endif
