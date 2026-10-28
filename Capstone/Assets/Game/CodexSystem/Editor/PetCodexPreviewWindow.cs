using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;
using Capstone.Game.MapSystem.UI;
using Capstone.Game.HudSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.CodexSystem.Editor {
    /// <summary>Editor-only fixture; no discovery tracking or save integration.</summary>
    public sealed class PetCodexPreviewWindow : EditorWindow {
        const string UI = "Assets/ThangTest/UIThang/Inventory/UI/";
        GameObject owner;
        PetCodexPanelController controller;
        VisualElement surface;
        static bool validating;
        static bool closeAfterValidation;
        static readonly Vector2[] Viewports = { new Vector2(1920,1080), new Vector2(1366,768), new Vector2(1920,1200), new Vector2(2560,1080) };

        [MenuItem("Game Tools/GameToolThang/HUD/Preview Pet Codex")]
        public static void OpenPreview() {
            var window = GetWindow<PetCodexPreviewWindow>("Cẩm nang — xem thử");
            window.position = new Rect(40, 70, 1366, 768); window.Show();
        }
        public void CreateGUI() {
            var toolbar = new VisualElement(); toolbar.style.flexDirection = FlexDirection.Row; rootVisualElement.Add(toolbar);
            toolbar.Add(new Label("DỮ LIỆU XEM THỬ — KHÔNG LƯU VÀO GAME"));
            foreach (int count in new[] { 0, 1, 25, 26, 320 }) { int size = count; toolbar.Add(new Button(() => SetFixture(size)) { text = size.ToString() + " mục" }); }
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal); scroll.style.flexGrow = 1; rootVisualElement.Add(scroll);
            surface = new VisualElement(); surface.style.width = 1366; surface.style.height = 700; surface.style.flexShrink = 0; scroll.Add(surface);
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UI + "CodexPanel.uxml").CloneTree(surface);
            owner = new GameObject("CodexEditorPreview") { hideFlags = HideFlags.HideAndDontSave };
            controller = owner.AddComponent<PetCodexPanelController>(); controller.Bind(surface); controller.BackRequested += Close;
            SetFixture(320);
        }
        void SetFixture(int count) { controller.SetProvider(new Fixture(count)); controller.Open(); }
        void OnDisable() { if (owner != null) DestroyImmediate(owner); owner = null; controller = null; }

        internal sealed class Fixture : IPetCodexProvider {
            readonly List<PetCodexEntryViewData> entries = new List<PetCodexEntryViewData>();
            public event Action Changed;
            public IReadOnlyList<PetCodexEntryViewData> Entries => entries;
            public bool CanSetFavorite => true;
            public Fixture(int count) {
                Sprite icon = null;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/HudSystem/Prefabs" })) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    icon = prefab?.GetComponentInChildren<PetHudRuntimeStats>(true)?.Icon;
                    if (icon != null) break;
                }
                for (int i = 0; i < count; i++) entries.Add(new PetCodexEntryViewData {
                    Id = "editor-fixture-" + i, Number = i + 1, Name = i == 0 ? "Rồng thử nghiệm Ánh sáng" : "Sinh vật xem thử " + (i+1),
                    PrimaryType = (CodexType)(1 + i % 10), SecondaryType = i % 3 == 0 ? CodexType.Nature : CodexType.Unknown,
                    Rarity = (PetRarity)(1 + i % 5), Region = "Vùng xem thử " + (i % 3 + 1),
                    Discovery = i % 4 == 3 ? CodexDiscovery.Unknown : (CodexDiscovery)(1 + i % 3),
                    Favorite = i == 0, Icon = icon, Portrait = icon,
                    Description = "Thông tin minh họa để kiểm tra bố cục và thao tác. Không phải dữ liệu chính thức của sinh vật.",
                    Evolution = new[] { new PetCodexEvolutionViewData { Name = "Dạng xem thử", Known = true, Icon = icon, Requirement = "Cấp 18" }, new PetCodexEvolutionViewData { Known = false } }
                });
            }
            public bool TrySetFavorite(string id, bool favorite) { var entry = entries.Find(e => e.Id == id); if (entry == null) return false; entry.Favorite = favorite; Changed?.Invoke(); return true; }
        }

        [MenuItem("Game Tools/GameToolThang/HUD/Validate UI Completion")]
        public static async void Validate() {
            if (validating) return;
            validating = true;
            var results = new List<string>();
            void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); results.Add("PASS " + label); }
            try {
                var output = new List<PetCodexEntryViewData>();
                var query = new PetCodexQuery();
                foreach (int count in new[] { 0,1,25,26,320 }) {
                    query.Apply(new Fixture(count).Entries, output); Check(output.Count == count, "query count " + count);
                    Check(Mathf.Max(1,Mathf.CeilToInt(output.Count/25f)) == Mathf.Max(1,(count+24)/25), "page count " + count);
                }
                var fixture = new Fixture(320);
                query.Search = "rong thu nghiem anh sang"; query.Apply(fixture.Entries, output);
                Check(output.Count == 1 && output[0].Number == 1, "Vietnamese search without accents");
                query.Search = "004"; query.Apply(fixture.Entries, output); Check(output.Count == 1 && !output[0].Known, "unknown number searchable");
                query.Search = "Sinh vat xem thu 4"; query.Apply(fixture.Entries, output); Check(!output.Exists(e => e.Number == 4), "unknown name remains private");
                query.Search = string.Empty; query.Discovery = CodexDiscovery.Unknown; query.Apply(fixture.Entries, output); Check(output.Count == 80, "unknown filter");
                query.Discovery = null; query.Type = CodexType.Fire; query.Apply(fixture.Entries, output); Check(output.TrueForAll(e => e.Known && (e.PrimaryType == CodexType.Fire || e.SecondaryType == CodexType.Fire)), "type filter");
                query.Type = null; query.Region = "Vùng xem thử 1"; query.Apply(fixture.Entries, output); Check(output.TrueForAll(e => e.Known && e.Region == query.Region), "region filter");
                query.Region = null; query.Rarity = PetRarity.Epic; query.Apply(fixture.Entries, output); Check(output.TrueForAll(e => e.Known && e.Rarity == PetRarity.Epic), "rarity filter");
                query.Rarity = null;
                foreach (CodexSort sort in Enum.GetValues(typeof(CodexSort))) { query.Sort = sort; query.Apply(fixture.Entries,output); Check(output.Count == 320,"sort " + sort); }
                Check(fixture.TrySetFavorite(fixture.Entries[0].Id,false) && !fixture.Entries[0].Favorite,"fixture favorite");
                ValidateNameSort(Check);
                ValidateMarkerVisibility(Check);
                var groups = new List<List<int>>();
                var clusterType = typeof(WorldMapUIController).Assembly.GetType("Capstone.Game.MapSystem.UI.MapMarkerClusters");
                var groupMethod = clusterType.GetMethod("Group", BindingFlags.Public | BindingFlags.Static);
                groupMethod.Invoke(null, new object[] { new[] { Vector2.zero, new Vector2(20,0), new Vector2(40,0), new Vector2(100,0) }, 36f, groups });
                Check(groups.Count == 2 && groups[0].Count == 3, "map transitive cluster and separate marker");
                groupMethod.Invoke(null, new object[] { Array.Empty<Vector2>(), 36f, groups });
                Check(groups.Count == 0,"map empty cluster set");
                var workspaceType = clusterType.GetNestedType("GroupWorkspace", BindingFlags.NonPublic);
                var workspace = Activator.CreateInstance(workspaceType, true);
                var regroup = (Action<IReadOnlyList<Vector2>,float,List<List<int>>>)Delegate.CreateDelegate(
                    typeof(Action<IReadOnlyList<Vector2>,float,List<List<int>>>), workspace, workspaceType.GetMethod("Build"));
                var points = new[] { Vector2.zero, new Vector2(20,0), new Vector2(40,0), new Vector2(100,0) };
                for (int i=0;i<32;i++) regroup(points,36f,groups);
                var roots=workspaceType.GetField("roots",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(workspace);
                var groupBuffer=groups[0];
                for (int i=0;i<100;i++) regroup(points,36f,groups);
                Check(ReferenceEquals(roots,workspaceType.GetField("roots",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(workspace))
                    && ReferenceEquals(groupBuffer,groups[0]),"map warmed regroup reuses roots and group buffers over 100 passes");
                regroup(Array.Empty<Vector2>(),36f,groups); regroup(new[]{Vector2.zero},36f,groups);
                Check(groups.Count==1 && groups[0].Count==1,"map workspace shrinks and resets old groups");
                regroup(new[]{Vector2.zero,Vector2.zero,Vector2.one},0,groups);
                Check(groups.Count==2 && groups[0].Count==2,"map zero radius groups only coincident points");
                var window = GetWindow<PetCodexPreviewWindow>("Cẩm nang — kiểm tra"); window.Show();
                window.position = new Rect(8, 32, 1340, 736);
                await Task.Delay(300);
                foreach (int count in new[] { 0,1,25,26,320 }) {
                    window.SetFixture(count); await Task.Delay(180);
                    Check(window.controller.VisibleCount == Math.Min(25,count), "render count " + count);
                    Check(window.controller.PageCount == Math.Max(1,(count+24)/25), "render pages " + count);
                }
                window.SetFixture(26); await Task.Delay(180);
                var codex = window.surface.Q("codex-panel");
                var cards = codex.Query<Button>(className:"codex-card").ToList();
                Submit(cards[0]); await Task.Delay(180);
                Check(codex.Q<Label>("codex-name").text == "Rồng thử nghiệm Ánh sáng", "card submit selects details");
                var name = codex.Q<Label>("codex-name").text;
                Submit(codex.Query<Button>(className:"codex-page-button").ToList()[8]); await Task.Delay(180);
                Check(window.controller.VisibleCount == 1 && codex.Q<Label>("codex-name").text == name, "last page and retained selection");
                codex.Q<TextField>("codex-search").value="rong thu nghiem anh sang"; await Task.Delay(180);
                Check(window.controller.VisibleCount == 1 && codex.Q<Label>("codex-name").text == name, "search resets page and retains selection");
                codex.Q<TextField>("codex-search").value="004"; await Task.Delay(180);
                Check(codex.Q("codex-details").resolvedStyle.display == DisplayStyle.None,"filter clears unavailable selection");
                Submit(cards[0]); await Task.Delay(180);
                Check(codex.Q<Label>("codex-name").text=="Chưa khám phá" && !codex.Q<Button>("codex-favorite").enabledSelf,"unknown details stay private");
                window.controller.SetProvider(new Fixture(26));
                var privateEntry = window.controller;
                var provider = Field<IPetCodexProvider>(privateEntry,"provider");
                provider.Entries[3].Favorite=true; window.controller.Refresh();
                Check(codex.Q<Button>("codex-favorite").text=="Đánh dấu yêu thích","unknown favorite state stays private");
                codex.Q<DropdownField>("codex-rarity").value="Sử thi";
                codex.Q<DropdownField>("codex-discovery").value="Đã gặp";
                codex.Q<DropdownField>("codex-extra-sort").value="Theo tên";
                int callbackCount=Field<IList>(window.controller,"unbind").Count;
                window.controller.Bind(window.surface); window.controller.Open(); await Task.Delay(180);
                Check(Field<IList>(window.controller,"unbind").Count==callbackCount,"codex callback count stays stable after rebind");
                Check(codex.Q<TextField>("codex-search").value=="004"
                    && codex.Q<DropdownField>("codex-rarity").value=="Sử thi"
                    && codex.Q<DropdownField>("codex-discovery").value=="Đã gặp"
                    && codex.Q<DropdownField>("codex-extra-sort").value=="Theo tên","codex rebind preserves visible filter state");
                Submit(codex.Q<Button>("codex-reset")); await Task.Delay(180);
                Check(window.controller.VisibleCount==25,"codex reset clears preserved filters");
                window.Focus(); await ValidatePopupFocus(window.surface,Check);
                codex.Q<TextField>("codex-search").value=string.Empty;
                foreach (var viewport in Viewports) {
                    window.surface.style.width = viewport.x; window.surface.style.height = viewport.y;
                    window.SetFixture(320); await Task.Delay(250);
                    var root = window.surface.Q("codex-panel");
                    Check(window.controller.VisibleCount == 25,"25 cards at " + viewport);
                    Check(root.Query<Button>(className:"codex-card").ToList().Count == 25,"fixed card pool at " + viewport);
                    var visibleCards=root.Query<Button>(className:"codex-card").ToList();
                    Check(Mathf.Abs(visibleCards[0].worldBound.y-visibleCards[4].worldBound.y)<1 && visibleCards[5].worldBound.y>visibleCards[0].worldBound.y+1,"five columns at "+viewport);
                    var back = root.Q<Button>("codex-back"); Check(back.worldBound.xMax <= root.worldBound.xMax + 1,"Back inside viewport " + viewport);
                    Check(root.Q("codex-pagination").worldBound.yMax <= root.worldBound.yMax + 1,"pagination inside viewport " + viewport);
                    window.controller.SetProvider(null); window.controller.Open(); await Task.Delay(80);
                    Check(root.Q<Label>("codex-progress-number").text == "—" && !root.Q<TextField>("codex-search").enabledSelf,"missing provider " + viewport);
                }
                await ValidatePortraits(Check);
                foreach (var viewport in Viewports) {
                    window.controller.Close();
                    window.surface.style.width=viewport.x; window.surface.style.height=viewport.y;
                    foreach (string panelName in new[] { "InventoryPanel", "PetsPanel", "BoxPanel", "QuestPanel" }) {
                        var holder=new VisualElement(); holder.style.position=Position.Absolute;
                        holder.style.left=holder.style.right=holder.style.top=holder.style.bottom=0; window.surface.Add(holder);
                        try {
                            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UI+panelName+".uxml").CloneTree(holder);
                            var header=holder.Q(className:"screen-header");
                            var panel=header;
                            while(panel.parent!=null && panel.parent!=holder && panel.parent is not TemplateContainer) panel=panel.parent;
                            panel.style.display=DisplayStyle.Flex;
                            await Task.Delay(180);
                            var back=holder.Q<Button>(className:"screen-back");
                            Check(back!=null && back.worldBound.xMax<=window.surface.worldBound.xMax+1, panelName+" Back fits "+viewport);
                            Check(holder.Q<Label>(className:"screen-title").resolvedStyle.fontSize==38, panelName+" shared title size "+viewport);
                        } finally { holder.RemoveFromHierarchy(); }
                    }
                }
                window.surface.style.width = 1366; window.surface.style.height = 700; window.SetFixture(320);
                foreach (string panel in new[] { "InventoryPanel", "PetsPanel", "BoxPanel", "QuestPanel", "SkillsPanel", "PetDetailsPanel", "PetLevelUpPanel", "PetHealPanel", "PetEvolutionPanel", "PetReleasePanel" }) {
                    var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UI+panel+".uxml"); Check(asset != null,"import " + panel);
                }
                Directory.CreateDirectory(Capstone.Game.UISystem.QaArtifactPaths.Root + "ui-completion");
                File.WriteAllLines(Capstone.Game.UISystem.QaArtifactPaths.Root + "ui-completion/validation.txt",results);
                Debug.Log("UI completion: " + results.Count + " checks passed.");
            } catch(Exception e) { results.Add("FAIL " + e); Directory.CreateDirectory(Capstone.Game.UISystem.QaArtifactPaths.Root + "ui-completion"); File.WriteAllLines(Capstone.Game.UISystem.QaArtifactPaths.Root + "ui-completion/validation.txt",results); Debug.LogException(e); }
            finally {
                validating=false;
                if (closeAfterValidation) {
                    closeAfterValidation=false;
                    foreach(var preview in Resources.FindObjectsOfTypeAll<PetCodexPreviewWindow>()) preview.Close();
                }
            }
        }
        // External verification can request one run without adding a runtime hook.
        // The request lives in Unity's temporary directory and is consumed once.
        [InitializeOnLoadMethod]
        static void ResumeRequestedValidation() {
            const string request="Temp/RunUICompletion.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            closeAfterValidation=true;
            EditorApplication.delayCall += Validate;
        }
        static void ValidateNameSort(Action<bool,string> check) {
            var entries=new Fixture(320).Entries;
            var optimized=new PetCodexQuery { Sort=CodexSort.Name };
            var actual=new List<PetCodexEntryViewData>(); var expected=new List<PetCodexEntryViewData>();
            Comparison<PetCodexEntryViewData> previous=(a,b)=>{
                int order=string.Compare(VietnameseUI.SearchKey(a.Known?a.Name:null),VietnameseUI.SearchKey(b.Known?b.Name:null),StringComparison.Ordinal);
                if(order==0) order=a.Number.CompareTo(b.Number);
                return order!=0?order:string.Compare(a.Id,b.Id,StringComparison.Ordinal);
            };
            void Baseline() { expected.Clear(); for(int i=0;i<entries.Count;i++) expected.Add(entries[i]); expected.Sort(previous); }
            for(int i=0;i<8;i++) { Baseline(); optimized.Apply(entries,actual); }
            var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<20;i++) Baseline();
            double oldMs=timer.Elapsed.TotalMilliseconds; timer.Restart();
            for(int i=0;i<20;i++) optimized.Apply(entries,actual);
            double newMs=timer.Elapsed.TotalMilliseconds; timer.Stop();
            bool same=actual.Count==expected.Count;
            for(int i=0;same && i<actual.Count;i++) same=actual[i].Id==expected[i].Id;
            check(same,"cached name sort preserves previous order");
            entries[0].Name="Đổi tên mới"; optimized.Search="doi ten moi"; optimized.Apply(entries,actual);
            check(actual.Count==1 && actual[0]==entries[0],"name search observes provider edits without stale cache");
            Directory.CreateDirectory(Capstone.Game.UISystem.QaArtifactPaths.Root + "ui-completion");
            long beforeProbe=GC.GetAllocatedBytesForCurrentThread(); GC.KeepAlive(new byte[4096]);
            bool counterAvailable=GC.GetAllocatedBytesForCurrentThread()>beforeProbe;
            File.WriteAllText(Capstone.Game.UISystem.QaArtifactPaths.Root + "ui-completion/performance.txt","Name sort; 320 entries; 20 queries after 8 warmup passes\nBaseline elapsed ms: "+oldMs.ToString("F2")+"\nOptimized elapsed ms: "+newMs.ToString("F2")+"\nGC byte counter available: "+counterAvailable+"\nIf unavailable, prior zero-byte results are not valid allocation measurements.\nTiming is a local microbenchmark, not whole-game FPS.\n");
        }
        static void ValidateMarkerVisibility(Action<bool,string> check) {
            var go=new GameObject("MarkerVisibilityFixture",typeof(RectTransform),typeof(UnityEngine.UI.Button),typeof(MapMarkerView)) { hideFlags=HideFlags.HideAndDontSave };
            try {
                var view=go.GetComponent<MapMarkerView>(); view.Bind(new MapMarkerData(),null,null);
                view.SetPosition(Vector2.one,true); view.SetClustered(true);
                bool stable=true;
                for(int i=0;i<100;i++) { view.SetPosition(Vector2.one,true); stable &= view.IsVisible && !go.activeSelf; }
                check(stable,"grouped marker stays inactive across 100 visible position updates");
                view.SetClustered(false); check(go.activeSelf,"marker becomes visible when it leaves a group");
                view.SetPosition(Vector2.one,false); view.SetClustered(false);
                check(!view.IsVisible && !go.activeSelf,"filter visibility survives group removal");
            } finally { DestroyImmediate(go); }
        }
        static T Field<T>(object owner,string name) => (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        static void Submit(Button button) {
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        static async Task ValidatePopupFocus(VisualElement surface, Action<bool,string> check) {
            var holder = new VisualElement(); var opener = new Button { text="Open" };
            holder.style.position=Position.Absolute; holder.style.left=30; holder.style.top=200; holder.style.width=300;
            var overlay = new VisualElement(); var input = new TextField { name="focus-input" };
            var disabled = new Button { text="Disabled" }; disabled.SetEnabled(false);
            var hidden = new Button { text="Hidden" }; hidden.style.display=DisplayStyle.None;
            var first = new Button { name="focus-first",text="First" }; var last = new Button { name="focus-last",text="Last" };
            surface.Add(holder); holder.Add(opener); holder.Add(overlay);
            overlay.Add(input); overlay.Add(disabled); overlay.Add(hidden); overlay.Add(first); overlay.Add(last);
            void Tab(bool shift=false) {
                using(var evt=NavigationMoveEvent.GetPooled(shift?NavigationMoveEvent.Direction.Previous:NavigationMoveEvent.Direction.Next,EventModifiers.None)) {
                    evt.target=surface.focusController.focusedElement; overlay.SendEvent(evt);
                }
            }
            try {
                await Task.Delay(100);
                opener.Focus(); PanelFocusUtility.Open(overlay); await Task.Delay(100);
                bool InputFocused() => surface.focusController.focusedElement==input
                    || input.Contains(surface.focusController.focusedElement as VisualElement);
                check(InputFocused(),"popup starts at text input ("+surface.focusController.focusedElement?.GetType().Name+")");
                Tab(); await Task.Delay(40); check(surface.focusController.focusedElement==first,"popup Tab leaves input and skips hidden/disabled buttons");
                Tab(); await Task.Delay(40);
                var stops=new List<string>(); overlay.Query<VisualElement>().ForEach(e=>{
                    if(e.focusable) stops.Add(e.GetType().Name+":"+e.name+":"+e.enabledInHierarchy+":"+e.resolvedStyle.display+":"+e.delegatesFocus);
                });
                check(surface.focusController.focusedElement==last,"popup Tab advances to next button ("+surface.focusController.focusedElement?.GetType().Name+":"+(surface.focusController.focusedElement as VisualElement)?.name+"; "+string.Join(",",stops)+")");
                Tab(); await Task.Delay(40); check(InputFocused(),"popup Tab wraps to input");
                Tab(true); await Task.Delay(40); check(surface.focusController.focusedElement==last,"popup Shift Tab wraps backwards");
                PanelFocusUtility.Close(overlay); check(surface.focusController.focusedElement==opener,"popup close restores opener focus");
            } finally { PanelFocusUtility.Close(overlay); holder.RemoveFromHierarchy(); }
        }
        static async Task ValidatePortraits(Action<bool,string> check) {
            foreach (string file in new[] { "Dragon01Gameplay", "Dragon02Gameplay", "Dragon03Gameplay" }) {
                var root=PrefabUtility.LoadPrefabContents("Assets/Game/HudSystem/Prefabs/"+file+".prefab");
                try {
                    var pet=root.GetComponentInChildren<PetController>(true);
                    var animator=pet.GetComponentInChildren<Animator>(true);
                    var clips=animator?.runtimeAnimatorController?.animationClips;
                    var clip=clips!=null && clips.Length>0 ? clips[0] : null;
                    var originalScale=root.transform.localScale;
                    foreach(float scale in new[] { .25f,1f,2.5f }) {
                        root.transform.localScale=originalScale*scale;
                        using(var preview=new PetPortraitPreview()) {
                            foreach(float phase in new[] { 0f,.5f,.95f }) {
                                if(clip!=null) clip.SampleAnimation(animator.gameObject,clip.length*phase);
                                var position=root.transform.position; var rotation=root.transform.rotation;
                                int components=root.GetComponentsInChildren<Component>(true).Length;
                                check(preview.Show(pet),file+" preview available "+scale+" / "+phase);
                                await Task.Delay(120);
                                // Edit Mode does not advance gameplay's unscaled clock.
                                typeof(PetPortraitPreview).GetField("nextBake",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(preview,0f);
                                preview.Update();
                                var camera=Field<Camera>(preview,"camera");
                                var parts=Field<IList>(preview,"parts");
                                check(camera!=null && camera.targetTexture!=null && camera.orthographicSize>0,file+" portrait camera fitted "+scale+" / "+phase);
                                check(root.transform.position==position && root.transform.rotation==rotation && root.GetComponentsInChildren<Component>(true).Length==components,file+" source untouched "+scale+" / "+phase);
                                foreach(var part in parts) {
                                    var type=part.GetType();
                                    var transform=(Transform)type.GetField("Transform").GetValue(part);
                                    var filter=(MeshFilter)type.GetField("Filter").GetValue(part);
                                    var bounds=filter.sharedMesh.bounds;
                                    var vertices=new List<Vector3>();
                                    if(filter.sharedMesh.isReadable) filter.sharedMesh.GetVertices(vertices);
                                    else for(int i=0;i<8;i++) vertices.Add(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                                    bool fits=true; Vector3 failed=Vector3.zero;
                                    foreach(var vertex in vertices) {
                                        var projected=camera.WorldToViewportPoint(transform.TransformPoint(vertex));
                                        if(projected.x<-.01f || projected.x>1.01f || projected.y<-.01f || projected.y>1.01f || projected.z<=0) { fits=false; failed=projected; break; }
                                    }
                                    check(fits,file+" mesh vertices fit "+scale+" / "+phase+(fits?"":" projected="+failed+" aspect="+camera.aspect));
                                }
                            }
                        }
                    }
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
}
