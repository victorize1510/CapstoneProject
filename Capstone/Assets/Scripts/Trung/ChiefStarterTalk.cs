using System;
using System.Collections;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UI;

// Trưởng làng nói chuyện khi Player bước vào nhà (trước bàn 3 quả bóng thú).
// Gắn lên 1 object có Box Collider (Is Trigger) đặt ở cửa / giữa nhà trưởng làng.
//   1. Player đi vào vùng -> khóa điều khiển, trưởng làng và Player quay mặt vào nhau.
//   2. Hội thoại giới thiệu 3 quả bóng thú.
//   3. Nói xong -> tắt hội thoại, Player đi tới bàn chọn bóng như bình thường.
// Đã chọn bóng rồi (có trong save) thì không nói nữa.
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class ChiefStarterTalk : MonoBehaviour
{
    [Header("Trưởng làng")]
    [Tooltip("Để trống = lấy NPC của VillageChiefCutscene")]
    [SerializeField] Transform npc;
    [SerializeField] string npcName = "Trưởng làng";
    [Tooltip("Để trống = lấy ảnh của VillageChiefCutscene")]
    [SerializeField] Sprite npcPortrait;
    [Tooltip("Ảnh chân dung Player (tùy chọn)")]
    [SerializeField] Sprite playerPortrait;
    [Tooltip("Trưởng làng nói xong thì quay lại hướng cũ")]
    [SerializeField] bool npcTurnBackAfter = true;
    [Tooltip("Player cũng quay mặt về phía trưởng làng")]
    [SerializeField] bool playerFacesNpc = true;
    [SerializeField, Min(30f)] float turnSpeed = 360f;
    [SerializeField] string playerIdleState = "IdleNeutral";

    [Header("Điều kiện")]
    [Tooltip("Chỉ nói khi đã xem cutscene trưởng làng ngoài cửa")]
    [SerializeField] bool requireElderIntro = false;
    [Tooltip("Bàn chọn bóng. Để trống = tự tìm. Đã chọn bóng thì không nói nữa")]
    [SerializeField] StarterBallTable ballTable;

    [Header("Hội thoại  ({name} = tên Player)")]
    [SerializeField] List<VillageChiefCutscene.Line> lines = new List<VillageChiefCutscene.Line>
    {
        new VillageChiefCutscene.Line { speaker = VillageChiefCutscene.Speaker.Npc,    text = "A, {name} tới rồi đấy à! Vào đây, vào đây, ông chờ cháu nãy giờ." },
        new VillageChiefCutscene.Line { speaker = VillageChiefCutscene.Speaker.Npc,    text = "Cháu thấy ba quả bóng thú trên bàn kia chứ? Trong mỗi quả là một bé thú đang chờ người bạn đồng hành của mình." },
        new VillageChiefCutscene.Line { speaker = VillageChiefCutscene.Speaker.Npc,    text = "Mỗi bé mang một hệ và một tính cách khác nhau. Có bé mạnh mẽ, có bé nhanh nhẹn, có bé lại rất hiền lành." },
        new VillageChiefCutscene.Line { speaker = VillageChiefCutscene.Speaker.Player, text = "Cháu được chọn bé nào cũng được ạ?" },
        new VillageChiefCutscene.Line { speaker = VillageChiefCutscene.Speaker.Npc,    text = "Đúng vậy, nhưng chỉ được chọn một thôi. Hãy xem kỹ từng bé rồi hẵng quyết định nhé." },
        new VillageChiefCutscene.Line { speaker = VillageChiefCutscene.Speaker.Npc,    text = "Người bạn đầu tiên sẽ đi cùng cháu suốt cả hành trình. Hãy luôn tin tưởng và chăm sóc nó thật tốt." },
        new VillageChiefCutscene.Line { speaker = VillageChiefCutscene.Speaker.Player, text = "Cháu cảm ơn ông! Cháu hồi hộp quá!" },
        new VillageChiefCutscene.Line { speaker = VillageChiefCutscene.Speaker.Npc,    text = "Ha ha! Lại gần bàn để xem từng quả bóng đi. Hành trình của cháu bắt đầu từ đây đấy!" }
    };
    [SerializeField, Min(5f)] float typingSpeed = 40f;
    [Tooltip("Để trống = dùng khung hội thoại trong DialogueCanvas (giống cutscene trưởng làng)")]
    [SerializeField] DialogueBoxUI dialogueBox;

    bool played;
    bool running;
    Transform player;
    GameMenuController menu;
    bool blockedWithMenu;
    Behaviour frozenMovement;

    public bool IsPlaying => running;

    void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4f, 2f, 3f);
        box.center = new Vector3(0f, 1f, 0f);
    }

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        var cutscene = FindFirstObjectByType<VillageChiefCutscene>(FindObjectsInactive.Include);
        if (cutscene != null)
        {
            if (npc == null) npc = cutscene.Chief;
            if (npcPortrait == null) npcPortrait = cutscene.ChiefPortrait;
        }
        if (ballTable == null) ballTable = FindFirstObjectByType<StarterBallTable>(FindObjectsInactive.Include);
    }

    void OnTriggerEnter(Collider other) => TryStart(other);
    void OnTriggerStay(Collider other) => TryStart(other);   // Đứng sẵn trong vùng lúc cutscene khác vừa xong.

    void TryStart(Collider other)
    {
        if (played || running || npc == null) return;
        var movement = other.GetComponentInParent<BasicPlayerMovement>();
        if (movement == null) return;
        if (!CanTalk()) return;

        player = movement.transform;
        StartCoroutine(Run());
    }

    bool CanTalk()
    {
        var quests = FindFirstObjectByType<Capstone.Game.QuestSystem.QuestManager>(FindObjectsInactive.Include);
        if (quests != null && quests.StarterChosen) { played = true; return false; }   // Đã chọn bóng rồi.
        if (requireElderIntro && (quests == null || !quests.ElderIntroductionComplete)) return false;

        if (InventoryInputController.GameplayInputBlocked) return false;
        if (ballTable != null && ballTable.IsSelecting) return false;
        var intro = FindFirstObjectByType<NewGameIntroSequence>();
        if (intro != null && intro.IsPlaying) return false;
        foreach (var c in FindObjectsByType<VillageChiefCutscene>(FindObjectsSortMode.None))
            if (c.IsPlaying) return false;
        return true;
    }

    [ContextMenu("Xem thử (chỉ dùng khi đang Play)")]
    void PlayNow()
    {
        if (!Application.isPlaying || running) return;
        var movement = FindFirstObjectByType<BasicPlayerMovement>();
        if (movement == null) return;
        player = movement.transform;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        running = true;
        played = true;
        EnsureDialogueBox();
        BlockGameplay(true);
        HoldPlayer();

        Quaternion npcStart = npc.rotation;

        // Hai người quay mặt vào nhau.
        yield return TurnToward(npc, player.position, playerFacesNpc ? player : null, npc.position);

        // Hội thoại.
        yield return PlayDialogue();

        if (npcTurnBackAfter) yield return TurnTo(npc, npcStart);

        ReleasePlayer();
        BlockGameplay(false);
        running = false;
    }

    // ---------- Quay mặt ----------

    IEnumerator TurnToward(Transform a, Vector3 aLookAt, Transform b, Vector3 bLookAt)
    {
        Quaternion aTarget = FlatLook(a, aLookAt);
        Quaternion bTarget = b != null ? FlatLook(b, bLookAt) : Quaternion.identity;
        float end = Time.unscaledTime + 1.5f;
        while (Time.unscaledTime < end)
        {
            float step = turnSpeed * Time.unscaledDeltaTime;
            a.rotation = Quaternion.RotateTowards(a.rotation, aTarget, step);
            bool done = Quaternion.Angle(a.rotation, aTarget) < 1f;
            if (b != null)
            {
                b.rotation = Quaternion.RotateTowards(b.rotation, bTarget, step);
                done &= Quaternion.Angle(b.rotation, bTarget) < 1f;
            }
            if (done) break;
            yield return null;
        }
    }

    IEnumerator TurnTo(Transform t, Quaternion target)
    {
        float end = Time.unscaledTime + 1.5f;
        while (Time.unscaledTime < end && Quaternion.Angle(t.rotation, target) > 1f)
        {
            t.rotation = Quaternion.RotateTowards(t.rotation, target, turnSpeed * Time.unscaledDeltaTime);
            yield return null;
        }
    }

    static Quaternion FlatLook(Transform from, Vector3 at)
    {
        Vector3 d = at - from.position;
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d) : from.rotation;
    }

    // ---------- Hội thoại ----------

    IEnumerator PlayDialogue()
    {
        if (lines.Count == 0 || dialogueBox == null) yield break;
        bool opened = false;
        foreach (var line in lines)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.text)) continue;
            bool isNpc = line.speaker == VillageChiefCutscene.Speaker.Npc;
            string speaker = isNpc ? npcName : PlayerName();
            Sprite portrait = isNpc ? npcPortrait : playerPortrait;

            if (!opened) { yield return dialogueBox.Show(speaker, portrait); opened = true; }
            else dialogueBox.SetSpeaker(speaker, portrait);
            yield return dialogueBox.TypeLine(line.text.Replace("{name}", PlayerName()), typingSpeed);
        }
        if (opened) yield return dialogueBox.Hide();
    }

    void EnsureDialogueBox()
    {
        if (dialogueBox == null)
            foreach (var candidate in FindObjectsByType<DialogueBoxUI>(FindObjectsSortMode.None))
                if (candidate.HasManualLayout) { dialogueBox = candidate; break; }
        if (dialogueBox == null) dialogueBox = gameObject.AddComponent<DialogueBoxUI>();

        if (!dialogueBox.HasManualLayout && !dialogueBox.IsBuilt)
        {
            var go = new GameObject("ChiefTalkCanvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1001;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            dialogueBox.Build(go.transform);
        }
        else dialogueBox.Build(null);
    }

    static string PlayerName()
    {
        var provider = FindFirstObjectByType<PlayerProfileRuntimeProvider>(FindObjectsInactive.Include);
        string n = provider != null ? provider.GetSnapshot().DisplayName : null;
        return string.IsNullOrWhiteSpace(n) ? "Bạn" : n;
    }

    // ---------- Khóa điều khiển ----------

    void HoldPlayer()
    {
        var movement = player != null ? player.GetComponent<BasicPlayerMovement>() : null;
        if (movement == null) return;
        if (movement.enabled) { movement.enabled = false; frozenMovement = movement; }

        var switcher = movement.GetComponent<PlayerVisualSwitcher>();
        Animator anim = switcher != null ? switcher.ActiveVisualAnimator : null;
        if (anim == null) anim = movement.GetComponentInChildren<Animator>();
        if (anim != null && !string.IsNullOrEmpty(playerIdleState))
        {
            int hash = Animator.StringToHash(playerIdleState);
            if (anim.HasState(0, hash)) anim.CrossFadeInFixedTime(hash, 0.15f, 0);
        }
    }

    void ReleasePlayer()
    {
        if (frozenMovement != null) frozenMovement.enabled = true;
        frozenMovement = null;
    }

    void BlockGameplay(bool block)
    {
        if (block)
        {
            menu = FindFirstObjectByType<GameMenuController>();
            blockedWithMenu = menu != null;
            if (blockedWithMenu) menu.SetDialogueOpen(this, true);
            else InventoryInputController.SetExternalGameplayInputBlocked(this, true);
            return;
        }
        if (blockedWithMenu && menu != null) menu.SetDialogueOpen(this, false);
        else InventoryInputController.SetExternalGameplayInputBlocked(this, false);
        blockedWithMenu = false;
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
    }
}