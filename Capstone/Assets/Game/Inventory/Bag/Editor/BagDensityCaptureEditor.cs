using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;
namespace Capstone.Game.Inventory.Bag.Editor {
    public static class BagDensityCaptureEditor {
        const string Root=BagDensityCapture.Root,Session="Capstone.BagDensityCapture";
        static bool running;
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
        static void Poll(){
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Root+"request.txt"))return;
            File.Delete(Root+"request.txt");File.WriteAllText(Root+"isolate.flag","capture");SessionState.SetBool(Session,true);
            SessionState.SetBool(Session+"enabled",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+"options",(int)EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange s){
            if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Session,false)&&!running)Capture();
            if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Session+"restore",false)){
                SessionState.SetBool(Session+"restore",false);EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+"enabled",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+"options",0);
            }
        }
        static async void Capture(){
            running=true;var log=new List<string>{"Actual Editor Play Mode: smaller BAG item cards."};var rows=new List<string>{"resolution,mode,card,width,height,icon_width,icon_height"};
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var index=view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);int old=(int)index.GetValue(view);bool maximized=view.maximized;MonsterInventoryController bag=null;
            try{
                await Task.Delay(1400);bag=Object.FindFirstObjectByType<MonsterInventoryController>();var ui=bag.GetComponent<UIDocument>().rootVisualElement;
                Object.FindFirstObjectByType<GameMenuController>()?.CloseMenu();view.maximized=true;
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,index,size);view.Focus();await Task.Delay(400);bag.OpenInventoryPanel();if(bag.SelectedItem==null)bag.SelectNextItem();
                    foreach(string mode in new[]{"grid","list"}){
                        await BagVerification.Shot(ui,Root,mode);
                        foreach(var card in ui.Q("item-grid").Children())if(card.resolvedStyle.display!=DisplayStyle.None){var icon=card.Q("card-icon");rows.Add(FormattableString.Invariant($"{size.x}x{size.y},{mode},{card.name},{card.layout.width},{card.layout.height},{icon.layout.width},{icon.layout.height}"));}
                        BagVerification.Click(ui,"inventory-view-button");await Task.Delay(180);
                    }
                }
                log.Add("COMPLETE: two modes at both target resolutions; actual card layout bounds in sizes.csv. No inventory action or real-save writes.");
            }catch(Exception e){log.Add("FAILED "+e);Debug.LogException(e);}finally{
                bag?.Close();index.SetValue(view,old);view.maximized=maximized;File.WriteAllLines(Root+"runtime.log",log);File.WriteAllLines(Root+"sizes.csv",rows);
                SessionState.SetBool(Session,false);SessionState.SetBool(Session+"restore",true);running=false;if(File.Exists(Root+"isolate.flag"))File.Delete(Root+"isolate.flag");EditorApplication.ExitPlaymode();
            }
        }
    }
}
