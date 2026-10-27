using System;
using System.Linq;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory.Bag {
    /// <summary>Scoped visual adapter. Never changes PanelSettings, item data, selection or save.</summary>
    public sealed class BagPresentation {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,BagPresentation> instances=new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,BagPresentation>();
        readonly VisualElement root,panel,canvas;
        BagPresentation(VisualElement root,VisualElement panel){
            this.root=root;this.panel=panel;canvas=panel.Q("bag-canvas");
            var sheet=Resources.Load<StyleSheet>("UI/BAG20261022/Bag");if(sheet!=null&&!root.styleSheets.Contains(sheet))root.styleSheets.Add(sheet);
            panel.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
            foreach(var label in panel.Query<Label>().ToList())if(label.ClassListContains("bag-title")||label.name=="detail-name"||label.name=="inventory-detail-empty")label.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);
            panel.style.backgroundImage=new StyleBackground(LatestMenuCompanionAssets.Load("Assets/Art/steel-enamel-r2"));panel.style.unityBackgroundImageTintColor=new Color(.45f,.50f,.64f,1);
            var emblem=panel.Q("bag-title-emblem");if(emblem!=null)emblem.style.backgroundImage=new StyleBackground(Capstone.Game.HudSystem.CompanionArt.Load("Art/companion-title-emblem"));
            var art=panel.Q("bag-browser-art");if(art!=null)art.style.backgroundImage=new StyleBackground(LatestMenuCompanionAssets.Load("Assets/Art/scene-bag-r2"));
            if(PanelMaterialField.UseUpdatedMaterials){
                foreach(var id in new[]{"inventory-shell","bag-browser","bag-inspector"}) {
                    var surface=panel.Q<BagSurface>(id);
                    if(surface!=null)surface.Background=PanelMaterialField.Bag(surface.Kind);
                }
                // Remove only the obsolete faint landscape; the continuous terrain field replaces it.
                if(art!=null)art.style.backgroundImage=StyleKeyword.None;
            }
            string[] cats={"all","capture-ball","medicine","food","material","key-item","quest-item","equipment"};string[] icons={"grid","capture","heal","food","material","keyitem","questitem","defense"};
            for(int i=0;i<cats.Length;i++)BagArt.Icon(panel.Q("bag-category-"+cats[i]+"-icon"),icons[i]);
            BagArt.Icon(panel.Q("bag-search-icon"),"search");
            var bagGlyph=Resources.Load<Texture2D>("UI/Readability20261026/bag-empty");
            foreach(string id in new[]{"bag-empty-icon","bag-detail-empty-art"}){var emptyArt=panel.Q(id);if(emptyArt!=null){emptyArt.style.backgroundImage=new StyleBackground(bagGlyph);emptyArt.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);emptyArt.style.unityBackgroundImageTintColor=Color.white;}}
            var search=panel.Q<TextField>("inventory-search-field");var clear=panel.Q<Button>("inventory-search-field-clear");
            if(clear is BagButton clearButton)clearButton.Surface.ClearFace=true;
            void SearchState(){if(clear!=null)clear.style.display=string.IsNullOrEmpty(search?.value)?DisplayStyle.None:DisplayStyle.Flex;}
            search?.RegisterValueChangedCallback(_=>SearchState());SearchState();
            panel.RegisterCallback<GeometryChangedEvent>(_=>Fit());canvas.RegisterCallback<AttachToPanelEvent>(_=>Fit());Fit();
            InteractionFeedback.Bind(root);
            EvolutionLayout(root);
            BagChoicePopup.Bind(panel.Q<DropdownField>("inventory-sort-field"));BagChoicePopup.Bind(panel.Q<DropdownField>("inventory-filter-field"));
            foreach(var placeholder in panel.Query<Label>(className:"unity-base-text-field__placeholder").ToList())placeholder.pickingMode=PickingMode.Ignore;
            foreach(var old in canvas.Children().OfType<AngularSurface>())old.style.opacity=0;
            var body=panel.Q("inventory-body");if(body!=null)body.pickingMode=PickingMode.Ignore;
            var shell=panel.Q("inventory-shell");if(shell!=null)shell.style.height=764;
            var gold=canvas.Children().OfType<BagSurface>().FirstOrDefault(s=>s.Kind=="category-row");
            if(gold!=null)Place(gold,1176,34,200,46);
            Place(panel.Q("inventory-gold-label"),1224,39,136,36);
            UnifiedPanelHeader.Attach(panel,canvas,panel.Q<Label>(className:"bag-title"),panel.Q("bag-title-emblem"),panel.Q<Button>("cancel-button"));
        }
        public static BagPresentation Attach(VisualElement root,VisualElement panel)=>root!=null&&panel?.Q("bag-canvas")!=null?instances.GetValue(panel,_=>new BagPresentation(root,panel)):null;
        void Fit(){float w=panel.resolvedStyle.width,h=panel.resolvedStyle.height;if(!float.IsFinite(w)||!float.IsFinite(h)||w<=0||h<=0)return;float scale=Mathf.Min(w/1600,h/900);canvas.style.left=(w-1600*scale)/2;canvas.style.top=(h-900*scale)/2;canvas.style.scale=new Scale(new Vector3(scale,scale,1));}
        public void SetMode(bool list){
            var label=panel.Q<Label>("bag-mode-label");if(label!=null)label.text=list?"Danh sách ngang":"Ô vuông";
            BagArt.Icon(panel.Q("bag-view-icon"),list?"grid":"list");var helper=panel.Q("bag-grid-helper");if(helper!=null)helper.style.display=list?DisplayStyle.None:DisplayStyle.Flex;
            var art=panel.Q("bag-browser-art");if(art!=null)art.style.display=list?DisplayStyle.None:DisplayStyle.Flex;
        }
        public void Refresh(int count,bool hasItem,bool hasItems){
            foreach(string id in new[]{"bag-type-count","bag-footer-count"}){var label=panel.Q<Label>(id);if(label!=null)label.text=count+" loại vật phẩm";}
            var empty=panel.Q("bag-empty-state");if(empty!=null)empty.style.display=hasItems?DisplayStyle.None:DisplayStyle.Flex;
            var icon=panel.Q("detail-icon");if(icon!=null)icon.style.display=hasItem?DisplayStyle.Flex:DisplayStyle.None;
            var fallback=panel.Q("bag-detail-empty-art");if(fallback!=null)fallback.style.display=hasItem?DisplayStyle.None:DisplayStyle.Flex;
            var hint=panel.Q("bag-detail-empty-hint");if(hint!=null)hint.style.display=hasItem?DisplayStyle.None:DisplayStyle.Flex;
            var placeholder=panel.Q("bag-detail-empty-group");if(placeholder!=null)placeholder.style.display=hasItem?DisplayStyle.None:DisplayStyle.Flex;
        }
        public void Feedback(string message){var label=panel.Q<Label>("bag-action-feedback");if(label!=null){label.text=message??"";label.tooltip=message??"";}}
        public void Suspend(){BagTooltip.HideAll(root);BagChoicePopup.Close(root);foreach(var surface in panel.Query<BagSurface>().ToList())surface.Stop();}
        public static void Place(VisualElement e,float x,float y,float w,float h){if(e==null)return;e.style.position=Position.Absolute;e.style.left=x;e.style.top=y;e.style.width=w;e.style.height=h;e.style.marginLeft=e.style.marginRight=e.style.marginTop=e.style.marginBottom=0;e.style.paddingLeft=e.style.paddingRight=e.style.paddingTop=e.style.paddingBottom=0;e.style.flexShrink=0;}
        public static void Stretch(VisualElement e){e.style.position=Position.Absolute;e.style.left=e.style.right=e.style.top=e.style.bottom=0;}
        public static void Modal(VisualElement overlay,VisualElement card,string component,float width,float height){
            overlay.AddToClassList("bag-modal");card.AddToClassList("bag-modal-card");card.style.backgroundColor=Color.clear;card.style.borderLeftWidth=card.style.borderRightWidth=card.style.borderTopWidth=card.style.borderBottomWidth=0;
            card.style.maxWidth=card.style.maxHeight=StyleKeyword.None;card.style.minWidth=card.style.minHeight=0;
            card.style.borderTopLeftRadius=card.style.borderTopRightRadius=card.style.borderBottomLeftRadius=card.style.borderBottomRightRadius=0;
            Place(card,0,0,width,height);card.style.transformOrigin=new TransformOrigin(0,0,0);card.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
            var surface=new BagSurface{Kind=component};Stretch(surface);card.Insert(0,surface);
            void Fit(){var r=overlay.panel?.visualTree;if(r==null)return;float w=r.resolvedStyle.width,h=r.resolvedStyle.height;if(w<=0||h<=0)return;float s=Mathf.Min(w/1600,h/900);card.style.left=(w-width*s)/2;card.style.top=(h-height*s)/2;card.style.scale=new Scale(new Vector3(s,s,1));}
            EventCallback<GeometryChangedEvent> geometry=_=>Fit();EventCallback<AttachToPanelEvent> attach=_=>Fit();
            overlay.RegisterCallback(geometry);overlay.RegisterCallback(attach);
            card.RegisterCallback<DetachFromPanelEvent>(_=>{overlay.UnregisterCallback(geometry);overlay.UnregisterCallback(attach);});Fit();
        }
        public static void Quantity(VisualElement overlay,Action close){
            var card=overlay.Q("quantity-popup-panel");Modal(overlay,card,"popup-quantity",704,568);
            var header=new BagSurface{Kind="action-primary"};Place(header,12,12,680,56);card.Insert(1,header);
            var title=overlay.Q<Label>("quantity-action-title");title.AddToClassList("bag-modal-title");title.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);Place(title,32,12,640,56);
            Place(overlay.Q("quantity-item-icon"),28,92,80,88);Place(overlay.Q("quantity-item-name"),128,104,540,38);overlay.Q("quantity-item-name").AddToClassList("bag-modal-item-name");Place(overlay.Q("quantity-owned"),128,148,540,28);
            var item=new BagSurface{Kind="detail-art"};Place(item,24,88,656,96);card.Insert(2,item);
            Place(overlay.Q("quantity-stepper"),24,236,656,52);Place(overlay.Q("quantity-minus"),0,0,52,52);Place(overlay.Q("quantity-field"),80,0,448,52);Place(overlay.Q("quantity-plus"),552,0,104,52);
            var field=new BagSurface{Kind="integer-field"};Place(field,104,236,448,52);card.Insert(3,field);
            Place(overlay.Q("quantity-slider"),24,304,656,34);Place(overlay.Q("quantity-quick-row"),24,350,656,44);Place(overlay.Q("quantity-one"),0,0,208,44);Place(overlay.Q("quantity-half"),224,0,208,44);Place(overlay.Q("quantity-all"),448,0,208,44);
            var note=new Label("Chỉ thực hiện sau khi xác nhận."){pickingMode=PickingMode.Ignore};note.AddToClassList("bag-modal-note");Place(note,24,414,656,40);card.Add(note);
            Place(overlay.Q("quantity-action-row"),24,478,656,56);Place(overlay.Q("quantity-cancel"),0,0,320,56);Place(overlay.Q("quantity-confirm"),336,0,320,56);
            var countLabel=new Label("Số lượng"){pickingMode=PickingMode.Ignore};countLabel.AddToClassList("bag-modal-note");Place(countLabel,24,202,656,28);card.Add(countLabel);
            var dismiss=new BagButton(close){text="×",Kind="action-primary",name="quantity-close"};Place(dismiss,644,20,36,36);card.Add(dismiss);
        }
        static void EvolutionLayout(VisualElement root){
            var overlay=root.Q("bag-evolution-confirm-overlay");var card=overlay?.Q("bag-evolution-card");if(card==null||card.Q("bag-evolution-item")!=null)return;
            Modal(overlay,card,"popup-evolution",792,552);
            void Surface(string kind,float x,float y,float w,float h){var e=new BagSurface{Kind=kind};Place(e,x,y,w,h);card.Insert(1,e);}
            Surface("action-primary",12,12,768,56);Surface("detail-art",24,88,744,88);Surface("detail-art",24,200,744,88);Surface("detail-art",24,312,744,72);Surface("action-primary",24,400,744,40);
            var title=card.Q<Label>("bag-evolution-title");title.AddToClassList("bag-modal-title");Place(title,32,12,728,56);
            title.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);
            foreach(string id in new[]{"item","pet"}){
                int y=id=="item"?88:200;var art=new VisualElement{name="bag-evolution-"+id+"-icon"};art.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);Place(art,32,y+8,68,68);card.Add(art);
                var name=new Label{name="bag-evolution-"+id};name.AddToClassList("bag-modal-item-name");Place(name,124,y+10,620,34);card.Add(name);
                var note=new Label{name="bag-evolution-"+id+"-note"};Place(note,124,y+50,620,28);card.Add(note);
            }
            Place(card.Q("bag-evolution-confirm-text"),40,320,712,56);Place(card.Q("bag-evolution-confirm-feedback"),40,400,712,40);card.Q("bag-evolution-confirm-feedback").AddToClassList("bag-modal-note");
            Place(card.Q(className:"bag-evolution-confirm-actions"),24,472,744,56);Place(card.Q("bag-evolution-cancel"),0,0,356,56);Place(card.Q("bag-evolution-confirm"),380,0,364,56);
        }
        public static void Evolution(VisualElement root,Capstone.Game.HudSystem.PetEvolutionPreview preview,InventoryItemSnapshot item){
            void Text(string id,string value){var e=root.Q<Label>(id);if(e!=null){e.text=value;e.tooltip=value;}}
            Text("bag-evolution-title",item!=null?"DÙNG "+item.Name.ToUpperInvariant():"VẬT PHẨM TIẾN HÓA");Text("bag-evolution-item",item?.Name??"Thiếu vật phẩm");Text("bag-evolution-item-note","Sở hữu: "+(item?.Quantity??0));Text("bag-evolution-pet",preview?.CurrentName??"Không có thú phù hợp");Text("bag-evolution-pet-note","Thú được chọn từ Thú đồng hành");
            var itemArt=root.Q("bag-evolution-item-icon");if(itemArt!=null)itemArt.style.backgroundImage=new StyleBackground(item?.Icon);
            var petArt=root.Q("bag-evolution-pet-icon");if(petArt!=null)petArt.style.backgroundImage=new StyleBackground(preview?.CurrentIcon);
        }
    }
}
