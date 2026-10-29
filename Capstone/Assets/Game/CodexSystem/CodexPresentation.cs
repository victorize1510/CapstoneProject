using System.Linq;
using System.Runtime.CompilerServices;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.CodexSystem {
    /// <summary>Codex-only view adapter. Does not create a catalog, save or provider.</summary>
    public sealed class CodexPresentation {
        static readonly ConditionalWeakTable<VisualElement,CodexPresentation> instances=new ConditionalWeakTable<VisualElement,CodexPresentation>();
        readonly VisualElement root,canvas,filters;readonly ScrollView gridScroll;
        readonly VisualElement emptyGroup,emptyHint,detailEmptyGroup,basicLock;
        readonly CodexSurface portrait;
        readonly TextField search;
        readonly CodexButton clearSearch;
        readonly CodexButton[] cards;readonly VisualElement[] discoveryMarkers;
        readonly string[] glyphs={"book","fire","water","nature","skills","ice","earth","wind","dark","light","mythic","dragon"};
        CodexPresentation(VisualElement panel){
            root=panel;root.AddToClassList("codex-family");
            // The family owns these surfaces; discard legacy per-section decorations only inside Codex.
            foreach(var layer in root.Query<UiSectionFrame>().ToList())layer.RemoveFromHierarchy();
            foreach(var layer in root.Query<UiReadingSurface>().ToList())layer.RemoveFromHierarchy();
            foreach(var section in root.Query<VisualElement>(className:"ui-section").ToList())section.RemoveFromClassList("ui-section");
            canvas=new VisualElement{name="codex-canvas"};canvas.style.position=Position.Absolute;canvas.style.width=1600;canvas.style.height=900;canvas.style.transformOrigin=new TransformOrigin(0,0,0);canvas.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);root.Add(canvas);
            var pattern=new CodexSurface{Kind="page-pattern"};Place(pattern,0,0,1600,900);canvas.Add(pattern);
            var header=root.Q(className:"screen-header");var title=header.Q<Label>(className:"screen-title");title.text="CẨM NANG";
            var back=root.Q<Button>("codex-back");var subtitle=header.Q<Label>(className:"codex-subtitle");
            var shared=UnifiedPanelHeader.Attach(root,canvas,title,null,back);subtitle.RemoveFromHierarchy();shared.Add(subtitle);Place(subtitle,728,34,648,46);subtitle.style.color=GreenAngularGeometry.Light;subtitle.pickingMode=PickingMode.Ignore;header.style.display=DisplayStyle.None;
            var left=root.Q<ScrollView>(className:"codex-left");left.contentContainer.AddToClassList("codex-left-content");Frame(left,"left",24,112,248,764);
            var center=root.Q(className:"codex-center");Frame(center,"center",288,112,784,764);
            var right=root.Q<ScrollView>(className:"codex-right");right.contentContainer.AddToClassList("codex-right-content");Frame(right,"right",1088,112,488,764);
            root.Q(className:"codex-body").style.display=DisplayStyle.None;
            root.Q<Label>(className:"codex-section-title").text="TIẾN ĐỘ KHÁM PHÁ";
            foreach(var label in root.Q(className:"codex-progress").Query<Label>().ToList())label.style.color=GreenAngularGeometry.Light;
            var name=root.Q<Label>("codex-name");name.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);name.style.unityFontStyleAndWeight=FontStyle.Bold;
            foreach(var label in root.Q("codex-details").Query<Label>(className:"codex-section-title").ToList())label.text=label.text.ToUpperInvariant();
            foreach(var badge in root.Query<Label>(className:"codex-type-badge").ToList()){
                var parent=badge.parent;badge.RemoveFromHierarchy();var host=new VisualElement();host.AddToClassList("codex-badge");CodexSurface.Behind(host,"badge");host.Add(badge);host.Add(Glyph("codex-badge-glyph","book"));parent.Add(host);
            }
            var categories=root.Query<CodexButton>(className:"codex-category").ToList();
            for(int i=0;i<categories.Count;i++){var icon=Glyph("codex-category-glyph",glyphs[i]);categories[i].Insert(1,icon);}
            search=root.Q<TextField>("codex-search");search.textEdition.placeholder="Tên hoặc số…";
            var searchGroup=search.parent;searchGroup.name="codex-search-group";CodexSurface.Behind(searchGroup,"input");searchGroup.Add(Glyph("codex-search-glyph","search"));
            clearSearch=root.Q<CodexButton>("codex-clear");clearSearch.Kind="input";clearSearch.Surface.ClearFace=true;SearchState();
            filters=root.Q("codex-filters");CodexSurface.Behind(filters,"shelf");
            foreach(var pair in new[]{new[]{"codex-region","Khu vực"},new[]{"codex-rarity","Độ hiếm"},new[]{"codex-discovery","Trạng thái"},new[]{"codex-extra-sort","Sắp xếp"}}){var field=root.Q<DropdownField>(pair[0]);field.label=pair[1];var input=field.Q(className:"unity-base-field__input");if(input!=null)CodexSurface.Behind(input,"input");}
            root.Q("codex-filter-toggle").Insert(1,Glyph("codex-filter-glyph","filter"));
            gridScroll=root.Q<ScrollView>(className:"codex-grid-scroll");filters.RegisterCallback<GeometryChangedEvent>(_=>FitFilters());
            var empty=root.Q<Label>("codex-empty");emptyGroup=new VisualElement{name="codex-empty-state"};empty.RemoveFromHierarchy();emptyGroup.Add(Book("codex-empty-book"));emptyGroup.Add(empty);var hint=new Label{name="codex-empty-hint",pickingMode=PickingMode.Ignore};emptyHint=hint;emptyGroup.Add(hint);gridScroll.Add(emptyGroup);
            var detailEmpty=root.Q<Label>("codex-detail-empty");detailEmptyGroup=new VisualElement{name="codex-detail-empty-state"};detailEmpty.RemoveFromHierarchy();detailEmptyGroup.Add(Glyph("codex-detail-book-glyph","book"));detailEmptyGroup.Add(detailEmpty);detailEmptyGroup.Add(Book("codex-detail-book-stamp"));right.Add(detailEmptyGroup);
            cards=root.Query<CodexButton>(className:"codex-card").ToList().ToArray();discoveryMarkers=new VisualElement[cards.Length];
            for(int i=0;i<cards.Length;i++){discoveryMarkers[i]=Glyph("codex-discovery-marker","eye");cards[i].Add(discoveryMarkers[i]);cards[i].Surface.Kind="card";var image=cards[i].Q("portrait");foreach(var cls in new[]{"unknown","favorite"}){var label=cards[i].Q(cls);label.RemoveFromHierarchy();cards[i].Add(label);}image.style.opacity=0;}
            portrait=CodexSurface.Behind(root.Q("codex-detail-portrait"),"portrait");portrait.ContainArt=true;
            var question=new Label("?"){name="codex-detail-question",pickingMode=PickingMode.Ignore};root.Q("codex-detail-portrait").Add(question);
            basicLock=Glyph("codex-basic-lock","lock");root.Q("codex-basic").parent.Insert(1,basicLock);
            foreach(var e in root.Query<Label>().ToList())e.pickingMode=PickingMode.Ignore;
            root.RegisterCallback<GeometryChangedEvent>(_=>Fit());canvas.RegisterCallback<AttachToPanelEvent>(_=>Fit());Fit();InteractionFeedback.Bind(root.panel?.visualTree);
        }
        public static CodexPresentation Attach(VisualElement root){
            if(root==null)return null;var view=instances.GetValue(root,p=>new CodexPresentation(p));
            foreach(var layer in root.Query<UiSectionFrame>().ToList())layer.RemoveFromHierarchy();
            foreach(var layer in root.Query<UiReadingSurface>().ToList())layer.RemoveFromHierarchy();
            foreach(var section in root.Query<VisualElement>(className:"ui-section").ToList())section.RemoveFromClassList("ui-section");
            return view;
        }
        void Frame(VisualElement content,string kind,float x,float y,float w,float h){content.RemoveFromHierarchy();var wrapper=new VisualElement{name="codex-"+kind+"-frame"};Place(wrapper,x,y,w,h);CodexSurface.Behind(wrapper,kind);wrapper.Add(content);canvas.Add(wrapper);}
        static VisualElement Glyph(string cls,string key){var e=new VisualElement{pickingMode=PickingMode.Ignore};e.AddToClassList(cls);e.style.backgroundImage=new StyleBackground(CodexArt.Load("Icons/"+key));e.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);return e;}
        static VisualElement Book(string cls){var e=new VisualElement{pickingMode=PickingMode.Ignore};e.AddToClassList(cls);e.style.backgroundImage=new StyleBackground(Resources.Load<Texture2D>("UI/TrainerMenu/05-codex-book-corner"));e.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);return e;}
        static void Place(VisualElement e,float x,float y,float w,float h)=>UnifiedPanelHeader.Place(e,new Rect(x,y,w,h));
        void Fit(){float w=root.resolvedStyle.width,h=root.resolvedStyle.height;if(!float.IsFinite(w)||!float.IsFinite(h)||w<=0||h<=0)return;float scale=Mathf.Min(w/1600,h/900);canvas.style.left=(w-1600*scale)/2;canvas.style.top=(h-900*scale)/2;canvas.style.scale=new Scale(new Vector3(scale,scale,1));FitFilters();}
        public void FitFilters(){if(gridScroll==null)return;bool visible=filters.resolvedStyle.display!=DisplayStyle.None;root.EnableInClassList("filters-open",visible);gridScroll.style.top=visible?308:108;}
        public void Refresh(PetCodexEntryViewData selected,bool available,int resultCount){
            SearchState();
            bool empty=resultCount==0;emptyGroup.style.display=empty?DisplayStyle.Flex:DisplayStyle.None;root.Q<Label>("codex-empty").text=available?"Không tìm thấy sinh vật phù hợp.":"Cẩm nang chưa có thông tin sinh vật.";((Label)emptyHint).text=available?"Hãy thử bộ lọc khác.":"Sẽ khả dụng khi có thông tin cẩm nang.";
            detailEmptyGroup.style.display=selected==null?DisplayStyle.Flex:DisplayStyle.None;
            foreach(var card in cards){card.Surface.Unknown=card.ClassListContains("is-unknown");card.Surface.MarkDirtyRepaint();}
            if(selected!=null){root.EnableInClassList("unknown-detail",!selected.Known);portrait.Art=selected.Portrait!=null?selected.Portrait:selected.Icon;portrait.Unknown=!selected.Known;portrait.MarkDirtyRepaint();root.Q("codex-detail-portrait").style.backgroundImage=StyleKeyword.None;root.Q("codex-detail-question").style.display=!selected.Known||portrait.Art==null?DisplayStyle.Flex:DisplayStyle.None;basicLock.style.display=selected.Known?DisplayStyle.None:DisplayStyle.Flex;
                var badges=root.Query<Label>(className:"codex-type-badge").ToList();for(int i=0;i<badges.Count;i++){var type=i==0?selected.PrimaryType:selected.SecondaryType;badges[i].parent.Q(className:"codex-badge-glyph").style.backgroundImage=new StyleBackground(CodexArt.Load("Icons/"+glyphs[Mathf.Clamp((int)type,0,glyphs.Length-1)]));}}
            foreach(var node in root.Query<VisualElement>(className:"codex-evolution-node").ToList())if(node.Q<CodexSurface>()==null)CodexSurface.Behind(node,"node");
            foreach(var button in root.Query<CodexButton>().ToList())button.Surface.MarkDirtyRepaint();FitFilters();
        }
        void SearchState(){clearSearch.style.display=string.IsNullOrEmpty(search.value)?DisplayStyle.None:DisplayStyle.Flex;}
        public void BindCard(int index,PetCodexEntryViewData entry){var card=cards[index];card.EnableInClassList("is-unknown",!entry.Known);card.Surface.Art=entry.Icon;card.Surface.Unknown=!entry.Known;card.Surface.MarkDirtyRepaint();var marker=discoveryMarkers[index];marker.style.display=entry.Known?DisplayStyle.Flex:DisplayStyle.None;marker.style.backgroundImage=new StyleBackground(CodexArt.Load("Icons/"+(entry.Discovery==CodexDiscovery.Seen?"eye":entry.Discovery==CodexDiscovery.Recorded?"book":"companion")));marker.tooltip=!entry.Known?string.Empty:entry.Discovery==CodexDiscovery.Seen?"Đã gặp":entry.Discovery==CodexDiscovery.Recorded?"Đã ghi nhận":"Đã thu phục";}
    }
}
