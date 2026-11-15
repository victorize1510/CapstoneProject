using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Capstone.Game.HudSystem {
    /// <summary>
    /// Đạo diễn hiệu ứng tiến hóa kiểu Pokémon cho hệ thống Pet THẬT.
    ///
    /// Đây là bản chuyển từ EvolutionController.cs (prototype) sang chạy cho PetController thật:
    /// - Y NGUYÊN hiệu ứng xoay (auto-correction về bội số 360°) + trắng xóa (WhiteoutOverlay)
    ///   + Animation Lock (ForceIdle) + Grow-in Animation + Disable During Evolution
    /// - Y NGUYÊN kiểu chuyển qua 1 Scene riêng (EvolutionScene) rồi quay lại, dùng
    ///   EvolutionStageAnchor để định vị và EvolutionUI để hiện chữ "bắt đầu/Congrats"
    /// - KHÁC: không tự quản lý mảng stages[] / Evolve() nữa — bên ngoài (PetEvolutionPanelController)
    ///   gọi Play(...) và truyền vào 1 hàm applyEvolution = PetEvolutionService.TryEvolve(...).
    ///   Hàm đó được gọi ĐÚNG LÚC màn hình đang trắng xóa hoàn toàn, nên việc đổi model/stat/save
    ///   diễn ra "vô hình".
    ///
    /// YÊU CẦU: PetEvolutionVisualController.cs cần có thêm 2 property public:
    ///   public Transform ActiveVisualTransform => runtimeVisual != null ? runtimeVisual.transform
    ///       : (baseVisual != null ? baseVisual.transform : transform);
    ///   public Animator ActiveAnimator => runtimeVisual != null ? runtimeVisual.GetComponentInChildren&lt;Animator&gt;(true)
    ///       : baseAnimator;
    ///
    /// CÁCH DÙNG: đặt component này vào 1 GameObject trong scene gameplay chính (chỉ 1 cái, như
    /// EvolutionUI/EvolutionStageAnchor), gán đầy đủ field trong Inspector (giống hệt cách bạn từng
    /// chỉnh cho EvolutionController), rồi gọi PetEvolutionPresentationController.Instance.Play(...).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetEvolutionPresentationController : MonoBehaviour {
        public static PetEvolutionPresentationController Instance { get; private set; }

        [Header("Scene Transition (kiểu Pokémon)")]
        [Tooltip("Tên Scene riêng dùng để chạy animation tiến hóa. PHẢI có trong Build Settings > Scene List.")]
        public string evolutionSceneName = "EvolutionScene";
        [Tooltip("Camera đang hiển thị gameplay hiện tại. Để trống sẽ tự dùng Camera.main lúc runtime.")]
        public Camera gameplayCamera;
        [Tooltip("Layer riêng dùng để cô lập pet trong lúc tiến hóa (xem giải thích gốc trong EvolutionController cũ).")]
        public string evolutionOnlyLayerName = "Evolution";

        [Header("Whiteout (glow) Effect")]
        public Material whiteOverlayMaterial;
        public float glowStartDelay = 0.3f;
        public float whiteoutFadeInDuration = 2.5f;
        public float whiteoutFadeOutDuration = 2f;
        public float whiteoutEmissionIntensity = 0f;

        [Header("Spin Effect")]
        [Tooltip("Tốc độ xoay THAM CHIẾU (độ/giây). Script tự tinh chỉnh (correction scale) để luôn dừng đúng ở bội số 360°.")]
        public float maxSpinSpeed = 720f;
        public float spinUpDuration = 0.5f;
        public AnimationCurve spinUpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float spinDownDuration = 2f;
        public AnimationCurve spinDownCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Ground Ring VFX")]
        public GameObject groundRingVfxPrefab;
        public float groundRingYOffset = 0.05f;
        public float groundRingScale = 1f;

        [Header("Grow-in Animation")]
        [Tooltip("Thời gian model mới 'lớn dần' từ scale 0 lên full, chạy song song lúc đang mờ dần lộ ra sau khi đổi model.")]
        public float growDuration = 0.5f;
        public AnimationCurve growCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Animation Lock (giữ nguyên Idle trong lúc tiến hóa)")]
        [Tooltip("Tên state Idle trong Animator Controller của pet (phải khớp chính xác tên state trong cửa sổ Animator).")]
        public string idleStateName = "Idle";
        [Tooltip("Các script khác (ngoài PetController) cần TẠM TẮT trong lúc tiến hóa. Sẽ tự bật lại khi xong.")]
        public MonoBehaviour[] disableDuringEvolution;

        [Header("Start Text")]
        public bool showStartText = true;
        [Tooltip("{0} = tên hiện tại của pet.")]
        public string startMessageFormat = "{0} đang bắt đầu tiến hóa!";
        public float startMessageDisplayTime = 1.5f;

        [Header("Congrats Text")]
        public bool showCongratsText = true;
        [Tooltip("{0} = tên cũ, {1} = tên mới.")]
        public string congratsMessageFormat = "Chúc mừng! {0} đã tiến hóa thành {1}!";
        public float congratsMinDisplayTime = 1f;
        public Key congratsContinueKey = Key.Space;

        GameObject activeRingVfx;
        static PetEvolutionPresentationController active;
        public static bool AnyPlaying => active != null && active.IsPlaying;
        EvolutionPresentationScope scope;
        PetController currentPet;
        public static void CancelFor(PetController pet){if(active!=null&&active.currentPet==pet)active.Cancel();}
        WhiteoutOverlay activeOverlay;
        Coroutine running;
        AsyncOperation loading;
        bool ownsScene, committed, completing;
        Action<bool> completion;
        Transform growing;
        Vector3 growingScale;
        public string LastError { get; private set; }
        public static void Present(Scene context, PetEvolutionService service, PetController pet, PetEvolutionMethod method, string itemId, Action<bool,string> complete) {
            var presenter=FindFor(context)??(pet!=null?FindFor(pet.gameObject.scene):null);
            if(presenter==null) { complete?.Invoke(false,"Scene này chưa có controller trình diễn tiến hóa.");return; }
            var preview=service!=null?service.CreatePreview(pet,method,itemId):null;
            if(preview==null||!preview.CanEvolve) { complete?.Invoke(false,preview?.DisabledReason??"Không tìm thấy dịch vụ tiến hóa.");return; }
            string message="Đã hủy trình diễn tiến hóa.";
            presenter.Play(pet,preview.CurrentName,preview.TargetName,
                ()=>service.TryEvolve(pet,out message,method,itemId),
                success=>complete?.Invoke(success,string.IsNullOrEmpty(presenter.LastError)?message:presenter.LastError));
        }
        public static PetEvolutionPresentationController FindFor(Scene scene) {
            foreach(var value in FindObjectsByType<PetEvolutionPresentationController>(FindObjectsSortMode.None))
                if(value.gameObject.scene==scene&&value.isActiveAndEnabled)return value;
            return null;
        }

        public bool IsPlaying { get; private set; }

        void Awake() => Instance = this;
        void OnDisable() => Cancel();
        void OnDestroy() { Cancel(); if (Instance == this) Instance = null; }
        public void Cancel() {
            if(!IsPlaying)return;
            if(running!=null)StopCoroutine(running);
            running=null; Finish();
        }
        void Finish() {
            if(completing||!IsPlaying)return; completing=true;
            activeOverlay?.Cleanup(); activeOverlay=null;
            if(growing!=null)growing.localScale=growingScale;
            StopAllCoroutines(); DespawnGroundRing();
            scope?.Dispose(); scope=null;
            if(EvolutionUI.Instance!=null)EvolutionUI.Instance.Hide();
            if(ownsScene) {
                string sceneName=evolutionSceneName;
                if(loading!=null&&!loading.isDone)loading.completed+=_=>UnloadOwned(sceneName);
                else UnloadOwned(sceneName);
            }
            loading=null; ownsScene=false; IsPlaying=false;currentPet=null;
            if(active==this)active=null;
            var callback=completion; completion=null; running=null; completing=false;
            try { callback?.Invoke(committed); } catch(Exception e) { Debug.LogException(e,this); }
        }
        static void UnloadOwned(string name) { var scene=SceneManager.GetSceneByName(name);if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene); }
        IEnumerator Guarded(IEnumerator routine) {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0) {
                bool more=false;object value=null;Exception failure=null;
                try { more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current; }
                catch(Exception e) { failure=e; }
                if(failure!=null) { LastError="Không thể hoàn tất trình diễn tiến hóa.";Debug.LogException(failure,this);break; }
                if(!more) { (stack.Pop() as IDisposable)?.Dispose();continue; }
                if(value is IEnumerator nested)stack.Push(nested);else yield return value;
            }
            Finish();
        }

        /// <summary>
        /// Chạy toàn bộ animation + scene-transition tiến hóa cho 1 pet.
        /// applyEvolution: hàm THỰC SỰ đổi dữ liệu/model (ví dụ PetEvolutionService.TryEvolve),
        ///   được gọi đúng lúc màn hình đang trắng xóa hoàn toàn. Trả về true nếu tiến hóa thành công.
        /// onComplete: gọi lại sau khi TOÀN BỘ animation + quay về scene gameplay xong, kèm kết quả.
        /// </summary>
        public void Play(PetController pet, string currentName, string targetName,
            Func<bool> applyEvolution, Action<bool> onComplete) {
            LastError=string.Empty;
            if(pet==null||applyEvolution==null||AnyPlaying||!isActiveAndEnabled) {
                LastError="Thao tác tiến hóa chưa sẵn sàng hoặc đang chạy.";onComplete?.Invoke(false);return;
            }
            if(!Application.CanStreamedLevelBeLoaded(evolutionSceneName)||SceneManager.GetSceneByName(evolutionSceneName).isLoaded||whiteOverlayMaterial==null) {
                LastError="Chưa cấu hình scene hoặc vật liệu trình diễn tiến hóa.";onComplete?.Invoke(false);return;
            }
            IsPlaying=true;active=this;committed=false;completion=onComplete;growing=null;currentPet=pet;
            try {
                scope=new EvolutionPresentationScope(this,pet,disableDuringEvolution);
                running=StartCoroutine(Guarded(PlayRoutine(pet,currentName,targetName,applyEvolution,onComplete)));
            }catch(Exception error){LastError="Không thể bắt đầu trình diễn tiến hóa.";Debug.LogException(error,this);Finish();}
        }

        IEnumerator PlayRoutine(PetController pet, string currentName, string targetName,
            Func<bool> applyEvolution, Action<bool> onComplete) {
            Transform petTransform=pet.transform;
            PetEvolutionVisualController visual=pet.GetComponentInChildren<PetEvolutionVisualController>(true);
            petTransform.SetParent(null,true);
            ownsScene=true;
            loading=SceneManager.LoadSceneAsync(evolutionSceneName,LoadSceneMode.Additive);
            if(loading==null)throw new InvalidOperationException("Không tải được scene tiến hóa.");
            yield return loading;
            Scene evoScene=SceneManager.GetSceneByName(evolutionSceneName);
            if(!evoScene.IsValid()||!evoScene.isLoaded)throw new InvalidOperationException("Scene tiến hóa chưa sẵn sàng.");
            SceneManager.MoveGameObjectToScene(pet.gameObject,evoScene);
            foreach(var renderer in pet.GetComponentsInChildren<Renderer>(true))
                if(visual==null||visual.IsCurrentVisualRenderer(renderer))renderer.enabled=true;
            if(visual!=null&&visual.ActiveAnimator!=null)visual.ActiveAnimator.enabled=true;
            int evoLayer = LayerMask.NameToLayer(evolutionOnlyLayerName);
            if (evoLayer == -1)
                Debug.LogWarning($"{name}: không tìm thấy Layer '{evolutionOnlyLayerName}'. Tạo layer này trong Project Settings.");
            else
                SetLayerRecursively(pet.gameObject, evoLayer);

            if (EvolutionStageAnchor.Instance != null) {
                petTransform.position = EvolutionStageAnchor.Instance.transform.position;
                petTransform.rotation = EvolutionStageAnchor.Instance.transform.rotation;
            }
            else {
                Debug.LogWarning($"{name}: không tìm thấy EvolutionStageAnchor trong '{evolutionSceneName}'.");
            }

            if (showStartText && EvolutionUI.Instance != null) {
                EvolutionUI.Instance.Show(string.Format(startMessageFormat, currentName));
                yield return new WaitForSecondsRealtime(startMessageDisplayTime);
                EvolutionUI.Instance.Hide();
            }

            SpawnGroundRing(petTransform);

            // 1) Ép animation model CŨ đứng yên ở Idle trước khi xoay.
            ForceIdle(visual != null ? visual.ActiveAnimator : pet.GetComponentInChildren<Animator>());

            // --- Xoay + sáng dần che model cũ ---
            // Chỉ phủ overlay lên đúng model ĐANG HIỂN THỊ (visual.ActiveVisualTransform), KHÔNG phải
            // cả petTransform — vì model cũ không bị Destroy (chỉ bị tắt renderer để còn phục hồi lúc
            // load save), nếu quét cả petTransform sẽ dính luôn "bóng" của model cũ đang ẩn.
            Transform overlayTarget = visual != null ? visual.ActiveVisualTransform : petTransform;
            WhiteoutOverlay overlay = activeOverlay = new WhiteoutOverlay(overlayTarget, whiteOverlayMaterial, whiteoutEmissionIntensity);
            yield return SpinAndFade(petTransform, overlay,
                spinUpDuration, spinUpCurve, glowStartDelay, whiteoutFadeInDuration, 0f, 1f);

            // --- Màn hình đang trắng hoàn toàn: đổi dữ liệu/model THẬT ngay lúc này ---
            bool success = committed = applyEvolution();
            overlay.Cleanup();

            // QUAN TRỌNG: model MỚI vừa Instantiate ra (bên trong applyEvolution/TryApply) KHÔNG tự
            // thừa hưởng layer "Evolution" của pet cha — phải gán lại layer cho cả cây con lần nữa,
            // nếu không camera trong Evolution Scene (Culling Mask chỉ chứa layer Evolution) sẽ không
            // thấy model mới, animation coi như "biến mất".
            if (evoLayer != -1)
                SetLayerRecursively(pet.gameObject, evoLayer);

            foreach(var renderer in pet.GetComponentsInChildren<Renderer>(true))
                if(visual==null||visual.IsCurrentVisualRenderer(renderer))renderer.enabled=true;
            if(visual!=null&&visual.ActiveAnimator!=null)visual.ActiveAnimator.enabled=true;

            // 2) Ép animation model MỚI (nếu đổi thành công) đứng yên ở Idle ngay khi vừa xuất hiện,
            //    rồi cho nó "lớn dần" song song lúc xoay + mờ dần lộ ra.
            if (success && visual != null) {
                ForceIdle(visual.ActiveAnimator);
                growing=visual.ActiveVisualTransform;growingScale=growing.localScale;
                StartCoroutine(GrowRoutine(growing));
            }

            // Model mới đã thay xong -> overlayTarget cũng phải lấy lại (ActiveVisualTransform giờ trỏ
            // sang model MỚI), không dùng lại overlayTarget cũ (đang trỏ model cũ).
            overlayTarget = visual != null ? visual.ActiveVisualTransform : petTransform;
            overlay = activeOverlay = new WhiteoutOverlay(overlayTarget, whiteOverlayMaterial, whiteoutEmissionIntensity);
            overlay.SetIntensity(1f);
            yield return SpinAndFade(petTransform, overlay,
                spinDownDuration, spinDownCurve, 0f, whiteoutFadeOutDuration, 1f, 0f);
            overlay.Cleanup();

            // Tắt vòng sáng NGAY khi vừa lộ rõ model mới, không đợi tới lúc người chơi tắt khung Congrats.
            DespawnGroundRing();

            if (success && showCongratsText && EvolutionUI.Instance != null) {
                EvolutionUI.Instance.Show(string.Format(congratsMessageFormat, currentName, targetName));
                yield return new WaitForSecondsRealtime(congratsMinDisplayTime);
                while (!(Keyboard.current != null && Keyboard.current[congratsContinueKey].wasPressedThisFrame))
                    yield return null;
                EvolutionUI.Instance.Hide();
            }

            // Guarded/Finish restores exact prior camera, HUD, input, layers and controller states.
        }

        /// <summary>Y nguyên kỹ thuật auto-correction 360° từ EvolutionController.cs, chỉ đổi target thành Transform truyền vào.</summary>
        IEnumerator SpinAndFade(Transform target, WhiteoutOverlay overlay, float spinDuration, AnimationCurve spinCurve,
            float fadeDelay, float fadeDuration, float fadeFrom, float fadeTo) {
            Quaternion phaseStartRotation = target.rotation;

            const int SAMPLE_COUNT = 60;
            float naiveTotalDegrees = 0f;
            if (spinDuration > 0f) {
                float sampleDt = spinDuration / SAMPLE_COUNT;
                for (int i = 0; i < SAMPLE_COUNT; i++) {
                    float sampleK = (i + 0.5f) / SAMPLE_COUNT;
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

            while (t < total) {
                float dt = Time.unscaledDeltaTime;
                t += dt;

                if (spinDuration > 0f) {
                    float spinK = Mathf.Clamp01(t / spinDuration);
                    float spinSpeed = spinCurve.Evaluate(spinK) * maxSpinSpeed * correctionScale;
                    rotatedSoFar += spinSpeed * dt;
                    target.rotation = phaseStartRotation * Quaternion.Euler(0f, rotatedSoFar, 0f);
                }

                if (t >= fadeDelay) {
                    float fadeK = fadeDuration > 0f ? Mathf.Clamp01((t - fadeDelay) / fadeDuration) : 1f;
                    overlay?.SetIntensity(Mathf.Lerp(fadeFrom, fadeTo, fadeK));
                }

                yield return null;
            }

            if (spinDuration > 0f)
                target.rotation = phaseStartRotation * Quaternion.Euler(0f, targetTotalDegrees, 0f);
            overlay?.SetIntensity(fadeTo);
        }

        IEnumerator GrowRoutine(Transform target) {
            if (target == null) yield break;
            Vector3 finalScale = target.localScale;
            float t = 0f;
            while (t < growDuration) {
                t += Time.unscaledDeltaTime;
                float k = growCurve.Evaluate(Mathf.Clamp01(t / growDuration));
                target.localScale = finalScale * k;
                yield return null;
            }
            target.localScale = finalScale;
        }

        /// <summary>Ép Animator (nếu có) chuyển ngay về state Idle và đứng yên ở đó.</summary>
        void ForceIdle(Animator anim) {
            if (anim == null || string.IsNullOrEmpty(idleStateName)) return;
            anim.Play(idleStateName, 0, 0f);
            anim.Update(0f);
        }

        void SetControllingScriptsEnabled(bool value) {
            if (disableDuringEvolution == null) return;
            foreach (var behaviour in disableDuringEvolution)
                if (behaviour != null) behaviour.enabled = value;
        }

        void SpawnGroundRing(Transform target) {
            if (groundRingVfxPrefab == null) return;
            Vector3 pos = target.position + Vector3.up * groundRingYOffset;
            activeRingVfx = Instantiate(groundRingVfxPrefab, pos, Quaternion.identity);
            activeRingVfx.transform.localScale = activeRingVfx.transform.localScale * groundRingScale;
            SetLayerRecursively(activeRingVfx, target.gameObject.layer);
        }

        void DespawnGroundRing() {
            if (activeRingVfx != null) {
                Destroy(activeRingVfx);
                activeRingVfx = null;
            }
        }

        static void SetLayerRecursively(GameObject root, int layer) {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }
    }
}