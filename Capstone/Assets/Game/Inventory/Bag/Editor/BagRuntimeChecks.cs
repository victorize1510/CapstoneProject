using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Profiling;
using Object=UnityEngine.Object;

namespace Capstone.Game.Inventory.Bag.Editor {
    static class BagRuntimeChecks {
        static void Check(bool ok,string label)=>BagVerification.Check(ok,label);
        static object Get(object o,string n)=>BagVerification.Get(o,n);
        static object Call(object o,string n,params object[] a)=>BagVerification.Call(o,n,a);
        static void Click(VisualElement ui,string n)=>BagVerification.Click(ui,n);
        public static async Task Screens(MonsterInventoryController bag,VisualElement ui,string folder){
            var adapter=bag.GetComponent<MonsterInventoryAdapter>();var inventory=adapter.CreateSaveData();
            var items=adapter.GetItems();var potion=items.FirstOrDefault(i=>i.IsRecoveryItem);var stone=items.FirstOrDefault(i=>i.Category==GameItemCategory.KeyItem);
            var popup=(ItemQuantityPopup)Get(bag,"quantityPopup");
            if(potion!=null){
                popup.Show("XÁC NHẬN BỎ VẬT PHẨM",potion,_=>{});await BagVerification.Shot(ui,folder,"03-quantity-drop");popup.Hide();
                var quick=Object.FindFirstObjectByType<GameplayHudQuickItemsView>();
                if(quick!=null){Call(quick,"ShowAssignment",new InventoryActionRequest(InventoryActionType.AssignQuickSlot,potion,false,0));await BagVerification.Shot(ui,folder,"04-assign-rt");quick.TryCloseAssignment();}
                var heal=bag.GetComponent<PetHealPanelController>();Check(heal.TryOpenFromInventory(potion),"recovery opens existing controller at "+Screen.width);await BagVerification.Shot(ui,folder,"05-heal");var track=ui.Q("bag-heal-dialog").Q(className:"ga-heal-track");Check(track.layout.y>=280&&track.layout.y<300,"heal resource bar stays inside preview at "+Screen.width);heal.Close();
            }
            var pet=Object.FindFirstObjectByType<PetCommandHudProvider>()?.GetSelectedPetController();
            if(pet!=null&&stone!=null){bag.OpenBagEvolutionConfirmation(pet,stone.ItemId);await BagVerification.Shot(ui,folder,"06-evolution");Call(bag,"CloseBagEvolutionConfirmation");}
            Call(bag,"SelectItem",-1,false);await BagVerification.Shot(ui,folder,"07-no-selection");
            adapter.SetInventory(null);await BagVerification.Shot(ui,folder,"08-empty-bag");Check(adapter.RestoreFromSaveData(inventory,out var error),"restore isolated inventory after empty case: "+error);
            ui.Q<TextField>("inventory-search-field").value="không có vật phẩm QA xyz";await BagVerification.Shot(ui,folder,"09-no-results");ui.Q<TextField>("inventory-search-field").value="";
        }
        public static async Task Transactions(MonsterInventoryController bag,VisualElement ui,string folder){
            var adapter=bag.GetComponent<MonsterInventoryAdapter>();var inventory=adapter.CreateSaveData();var save=Object.FindFirstObjectByType<PlayerSaveController>();
            var search=ui.Q<TextField>("inventory-search-field");var sort=ui.Q<DropdownField>("inventory-sort-field");var filter=ui.Q<DropdownField>("inventory-filter-field");
            int saves=0;Action<PlayerSaveData> saved=_=>saves++;save.SaveCompleted+=saved;
            try{
                await Task.Delay(700);saves=0;search.value="Bình";sort.value=sort.choices[2];filter.value=filter.choices[3];Call(bag,"SelectItem",0,false);
                var choiceType=typeof(BagSurface).Assembly.GetType("Capstone.Game.Inventory.Bag.BagChoicePopup");var showChoice=choiceType.GetMethod("Show",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                var portal=ui.panel.visualTree;showChoice.Invoke(null,new object[]{sort});await Task.Delay(100);Check(portal.Q("bag-choice-popup")!=null,"BAG sort popup renders through local polygon presentation");
                var beforeChoice=bag.SelectedItem?.ItemBase;Click(portal,"bag-choice-1");Check(sort.value==sort.choices[1]&&ReferenceEquals(beforeChoice,bag.SelectedItem?.ItemBase),"choice row updates original DropdownField and retains item");
                showChoice.Invoke(null,new object[]{filter});await BagVerification.Shot(ui,folder,"11-filter-popup");Check(PanelFocusUtility.TryCloseNativeDropdown(ui)&&portal.Q("bag-choice-popup")==null,"existing Escape cancellation path closes only BAG choice popup");sort.value=sort.choices[2];
                var chosen=bag.SelectedItem?.ItemBase;int pooled=ui.Query<BagButton>().ToList().Count;bool mode=(bool)Get(bag,"listView");
                for(int i=0;i<6;i++){Click(ui,"inventory-view-button");await Task.Delay(180);}
                Check(ReferenceEquals(chosen,bag.SelectedItem?.ItemBase)&&search.value=="Bình"&&sort.value==sort.choices[2]&&filter.value==filter.choices[3],"six mode switches preserve selected item/query/sort/filter");
                Check((bool)Get(bag,"listView")==mode&&ui.Query<BagButton>().ToList().Count==pooled,"mode switches preserve card pool and mode parity");Check(saves==0,"presentation changes do not save");
                search.value="";sort.value=sort.choices[0];filter.value=filter.choices[0];
                var potion=adapter.GetItems().First(i=>i.IsRecoveryItem);var quantity=(ItemQuantityPopup)Get(bag,"quantityPopup");int result=0;bool hiddenBeforeHandler=false;
                quantity.Show("BỎ VẬT PHẨM",potion,n=>{result=n;hiddenBeforeHandler=!quantity.IsOpen;});
                var field=ui.Q<IntegerField>("quantity-field");field.value=-100;Check(field.value==1,"quantity clamps minimum to one");field.value=int.MaxValue;Check(field.value==potion.Quantity,"quantity clamps maximum to owned");
                Click(ui,"quantity-half");Check(field.value==Mathf.Max(1,potion.Quantity/2),"quantity half uses integer floor");Click(ui,"quantity-one");Check(field.value==1,"quantity one preset");Click(ui,"quantity-all");Check(field.value==potion.Quantity,"quantity all preset");Click(ui,"quantity-confirm");Check(result==potion.Quantity&&hiddenBeforeHandler,"quantity popup closes before one confirmation callback");
                var cards=ui.Query<BagButton>().ToList().Where(b=>b.name.StartsWith("inventory-item-")&&b.resolvedStyle.display!=DisplayStyle.None).ToArray();
                var stateRows=new System.Collections.Generic.List<string>{"state,control,part,x,y,w,h"};
                foreach(var button in cards){var labels=button.Query<Label>().ToList().Cast<VisualElement>().Concat(new[]{button.Q("card-icon")}).Where(e=>e!=null).ToArray();var initial=labels.Select(e=>e.worldBound).ToArray();
                    foreach(string state in new[]{"normal","hover","pressed","focus","selected-focus","disabled"}){button.Surface.Hovered=state=="hover";button.Surface.Pressed=state=="pressed";button.Surface.Focused=state.Contains("focus");button.EnableInClassList("is-selected",state=="selected-focus");button.SetEnabled(state!="disabled");button.Surface.MarkDirtyRepaint();await Task.Delay(30);for(int i=0;i<labels.Length;i++){var b=labels[i].worldBound;stateRows.Add(FormattableString.Invariant($"{state},{button.name},{labels[i].name},{b.x},{b.y},{b.width},{b.height}"));Check(Vector2.Distance(b.position,initial[i].position)<.01f&&Vector2.Distance(b.size,initial[i].size)<.01f,"immutable bounds "+button.name+" "+labels[i].name+" "+state);}}
                    button.SetEnabled(true);button.Surface.Stop();}
                File.WriteAllLines(folder+"/state-bounds.csv",stateRows);Call(bag,"RefreshItemCardSelection");
                var singleton=MonsterItemDefinition.CreateRuntime("Vật phẩm không cộng dồn QA",GameItemCategory.Material,"","",null,false);try{quantity.Show("BỎ",new InventoryItemSnapshot(singleton,null,12),_=>{});Check(field.value==1&&!field.enabledInHierarchy,"non-stackable quantity fixed at one");quantity.Hide();}finally{Object.Destroy(singleton);}
                // Exercise the real drop callback and the controller's failed-handler branch separately.
                Call(bag,"SelectItem",((System.Collections.Generic.List<InventoryItemSnapshot>)Get(bag,"filteredItems")).FindIndex(i=>i.ItemId==potion.ItemId),false);int dropOwned=adapter.GetQuantity(potion.ItemBase);Click(ui,"drop-button");Click(ui,"quantity-confirm");Check(adapter.GetQuantity(potion.ItemBase)==dropOwned-1,"real Drop UI consumes confirmed one item");
                var originalDrop=Get(bag,"DropItem");Action<InventoryActionRequest> rejected=r=>r.Complete(false,false,0,"QA: từ chối bỏ vật phẩm");BagVerification.Set(bag,"DropItem",rejected);
                try{await Task.Delay(200);dropOwned=adapter.GetQuantity(potion.ItemBase);Call(bag,"RequestAction",InventoryActionType.DropItem,1);Check(adapter.GetQuantity(potion.ItemBase)==dropOwned&&ui.Q<Label>("bag-action-feedback").text.Contains("từ chối"),"failed drop handler preserves quantity and displays feedback");}finally{BagVerification.Set(bag,"DropItem",originalDrop);}
                await Task.Delay(200);Call(bag,"OpenQuantityPopup",InventoryActionType.DropItem);dropOwned=adapter.GetQuantity(potion.ItemBase);var other=((System.Collections.Generic.List<InventoryItemSnapshot>)Get(bag,"filteredItems")).FindIndex(i=>i.ItemBase!=potion.ItemBase);Call(bag,"SelectItem",other,false);Click(ui,"quantity-confirm");Check(adapter.GetQuantity(potion.ItemBase)==dropOwned&&ui.Q<Label>("bag-action-feedback").text.Contains("đã thay đổi"),"stale quantity dialog cannot drop another selected item");
                var quick=Object.FindFirstObjectByType<GameplayHudQuickItemsView>();Check(quick!=null,"existing gameplay quick item view is available");
                string old=adapter.GetQuickItemId(0);int owned=adapter.GetQuantity(potion.ItemBase);saves=0;
                Call(quick,"ShowAssignment",new InventoryActionRequest(InventoryActionType.AssignQuickSlot,potion,false,0));Check(quick.Assign(0),"R assignment commits immediately");Check(!quick.IsAssignmentOpen&&adapter.GetQuickItemId(0)==potion.ItemId&&adapter.GetQuantity(potion.ItemBase)==owned&&saves==1,"R commits one save, closes, consumes nothing");
                await Task.Delay(200);string beforeFail=adapter.GetQuickItemId(1);BagVerification.Set(save,"saveBlockedReason","QA deliberately blocked save on E");Call(quick,"ShowAssignment",new InventoryActionRequest(InventoryActionType.AssignQuickSlot,potion,false,0));Check(!quick.Assign(1)&&quick.IsAssignmentOpen&&adapter.GetQuickItemId(1)==beforeFail,"failed T save rolls back binding and keeps popup");quick.TryCloseAssignment();BagVerification.Set(save,"saveBlockedReason","");
                var heal=bag.GetComponent<PetHealPanelController>();var provider=Object.FindFirstObjectByType<PetCommandHudProvider>();var pet=provider.GetSelectedPetController();var stats=pet.GetComponentInChildren<PetHudRuntimeStats>(true);var statsSave=stats.CreateSaveData();
                try{
                    stats.SetStatus(1,stats.MaxHealth,1,stats.MaxEnergy);heal.TryOpenFromInventory(potion);Check(ReferenceEquals(Get(heal,"targetPet"),pet)&&ReferenceEquals(Get(heal,"exactHealingItem"),potion.ItemBase),"BAG recovery targets active pet with exact chosen ItemBase");
                    owned=adapter.GetQuantity(potion.ItemBase);saves=0;Click(ui,"bag-heal-confirm");Check(adapter.GetQuantity(potion.ItemBase)==owned-1&&saves==1,"recovery consumes exactly one and saves once");heal.Close();
                    stats.SetStatus(1,stats.MaxHealth,1,stats.MaxEnergy);owned=adapter.GetQuantity(potion.ItemBase);float hp=stats.Health;BagVerification.Set(save,"saveBlockedReason","QA deliberate recovery save failure");await Task.Delay(200);heal.TryOpenFromInventory(potion);Click(ui,"bag-heal-confirm");Check(stats.Health==hp&&adapter.GetQuantity(potion.ItemBase)==owned,"recovery save failure restores HP and quantity");heal.Close();BagVerification.Set(save,"saveBlockedReason","");
                    stats.SetStatus(stats.MaxHealth,stats.MaxHealth,stats.MaxEnergy,stats.MaxEnergy);heal.TryOpenFromInventory(potion);Check(!ui.Q<BagButton>("bag-heal-confirm").enabledInHierarchy,"full resource recovery is disabled with service reason");heal.Close();
                    Check(ui.Q("heal-pet-name").GetFirstAncestorOfType<AngularSurface>()!=null,"closing BAG heal restores original Companion binding tree");
                }finally{stats.RestoreFromSaveData(statsSave);heal.Close();}
                // Long/missing art presentation uses a transient definition, never writes project assets.
                var longItem=MonsterItemDefinition.CreateRuntime("Bình hồi phục dành cho thú đồng hành với tên tiếng Việt rất dài để kiểm tra vùng hiển thị",GameItemCategory.Material,string.Concat(Enumerable.Repeat("Mô tả dài có dấu. ",35)),"Hiệu ứng dài có nhiều dòng.",null,true,999999);
                try{adapter.TryAddItemAtomic(longItem,123456);search.value="dành cho";Call(bag,"SelectItem",0,false);await BagVerification.Shot(ui,folder,"10-long-missing-icon");Check(ui.Q<ScrollView>("detail-panel").verticalScrollerVisibility!=ScrollerVisibility.Hidden,"long inspector has scroll independent of actions");}finally{adapter.RestoreFromSaveData(inventory,out _);Object.Destroy(longItem);search.value="";}
            }finally{save.SaveCompleted-=saved;BagVerification.Set(save,"saveBlockedReason","");adapter.RestoreFromSaveData(inventory,out _);search.value="";sort.value=sort.choices[0];filter.value=filter.choices[0];}
        }
        public static async Task Profile(MonsterInventoryController bag,string folder){
            using(var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1))using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1)){
                var rows=new System.Collections.Generic.List<string>{"editor_frame,cpu_ns,gc_bytes"};int last=-1,warm=0;double until=Time.realtimeSinceStartupAsDouble+8;
                while(rows.Count<=120&&Time.realtimeSinceStartupAsDouble<until){await Task.Delay(8);if(Time.frameCount==last)continue;last=Time.frameCount;if(warm++<30)continue;rows.Add(last+","+(cpu.Valid?cpu.LastValue:-1)+","+(gc.Valid?gc.LastValue:-1));}
                File.WriteAllLines(folder+"/editor-profiler.csv",rows);Check(cpu.Valid&&gc.Valid&&rows.Count>30,"actual Editor ProfilerRecorder samples="+(rows.Count-1)+"; no standalone claim");
            }
        }
    }
}
