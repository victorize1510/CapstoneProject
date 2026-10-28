using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem.Editor {
    // Compare Unity's resolved layout/styles, including template inheritance, before
    // and after stylesheet migration. This window never touches scene or save data.
    public sealed class UIStyleReviewWindow : EditorWindow {
        const string Folder = "Assets/ThangTest/UIThang/Inventory/UI/";
        const string ArchivedFolder = "Assets/Editor/UIStyleBaselineTemp/";
        const string Output = Capstone.Game.UISystem.QaArtifactPaths.Root + "ui-style-audit/resolved/";
        static readonly string[] Documents = { "MonsterInventory", "InventoryPanel", "PetsPanel", "BoxPanel", "QuestPanel", "SkillsPanel", "PetDetailsPanel", "PetLevelUpPanel", "PetHealPanel", "PetEvolutionPanel", "PetReleasePanel" };
        static readonly Vector2[] Viewports = { new Vector2(1920,1080), new Vector2(1366,768) };
        static bool running;

        [Serializable] public sealed class Capture { public List<Entry> entries = new List<Entry>(); }
        [Serializable] public sealed class Entry { public string key; public string value; }

        [MenuItem("Game Tools/GameToolThang/HUD/Capture UI Style Baseline")]
        static void Baseline() {
            // A baseline is immutable once recorded. Archived source snapshots are
            // used to recover the original after Unity clears Temp on restart.
            if (File.Exists(Output + "baseline.json")) {
                Debug.LogWarning("Baseline already exists; capture refused to preserve the original comparison.");
                return;
            }
            Run(true);
        }
        [MenuItem("Game Tools/GameToolThang/HUD/Validate UI Style Review")]
        static void Validate() => Run(false);

        static async void Run(bool baseline) {
            if (running) return;
            if (!baseline && !File.Exists(Output + "baseline.json")) {
                Debug.LogWarning("Missing style baseline. Capture the archived original before validation.");
                return;
            }
            running = true;
            var window = GetWindow<UIStyleReviewWindow>(true,"UI Style Review",true);
            window.position = new Rect(60,80,1366,700);
            try {
                var capture = new Capture();
                foreach (var viewport in Viewports) foreach (string document in Documents) {
                    string sourceFolder = baseline && Directory.Exists(ArchivedFolder) ? ArchivedFolder : Folder;
                    var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(sourceFolder+document+".uxml");
                    if (asset == null) throw new InvalidOperationException("Missing document: "+document);
                    window.rootVisualElement.Clear();
                    var surface = new VisualElement();
                    surface.style.width = viewport.x; surface.style.height = viewport.y;
                    surface.style.flexShrink = 0;
                    window.rootVisualElement.Add(surface);
                    asset.CloneTree(surface);
                    await Task.Delay(180);
                    if (window == null) throw new InvalidOperationException("Style review window closed during capture.");
                    CaptureTree(surface, document+"/"+viewport, capture);
                }
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output+(baseline?"baseline":"current")+".json",JsonUtility.ToJson(capture,true));
                if (baseline) Debug.Log("UI style baseline captured: "+capture.entries.Count+" resolved elements at 1920x1080 and 1366x768.");
                else {
                    var previous = JsonUtility.FromJson<Capture>(File.ReadAllText(Output+"baseline.json"));
                    var lookup = new Dictionary<string,string>();
                    foreach (var entry in previous.entries) lookup[entry.key] = entry.value;
                    var changes = new List<string>();
                    foreach (var entry in capture.entries)
                        if (!lookup.TryGetValue(entry.key,out string value) || value != entry.value) changes.Add(entry.key+"\nBEFORE "+value+"\nAFTER  "+entry.value);
                    var currentKeys = new HashSet<string>();
                    foreach (var entry in capture.entries) currentKeys.Add(entry.key);
                    foreach (var entry in previous.entries)
                        if (!currentKeys.Contains(entry.key)) changes.Add(entry.key + "\nREMOVED");
                    File.WriteAllLines(Output+"changes.txt",changes);
                    Debug.Log("UI style review: "+capture.entries.Count+" elements, "+changes.Count+" changes. Inspect "+Output+"changes.txt for intended changes.");
                }
            } catch (Exception error) { Debug.LogException(error); }
            finally { running=false; }
        }

        static void CaptureTree(VisualElement element,string path,Capture capture) {
            var s=element.resolvedStyle;
            string F(float v)=>float.IsNaN(v)?"auto":v.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
            string C(Color v)=>ColorUtility.ToHtmlStringRGBA(v);
            capture.entries.Add(new Entry {key=path+":"+element.GetType().Name+":"+element.name,value=
                string.Join("|",F(element.layout.x),F(element.layout.y),F(s.width),F(s.height),F(s.flexGrow),F(s.flexShrink),s.display.ToString(),s.position.ToString(),C(s.color),C(s.backgroundColor),F(s.opacity),F(s.fontSize),s.unityFontStyleAndWeight.ToString(),s.unityTextAlign.ToString(),s.whiteSpace.ToString(),
                    F(s.marginLeft),F(s.marginTop),F(s.marginRight),F(s.marginBottom),F(s.paddingLeft),F(s.paddingTop),F(s.paddingRight),F(s.paddingBottom),F(s.borderLeftWidth),F(s.borderTopWidth),F(s.borderRightWidth),F(s.borderBottomWidth),C(s.borderLeftColor),C(s.borderTopColor),C(s.borderRightColor),C(s.borderBottomColor))});
            for(int i=0;i<element.hierarchy.childCount;i++) CaptureTree(element.hierarchy[i],path+"/"+i,capture);
        }
    }
}
