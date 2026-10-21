using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class EvolutionController : MonoBehaviour
{
    [Header("Testing")]
    [Tooltip("Nhấn phím này trong lúc Play để test tiến hóa thủ công, không cần chờ lên cấp thật.")]
    public Key testEvolveKey = Key.E;

    [Header("Scene Transition (kiểu Pokémon)")]
    [Tooltip("Bật để nhảy qua 1 Scene riêng chạy animation tiến hóa, xong tự quay lại scene cũ với model đã tiến hóa.")]
    public bool useEvolutionScene = false;
    [Tooltip("Tên Scene riêng dùng để chạy animation tiến hóa. Scene này PHẢI có trong Build Settings > Scene List.")]
    public string evolutionSceneName = "EvolutionScene";
    [Tooltip("Camera đang hiển thị gameplay hiện tại — BẮT BUỘC phải gán, sẽ tự tắt lúc Evolution Scene hiện lên và bật lại lúc xong. Để trống sẽ tự dùng Camera.main lúc runtime, nhưng nên gán tay cho chắc.")]
    public Camera gameplayCamera;
    [Tooltip("QUAN TRỌNG: Layer riêng dùng để cô lập con thú trong lúc tiến hóa, tránh việc camera Evolution Scene vô tình nhìn thấy Terrain/vật thể của scene gameplay (do 2 scene dùng chung 1 không gian tọa độ). " +
        "Tạo 1 Layer mới trong Unity (VD: 'Evolution'), gán tên đúng layer đó vào đây, và đặt Culling Mask của Camera trong Evolution Scene = CHỈ layer này (+ layer UI nếu có Canvas). " +
        "Script sẽ tự đổi layer của con thú sang layer này lúc bắt đầu tiến hóa, và trả về layer gốc lúc xong.")]
    public string evolutionOnlyLayerName = "Evolution";

    [Header("Evolution Stages (in order, e.g. Bat -> Vampire Bat -> Bat Lord)")]
    public GameObject[] stages;

    [Header("Timing")]
    [Tooltip("Thời gian giữ nguyên trạng thái 'trắng xóa hoàn toàn' trước khi đổi model (ngoài thời gian fade-in).")]
    public float holdBeforeSwap = 0f;

    [Header("Whiteout (glow) Effect")]
    [Tooltip("Vật liệu Unlit hoặc Lit, Transparent, màu trắng. Dùng để phủ lên model tạo hiệu ứng phát sáng.")]
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
    [Tooltip("Tốc độ xoay (độ/giây) tại đỉnh điểm. Đây chỉ là giá trị THAM CHIẾU để tạo cảm giác nhanh/chậm — script sẽ tự động tinh chỉnh nhẹ tốc độ thực tế (correction scale) để pha xoay LUÔN kết thúc đúng ở bội số 360°, không cần bạn tự tính số vòng.")]
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
    [Tooltip("Hệ số phóng to/thu nhỏ vòng ring VFX. 1 = giữ nguyên kích thước gốc của prefab. Nếu ring quá to (che hết màn hình) thì giảm xuống, ví dụ 0.1 - 0.3.")]
    public float groundRingScale = 1f;

    [Header("Grow-in Animation")]
    public float growDuration = 0.5f;
    public AnimationCurve growCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Animation Lock (giữ nguyên Idle trong lúc tiến hóa)")]
    [Tooltip("Tên state Idle trong Animator Controller của thú (phải khớp chính xác tên state trong cửa sổ Animator).")]
    public string idleStateName = "Idle";
    [Tooltip("Các script điều khiển AI/di chuyển/animation cần TẠM TẮT trong lúc tiến hóa, để chúng không giành quyền đổi animation. Sẽ tự bật lại khi tiến hóa xong.")]
    public MonoBehaviour[] disableDuringEvolution;

    [Header("Start Text (kiểu Pokémon)")]
    [Tooltip("Chỉ dùng khi useEvolutionScene = true. Bật để hiện khung chữ 'X đang bắt đầu tiến hóa' NGAY KHI vừa nhảy sang Evolution Scene, trước khi chạy animation trắng xóa/xoay.")]
    public bool showStartText = false;
    [Tooltip("Mẫu câu hiển thị lúc bắt đầu. {0} = tên form hiện tại (lấy theo tên GameObject prefab trong Stages).")]
    public string startMessageFormat = "{0} đang bắt đầu tiến hóa!";
    [Tooltip("Thời gian (giây) giữ khung chữ này trên màn hình trước khi tự tắt và chạy animation tiến hóa.")]
    public float startMessageDisplayTime = 1.5f;

    [Header("Congrats Text (kiểu Pokémon)")]
    [Tooltip("Chỉ dùng khi useEvolutionScene = true. Bật để hiện khung chữ 'Congratulations!' sau khi tiến hóa xong, chờ người chơi bấm phím rồi mới quay lại scene cũ.")]
    public bool showCongratsText = false;
    [Tooltip("Mẫu câu hiển thị. {0} = tên form cũ, {1} = tên form mới (lấy theo tên GameObject prefab trong Stages).")]
    public string congratsMessageFormat = "Chúc mừng! {0} đã tiến hóa thành {1}!";
    [Tooltip("Thời gian tối thiểu (giây) giữ khung chữ hiện trên màn hình trước khi cho phép bấm phím để tiếp tục — tránh bị bấm lướt qua quá nhanh.")]
    public float congratsMinDisplayTime = 1f;
    [Tooltip("Phím để tiếp tục / tắt khung chữ sau khi hết congratsMinDisplayTime.")]
    public Key congratsContinueKey = Key.Space;

    public int CurrentStageIndex { get; private set; } = -1;
    public GameObject CurrentInstance { get; private set; }

    private WhiteoutOverlay currentOverlay;
    private GameObject activeRingVfx;
    // -1 = không ép layer gì cả (chế độ tiến hóa tại chỗ bình thường). Khi đang tiến hóa
    // trong Evolution Scene, được set = layer riêng để SpawnStage() tự gán cho model mới.
    private int activeEvolutionLayer = -1;

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

        if (useEvolutionScene)
            StartCoroutine(EvolveInSeparateScene(CurrentStageIndex + 1));
        else
            StartCoroutine(EvolveRoutine(CurrentStageIndex + 1));
    }

    /// <summary>
    /// Kiểu Pokémon: chuyển hẳn GameObject con thú này sang 1 Scene riêng để chạy
    /// animation tiến hóa (trắng xóa + xoay + ring VFX như thường), rồi chuyển
    /// nó về LẠI đúng vị trí cũ ở scene gameplay khi xong — lúc đó model đã là bản tiến hóa.
    /// </summary>
    IEnumerator EvolveInSeparateScene(int nextIndex)
    {
        if (transform.parent != null)
        {
            Debug.LogError($"{name}: EvolveInSeparateScene yêu cầu object này phải là ROOT " +
                $"(không có parent) để có thể chuyển scene. Hiện đang có parent là '{transform.parent.name}'.");
            yield break;
        }

        Scene originalScene = gameObject.scene;
        Vector3 originalPos = transform.position;
        Quaternion originalRot = transform.rotation;
        Camera camToHide = gameplayCamera != null ? gameplayCamera : Camera.main;

        if (camToHide == null)
        {
            Debug.LogWarning($"{name}: 'Gameplay Camera' đang để trống và cũng không tìm thấy Camera.main. " +
                "Camera gameplay sẽ KHÔNG được tắt, 2 scene có thể bị chồng hình lên nhau.");
        }

        // 1) Tắt camera gameplay hiện tại, để người chơi không còn thấy scene cũ nữa
        if (camToHide != null) camToHide.gameObject.SetActive(false);

        // 2) Load Evolution Scene dạng ADDITIVE (cộng thêm, KHÔNG xóa scene gameplay hiện tại)
        yield return SceneManager.LoadSceneAsync(evolutionSceneName, LoadSceneMode.Additive);
        Scene evoScene = SceneManager.GetSceneByName(evolutionSceneName);

        // 3) Chuyển hẳn GameObject con thú sang Evolution Scene
        SceneManager.MoveGameObjectToScene(gameObject, evoScene);

        // 3b) Đổi layer của con thú (và toàn bộ con cháu) sang layer riêng, để camera của
        // Evolution Scene (Culling Mask chỉ chứa layer này) KHÔNG BAO GIỜ thấy nhầm Terrain/
        // vật thể khác của scene gameplay, dù 2 scene có tình cờ trùng tọa độ hay không.
        // Lưu lại layer GỐC của chính root object (giả định các phần khác của model cũng
        // cùng layer với root — đúng với đa số trường hợp) để khôi phục ở bước 6.
        int originalRootLayer = gameObject.layer;
        int evoLayer = LayerMask.NameToLayer(evolutionOnlyLayerName);
        if (evoLayer == -1)
        {
            Debug.LogWarning($"{name}: không tìm thấy Layer '{evolutionOnlyLayerName}'. " +
                "Vào Edit > Project Settings > Tags and Layers để tạo layer này, nếu không camera " +
                "Evolution Scene có thể vẫn nhìn thấy vật thể của scene gameplay.");
        }
        else
        {
            SetLayerRecursively(gameObject, evoLayer);
            // Đánh dấu để SpawnStage() cũng tự gán layer này cho model MỚI được tạo ra
            // giữa chừng lúc tiến hóa (vì Instantiate() không tự thừa hưởng layer của parent).
            activeEvolutionLayer = evoLayer;
        }

        // 4) Dịch chuyển tới đúng vị trí "sân khấu" đã đặt sẵn trong Evolution Scene
        if (EvolutionStageAnchor.Instance != null)
        {
            transform.position = EvolutionStageAnchor.Instance.transform.position;
            transform.rotation = EvolutionStageAnchor.Instance.transform.rotation;
        }
        else
        {
            Debug.LogWarning($"{name}: không tìm thấy EvolutionStageAnchor trong '{evolutionSceneName}'. " +
                "Con thú sẽ giữ nguyên toạ độ cũ, có thể không nằm trong khung hình camera.");
        }

        // 4b) Hiện khung chữ "X đang bắt đầu tiến hóa" NGAY khi vừa vào Evolution Scene,
        //     giữ 1 lúc rồi tự tắt, SAU ĐÓ mới chạy animation trắng xóa/xoay/ring VFX.
        if (showStartText && EvolutionUI.Instance != null)
        {
            string currentName = stages[nextIndex - 1].name;
            string startMessage = string.Format(startMessageFormat, currentName);

            EvolutionUI.Instance.Show(startMessage);
            yield return new WaitForSeconds(startMessageDisplayTime);
            EvolutionUI.Instance.Hide();
        }
        else if (showStartText && EvolutionUI.Instance == null)
        {
            Debug.LogWarning($"{name}: showStartText đang bật nhưng không tìm thấy EvolutionUI trong " +
                $"'{evolutionSceneName}'. Kiểm tra lại đã đặt script EvolutionUI vào Canvas trong Evolution Scene chưa.");
        }

        // 5) Chạy Y NGUYÊN đoạn hiệu ứng tiến hóa đã có (trắng xóa, xoay, ring VFX, grow...)
        //    không cần sửa gì thêm — nó vẫn hoạt động bình thường vì đang thao tác trên
        //    transform/GameObject của chính con thú này.
        yield return EvolveRoutine(nextIndex);

        // 5b) Hiện khung chữ "Congratulations" kiểu Pokémon, chờ người chơi bấm phím
        //     rồi mới quay lại scene cũ (giữ đúng nhịp: xem animation xong -> đọc thông báo -> về game)
        if (showCongratsText && EvolutionUI.Instance != null)
        {
            string fromName = stages[nextIndex - 1].name;
            string toName = stages[nextIndex].name;
            string message = string.Format(congratsMessageFormat, fromName, toName);

            EvolutionUI.Instance.Show(message);
            yield return new WaitForSeconds(congratsMinDisplayTime);

            while (!(Keyboard.current != null && Keyboard.current[congratsContinueKey].wasPressedThisFrame))
                yield return null;

            EvolutionUI.Instance.Hide();
        }
        else if (showCongratsText && EvolutionUI.Instance == null)
        {
            Debug.LogWarning($"{name}: showCongratsText đang bật nhưng không tìm thấy EvolutionUI trong " +
                $"'{evolutionSceneName}'. Kiểm tra lại đã đặt script EvolutionUI vào Canvas trong Evolution Scene chưa.");
        }

        // 6) Trả layer về đúng như ban đầu (áp dụng lên model MỚI hiện đang có, vì model cũ
        // đã bị Destroy giữa chừng trong EvolveRoutine rồi) trước khi chuyển về scene gameplay.
        if (evoLayer != -1)
        {
            SetLayerRecursively(gameObject, originalRootLayer);
            activeEvolutionLayer = -1;
        }

        // 6b) Xong rồi -> chuyển con thú (đã tiến hóa) về LẠI đúng vị trí cũ ở scene gameplay
        SceneManager.MoveGameObjectToScene(gameObject, originalScene);
        transform.position = originalPos;
        transform.rotation = originalRot;

        // 7) Bật lại camera gameplay, dỡ Evolution Scene ra khỏi bộ nhớ
        if (camToHide != null) camToHide.gameObject.SetActive(true);
        yield return SceneManager.UnloadSceneAsync(evoScene);
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
    /// - Xoay bắt đầu ngay tại t=0, kéo dài "spinDuration" giây, tốc độ tại mỗi thời điểm =
    ///   spinCurve.Evaluate(...) * maxSpinSpeed — GIỐNG HỆT cảm giác cũ (tăng/giảm tốc theo curve).
    ///
    ///   ĐIỂM MỚI: trước khi chạy, hàm tự LẤY MẪU (sample) trước đường cong tốc độ để ước tính
    ///   tổng số độ sẽ xoay được ("naiveTotalDegrees") nếu chạy y nguyên như cũ. Sau đó làm tròn
    ///   số đó về BỘI SỐ 360° gần nhất ("targetTotalDegrees", tối thiểu 1 vòng), rồi tính ra 1 hệ
    ///   số điều chỉnh NHẸ ("correctionScale", thường rất gần 1) nhân thêm vào tốc độ mỗi frame.
    ///   Nhờ vậy, pha xoay này LUÔN kết thúc CHÍNH XÁC ở đúng hướng lúc bắt đầu gọi hàm — không
    ///   cần khai báo "số vòng xoay" riêng, không cần "ép" lại hướng sau khi xoay xong, và vẫn
    ///   giữ đúng cảm giác nhanh/chậm theo maxSpinSpeed + curve như thiết kế ban đầu.
    ///
    /// - Ánh sáng chỉ bắt đầu lerp từ fadeFrom -> fadeTo sau khi trôi qua "fadeDelay" giây,
    ///   rồi chạy trong "fadeDuration" giây kế tiếp.
    /// Truyền fadeDelay = 0 để xoay và sáng/tắt chạy đúng cùng lúc (dùng cho lúc kết thúc);
    /// truyền fadeDelay > 0 để xoay trước một nhịp rồi mới bắt đầu sáng lên (dùng cho lúc bắt đầu).
    /// </summary>
    IEnumerator SpinAndFade(float spinDuration, AnimationCurve spinCurve,
        float fadeDelay, float fadeDuration, float fadeFrom, float fadeTo)
    {
        Quaternion phaseStartRotation = transform.rotation;

        // Lấy mẫu trước đường cong tốc độ để ước lượng tổng số độ sẽ xoay được (nếu chạy y như cũ),
        // rồi suy ra hệ số điều chỉnh để tổng đó CHẮC CHẮN là bội số của 360°.
        const int SAMPLE_COUNT = 60;
        float naiveTotalDegrees = 0f;
        if (spinDuration > 0f)
        {
            float sampleDt = spinDuration / SAMPLE_COUNT;
            for (int i = 0; i < SAMPLE_COUNT; i++)
            {
                float sampleK = (i + 0.5f) / SAMPLE_COUNT; // lấy mẫu tại điểm giữa mỗi lát cắt thời gian
                naiveTotalDegrees += spinCurve.Evaluate(sampleK) * maxSpinSpeed * sampleDt;
            }
        }

        float targetTotalDegrees = spinDuration > 0f
            ? Mathf.Max(360f, Mathf.Round(naiveTotalDegrees / 360f) * 360f)
            : 0f;
        float correctionScale = naiveTotalDegrees > 0.01f ? targetTotalDegrees / naiveTotalDegrees : 0f;

        float total = Mathf.Max(spinDuration, fadeDelay + fadeDuration);
        float t = 0f;
        float rotatedSoFar = 0f;

        while (t < total)
        {
            float dt = Time.deltaTime;
            t += dt;

            // Xoay: chỉ áp dụng tốc độ trong khoảng [0, spinDuration], sau đó coi như đã đạt max và giữ nguyên
            if (spinDuration > 0f)
            {
                float spinK = Mathf.Clamp01(t / spinDuration);
                float spinSpeed = spinCurve.Evaluate(spinK) * maxSpinSpeed * correctionScale;
                rotatedSoFar += spinSpeed * dt;
                transform.rotation = phaseStartRotation * Quaternion.Euler(0f, rotatedSoFar, 0f);
            }

            // Ánh sáng: chỉ bắt đầu lerp sau khi qua mốc fadeDelay
            if (t >= fadeDelay)
            {
                float fadeK = fadeDuration > 0f ? Mathf.Clamp01((t - fadeDelay) / fadeDuration) : 1f;
                currentOverlay?.SetIntensity(Mathf.Lerp(fadeFrom, fadeTo, fadeK));
            }

            yield return null;
        }

        // Ép chính xác 100% ở khung hình cuối, phòng sai số cộng dồn do frame rate dao động —
        // vì targetTotalDegrees luôn là bội số 360, kết quả CHẮC CHẮN trùng khớp hướng ban đầu.
        if (spinDuration > 0f)
            transform.rotation = phaseStartRotation * Quaternion.Euler(0f, targetTotalDegrees, 0f);
        currentOverlay?.SetIntensity(fadeTo);
    }

    void SpawnStage(int index, bool animateGrow)
    {
        if (CurrentInstance != null)
            Destroy(CurrentInstance);

        GameObject prefab = stages[index];
        CurrentInstance = Instantiate(prefab, transform.position, transform.rotation, transform);
        CurrentStageIndex = index;

        // Nếu đang trong lúc chạy Evolution Scene (activeEvolutionLayer != -1), model mới
        // này KHÔNG tự thừa hưởng layer của root — phải gán tay, nếu không camera (Culling
        // Mask chỉ chứa layer riêng đó) sẽ không thấy model mới, animation coi như "biến mất".
        if (activeEvolutionLayer != -1)
            SetLayerRecursively(CurrentInstance, activeEvolutionLayer);

        if (animateGrow)
            StartCoroutine(GrowRoutine(CurrentInstance.transform));
    }

    /// <summary>Gán 1 layer cho GameObject và TOÀN BỘ con cháu bên trong nó (kể cả object đang tắt).</summary>
    static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
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
        // Thu nhỏ/phóng to VFX theo hệ số groundRingScale, vì nhiều VFX pack (như flipbook
        // dạng cổng dịch chuyển) có scale gốc rất to, không vừa dưới chân 1 con thú.
        activeRingVfx.transform.localScale = activeRingVfx.transform.localScale * groundRingScale;
        // Gán CÙNG layer với con thú hiện tại (VD: "Evolution" lúc đang ở Evolution Scene),
        // nếu không camera Culling Mask giới hạn sẽ không thấy ring này (dù logic vẫn chạy đúng).
        SetLayerRecursively(activeRingVfx, gameObject.layer);
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