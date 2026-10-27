using UnityEngine;
using UnityEngine.UI;
using Capstone.Game.HudSystem;
using Capstone.Game.UISystem;
using Icon = Capstone.Game.ProfileSystem.ProfileIconGraphic.Symbol;

namespace Capstone.Game.ProfileSystem {
    public sealed partial class ProfilePanelController {
        static readonly Color Gold = new Color(.8f, .68f, .51f, .48f);
        static Sprite roundedSprite;
        RectTransform designSurface;
        Vector2 fittedSize;
        float nextRefresh;
        Button avatarCameraButton;
        IPetHudProvider teamProvider;
        int selectedTeamIndex = -1;
        readonly TeamSlotView[] teamSlots = new TeamSlotView[6];

        sealed class TeamSlotView {
            public Button button;
            public Image portrait;
            public ProfileIconGraphic empty;
            public AspectRatioFitter portraitFit;
            public Outline outline;
            public ProfileR2Feedback feedback;
        }

        void LateUpdate() {
            if (!IsOpen) return;
            FitDesignSurface();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f;
            Refresh();
        }

        void BuildDesignSurface() {
            modalCanvases.Clear();
            designSurface = CreatePanel(root, "ProfileScreen", Color.clear, false);
            SetCenter(designSurface, 0, 0, 1920, 1080);
            fittedSize = Vector2.zero;
        }

        void FitDesignSurface() {
            if (root == null || designSurface == null || root.rect.size == fittedSize) return;
            fittedSize = root.rect.size;
            float scale = Mathf.Min(fittedSize.x / 1920f, fittedSize.y / 1080f);
            designSurface.localScale = Vector3.one * Mathf.Max(.01f, scale);
            FitR2Modals();
        }

        void BuildHeader() {
            BuildUnifiedHeader();
        }

        void BuildUnifiedHeader() {
            RectTransform header=CreatePanel(designSurface,"HeaderBar",Color.clear,false);SetTopLeft(header,0,0,1920,124.8f);
            var ribbon=Graphic(header,"UnifiedTitleRibbon","parallel",UnifiedPanelHeader.Texture("Components/title-ribbon__fill"),Color.white);UnifiedRect(ribbon.rectTransform,UnifiedPanelHeader.Ribbon);ribbon.LogicalScale=1.2f;
            var edge=Graphic(header,"UnifiedRibbonBorder","parallel",UnifiedPanelHeader.Texture("Components/title-ribbon__border"),Color.white);UnifiedRect(edge.rectTransform,UnifiedPanelHeader.Ribbon);edge.LogicalScale=1.2f;
            var emblem=Graphic(header,"Emblem","rect",UnifiedPanelHeader.Texture("Art/emblem"),Color.white);UnifiedRect(emblem.rectTransform,UnifiedPanelHeader.Emblem);
            var title=CreateText(header,"Title","HỒ SƠ",36,FontStyle.Bold,Forest,TextAnchor.MiddleLeft);title.font=TrainerMenuArt.Serif;UnifiedRect(title.rectTransform,UnifiedPanelHeader.Title);
            var subtitle = CreateText(header, "Subtitle", "Xem thông tin và tiến trình phiêu lưu của bạn", 18, FontStyle.Normal, Muted, TextAnchor.MiddleLeft);
            SetTopLeft(subtitle.rectTransform,873.6f,40.8f,777.6f,55.2f);subtitle.color=GreenAngularGeometry.Light;
            Button back=CreateOutlinedButton(header,"BackButton","",25);UnifiedRect((RectTransform)back.transform,UnifiedPanelHeader.Back);
            back.onClick.AddListener(RequestBack);
        }
        static void UnifiedRect(RectTransform target,Rect logical)=>SetTopLeft(target,logical.x*1.2f,logical.y*1.2f,logical.width*1.2f,logical.height*1.2f);

        void BuildProfileSummary() {
            RectTransform panel = Surface(designSurface, "MainProfilePanel");
            SetTopStretch(panel,28.8f,134.4f,28.8f,415.2f);
            var avatarSection = CreatePanel(panel, "AvatarSection", Color.clear, false);
            SetTopLeft(avatarSection, 24, 16, 448, 404);
            var outer = CreatePanel(avatarSection, "AvatarOuter", Border, false);
            outer.GetComponent<Image>().sprite = CircleSprite;
            SetTopLeft(outer, 65, 47, 310, 310);
            var inner = CreatePanel(outer, "AvatarMask", Pale, true);
            inner.GetComponent<Image>().sprite = CircleSprite; SetInset(inner, 3);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            avatarImage = CreatePanel(inner, "Avatar", Color.clear, false).GetComponent<Image>();
            SetInset(avatarImage.rectTransform, 18); avatarImage.preserveAspect = true;
            avatarPlaceholder = CreateText(inner, "AvatarPlaceholder", "?", 80, FontStyle.Normal, Accent, TextAnchor.MiddleCenter);
            Stretch(avatarPlaceholder.rectTransform);
            Diamond(avatarSection, 220, 46);
            avatarCameraButton = CreateOutlinedButton(avatarSection, "AvatarCameraButton", "", 1);
            SetTopLeft(avatarCameraButton.GetComponent<RectTransform>(), 302, 293, 68, 68);
            avatarCameraButton.image.sprite = CircleSprite;
            AddIcon(avatarCameraButton.transform, "CameraIcon", Icon.Camera, 18, 18, 32, Forest);
            avatarCameraButton.onClick.AddListener(ChangeAvatar);
            changeAvatarButton = null;
            avatarCameraButton.gameObject.AddComponent<ProfileCameraTooltip>().Text = "Đổi ảnh đại diện";

            var basic = CreatePanel(panel, "BasicInfoSection", Color.clear, false);
            SetTopLeft(basic, 506, 32, 742, 370);
            displayNameText = CreateText(basic, "DisplayName", "", 48, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            displayNameText.resizeTextForBestFit = true; displayNameText.resizeTextMinSize = 28; displayNameText.resizeTextMaxSize = 48;
            SetTopLeft(displayNameText.rectTransform, 0, 18, 626, 84);
            Button edit = CreateOutlinedButton(basic, "ChangeNameButton", "", 1);
            SetTopRight(edit.GetComponent<RectTransform>(), 0, 30, 52, 52);
            AddIcon(edit.transform, "EditIcon", Icon.Edit, 13, 13, 26, Forest);
            edit.onClick.AddListener(ShowNameDialog);
            Line(basic, "NameDivider", 0, 118, 742, Gold);
            Diamond(basic, 346, 118);
            var level = Surface(basic, "LevelBadge"); SetTopLeft(level, 0, 148, 150, 138);
            var levelCaption = CreateText(level, "LevelCaption", "Cấp độ", 22, FontStyle.Normal, Forest, TextAnchor.MiddleCenter);
            SetTopStretch(levelCaption.rectTransform, 8, 10, 8, 40);
            levelText = CreateText(level, "LevelValue", "-", 42, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            SetBottomStretch(levelText.rectTransform, 8, 18, 8, 64);
            var exp = CreateText(basic, "ExperienceCaption", "EXP", 23, FontStyle.Normal, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(exp.rectTransform, 186, 154, 160, 40);
            experiencePercentText = CreateText(basic, "ExperiencePercent", "-", 22, FontStyle.Normal, Forest, TextAnchor.MiddleRight);
            SetTopRight(experiencePercentText.rectTransform, 0, 154, 160, 40);
            var track = Surface(basic, "ExperienceTrack"); track.GetComponent<Image>().color = Pale;
            SetTopLeft(track, 186, 208, 556, 20);
            experienceFill = CreatePanel(track, "Fill", new Color(.59f,.76f,.53f), false).GetComponent<Image>();
            SetInset(experienceFill.rectTransform, 3); experienceFill.type = Image.Type.Filled;
            experienceFill.fillMethod = Image.FillMethod.Horizontal; experienceFill.fillAmount = 0;
            experienceValueText = CreateText(basic, "ExperienceValue", "- / -", 22, FontStyle.Normal, Muted, TextAnchor.MiddleLeft);
            SetTopLeft(experienceValueText.rectTransform, 186, 246, 556, 42);
            Line(basic, "BottomDivider", 0, 336, 742, Border);

            var team = CreatePanel(panel, "CurrentTeamSection", Color.clear, false);
            SetTopLeft(team, 1290, 26, 540, 384);
            var separator = CreatePanel(panel, "TeamDivider", Gold, false);
            SetTopLeft(separator, 1270, 30, 1, 370);
            AddIcon(team, "TeamIcon", Icon.Paw, 0, 4, 34, Accent);
            var teamTitle = CreateText(team, "TeamTitle", "ĐỘI THÚ HIỆN TẠI", 25, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(teamTitle.rectTransform, 46, 0, 488, 42);
            for (int i=0;i<teamSlots.Length;i++) {
                int index=i;
                Button slot = CreateOutlinedButton(team, $"TeamPetSlot_{i+1:00}", "", 1);
                var colors=slot.colors; colors.disabledColor=Color.white; slot.colors=colors;
                SetTopLeft(slot.GetComponent<RectTransform>(), (i%3)*180, 56+(i/3)*160, 168, 148);
                slot.gameObject.AddComponent<RectMask2D>();
                var icon = CreatePanel(slot.transform, "PetPortrait", Color.clear, false).GetComponent<Image>();
                Stretch(icon.rectTransform); icon.preserveAspect = false;
                var fit = icon.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
                var empty = AddIcon(slot.transform, "EmptyPetIcon", Icon.Paw, 60, 40, 48, new Color(.55f,.65f,.53f,.5f));
                teamSlots[i] = new TeamSlotView { button=slot, portrait=icon, empty=empty, portraitFit=fit, outline=slot.GetComponent<Outline>() };
                slot.onClick.AddListener(() => SelectTeamSlot(index));
            }
        }

        void BuildLowerPanels() {
            var info = Surface(designSurface, "PlayerInfoPanel");
            SetTopLeft(info,28.8f,573.6f,780,477.6f);
            BuildSectionHeader(info, "THÔNG TIN NGƯỜI CHƠI", Icon.Identity);
            playerIdValue = BuildInfoRow(info, "PlayerId", Icon.Identity, "Mã người chơi", 92);
            playTimeValue = BuildInfoRow(info, "PlayTime", Icon.Clock, "Thời gian chơi", 188);
            currentAreaValue = BuildInfoRow(info, "CurrentArea", Icon.Location, "Khu vực hiện tại", 284);
            dateStartedValue = BuildInfoRow(info, "DateStarted", Icon.Calendar, "Ngày bắt đầu chơi", 380);
            var stats = Surface(designSurface, "StatsOverviewPanel");
            SetTopLeft(stats,832.8f,573.6f,1058.4f,477.6f);
            BuildSectionHeader(stats, "TỔNG QUAN THỐNG KÊ", Icon.Chart);
            creaturesSeenValue = BuildStatCard(stats, "CreaturesSeen", Icon.Paw, "Sinh vật đã gặp", 0);
            speciesCapturedValue = BuildStatCard(stats, "SpeciesCaptured", Icon.Leaf, "Loài đã thu phục", 1);
            codexValue = BuildStatCard(stats, "Codex", Icon.Book, "Cẩm nang", 2);
            storyProgressValue = BuildStatCard(stats, "StoryProgress", Icon.Story, "Tiến trình cốt truyện", 3);
            bossesDefeatedValue = BuildStatCard(stats, "BossDefeated", Icon.Boss, "Trùm đã đánh bại", 4);
            totalBattlesValue = BuildStatCard(stats, "TotalBattles", Icon.Swords, "Tổng số trận chiến", 5);
        }

        void BuildSectionHeader(RectTransform panel, string title, Icon symbol) {
            AddIcon(panel, "HeaderIcon", symbol, 28, 22, 34, Accent);
            var heading = CreateText(panel, "HeaderTitle", title, 27, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(heading.rectTransform, 80, 16, 550, 50);
            Line(panel, "HeaderDivider", 28, 76, panel.sizeDelta.x-56, Gold);
        }

        Text BuildInfoRow(RectTransform parent, string name, Icon symbol, string caption, float top) {
            var row = CreatePanel(parent, name+"Row", Color.clear, false);
            SetTopLeft(row, 26, top, 728, 84);
            var badge = CreatePanel(row, "IconBadge", Pale, false); SetTopLeft(badge, 0, 7, 64, 64);
            AddIcon(badge, "Icon", symbol, 15, 15, 34, Accent);
            var label = CreateText(row, "Label", caption, 23, FontStyle.Normal, Muted, TextAnchor.MiddleLeft);
            SetTopLeft(label.rectTransform, 92, 0, 260, 78);
            var value = CreateText(row, "Value", "-", 22, FontStyle.Normal, Forest, TextAnchor.MiddleRight);
            SetTopLeft(value.rectTransform, 350, 0, 370, 78);
            if (name != "DateStarted") Line(row, "Divider", 0, 88, 728, new Color(Border.r, Border.g, Border.b, .3f));
            return value;
        }

        Text BuildStatCard(RectTransform parent, string name, Icon symbol, string caption, int index) {
            var card = Surface(parent, "StatsCard_"+name);
            SetTopLeft(card,28.8f+(index%3)*336,96+(index/3)*170.4f,316.8f,158.4f);
            var circle = CreatePanel(card, "IconCircle", Pale, false);
            circle.GetComponent<Image>().sprite = CircleSprite; SetTopLeft(circle, 18, 30, 88, 88);
            AddIcon(circle, "Icon", symbol, 22, 22, 44, Accent);
            var value = CreateText(card, "Value", "-", 36, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            SetTopLeft(value.rectTransform, 120, 24, 178, 60);
            Line(card, "ValueDivider", 126, 92, 166, Gold);
            var label = CreateText(card, "Label", caption, 20, FontStyle.Normal, Muted, TextAnchor.MiddleCenter);
            SetTopLeft(label.rectTransform, 116, 106, 188, 44);
            return value;
        }

        void ResolveTeamProvider() {
            var next = teamProviderSource as IPetHudProvider;
            if (next == null) {
                teamProviderSource = FindFirstObjectByType<PetCommandHudProvider>(FindObjectsInactive.Include);
                next = teamProviderSource as IPetHudProvider;
            }
            if (ReferenceEquals(next, teamProvider)) return;
            UnsubscribeTeamProvider(); teamProvider=next;
            if (isActiveAndEnabled) SubscribeTeamProvider();
        }
        void SubscribeTeamProvider() { if (teamProvider == null || !isActiveAndEnabled) return; teamProvider.HudDataChanged -= HandleTeamChanged; teamProvider.HudDataChanged += HandleTeamChanged; }
        void UnsubscribeTeamProvider() { if (teamProvider != null) teamProvider.HudDataChanged -= HandleTeamChanged; }
        void HandleTeamChanged() { if (IsOpen) RefreshTeam(); }
        void SelectTeamSlot(int index) {
            if (teamProvider == null || !PlayerActionGate.TryAccept(this, "team:"+index)) return;
            var slots=teamProvider.GetPetSlots();
            if (index >= slots.Count || !slots[index].occupied) return;
            // This is a profile overview selection, never a summon/active-pet command.
            selectedTeamIndex=index; RefreshTeam();
        }
        void RefreshTeam() {
            var slots = teamProvider?.GetPetSlots();
            if (slots == null || selectedTeamIndex >= slots.Count || selectedTeamIndex < 0 || !slots[selectedTeamIndex].occupied) {
                selectedTeamIndex=-1;
                if (slots != null) for (int i=0;i<Mathf.Min(6,slots.Count);i++) if (slots[i].occupied && slots[i].selected) { selectedTeamIndex=i; break; }
            }
            for(int i=0;i<teamSlots.Length;i++) {
                var view=teamSlots[i]; if(view==null) continue;
                PetSlotHudData data=slots!=null && i<slots.Count ? slots[i] : default;
                view.button.interactable=data.occupied;
                view.portrait.sprite=data.icon; view.portrait.color=data.occupied && data.icon!=null ? Color.white : Color.clear;
                view.empty.gameObject.SetActive(!data.occupied || data.icon==null);
                if(data.icon!=null) view.portraitFit.aspectRatio=data.icon.rect.width/data.icon.rect.height;
                view.outline.effectColor=data.occupied && i==selectedTeamIndex ? Accent : Border;
                view.outline.effectDistance=Vector2.one*(data.occupied && i==selectedTeamIndex ? 2 : 1);
                view.feedback?.SetSelected(data.occupied && i==selectedTeamIndex);
                view.feedback?.RefreshDisabled();
            }
        }

        static RectTransform Surface(Transform parent, string name) { var panel=CreatePanel(parent,name,White,true); AddOutline(panel.gameObject,Border,1); return panel; }
        static ProfileIconGraphic AddIcon(Transform parent,string name,Icon symbol,float x,float y,float size,Color color) {
            var obj=new GameObject(name,typeof(RectTransform),typeof(ProfileIconGraphic)); obj.transform.SetParent(parent,false);
            SetTopLeft((RectTransform)obj.transform,x,y,size,size);
            var icon=obj.GetComponent<ProfileIconGraphic>(); icon.Configure(symbol,color);
            string key=symbol==Icon.Identity?"profile":symbol==Icon.Clock?"time":symbol.ToString().ToLowerInvariant();
            var sprite=ProfileR2Art.Sprite("Icons/"+key);
            if(sprite!=null){icon.enabled=false;var art=SpriteImage(obj.transform,"R2Icon",sprite);Stretch(art.rectTransform);}
            return icon;
        }
        static void Line(Transform parent,string name,float x,float y,float width,Color color) { var line=CreatePanel(parent,name,color,false); SetTopLeft(line,x,y,width,1); }
        static void Diamond(Transform parent,float x,float y) { var obj=CreatePanel(parent,"Diamond",Gold,false); obj.GetComponent<Image>().sprite=SolidSprite; SetTopLeft(obj,x-4,y-4,8,8); obj.localRotation=Quaternion.Euler(0,0,45); }

        static Sprite RoundedSprite {
            get {
                if(roundedSprite!=null) return roundedSprite;
                const int size=64; const float radius=12;
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false) { name="ProfileRoundedPanel",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear };
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
                    float dx=Mathf.Max(Mathf.Abs(x+.5f-size*.5f)-(size*.5f-radius),0);
                    float dy=Mathf.Max(Mathf.Abs(y+.5f-size*.5f)-(size*.5f-radius),0);
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius-Mathf.Sqrt(dx*dx+dy*dy)+.5f));
                }
                texture.SetPixels(pixels);texture.Apply();
                roundedSprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
                roundedSprite.hideFlags=HideFlags.HideAndDontSave; return roundedSprite;
            }
        }
    }
}
