using System;
using UnityEngine;
using UnityEngine.UI;
using Capstone.Game.HudSystem;
using Capstone.Game.UISystem;

namespace Capstone.Game.ProfileSystem {
    [DisallowMultipleComponent]
    public sealed partial class ProfilePanelController : MonoBehaviour {
        public Transform NavigationRoot => nameDialog != null && nameDialog.gameObject.activeSelf ? nameDialog : avatarDialog != null && avatarDialog.gameObject.activeSelf ? avatarDialog : root;
        GameObject popupReturnFocus;
        const string RootName = "ProfilePanelRoot";

        static readonly Color White = GreenAngularGeometry.Panel;
        static readonly Color Forest = GreenAngularGeometry.Ink;
        static readonly Color Accent = GreenAngularGeometry.Primary;
        static readonly Color Pale = GreenAngularGeometry.Raised;
        static readonly Color Border = GreenAngularGeometry.Edge;
        static readonly Color Muted = GreenAngularGeometry.Muted;

        static Sprite solidSprite;
        static Sprite circleSprite;

        [Header("References")]
        [SerializeField] Canvas targetCanvas;
        [SerializeField] MonoBehaviour providerSource;
        [SerializeField] MonoBehaviour teamProviderSource;

        [Header("Behaviour")]
        [SerializeField] bool buildOnAwake = true;
        [SerializeField] bool closeOnStart = true;

        RectTransform root;
        Image avatarImage;
        Text avatarPlaceholder;
        Button changeAvatarButton;
        Text displayNameText;
        Text levelText;
        Image experienceFill;
        Text experienceValueText;
        Text experiencePercentText;
        Text playerIdValue;
        Text playTimeValue;
        Text dateStartedValue;
        Text currentAreaValue;
        Text creaturesSeenValue;
        Text speciesCapturedValue;
        Text codexValue;
        Text storyProgressValue;
        Text bossesDefeatedValue;
        Text totalBattlesValue;
        RectTransform nameDialog;
        InputField nameInput;
        Text nameError;
        RectTransform avatarDialog;
        RectTransform avatarGrid;
        ScrollRect avatarScroll;
        IPlayerProfileProvider provider;
        bool openedExplicitly;

        public event Action BackRequested;

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        void Awake() {
            ResolveCanvas();
            ResolveProvider();
            if (buildOnAwake) RebuildPanel();
        }

        void OnEnable() {
            ResolveProvider();
            SubscribeProvider();
            ResolveTeamProvider();
            SubscribeTeamProvider();
        }

        void Start() {
            if (closeOnStart && !openedExplicitly) Close();
        }

        void OnDisable() {
            Close();
            UnsubscribeProvider();
            UnsubscribeTeamProvider();
        }

        public void Configure(Canvas canvas, MonoBehaviour profileProvider = null) {
            targetCanvas = canvas;
            if (profileProvider != null) providerSource = profileProvider;
            ResolveProvider();
        }

        public void SetTargetCanvas(Canvas canvas) {
            if (canvas != null) targetCanvas = canvas;
        }

        [ContextMenu("Rebuild Profile Panel")]
        public void RebuildPanel() {
            ResolveCanvas();
            ResolveProvider();
            if (targetCanvas == null) return;

            Transform existing = targetCanvas.transform.Find(RootName);
            if (existing != null) {
                root = existing.GetComponent<RectTransform>();
                ClearChildren(root);
            } else {
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(Image));
                rootObject.transform.SetParent(targetCanvas.transform, false);
                root = rootObject.GetComponent<RectTransform>();
            }

            Stretch(root);
            Image rootImage = root.GetComponent<Image>();
            rootImage.sprite = SolidSprite;
            rootImage.color = MenuPalette.Background;
            rootImage.raycastTarget = true;

            BuildDesignSurface();
            BuildHeader();
            BuildProfileSummary();
            BuildLowerPanels();
            ResolveTeamProvider();
            FitDesignSurface();
            BuildNameDialog();
            BuildAvatarDialog();
            ApplyR2Skin();

            root.SetAsLastSibling();
            Refresh();
            if (closeOnStart) root.gameObject.SetActive(false);
        }

        public void Open() {
            openedExplicitly = true;
            selectedTeamIndex = -1;
            if (root == null) RebuildPanel();
            if (root == null) return;

            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            ResolveTeamProvider();
            FitDesignSurface();
            HideNameDialog();
            HideAvatarDialog();
            Refresh();
            Capstone.Game.UISystem.MenuKeyboardNavigation.FocusFirst(root);
        }

        public void Close() {
            openedExplicitly = false;
            HideNameDialog();
            HideAvatarDialog();
            if (root != null) root.gameObject.SetActive(false);
        }

        void RequestBack() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "RequestBack")) return;
            if (TryClosePopup()) return;
            if (BackRequested != null) BackRequested.Invoke();
            else Close();
        }

        public bool TryClosePopup() {
            if (avatarDialog != null && avatarDialog.gameObject.activeSelf) {
                HideAvatarDialog();
                return true;
            }

            if (nameDialog != null && nameDialog.gameObject.activeSelf) {
                HideNameDialog();
                return true;
            }

            return false;
        }

        void BuildNameDialog() { BuildR2NameDialog(); }

        void BuildAvatarDialog() { BuildR2AvatarDialog(); }

        // Reuse the existing rename modal from the Trainer summary; no new save/edit flow.
        public void EditDisplayName() { if (IsOpen) ShowNameDialog(); }

        void ShowNameDialog() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "ShowNameDialog")) return;
            if (nameDialog == null || nameInput == null) return;
            popupReturnFocus = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            PlayerProfileSnapshot snapshot = provider != null ? provider.GetSnapshot() : default;
            nameInput.text = snapshot.DisplayName ?? string.Empty;
            RefreshR2PopupIdentity(snapshot);
            nameError.text = string.Empty;
            nameDialog.gameObject.SetActive(true);
            nameDialog.SetAsLastSibling();
            nameInput.ActivateInputField();
        }

        void HideNameDialog() {
            bool wasOpen = nameDialog != null && nameDialog.gameObject.activeSelf;
            if (nameDialog != null) nameDialog.gameObject.SetActive(false);
            if (wasOpen) { Capstone.Game.UISystem.MenuKeyboardNavigation.Restore(popupReturnFocus); popupReturnFocus = null; }
        }

        void ShowAvatarDialog() {
            if (avatarDialog == null || avatarGrid == null || provider == null || !provider.CanChangeAvatar) return;
            popupReturnFocus = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            RebuildAvatarGrid();
            RefreshR2PopupIdentity(provider.GetSnapshot());
            avatarDialog.gameObject.SetActive(true);
            avatarDialog.SetAsLastSibling();
            if (avatarScroll != null) avatarScroll.verticalNormalizedPosition = 1f;
            Capstone.Game.UISystem.MenuKeyboardNavigation.Advance(avatarDialog, false);
        }

        void HideAvatarDialog() {
            bool wasOpen = avatarDialog != null && avatarDialog.gameObject.activeSelf;
            if (avatarDialog != null) avatarDialog.gameObject.SetActive(false);
            if (wasOpen) { Capstone.Game.UISystem.MenuKeyboardNavigation.Restore(popupReturnFocus); popupReturnFocus = null; }
        }

        void RebuildAvatarGrid() { RebuildR2AvatarGrid(); }

        void SelectAvatar(int avatarIndex) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "SelectAvatar")) return;
            if (provider == null || !provider.TrySelectAvatar(avatarIndex)) return;
            HideAvatarDialog();
            Refresh();
        }

        void ConfirmNameChange() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "ConfirmNameChange")) return;
            if (provider == null || nameInput == null) return;
            if (provider.TrySetDisplayName(nameInput.text, out string error)) {
                HideNameDialog();
                Refresh();
            } else if (nameError != null) {
                nameError.text = error;
            }
        }

        void ChangeAvatar() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "ChangeAvatar")) return;
            ShowAvatarDialog();
        }

        void ResolveCanvas() {
            if (targetCanvas != null) return;
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases) {
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) {
                    targetCanvas = canvas;
                    return;
                }
            }
        }

        void ResolveProvider() {
            IPlayerProfileProvider nextProvider = providerSource as IPlayerProfileProvider;
            if (nextProvider == null) {
                MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (MonoBehaviour behaviour in behaviours) {
                    if (behaviour is IPlayerProfileProvider candidate) {
                        providerSource = behaviour;
                        nextProvider = candidate;
                        break;
                    }
                }
            }

            if (nextProvider == null) {
                PlayerProfileRuntimeProvider runtimeProvider = GetComponent<PlayerProfileRuntimeProvider>();
                if (runtimeProvider == null) runtimeProvider = gameObject.AddComponent<PlayerProfileRuntimeProvider>();
                providerSource = runtimeProvider;
                nextProvider = runtimeProvider;
            }

            if (ReferenceEquals(provider, nextProvider)) return;
            UnsubscribeProvider();
            provider = nextProvider;
            SubscribeProvider();
        }

        void SubscribeProvider() {
            if (provider == null || !isActiveAndEnabled) return;
            provider.ProfileChanged -= HandleProfileChanged;
            provider.ProfileChanged += HandleProfileChanged;
        }

        void UnsubscribeProvider() {
            if (provider == null) return;
            provider.ProfileChanged -= HandleProfileChanged;
        }

        void HandleProfileChanged() { if (IsOpen) Refresh(); }

        void Refresh() {
            if (provider == null) return;
            PlayerProfileSnapshot snapshot = provider.GetSnapshot();

            if (displayNameText != null) {
                displayNameText.text = string.IsNullOrWhiteSpace(snapshot.DisplayName)
                    ? "Tên người chơi"
                    : snapshot.DisplayName;
            }

            if (levelText != null) levelText.text = snapshot.Level > 0 ? snapshot.Level.ToString() : "-";

            float experienceProgress = snapshot.RequiredExperience > 0
                ? Mathf.Clamp01((float)snapshot.CurrentExperience / snapshot.RequiredExperience)
                : 0f;
            if (experienceFill != null) {
                experienceFill.fillAmount = experienceProgress;
            }

            if (experienceValueText != null) {
                experienceValueText.text = snapshot.RequiredExperience > 0
                    ? snapshot.CurrentExperience + " / " + snapshot.RequiredExperience
                    : "- / -";
            }

            if (experiencePercentText != null) {
                experiencePercentText.text = snapshot.RequiredExperience > 0
                    ? Mathf.RoundToInt(experienceProgress * 100f) + "%"
                    : "-";
            }

            if (avatarImage != null) {
                avatarImage.sprite = snapshot.Avatar;
                avatarImage.color = snapshot.Avatar != null ? Color.white : Color.clear;
            }

            if (avatarPlaceholder != null) {
                avatarPlaceholder.gameObject.SetActive(snapshot.Avatar == null);
            }

            if (changeAvatarButton != null) changeAvatarButton.interactable = provider.CanChangeAvatar;
            if (avatarCameraButton != null) avatarCameraButton.interactable = provider.CanChangeAvatar;
            if (avatarPlaceholder != null) avatarPlaceholder.text = string.IsNullOrWhiteSpace(snapshot.DisplayName) ? "?" : snapshot.DisplayName.Substring(0, 1).ToUpperInvariant();
            SetText(playerIdValue, snapshot.PlayerId);
            SetText(playTimeValue, FormatPlayTime(snapshot.PlayTimeSeconds));
            SetText(dateStartedValue, snapshot.DateStarted);
            SetText(currentAreaValue, snapshot.CurrentArea);
            SetText(creaturesSeenValue, FormatOptionalCount(snapshot.CreaturesSeen));
            SetText(speciesCapturedValue, FormatOptionalCount(snapshot.SpeciesCaptured));
            SetText(codexValue, FormatPercent(snapshot.CodexEntries, snapshot.CodexTotal));
            SetText(storyProgressValue, FormatPercent(snapshot.StoryCompleted, snapshot.StoryTotal));
            SetText(bossesDefeatedValue, FormatOptionalCount(snapshot.BossesDefeated));
            SetText(totalBattlesValue, FormatOptionalCount(snapshot.TotalBattles));
            RefreshTeam();
            RefreshR2Skin(snapshot);
        }

        static string FormatOptionalCount(int value) {
            return value >= 0 ? value.ToString() : Capstone.Game.UISystem.VietnameseUI.Unknown;
        }

        static string FormatPercent(int value, int total) {
            if (value < 0 || total <= 0) return "-";
            return Mathf.RoundToInt(Mathf.Clamp01((float)value / total) * 100f) + "%";
        }

        static string FormatPlayTime(double seconds) {
            if (seconds < 0d) return "-";
            TimeSpan duration = TimeSpan.FromSeconds(seconds);
            return duration.Days + " ngày "
                + duration.Hours.ToString("00") + " giờ "
                + duration.Minutes.ToString("00") + " phút";
        }

        static void SetText(Text label, string value) {
            if (label != null) label.text = string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        static InputField CreateInputField(Transform parent, string name, float x, float y, float width, float height) {
            RectTransform rect = CreatePanel(parent, name, Color.white, true);
            SetTopLeft(rect, x, y, width, height);
            AddOutline(rect.gameObject, Border, 1f);

            InputField input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = rect.GetComponent<Image>();
            Text text = CreateText(rect, "Text", string.Empty, 18, FontStyle.Normal, Forest, TextAnchor.MiddleLeft);
            SetInset(text.rectTransform, 14f);
            text.supportRichText = false;
            input.textComponent = text;

            Text placeholder = CreateText(rect, "Placeholder", "Nhập tên người chơi", 18, FontStyle.Italic, new Color(Muted.r, Muted.g, Muted.b, 0.55f), TextAnchor.MiddleLeft);
            SetInset(placeholder.rectTransform, 14f);
            input.placeholder = placeholder;
            input.characterLimit = 20;
            return input;
        }

        static Button CreateOutlinedButton(Transform parent, string name, string label, int fontSize) {
            RectTransform rect = CreatePanel(parent, name, White, true);
            AddOutline(rect.gameObject, Border, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            // Selectable.Reset assigns this only in the Editor, never on runtime AddComponent.
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Pale;
            colors.pressedColor = new Color(0.78f, 0.89f, 0.75f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.90f, 0.92f, 0.89f, 0.58f);
            button.colors = colors;

            if (!string.IsNullOrEmpty(label)) {
                Text text = CreateText(rect, "Text", label, fontSize, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform);
            }
            return button;
        }

        static RectTransform CreatePanel(Transform parent, string name, Color color, bool raycastTarget) {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return obj.GetComponent<RectTransform>();
        }

        static Text CreateText(Transform parent, string name, string value, int size, FontStyle style, Color color, TextAnchor alignment) {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text text = obj.GetComponent<Text>();
            text.font = DefaultFont;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        static void AddOutline(GameObject obj, Color color, float distance) {
            Outline outline = obj.GetComponent<Outline>();
            if (outline == null) outline = obj.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        static void ClearChildren(Transform parent) {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) DestroySafe(parent.GetChild(i).gameObject);
        }

        static void DestroySafe(GameObject obj) {
            if (obj == null) return;
            obj.SetActive(false);
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        static void Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        static void SetInset(RectTransform rect, float inset) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
        }

        static void SetTopLeft(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static void SetTopRight(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static void SetTopStretch(RectTransform rect, float left, float top, float right, float height) {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        static void SetBottomLeft(RectTransform rect, float left, float bottom, float width, float height) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(left, bottom);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static void SetBottomRight(RectTransform rect, float right, float bottom, float width, float height) {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-right, bottom);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static void SetBottomCenter(RectTransform rect, float x, float bottom, float width, float height) {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, bottom);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static void SetBottomStretch(RectTransform rect, float left, float bottom, float right, float height) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
            rect.localScale = Vector3.one;
        }

        static void SetCenter(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static Sprite SolidSprite {
            get {
                if (solidSprite != null) return solidSprite;
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) {
                    name = "ProfileSolidTexture",
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                solidSprite.name = "ProfileSolidSprite";
                solidSprite.hideFlags = HideFlags.HideAndDontSave;
                return solidSprite;
            }
        }

        static Sprite CircleSprite {
            get {
                if (circleSprite != null) return circleSprite;
                const int size = 512;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                    name = "ProfileCircleTexture",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                Color[] pixels = new Color[size * size];
                float center = (size - 1) * 0.5f;
                float radius = center - 1f;
                for (int y = 0; y < size; y++) {
                    for (int x = 0; x < size; x++) {
                        float dx = x - center;
                        float dy = y - center;
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f));
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();
                circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                circleSprite.name = "ProfileCircleSprite";
                circleSprite.hideFlags = HideFlags.HideAndDontSave;
                return circleSprite;
            }
        }

        static Font DefaultFont => Capstone.Game.UISystem.MenuTypography.Font;
    }
}
