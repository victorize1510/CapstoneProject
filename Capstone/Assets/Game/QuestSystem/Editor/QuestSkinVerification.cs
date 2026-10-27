using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.Inventory;
using Capstone.Game.Inventory.Bag.Editor;
using Capstone.Game.QuestSystem.UI;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;
namespace Capstone.Game.QuestSystem.Editor {
    public static partial class QuestSkinVerification {
        const string Root=QuestSkinQaIsolation.Root,Session="Capstone.QuestSkin.QA";
        static readonly BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static bool running;
        static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Any).Invoke(owner,args);
        static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Any).SetValue(owner,value);
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
        static void Poll(){if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.Exists(Root+"refresh.request")){File.Delete(Root+"refresh.request");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);return;}
            if(!File.Exists(Root+"request.txt"))return;var phase=File.ReadAllText(Root+"request.txt").Trim();File.Delete(Root+"request.txt");SessionState.SetString(Session+"phase",phase);QuestSkinAssetSetup.Configure();
            File.WriteAllText(Root+"isolate.flag","Quest skin QA");SessionState.SetBool(Session,true);SessionState.SetBool(Session+"enabled",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+"options",(int)EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;EditorApplication.EnterPlaymode();}
        static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Session,false)&&!running)Run();
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Session+"restore",false)){SessionState.SetBool(Session+"restore",false);EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+"enabled",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+"options",0);}}
        static async void Run(){running=true;var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var size=view.GetType().GetProperty("selectedSizeIndex",Any);int old=(int)size.GetValue(view);bool max=view.maximized;GameMenuController menu=null;
            try{await Task.Delay(1500);menu=Object.FindFirstObjectByType<GameMenuController>();menu.OpenQuestDirect();var controller=Object.FindFirstObjectByType<QuestPanelController>();Set(controller,"completedSectionExpanded",true);var tab=controller.GetType().GetField("currentTab",Any).FieldType;Call(controller,"SelectTab",Enum.Parse(tab,"Main"));
                var ui=controller.GetComponent<UIDocument>().rootVisualElement;view.maximized=true;Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,size,new Vector2Int(1600,900));await Task.Delay(500);
                string phase=SessionState.GetString(Session+"phase","");
                if(phase=="final"){await VerifyFinal(controller,menu,view,size,ui);File.WriteAllText(Root+"status.txt","COMPLETE");}
                else if(phase=="native"){await Native(controller,ui);File.WriteAllText(Root+"native-status.txt","STOPPED");}
                else{await BagVerification.Shot(ui,Root,phase=="before"?"00-before":"01-final");File.WriteAllText(Root+"status.txt","CAPTURED "+phase);}
            }catch(Exception e){File.WriteAllText(Root+"status.txt","FAILED "+e);File.AppendAllText(Root+"checks.txt","FAILED "+e);Debug.LogException(e);}finally{menu?.CloseMenu();Object.FindFirstObjectByType<MonsterInventoryController>()?.Close();size.SetValue(view,old);view.maximized=max;running=false;SessionState.SetBool(Session,false);SessionState.SetBool(Session+"restore",true);if(File.Exists(Root+"isolate.flag"))File.Delete(Root+"isolate.flag");EditorApplication.ExitPlaymode();}}
    }
}
