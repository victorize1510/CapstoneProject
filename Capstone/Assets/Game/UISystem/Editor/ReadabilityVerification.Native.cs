using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.Inventory.Bag.Editor;
using Capstone.Game.ProfileSystem;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    public static partial class ReadabilityVerification {
        static async Task ContrastChecks(GameMenuController menu,MonsterInventoryController bag,VisualElement ui,UnityEditor.EditorWindow view,System.Reflection.PropertyInfo size){
            menu.OpenPetsDirect();await Task.Delay(220);BagVerification.Click(ui,"pets-rename-button");await Task.Delay(200);var input=ui.Q<TextField>("pets-rename-input");input.value="";await Task.Delay(160);Check(!ui.Q<Button>("pets-rename-save").enabledInHierarchy,"empty pet rename rejected in actual popup");BagVerification.Click(ui,"pets-rename-cancel");bag.Close();menu.CloseMenu();
            var lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None);var original=lights.Select(l=>l.intensity).ToArray();float ambient=RenderSettings.ambientIntensity;
            try{foreach(var res in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){TrainerMenuPolishCapture.SelectSize(view,size,res);await Task.Delay(200);foreach(string condition in new[]{"bright","dark"}){RenderSettings.ambientIntensity=condition=="bright"?1.5f:.06f;foreach(var l in lights)l.intensity=condition=="bright"?2f:.03f;await Shot("contrast","hud-"+condition);checks.Add("CAPTURE actual HUD "+condition+" scene at "+res);}}}
            finally{for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].intensity=original[i];RenderSettings.ambientIntensity=ambient;}
        }
        static async Task NativeChecks(GameMenuController menu,MonsterInventoryController bag,ProfilePanelController profile,VisualElement ui) {
            var events=new List<string>{"utc,event,target,local_x,local_y"};double deadline=Time.realtimeSinceStartupAsDouble+180;
            EventCallback<PointerDownEvent> down=e=>{var t=e.target as VisualElement;var b=t as Button??t?.GetFirstAncestorOfType<Button>();events.Add(FormattableString.Invariant($"{DateTime.UtcNow:O},PointerDown,{b?.name??t?.name},{e.localPosition.x},{e.localPosition.y}"));File.WriteAllLines(Root+"native-input.csv",events);};
            EventCallback<KeyDownEvent> key=e=>{events.Add($"{DateTime.UtcNow:O},KeyDown,{e.keyCode},{(e.target as VisualElement)?.name},{(ui.focusController.focusedElement as VisualElement)?.name}");File.WriteAllLines(Root+"native-input.csv",events);};
            ui.RegisterCallback(down,TrickleDown.TrickleDown);ui.RegisterCallback(key,TrickleDown.TrickleDown);
            async Task Wait(Func<bool> condition,string label){while(!condition()&&Time.realtimeSinceStartupAsDouble<deadline)await Task.Delay(100);Check(condition(),"native "+label);}
            void Stage(string label,VisualElement target=null){File.WriteAllText(Root+"native-stage.txt",label+(target==null?"":FormattableString.Invariant($"\nbutton={target.name} world={target.worldBound} panel={ui.panel.visualTree.worldBound} screen={Screen.width}x{Screen.height}")));}
            try {
                var party=Object.FindFirstObjectByType<PetPartyPanelController>();menu.OpenPetsDirect();await Task.Delay(250);BagVerification.Click(ui,"pets-tab-growth");await Task.Delay(250);
                var level=ui.Q<Button>("pets-growth-level");ui.Q<ScrollView>("pets-tab-body-growth").ScrollTo(level);await Task.Delay(200);Stage("CLICK_GROWTH_BLANK",level);
                var levelPopup=Object.FindFirstObjectByType<PetLevelUpPanelController>();await Wait(()=>levelPopup.IsOpen,"blank growth click opens level preview");Stage("ESCAPE_POPUP");await Wait(()=>!levelPopup.IsOpen,"Escape closes preview");
                BagVerification.Click(ui,"pets-tab-overview");await Task.Delay(250);var toggle=ui.Q<Button>("pets-mode-toggle");Stage("CLICK_MODE",toggle);await Wait(()=>party.IsPreview3D&&party.HasPreviewTexture,"mouse enables 3D");
                toggle.Focus();Stage("ENTER_MODE",toggle);await Wait(()=>!party.IsPreview3D&&!party.HasPreviewTexture,"Enter returns 2D and releases preview");
                toggle.Focus();Stage("SPACE_MODE",toggle);await Wait(()=>party.IsPreview3D,"Space enables 3D");Stage("SPACE_MODE_BACK",toggle);await Wait(()=>!party.IsPreview3D&&!party.HasPreviewTexture,"Space returns 2D");
                bag.Close();menu.OpenInventoryDirect();await Task.Delay(250);Stage("SEARCH_TYPE",ui.Q("inventory-search-field"));var search=ui.Q<TextField>("inventory-search-field");await Wait(()=>!string.IsNullOrEmpty(search.value),"native typing reaches search");Stage("SEARCH_CLEAR",ui.Q("inventory-search-field-clear"));await Wait(()=>search.value=="","native X clears search");Check(search.focusController.focusedElement==search||search.Contains(search.focusController.focusedElement as VisualElement),"search focus restored after X");
                Stage("BAG_BACK",ui.Q("cancel-button"));await Wait(()=>!bag.IsOpen,"native Bag back closes once");
                Check(events.Any(e=>e.Contains("PointerDown,pets-growth-level")),"real pointer growth event recorded");Check(events.Any(e=>e.Contains("KeyDown,Space")),"real Space event recorded");Stage("COMPLETE");
            } finally {ui.UnregisterCallback(down,TrickleDown.TrickleDown);ui.UnregisterCallback(key,TrickleDown.TrickleDown);File.WriteAllLines(Root+"native-input.csv",events);}
        }
    }
}
