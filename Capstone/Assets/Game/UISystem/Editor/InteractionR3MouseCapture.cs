using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Capstone.Game.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Observes native mouse/keyboard events; does not synthesize UI input.</summary>
    static class InteractionR3MouseCapture {
        const string Request="Temp/R3MouseCapture.request";
        static bool running;
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll(){
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            if(File.ReadAllText(Request).Trim()=="import"){
                File.Delete(Request);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Game/UISystem/Resources/UI/InteractionR3"})){
                    var asset=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(asset) as TextureImporter;if(importer==null)continue;
                    importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
                    importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
                }
                Directory.CreateDirectory(InteractionR3SaveIsolation.Output);File.WriteAllText(Path.Combine(InteractionR3SaveIsolation.Output,"r3-import.txt"),"Imported loose R3 sprites "+DateTime.UtcNow.ToString("O"));return;
            }
            if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.EnterPlaymode();return;}
            if(Object.FindFirstObjectByType<GameMenuController>()==null)return;
            var stage=File.ReadAllText(Request).Trim();File.Delete(Request);Run(stage);
        }
        static async void Run(string stage){
            running=true;
            string path=Path.Combine(InteractionR3SaveIsolation.Output,stage=="before"?"before/mouse":"after/mouse");Directory.CreateDirectory(path);
            var events=new StringBuilder("utc,event,target,position\n");var metrics=new StringBuilder("frame,mouse_x,mouse_y,pressed,nav,label,x,y,width,height,font,font_size,weight\n");
            var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();var root=inventory.GetComponent<UIDocument>().rootVisualElement;
            string Name(IEventHandler target){var element=target as VisualElement;return element is Button?element.name:element?.GetFirstAncestorOfType<Button>()?.name??element?.name;}
            string lastHover=null;
            EventCallback<PointerMoveEvent> move=e=>{string name=Name(e.target);if(name!=lastHover){lastHover=name;events.AppendLine(DateTime.UtcNow.ToString("O")+",PointerMove,"+name+","+e.position.ToString().Replace(',', ';'));}};
            EventCallback<PointerDownEvent> down=e=>events.AppendLine(DateTime.UtcNow.ToString("O")+",PointerDown,"+Name(e.target)+","+e.position.ToString().Replace(',', ';'));
            EventCallback<PointerUpEvent> up=e=>events.AppendLine(DateTime.UtcNow.ToString("O")+",PointerUp,"+Name(e.target)+","+e.position.ToString().Replace(',', ';'));
            root.RegisterCallback(down,TrickleDown.TrickleDown);root.RegisterCallback(up,TrickleDown.TrickleDown);
            root.RegisterCallback(move,TrickleDown.TrickleDown);
            var menu=Object.FindFirstObjectByType<GameMenuController>();var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));bool max=view.maximized;
            try{
                var selected=view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                TrainerMenuPolishCapture.SelectSize(view,selected,new Vector2Int(1920,1080));view.maximized=true;view.Focus();await Task.Delay(800);menu.CloseMenu();menu.OpenPetsDirect();
                int frame=0;double end=EditorApplication.timeSinceStartup+600;
                while(EditorApplication.isPlaying&&EditorApplication.timeSinceStartup<end&&!File.Exists("Temp/R3MouseCapture.stop")){
                    var mouse=Mouse.current;var pos=mouse!=null?mouse.position.ReadValue():Vector2.zero;
                    foreach(string id in new[]{"overview","skills","growth"}){
                        var button=root.Q<UnityEngine.UIElements.Button>("pets-tab-"+id);var label=button?.Query<Label>().ToList().FirstOrDefault(l=>!string.IsNullOrEmpty(l.text));if(label==null)continue;
                        var r=label.worldBound;var s=label.resolvedStyle;var font=s.unityFontDefinition.font??s.unityFont;metrics.AppendLine(FormattableString.Invariant($"{Time.frameCount},{pos.x},{pos.y},{mouse?.leftButton.isPressed},{id},{label.text},{r.x},{r.y},{r.width},{r.height},{font?.name},{s.fontSize},{s.unityFontStyleAndWeight}"));
                    }
                    ScreenCapture.CaptureScreenshot(Path.Combine(path,$"frame-{frame++:D4}.png"));
                    File.WriteAllText(Path.Combine(path,"mouse-events.csv"),events.ToString());File.WriteAllText(Path.Combine(path,"text-metrics.csv"),metrics.ToString());
                    await Task.Delay(250);
                }
                File.WriteAllText(Path.Combine(path,"capture.txt"),"Actual Game view PNG sequence at nominal 4fps. Native input via Computer Use; not handler invocation. "+DateTime.UtcNow.ToString("O"));
            }catch(Exception e){File.WriteAllText(Path.Combine(path,"error.txt"),e.ToString());Debug.LogException(e);}
            finally{root.UnregisterCallback(down,TrickleDown.TrickleDown);root.UnregisterCallback(up,TrickleDown.TrickleDown);root.UnregisterCallback(move,TrickleDown.TrickleDown);if(File.Exists("Temp/R3MouseCapture.stop"))File.Delete("Temp/R3MouseCapture.stop");view.maximized=max;running=false;EditorApplication.ExitPlaymode();}
        }
    }
}
