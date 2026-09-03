using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class EvolutionController : MonoBehaviour
{
    [Header("Testing")]
    [Tooltip("Nhấn phím này trong lúc Play để test tiến hóa thủ công, không cần chờ lên cấp thật.")]
    public Key testEvolveKey = Key.E;

    [Header("Evolution Stages (in order, e.g. Bat -> Vampire Bat -> Bat Lord)")]
    public GameObject[] stages;

    [Header("Timing")]
    [Tooltip("Thời gian giữ nguyên trạng thái 'trắng xóa hoàn toàn' trước khi đổi model (ngoài thời gian fade-in).")]
    public float holdBeforeSwap = 0f;

    [Header("Whiteout (glow) Effect")]
    [Tooltip("Vật liệu Unlit, Transparent, màu trắng. Dùng để phủ lên model tạo hiệu ứng phát sáng.")]
    public Material whiteOverlayMaterial;
    [Tooltip("Độ trễ (giây) giữa lúc BẮT ĐẦU XOAY và lúc ánh sáng trắng bắt đầu tăng lên. Để thú xoay 1 nhịp trước rồi mới sáng dần.")]
    public float glowStartDelay = 0.3f;
    [Tooltip("Thời gian để độ trắng tăng dần từ 0 lên 1 — để VÀI GIÂY cho cảm giác sáng từ từ, kể từ sau glowStartDelay.")]
    public float whiteoutFadeInDuration = 2.5f;
    [Tooltip("Thời gian để độ trắng giảm dần từ 1 về 0 khi tiến hóa xong, chạy CÙNG LÚC với lúc xoay chậm dần.")]
    public float whiteoutFadeOutDuration = 2f;
    [Tooltip("Độ chói/bloom (HDR) khi trắng xóa hoàn toàn. Để 0 = chỉ là khối trắng phẳng (không chói viền). Tăng lên (VD: 2-6) để có hiệu ứng phát sáng tràn viền — CẦN material bật sẵn Emission và scene có bật Bloom trong URP Volume mới thấy hiệu ứng.")]
    public float whiteoutEmissionIntensity = 0f;

    [Header("Spin Effect")]
    [Tooltip("Tốc độ xoay (độ/giây) tại đỉnh điểm.")]
    public float maxSpinSpeed = 720f;
    [Tooltip("Thời gian tăng tốc xoay từ 0 -> maxSpinSpeed. Nên để NGẮN hơn whiteoutFadeInDuration, vì xoay cần đạt tốc độ trước khi ánh sáng kịp phủ trắng.")]
    public float spinUpDuration = 0.5f;
    [Tooltip("Đường cong tăng tốc xoay, từ 0 -> maxSpinSpeed.")]
    public AnimationCurve spinUpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Thời gian giảm tốc xoay từ maxSpinSpeed -> 0 khi tiến hóa xong, chạy CÙNG LÚC với lúc ánh sáng đang tắt dần (nên đặt bằng whiteoutFadeOutDuration).")]
    public float spinDownDuration = 2f;
    [Tooltip("Đường cong giảm tốc xoay, từ maxSpinSpeed -> 0.")]
    public AnimationCurve spinDownCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Ground Ring VFX")]
    [Tooltip("Prefab hiệu ứng vòng tròn dưới chân (particle/decal). Sẽ bật khi bắt đầu và tắt khi kết thúc tiến hóa.")]
    public GameObject groundRingVfxPrefab;
    [Tooltip("Offset vị trí Y để đặt vòng tròn sát mặt đất dưới chân thú.")]
    public float groundRingYOffset = 0.05f;

    [Header("Grow-in Animation")]
    public float growDuration = 0.5f;
    public AnimationCurve growCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Animation Lock (giữ nguyên Idle trong lúc tiến hóa)")]
    [Tooltip("Tên state Idle trong Animator Controller của thú (phải khớp chính xác tên state trong cửa sổ Animator).")]
    public string idleStateName = "Idle";
    [Tooltip("Các script điều khiển AI/di chuyển/animation cần TẠM TẮT trong lúc tiến hóa, để chúng không giành quyền đổi animation. Sẽ tự bật lại khi tiến hóa xong.")]
    public MonoBehaviour[] disableDuringEvolution;

    public int CurrentStageIndex { get; private set; } = -1;
    public GameObject CurrentInstance { get; private set; }

    private WhiteoutOverlay currentOverlay;
    private GameObject activeRingVfx;

    void Start()
    {
        if (stages.Length > 0)
            SpawnStage(0, animateGrow: false);
    }

    void Update()
    {
        // Chỉ để TEST bằng tay trong lúc Play — bắn phím testEvolveKey là gọi Evolve() ngay,
        // không cần đợi hệ thống lên cấp thật gọi tới.
        if (Keyboard.current != null && Keyboard.current[testEvolveKey].wasPressedThisFrame)
        {
            Evolve();
        }
    }

    [ContextMenu("Evolve To Next Stage")]
    public void Evolve()
    {
        if (CurrentStageIndex + 1 >= stages.Length)
        {
            Debug.LogWarning($"{name}: already at final evolution stage.");
            return;
        }
        StartCoroutine(EvolveRoutine(CurrentStageIndex + 1));
    }

    IEnumerator EvolveRoutine(int nextIndex)
    {
        // 0) Tạm tắt các script điều khiển AI/di chuyển, để animation không bị giành quyền
        SetControllingScriptsEnabled(false);

        // 1) Bật vòng tròn VFX dưới chân ngay khi bắt đầu tiến hóa
        SpawnGroundRing();

        // 2) Ép animation về Idle và giữ nguyên tại đó
        ForceIdle(CurrentInstance);

        // 3) Tạo overlay trắng cho model HIỆN TẠI
        currentOverlay = new WhiteoutOverlay(CurrentInstance.transform, whiteOverlayMaterial, whiteoutEmissionIntensity);

        // 3a) XOAY BẮT ĐẦU NGAY (t=0). Ánh sáng trắng chờ "glowStartDelay" giây rồi
        // mới từ từ tăng lên trong "whiteoutFadeInDuration" giây tiếp theo.
        yield return SpinAndFade(
            spinDuration: spinUpDuration,
            spinCurve: spinUpCurve,
            fadeDelay: glowStartDelay,
            fadeDuration: whiteoutFadeInDuration,
            fadeFrom: 0f,
            fadeTo: 1f);

        // Lúc này model đã bị che trắng hoàn toàn -> giữ thêm 1 chút nếu muốn, rồi đổi model
        // mà không ai nhìn thấy "giật hình"
        if (holdBeforeSwap > 0f)
            yield return new WaitForSeconds(holdBeforeSwap);

        SpawnStage(nextIndex, animateGrow: true);

        // 4) Ép animation của model MỚI về Idle ngay khi vừa spawn
        ForceIdle(CurrentInstance);

        // 5) Tạo overlay trắng cho model MỚI (đang ở trạng thái trắng hoàn toàn, alpha = 1)
        currentOverlay = new WhiteoutOverlay(CurrentInstance.transform, whiteOverlayMaterial, whiteoutEmissionIntensity);
        currentOverlay.SetIntensity(1f);

        // 6) XOAY VÀ ÁNH SÁNG GIẢM CÙNG LÚC (fadeDelay = 0) để lộ model mới ra dần
        yield return SpinAndFade(
            spinDuration: spinDownDuration,
            spinCurve: spinDownCurve,
            fadeDelay: 0f,
            fadeDuration: whiteoutFadeOutDuration,
            fadeFrom: 1f,
            fadeTo: 0f);

        currentOverlay.Cleanup();
        currentOverlay = null;

        // 7) Tắt vòng tròn VFX dưới chân khi tiến hóa xong
        DespawnGroundRing();

        // 8) Bật lại các script điều khiển AI/di chuyển
        SetControllingScriptsEnabled(true);
    }

    /// <summary>
    /// Chạy xoay và fade độ trắng SONG SONG trong cùng 1 vòng lặp, nhưng với 2 mốc thời gian riêng:
    /// - Xoay bắt đầu ngay tại t=0, kéo dài "spinDuration" giây theo spinCurve.
    /// - Ánh sáng chỉ bắt đầu lerp từ fadeFrom -> fadeTo sau khi trôi qua "fadeDelay" giây,
    ///   rồi chạy trong "fadeDuration" giây kế tiếp.
    /// Truyền fadeDelay = 0 để xoay và sáng/tắt chạy đúng cùng lúc (dùng cho lúc kết thúc);
    /// truyền fadeDelay > 0 để xoay trước một nhịp rồi mới bắt đầu sáng lên (dùng cho lúc bắt đầu).
    /// </summary>
    IEnumerator SpinAndFade(float spinDuration, AnimationCurve spinCurve,
        float fadeDelay, float fadeDuration, float fadeFrom, float fadeTo)
    {
        float total = Mathf.Max(spinDuration, fadeDelay + fadeDuration);
        float t = 0f;

        while (t < total)
        {
            float dt = Time.deltaTime;
            t += dt;

            // Xoay: chỉ áp dụng tốc độ trong khoảng [0, spinDuration], sau đó coi như đã đạt max và giữ nguyên
            if (spinDuration > 0f)
            {
                float spinK = Mathf.Clamp01(t / spinDuration);
                float spinSpeed = spinCurve.Evaluate(spinK) * maxSpinSpeed;
                transform.Rotate(Vector3.up, spinSpeed * dt, Space.Self);
            }

            // Ánh sáng: chỉ bắt đầu lerp sau khi qua mốc fadeDelay
            if (t >= fadeDelay)
            {
                float fadeK = fadeDuration > 0f ? Mathf.Clamp01((t - fadeDelay) / fadeDuration) : 1f;
                currentOverlay?.SetIntensity(Mathf.Lerp(fadeFrom, fadeTo, fadeK));
            }

            yield return null;
        }

        currentOverlay?.SetIntensity(fadeTo);
    }

    void SpawnStage(int index, bool animateGrow)
    {
        if (CurrentInstance != null)
            Destroy(CurrentInstance);

        GameObject prefab = stages[index];
        CurrentInstance = Instantiate(prefab, transform.position, transform.rotation, transform);
        CurrentStageIndex = index;

        if (animateGrow)
            StartCoroutine(GrowRoutine(CurrentInstance.transform));
    }

    IEnumerator GrowRoutine(Transform target)
    {
        Vector3 finalScale = target.localScale;
        float t = 0f;
        while (t < growDuration)
        {
            t += Time.deltaTime;
            float k = growCurve.Evaluate(Mathf.Clamp01(t / growDuration));
            target.localScale = finalScale * k;
            yield return null;
        }
        target.localScale = finalScale;
    }

    void SpawnGroundRing()
    {
        if (groundRingVfxPrefab == null) return;
        Vector3 pos = transform.position + Vector3.up * groundRingYOffset;
        // Không parent vào transform của con thú, để ring không bị xoay theo lúc thú quay tròn.
        activeRingVfx = Instantiate(groundRingVfxPrefab, pos, Quaternion.identity);
    }

    void DespawnGroundRing()
    {
        if (activeRingVfx != null)
        {
            Destroy(activeRingVfx);
            activeRingVfx = null;
        }
    }

    /// <summary>Ép Animator (nếu có) của target chuyển ngay về state Idle và đứng yên ở đó.</summary>
    void ForceIdle(GameObject target)
    {
        if (target == null || string.IsNullOrEmpty(idleStateName)) return;

        Animator anim = target.GetComponentInChildren<Animator>();
        if (anim == null) return;

        anim.Play(idleStateName, 0, 0f);
        anim.Update(0f); // ép áp dụng pose Idle ngay lập tức, tránh giật 1 frame
    }

    void SetControllingScriptsEnabled(bool enabled)
    {
        if (disableDuringEvolution == null) return;
        foreach (var behaviour in disableDuringEvolution)
        {
            if (behaviour != null)
                behaviour.enabled = enabled;
        }
    }
}