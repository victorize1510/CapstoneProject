#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Capstone.Game.HudSystem.Editor {
    /// <summary>Deterministic edge regression through runtime handlers; never grants items to real storage.</summary>
    public sealed class HudHoldReleaseVerification:MonoBehaviour {
        const string Root=QaArtifactPaths.Root+"hud-hold-release-2026-11-05/",Session="Capstone.HoldRelease.QA";
        const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        static readonly List<string> checks=new List<string>();
        static void Write(string file,string text){Directory.CreateDirectory(Root);File.WriteAllText(Root+file,text);File.AppendAllText(Root+"owned-paths.txt",Root+file+"\n");}
        static void Check(bool pass,string label){checks.Add((pass?"PASS ":"FAIL ")+label);Write("checks.txt",string.Join("\n",checks));if(!pass)throw new Exception(label);}
        static void Set(object o,string key,object value)=>o.GetType().GetField(key,Any).SetValue(o,value);
        static object Call(object o,string key,params object[] args)=>o.GetType().GetMethod(key,Any).Invoke(o,args);
        [MenuItem("Game Tools/GameToolThang/HUD/R11/Verify hold release regression")]
        public static void Run(){
            if(EditorApplication.isPlayingOrWillChangePlaymode){Write("status.txt","NOT RUN: stop current Play Mode before isolated verification.");return;}
            SessionState.SetString(Session+"save",Root+"test-saves/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));SessionState.SetBool(Session,true);SessionState.SetBool(Session+"enabled",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+"options",(int)EditorSettings.enterPlayModeOptions);EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(!SessionState.GetBool(Session,false))return;
            foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new PlayerJsonFileSaveStore(SessionState.GetString(Session+"save","")));
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(SessionState.GetString(Session+"save","")));
            new GameObject("Hold release verification",typeof(HudHoldReleaseVerification)){hideFlags=HideFlags.DontSave};
        }
        static async Task Gap(int ms=120)=>await Task.Delay(ms);
        static void PressParty(GameplayHudController hud){Set(hud,"partyKeyFrame",-1);Check(hud.HandlePartyKey(hud.PartyKey),"P down accepted by HUD owner");}
        async void Start(){checks.Clear();Write("status.txt","RUNNING "+DateTime.Now);EditorWindow view=null;PropertyInfo size=null;int originalSize=-1;
            try {
                await Gap(1500);var hud=Object.FindFirstObjectByType<GameplayHudController>();var save=Object.FindFirstObjectByType<PlayerSaveController>();var inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();var camera=Object.FindFirstObjectByType<BasicCameraFollow>();
                Check(hud!=null&&hud.Radial!=null,"existing R11 HUD available");Check(save.SavePath.StartsWith(Root),"save isolated on E before gameplay Start");
                var capture=hud.GetComponent<HubCaptureHud>();Set(camera,"lockedEnemy",null);var ball=CaptureAssets.LoadBall();inventory.TryAddItemAtomic(ball,1);capture.Refresh();
                var gesture=new HubPressGesture();Check(gesture.Step(true,true,false,0,.28f)==HubPressGesture.Intent.None,"G starts pending");Check(gesture.Step(false,true,false,.3f,.28f)==HubPressGesture.Intent.Hold,"G threshold emits hold");Check(gesture.Step(false,false,true,.4f,.28f)==HubPressGesture.Intent.ReleaseHold,"G up emits close intent");Check(gesture.Step(false,false,true,.5f,.28f)==HubPressGesture.Intent.None,"duplicate release emits nothing");
                gesture=new HubPressGesture();gesture.Step(true,true,false,0,.28f);Check(gesture.Step(false,false,true,.6f,.28f)==HubPressGesture.Intent.ReleaseHold,"long frame ending at key-up cannot become tap");
                gesture=new HubPressGesture();gesture.Step(true,true,false,0,.28f);gesture.Step(false,true,false,.3f,.28f);Check(gesture.Step(false,false,false,.5f,.28f)==HubPressGesture.Intent.ReleaseHold,"lost key-up still closes hold");
                gesture=new HubPressGesture();gesture.Step(true,true,false,0,.28f);Check(gesture.Step(false,false,false,.1f,.28f)==HubPressGesture.Intent.None,"missing short key-up never throws");
                var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");view=EditorWindow.GetWindow(type);size=type.GetProperty("selectedSizeIndex",Any);originalSize=(int)size.GetValue(view);view.Show();
                foreach(var resolution in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    var select=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture")).First(t=>t!=null);select.GetMethod("SelectSize",Any).Invoke(null,new object[]{view,size,resolution});await Gap(350);Check(Screen.width==resolution.x&&Screen.height==resolution.y,"runtime resolution "+resolution);
                    foreach(var mode in new[]{PartyPickerMode.Toggle,PartyPickerMode.Hold}){
                        Set(hud,"partyPickerMode",mode);await Gap();Call(hud,"CancelTransientHud");PressParty(hud);Check(hud.IsPartyOpen,"P opens while down "+mode);float down=(float)typeof(GameplayHudController).GetField("partyPressedAt",Any).GetValue(hud);hud.ProcessPartyRelease(true,false,down+.5f);Check(hud.IsPartyOpen,"P remains open while held "+mode);hud.ProcessPartyRelease(false,true,down+.6f);Check(!hud.IsPartyOpen,"P release closes after hold "+mode);hud.ProcessPartyRelease(false,true,down+.7f);Check(!hud.IsPartyOpen,"P duplicate up cannot reopen "+mode);
                        PressParty(hud);down=(float)typeof(GameplayHudController).GetField("partyPressedAt",Any).GetValue(hud);hud.ProcessPartyRelease(false,false,down+.6f);Check(!hud.IsPartyOpen,"P lost key-up closes "+mode);
                    }
                    Set(hud,"partyPickerMode",PartyPickerMode.Toggle);await Gap();Call(hud,"CancelTransientHud");PressParty(hud);float stamp=(float)typeof(GameplayHudController).GetField("partyPressedAt",Any).GetValue(hud);hud.ProcessPartyRelease(false,true,stamp+.05f);Check(hud.IsPartyOpen,"P short tap retains explicit Toggle mode");PressParty(hud);stamp=(float)typeof(GameplayHudController).GetField("partyPressedAt",Any).GetValue(hud);hud.ProcessPartyRelease(false,true,stamp+.05f);Check(!hud.IsPartyOpen,"P second short tap closes Toggle");
                    PressParty(hud);Check(hud.TryCloseTransientHud(),"Escape closes held P");hud.ProcessPartyRelease(false,true,Time.unscaledTime+1);Check(!hud.IsPartyOpen,"release after Escape cannot reopen P");
                    int intents=capture.ThrowIntents,quantity=inventory.GetQuantity(ball);capture.ProcessCaptureKey(false,false,false,10);capture.ProcessCaptureKey(true,true,false,11);capture.ProcessCaptureKey(false,true,false,11.4f);Check(capture.IsPickerOpen,"G hold opens real picker");capture.ProcessCaptureKey(false,false,true,11.5f);Check(!capture.IsPickerOpen,"G release closes real picker immediately");Call(hud,"SyncVisibility");Check(hud.PresentationRoot.gameObject.activeInHierarchy,"HUD does not blink during delayed input release");await Gap();Check(!InventoryInputController.GameplayInputBlocked,"G release restores input lease");Check(capture.ThrowIntents==intents&&inventory.GetQuantity(ball)==quantity,"G hold/release neither throws nor consumes");
                    capture.ProcessCaptureKey(false,false,false,12);capture.ProcessCaptureKey(true,true,false,13);capture.ProcessCaptureKey(false,true,false,13.4f);Check(capture.IsPickerOpen&&capture.SelectBall(ball.StableId),"select ball while G held");capture.ProcessCaptureKey(false,true,false,14);Check(!capture.IsPickerOpen,"selection suppresses reopening while G stays down");capture.ProcessCaptureKey(false,false,true,14.1f);await Gap();Check(capture.ThrowIntents==intents&&inventory.GetQuantity(ball)==quantity,"release after selection never throws");
                    capture.ProcessCaptureKey(false,false,false,15);capture.ProcessCaptureKey(true,true,false,16);capture.ProcessCaptureKey(false,true,false,16.4f);capture.ProcessCaptureKey(false,false,false,16.5f);Check(!capture.IsPickerOpen,"G missing up closes actual picker");await Gap();
                    capture.ProcessCaptureKey(false,false,false,17);capture.ProcessCaptureKey(true,true,false,18);capture.ProcessCaptureKey(false,false,true,18.1f);Check(capture.ThrowIntents==intents+1,"G short tap still sends exactly one throw intent");capture.ProcessCaptureKey(false,false,true,18.2f);Check(capture.ThrowIntents==intents+1&&inventory.GetQuantity(ball)==quantity,"duplicate release cannot throw or consume on invalid target");
                }
                Write("status.txt","COMPLETE "+checks.Count+" checks; actual Editor runtime handlers and injected held/up edges at both resolutions. Physical held key not claimed. No screenshots/builds generated.");
            }catch(Exception e){Write("status.txt","FAILED "+e);Debug.LogException(e);}
            finally{GameplayHudController.Live?.TryCloseTransientHud();if(view!=null&&originalSize>=0)size.SetValue(view,originalSize);SessionState.SetBool(Session,false);EditorApplication.ExitPlaymode();EditorApplication.delayCall+=()=>{EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+"enabled",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+"options",0);};}
        }
    }
}
#endif
