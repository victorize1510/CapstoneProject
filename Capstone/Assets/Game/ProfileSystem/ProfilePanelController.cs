using System;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.ProfileSystem {
    [DisallowMultipleComponent]
    public sealed class ProfilePanelController : MonoBehaviour {
        const string RootName = "ProfilePanelRoot";

        static readonly Color White = new Color(0.985f, 0.99f, 0.975f, 1f);
        static readonly Color Forest = new Color(0.035f, 0.29f, 0.09f, 1f);
        static readonly Color Accent = new Color(0.20f, 0.58f, 0.16f, 1f);
        static readonly Color Pale = new Color(0.88f, 0.94f, 0.86f, 1f);
        static readonly Color Border = new Color(0.35f, 0.55f, 0.34f, 0.72f);
        static readonly Color Muted = new Color(0.30f, 0.40f, 0.31f, 1f);

        static Sprite solidSprite;
        static Sprite circleSprite;
        static Font cachedFont;

        [Header("References")]
        [SerializeField] Canvas targetCanvas;
        [SerializeField] MonoBehaviour providerSource;

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
        RectTransform badgeContainer;
        Text achievementCompletionText;
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
        }

        void Start() {
            if (closeOnStart && !openedExplicitly) Close();
        }

        void OnDisable() {
            UnsubscribeProvider();
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
            rootImage.color = White;
            rootImage.raycastTarget = true;

            BuildOuterFrame();
            BuildHeader();
            BuildProfileSummary();
            BuildLowerPanels();
            BuildNameDialog();
            BuildAvatarDialog();

            root.SetAsLastSibling();
            Refresh();
            if (closeOnStart) root.gameObject.SetActive(false);
        }

        public void Open() {
            openedExplicitly = true;
            if (root == null) RebuildPanel();
            if (root == null) return;

            root.SetAsLastSibling();
            root.gameObject.SetActive(true);
            HideNameDialog();
            HideAvatarDialog();
            Refresh();
        }

        public void Close() {
            openedExplicitly = false;
            HideNameDialog();
            HideAvatarDialog();
            if (root != null) root.gameObject.SetActive(false);
        }

        void RequestBack() {
            if (avatarDialog != null && avatarDialog.gameObject.activeSelf) {
                HideAvatarDialog();
                return;
            }

            if (nameDialog != null && nameDialog.gameObject.activeSelf) {
                HideNameDialog();
                return;
            }

            if (BackRequested != null) BackRequested.Invoke();
            else Close();
        }

        void BuildOuterFrame() {
            Outline outline = root.GetComponent<Outline>();
            if (outline == null) outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            RectTransform bottomLeft = CreatePanel(root, "BottomLeftDecoration", Pale, false);
            SetBottomLeft(bottomLeft, 28f, 24f, 110f, 5f);
            RectTransform bottomRight = CreatePanel(root, "BottomRightDecoration", Pale, false);
            SetBottomRight(bottomRight, 28f, 24f, 110f, 5f);
        }

        void BuildHeader() {
            RectTransform header = CreatePanel(root, "ProfileHeader", White, true);
            SetTopStretch(header, 8f, 8f, 8f, 88f);
            AddOutline(header.gameObject, Border, 1f);

            RectTransform separator = CreatePanel(header, "Separator", Pale, false);
            SetBottomStretch(separator, 0f, 0f, 0f, 2f);

            RectTransform emblem = CreatePanel(header, "Emblem", Accent, false);
            SetTopLeft(emblem, 42f, 27f, 30f, 30f);
            emblem.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Text title = CreateText(header, "Title", "PROFILE", 39, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(title.rectTransform, 92f, 11f, 350f, 66f);

            Button back = CreateOutlinedButton(header, "BackButton", "<  QUAY LẠI", 18);
            SetTopRight(back.GetComponent<RectTransform>(), 28f, 18f, 202f, 52f);
            back.onClick.AddListener(RequestBack);
        }

        void BuildProfileSummary() {
            RectTransform summary = CreatePanel(root, "ProfileSummary", White, true);
            SetTopStretch(summary, 24f, 110f, 24f, 392f);
            AddOutline(summary.gameObject, Border, 1f);

            RectTransform avatarOuter = CreatePanel(summary, "AvatarOuter", Forest, false);
            avatarOuter.GetComponent<Image>().sprite = CircleSprite;
            SetTopLeft(avatarOuter, 138f, 24f, 274f, 274f);

            RectTransform avatarInner = CreatePanel(avatarOuter, "AvatarMask", White, true);
            avatarInner.GetComponent<Image>().sprite = CircleSprite;
            SetInset(avatarInner, 4f);
            Mask mask = avatarInner.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            avatarImage = CreatePanel(avatarInner, "Avatar", Color.clear, false).GetComponent<Image>();
            Stretch(avatarImage.rectTransform);
            avatarImage.preserveAspect = true;

            avatarPlaceholder = CreateText(avatarInner, "AvatarPlaceholder", "AVATAR", 22, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            Stretch(avatarPlaceholder.rectTransform);

            RectTransform avatarDiamond = CreatePanel(summary, "AvatarDiamond", new Color(0.75f, 0.94f, 0.95f, 1f), false);
            SetTopLeft(avatarDiamond, 263f, 13f, 24f, 24f);
            avatarDiamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddOutline(avatarDiamond.gameObject, Forest, 1f);

            RectTransform leafLeft = CreatePanel(summary, "AvatarLeafLeft", Pale, false);
            SetTopLeft(leafLeft, 128f, 260f, 66f, 9f);
            leafLeft.localRotation = Quaternion.Euler(0f, 0f, 32f);
            RectTransform leafRight = CreatePanel(summary, "AvatarLeafRight", Pale, false);
            SetTopLeft(leafRight, 356f, 260f, 66f, 9f);
            leafRight.localRotation = Quaternion.Euler(0f, 0f, -32f);

            changeAvatarButton = CreateOutlinedButton(summary, "ChangeAvatarButton", "CHANGE AVATAR", 17);
            SetTopLeft(changeAvatarButton.GetComponent<RectTransform>(), 156f, 306f, 238f, 52f);
            changeAvatarButton.onClick.AddListener(ChangeAvatar);

            displayNameText = CreateText(summary, "DisplayName", string.Empty, 38, FontStyle.Normal, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(displayNameText.rectTransform, 500f, 54f, 520f, 58f);

            RectTransform nameLine = CreatePanel(summary, "NameLine", Border, false);
            SetTopLeft(nameLine, 500f, 124f, 520f, 1f);

            Button changeName = CreateOutlinedButton(summary, "ChangeNameButton", "CHANGE NAME", 17);
            SetTopLeft(changeName.GetComponent<RectTransform>(), 1032f, 57f, 220f, 52f);
            changeName.onClick.AddListener(ShowNameDialog);

            RectTransform levelBadge = CreatePanel(summary, "LevelBadge", White, false);
            SetTopLeft(levelBadge, 500f, 160f, 110f, 110f);
            AddOutline(levelBadge.gameObject, Border, 2f);
            Text levelCaption = CreateText(levelBadge, "LevelCaption", "Lv.", 18, FontStyle.Normal, Forest, TextAnchor.UpperCenter);
            SetTopStretch(levelCaption.rectTransform, 4f, 12f, 4f, 28f);
            levelText = CreateText(levelBadge, "LevelValue", "-", 42, FontStyle.Normal, Accent, TextAnchor.MiddleCenter);
            SetBottomStretch(levelText.rectTransform, 4f, 10f, 4f, 62f);

            Text experienceCaption = CreateText(summary, "ExperienceCaption", "EXP", 18, FontStyle.Normal, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(experienceCaption.rectTransform, 648f, 168f, 100f, 32f);
            experiencePercentText = CreateText(summary, "ExperiencePercent", "-", 18, FontStyle.Normal, Forest, TextAnchor.MiddleRight);
            SetTopLeft(experiencePercentText.rectTransform, 1140f, 168f, 116f, 32f);

            RectTransform experienceTrack = CreatePanel(summary, "ExperienceTrack", Pale, false);
            SetTopLeft(experienceTrack, 648f, 210f, 608f, 18f);
            experienceFill = CreatePanel(experienceTrack, "Fill", Accent, false).GetComponent<Image>();
            experienceFill.type = Image.Type.Filled;
            experienceFill.fillMethod = Image.FillMethod.Horizontal;
            experienceFill.fillOrigin = 0;
            experienceFill.fillAmount = 0f;
            Stretch(experienceFill.rectTransform);

            experienceValueText = CreateText(summary, "ExperienceValue", "- / -", 18, FontStyle.Normal, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(experienceValueText.rectTransform, 648f, 240f, 608f, 36f);

            Text badgeTitle = CreateText(summary, "BadgeTitle", "HUY HIỆU", 17, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(badgeTitle.rectTransform, 1328f, 46f, 210f, 32f);
            achievementCompletionText = CreateText(summary, "AchievementCompletion", "0%", 16, FontStyle.Normal, Muted, TextAnchor.MiddleRight);
            SetTopLeft(achievementCompletionText.rectTransform, 1530f, 46f, 170f, 32f);
            badgeContainer = CreatePanel(summary, "BadgeShowcase", new Color(Pale.r, Pale.g, Pale.b, 0.35f), false);
            SetTopLeft(badgeContainer, 1328f, 88f, 372f, 190f);
            AddOutline(badgeContainer.gameObject, Border, 1f);

            RectTransform decoration = CreatePanel(summary, "SummaryDecoration", Pale, false);
            SetTopStretch(decoration, 500f, 318f, 392f, 2f);
        }

        void BuildLowerPanels() {
            RectTransform playerInfo = CreatePanel(root, "PlayerInfoPanel", White, true);
            SetBottomLeft(playerInfo, 24f, 24f, 680f, 530f);
            AddOutline(playerInfo.gameObject, Border, 1f);
            BuildSectionHeader(playerInfo, "PLAYER INFO", "P");

            playerIdValue = BuildInfoRow(playerInfo, "ID", "Player ID", 100f);
            playTimeValue = BuildInfoRow(playerInfo, "T", "Play Time", 198f);
            currentAreaValue = BuildInfoRow(playerInfo, "A", "Current Area", 296f);
            dateStartedValue = BuildInfoRow(playerInfo, "D", "Play Start Date", 394f);

            RectTransform stats = CreatePanel(root, "StatsOverviewPanel", White, true);
            SetBottomStretch(stats, 720f, 24f, 24f, 530f);
            AddOutline(stats.gameObject, Border, 1f);
            BuildSectionHeader(stats, "STATS OVERVIEW", "S");

            creaturesSeenValue = BuildStatCard(stats, "CreaturesSeen", "PAW", "Creatures Seen", 26f, 116f);
            speciesCapturedValue = BuildStatCard(stats, "SpeciesCaptured", "PET", "Species Captured", 403f, 116f);
            codexValue = BuildStatCard(stats, "Codex", "BOOK", "Codex", 780f, 116f);
            storyProgressValue = BuildStatCard(stats, "StoryProgress", "STORY", "Story Progress", 26f, 318f);
            bossesDefeatedValue = BuildStatCard(stats, "BossesDefeated", "BOSS", "Bosses Defeated", 403f, 318f);
            totalBattlesValue = BuildStatCard(stats, "TotalBattles", "VS", "Total Battles", 780f, 318f);
        }

        void BuildSectionHeader(RectTransform panel, string title, string iconText) {
            Text icon = CreateText(panel, "HeaderIcon", iconText, 13, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            SetTopLeft(icon.rectTransform, 26f, 20f, 62f, 38f);
            Text heading = CreateText(panel, "HeaderTitle", title, 25, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(heading.rectTransform, 94f, 18f, 330f, 42f);
            RectTransform divider = CreatePanel(panel, "HeaderDivider", Pale, false);
            SetTopStretch(divider, 26f, 78f, 26f, 2f);
        }

        Text BuildInfoRow(RectTransform parent, string iconText, string labelText, float top) {
            RectTransform iconBackground = CreatePanel(parent, labelText + "IconBackground", Accent, false);
            SetTopLeft(iconBackground, 34f, top, 46f, 46f);
            Text icon = CreateText(iconBackground, labelText + "Icon", iconText, 13, FontStyle.Bold, White, TextAnchor.MiddleCenter);
            Stretch(icon.rectTransform);

            Text label = CreateText(parent, labelText + "Label", labelText, 20, FontStyle.Normal, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(label.rectTransform, 102f, top, 235f, 46f);

            Text value = CreateText(parent, labelText + "Value", string.Empty, 20, FontStyle.Normal, Forest, TextAnchor.MiddleRight);
            SetTopStretch(value.rectTransform, 338f, top, 38f, 46f);

            RectTransform line = CreatePanel(parent, labelText + "Line", new Color(Border.r, Border.g, Border.b, 0.45f), false);
            SetTopStretch(line, 34f, top + 66f, 38f, 1f);
            return value;
        }

        Text BuildStatCard(RectTransform parent, string name, string iconText, string labelText, float left, float top) {
            RectTransform card = CreatePanel(parent, name + "Card", White, false);
            SetTopLeft(card, left, top, 350f, 174f);
            AddOutline(card.gameObject, new Color(Border.r, Border.g, Border.b, 0.45f), 1f);

            RectTransform iconCircle = CreatePanel(card, "IconCircle", Pale, false);
            iconCircle.GetComponent<Image>().sprite = CircleSprite;
            SetTopLeft(iconCircle, 26f, 26f, 86f, 86f);
            Text icon = CreateText(iconCircle, "Icon", iconText, 12, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            Stretch(icon.rectTransform);

            Text value = CreateText(card, "Value", string.Empty, 34, FontStyle.Normal, Forest, TextAnchor.MiddleCenter);
            SetTopStretch(value.rectTransform, 126f, 28f, 18f, 80f);
            RectTransform line = CreatePanel(card, "ValueLine", new Color(Border.r, Border.g, Border.b, 0.42f), false);
            SetTopStretch(line, 22f, 121f, 22f, 1f);
            Text label = CreateText(card, "Label", labelText, 18, FontStyle.Normal, Forest, TextAnchor.MiddleCenter);
            SetBottomStretch(label.rectTransform, 14f, 8f, 14f, 46f);
            return value;
        }

        void BuildBadgeShowcase(AchievementSnapshot[] badges, float completion01) {
            if (badgeContainer == null) return;
            ClearChildren(badgeContainer);

            if (achievementCompletionText != null) {
                achievementCompletionText.text = Mathf.RoundToInt(Mathf.Clamp01(completion01) * 100f) + "%";
            }

            if (badges == null || badges.Length == 0) {
                Text empty = CreateText(badgeContainer, "Empty", "Chưa có huy hiệu", 17, FontStyle.Italic, Muted, TextAnchor.MiddleCenter);
                Stretch(empty.rectTransform);
                return;
            }

            int count = Mathf.Min(4, badges.Length);
            const float slotWidth = 88f;
            for (int i = 0; i < count; i++) {
                AchievementSnapshot badge = badges[i];
                RectTransform slot = CreatePanel(badgeContainer, "Badge_" + badge.AchievementId, Color.clear, false);
                SetTopLeft(slot, 10f + i * slotWidth, 18f, 82f, 154f);

                RectTransform iconFrame = CreatePanel(slot, "IconFrame", Pale, false);
                iconFrame.GetComponent<Image>().sprite = CircleSprite;
                SetTopLeft(iconFrame, 5f, 4f, 72f, 72f);
                AddOutline(iconFrame.gameObject, Accent, 1f);

                if (badge.Icon != null) {
                    Image icon = CreatePanel(iconFrame, "Icon", Color.white, false).GetComponent<Image>();
                    SetInset(icon.rectTransform, 9f);
                    icon.sprite = badge.Icon;
                    icon.preserveAspect = true;
                } else {
                    string initial = !string.IsNullOrWhiteSpace(badge.DisplayName)
                        ? badge.DisplayName.Substring(0, 1).ToUpperInvariant()
                        : "?";
                    Text placeholder = CreateText(iconFrame, "Placeholder", initial, 25, FontStyle.Bold, Accent, TextAnchor.MiddleCenter);
                    Stretch(placeholder.rectTransform);
                }

                Text title = CreateText(slot, "Title", badge.DisplayName, 13, FontStyle.Bold, Forest, TextAnchor.UpperCenter);
                SetTopStretch(title.rectTransform, 0f, 88f, 0f, 58f);
            }
        }

        void BuildNameDialog() {
            nameDialog = CreatePanel(root, "ChangeNameDialogOverlay", new Color(0f, 0f, 0f, 0.36f), true);
            Stretch(nameDialog);

            RectTransform dialog = CreatePanel(nameDialog, "Dialog", White, true);
            SetCenter(dialog, 0f, 0f, 570f, 278f);
            AddOutline(dialog.gameObject, Border, 2f);

            Text title = CreateText(dialog, "Title", "CHANGE NAME", 27, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopStretch(title.rectTransform, 30f, 22f, 30f, 48f);

            nameInput = CreateInputField(dialog, "NameInput", 30f, 86f, 510f, 58f);
            nameError = CreateText(dialog, "Error", string.Empty, 14, FontStyle.Normal, new Color(0.68f, 0.12f, 0.10f, 1f), TextAnchor.MiddleLeft);
            SetTopStretch(nameError.rectTransform, 32f, 150f, 32f, 30f);

            Button cancel = CreateOutlinedButton(dialog, "CancelButton", "CANCEL", 16);
            SetBottomRight(cancel.GetComponent<RectTransform>(), 164f, 24f, 150f, 48f);
            cancel.onClick.AddListener(HideNameDialog);

            Button confirm = CreateOutlinedButton(dialog, "ConfirmButton", "CONFIRM", 16);
            SetBottomRight(confirm.GetComponent<RectTransform>(), 0f, 24f, 150f, 48f);
            confirm.onClick.AddListener(ConfirmNameChange);

            HideNameDialog();
        }

        void BuildAvatarDialog() {
            avatarDialog = CreatePanel(root, "AvatarPickerOverlay", new Color(0f, 0f, 0f, 0.42f), true);
            Stretch(avatarDialog);

            RectTransform dialog = CreatePanel(avatarDialog, "AvatarPickerDialog", White, true);
            SetCenter(dialog, 0f, 0f, 760f, 700f);
            AddOutline(dialog.gameObject, Border, 2f);

            Text title = CreateText(dialog, "Title", "CHỌN AVATAR", 28, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(title.rectTransform, 28f, 18f, 560f, 52f);

            Button close = CreateOutlinedButton(dialog, "CloseButton", "×", 28);
            SetTopRight(close.GetComponent<RectTransform>(), 20f, 16f, 54f, 54f);
            close.onClick.AddListener(HideAvatarDialog);

            RectTransform separator = CreatePanel(dialog, "Separator", Pale, false);
            SetTopStretch(separator, 24f, 80f, 24f, 2f);

            RectTransform scrollRoot = CreatePanel(dialog, "AvatarScroll", Color.clear, true);
            SetTopLeft(scrollRoot, 28f, 98f, 704f, 568f);
            avatarScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            avatarScroll.horizontal = false;
            avatarScroll.vertical = true;
            avatarScroll.movementType = ScrollRect.MovementType.Clamped;
            avatarScroll.scrollSensitivity = 34f;

            RectTransform viewport = CreatePanel(scrollRoot, "Viewport", new Color(1f, 1f, 1f, 0.01f), true);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            avatarGrid = CreatePanel(viewport, "AvatarGrid", Color.clear, false);
            avatarGrid.anchorMin = new Vector2(0f, 1f);
            avatarGrid.anchorMax = new Vector2(1f, 1f);
            avatarGrid.pivot = new Vector2(0.5f, 1f);
            avatarGrid.anchoredPosition = Vector2.zero;
            avatarGrid.sizeDelta = Vector2.zero;

            avatarScroll.viewport = viewport;
            avatarScroll.content = avatarGrid;
            HideAvatarDialog();
        }

        void ShowNameDialog() {
            if (nameDialog == null || nameInput == null) return;
            PlayerProfileSnapshot snapshot = provider != null ? provider.GetSnapshot() : default;
            nameInput.text = snapshot.DisplayName ?? string.Empty;
            nameError.text = string.Empty;
            nameDialog.gameObject.SetActive(true);
            nameDialog.SetAsLastSibling();
            nameInput.ActivateInputField();
        }

        void HideNameDialog() {
            if (nameDialog != null) nameDialog.gameObject.SetActive(false);
        }

        void ShowAvatarDialog() {
            if (avatarDialog == null || avatarGrid == null || provider == null || !provider.CanChangeAvatar) return;
            RebuildAvatarGrid();
            avatarDialog.gameObject.SetActive(true);
            avatarDialog.SetAsLastSibling();
            if (avatarScroll != null) avatarScroll.verticalNormalizedPosition = 1f;
        }

        void HideAvatarDialog() {
            if (avatarDialog != null) avatarDialog.gameObject.SetActive(false);
        }

        void RebuildAvatarGrid() {
            if (avatarGrid == null || provider == null) return;
            ClearChildren(avatarGrid);

            const int columnCount = 3;
            const float cardWidth = 205f;
            const float cardHeight = 190f;
            const float gapX = 16f;
            const float gapY = 16f;
            const float startX = 18f;
            const float startY = 12f;

            int visibleIndex = 0;
            for (int optionIndex = 0; optionIndex < provider.AvatarOptions.Count; optionIndex++) {
                Sprite sprite = provider.AvatarOptions[optionIndex];
                if (sprite == null) continue;

                int capturedIndex = optionIndex;
                int column = visibleIndex % columnCount;
                int row = visibleIndex / columnCount;
                bool selected = optionIndex == provider.SelectedAvatarIndex;

                RectTransform card = CreatePanel(avatarGrid, "AvatarOption_" + optionIndex, selected ? Pale : White, true);
                SetTopLeft(card, startX + column * (cardWidth + gapX), startY + row * (cardHeight + gapY), cardWidth, cardHeight);
                AddOutline(card.gameObject, selected ? Accent : Border, selected ? 3f : 1f);

                Button button = card.gameObject.AddComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = selected ? Pale : White;
                colors.highlightedColor = new Color(0.87f, 0.95f, 0.84f, 1f);
                colors.pressedColor = new Color(0.76f, 0.89f, 0.72f, 1f);
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
                button.onClick.AddListener(() => SelectAvatar(capturedIndex));

                Image portrait = CreatePanel(card, "Portrait", Color.white, false).GetComponent<Image>();
                SetTopLeft(portrait.rectTransform, 14f, 12f, 177f, 140f);
                portrait.sprite = sprite;
                portrait.preserveAspect = true;

                Text label = CreateText(card, "Label", "Avatar " + (visibleIndex + 1), 15, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
                SetBottomStretch(label.rectTransform, 10f, 7f, 10f, 30f);

                if (selected) {
                    Text selectedMark = CreateText(card, "Selected", "✓", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                    RectTransform markBackground = CreatePanel(card, "SelectedMark", Accent, false);
                    SetTopRight(markBackground, 7f, 7f, 34f, 34f);
                    selectedMark.transform.SetParent(markBackground, false);
                    Stretch(selectedMark.rectTransform);
                }

                visibleIndex++;
            }

            if (visibleIndex == 0) {
                Text empty = CreateText(avatarGrid, "Empty", "Chưa có avatar trong Avatar Options.", 18, FontStyle.Italic, Muted, TextAnchor.MiddleCenter);
                SetTopStretch(empty.rectTransform, 20f, 180f, 20f, 60f);
            }

            int rowCount = Mathf.Max(1, Mathf.CeilToInt(visibleIndex / (float)columnCount));
            avatarGrid.sizeDelta = new Vector2(0f, startY + rowCount * cardHeight + Mathf.Max(0, rowCount - 1) * gapY + 12f);
        }

        void SelectAvatar(int avatarIndex) {
            if (provider == null || !provider.TrySelectAvatar(avatarIndex)) return;
            HideAvatarDialog();
            Refresh();
        }

        void ConfirmNameChange() {
            if (provider == null || nameInput == null) return;
            if (provider.TrySetDisplayName(nameInput.text, out string error)) {
                HideNameDialog();
                Refresh();
            } else if (nameError != null) {
                nameError.text = error;
            }
        }

        void ChangeAvatar() {
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
            if (provider == null) return;
            provider.ProfileChanged -= Refresh;
            provider.ProfileChanged += Refresh;
        }

        void UnsubscribeProvider() {
            if (provider == null) return;
            provider.ProfileChanged -= Refresh;
        }

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
            BuildBadgeShowcase(snapshot.UnlockedBadges, snapshot.AchievementCompletion01);
        }

        static string FormatOptionalCount(int value) {
            return value >= 0 ? value.ToString() : "-";
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
            Text text = CreateText(rect, "Text", string.Empty, 18, FontStyle.Normal, Forest, TextAnchor.MiddleLeft);
            SetInset(text.rectTransform, 14f);
            text.supportRichText = false;
            input.textComponent = text;

            Text placeholder = CreateText(rect, "Placeholder", "Enter player name", 18, FontStyle.Italic, new Color(Muted.r, Muted.g, Muted.b, 0.55f), TextAnchor.MiddleLeft);
            SetInset(placeholder.rectTransform, 14f);
            input.placeholder = placeholder;
            input.characterLimit = 20;
            return input;
        }

        static Button CreateOutlinedButton(Transform parent, string name, string label, int fontSize) {
            RectTransform rect = CreatePanel(parent, name, White, true);
            AddOutline(rect.gameObject, Border, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = White;
            colors.highlightedColor = Pale;
            colors.pressedColor = new Color(0.78f, 0.89f, 0.75f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.90f, 0.92f, 0.89f, 0.58f);
            button.colors = colors;

            Text text = CreateText(rect, "Text", label, fontSize, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        static RectTransform CreatePanel(Transform parent, string name, Color color, bool raycastTarget) {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.sprite = SolidSprite;
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
                const int size = 128;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                    name = "ProfileCircleTexture",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp
                };

                Color[] pixels = new Color[size * size];
                float center = (size - 1) * 0.5f;
                float radius = center - 1f;
                float radiusSquared = radius * radius;
                for (int y = 0; y < size; y++) {
                    for (int x = 0; x < size; x++) {
                        float dx = x - center;
                        float dy = y - center;
                        pixels[y * size + x] = dx * dx + dy * dy <= radiusSquared ? Color.white : Color.clear;
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

        static Font DefaultFont {
            get {
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return cachedFont;
            }
        }
    }
}
