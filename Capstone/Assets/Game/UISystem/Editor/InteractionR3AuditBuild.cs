using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.UISystem.Editor {
    // Opt-in verification tooling. Does not modify the production scene or build settings.
    static class InteractionR3AuditBuild {
        const string Request="Temp/R3AuditBuild.request";
        static bool running;
        [InitializeOnLoadMethod] static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll(){
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            string request=File.ReadAllText(Request).Trim();string stage=request=="before"?"before":request=="mono"?"after-mono":"after";File.Delete(Request);running=true;
            string output=Path.GetFullPath(Capstone.Game.UISystem.QaArtifactPaths.Root + "ui-polish-2026-10-22/builds/"+stage);Directory.CreateDirectory(output);
            var scene=SceneManager.GetActiveScene();string source=scene.path;bool dirty=scene.isDirty;
            var target=UnityEditor.Build.NamedBuildTarget.Standalone;var backend=PlayerSettings.GetScriptingBackend(target);
            try {
                if(scene.name!="TestChar")throw new Exception("Expected TestChar; refusing to replace the user's current scene: "+source);
                if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))throw new Exception("Windows x64 build support unavailable");
                const string snapshot="Assets/R3AuditSnapshot/TestCharAudit.unity";
                Directory.CreateDirectory("Assets/R3AuditSnapshot");
                if(!EditorSceneManager.SaveScene(scene,snapshot,true))throw new Exception("Cannot save verification scene copy");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                File.WriteAllText(output+"/build-start.txt",DateTime.UtcNow.ToString("O")+" source="+source+" dirty="+dirty+" snapshot="+snapshot+"; original scene untouched\n");
                if(request=="mono")PlayerSettings.SetScriptingBackend(target,ScriptingImplementation.Mono2x);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{snapshot},locationPathName=output+"/standalone/R3Audit.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                var summary=report.summary;
                File.WriteAllText(output+"/build-result.txt",$"{summary.result}\nUTC={DateTime.UtcNow:O}\nplatform={summary.platform}\nbytes={summary.totalSize}\nduration={summary.totalTime}\nerrors={summary.totalErrors}\nwarnings={summary.totalWarnings}\nsource={source}\ndirtyBefore={dirty}\nsourceAfter={scene.path}\ndirtyAfter={scene.isDirty}\n"+string.Join("\n",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.content)));
            }catch(Exception e){File.WriteAllText(output+"/build-error.txt",e.ToString());Debug.LogException(e);}finally{
                if(PlayerSettings.GetScriptingBackend(target)!=backend)PlayerSettings.SetScriptingBackend(target,backend);
                File.WriteAllText(output+"/backend-restored.txt",DateTime.UtcNow.ToString("O")+" backend="+PlayerSettings.GetScriptingBackend(target)+"; verify serialized ProjectSettings separately");
                running=false;
            }
        }
    }
    static class InteractionR3AuditEditor {
        const string Request="Temp/R3AuditEditor.request",Own="R3AuditEditor.Own";
        static bool configured;
        static readonly string Root=Path.GetFullPath(Capstone.Game.UISystem.QaArtifactPaths.Root + "ui-r3-fixes-2026-10-20/editor");
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll(){
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(SessionState.GetBool(Own,false)&&!EditorApplication.isPlayingOrWillChangePlaymode){
                SessionState.SetBool(Own,false);
                foreach(string flag in new[]{"Temp/R3Audit.run","Temp/R3Audit.manual","Temp/R3Audit.allocations"})if(File.Exists(flag))File.Delete(flag);
            }
            if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode){
                string mode=File.ReadAllText(Request).Trim();File.Delete(Request);File.WriteAllText("Temp/R3Audit.run","audit");
                if(mode=="manual")File.WriteAllText("Temp/R3Audit.manual","manual");else if(File.Exists("Temp/R3Audit.manual"))File.Delete("Temp/R3Audit.manual");
                if(mode=="allocations")File.WriteAllText("Temp/R3Audit.allocations","probe");else if(File.Exists("Temp/R3Audit.allocations"))File.Delete("Temp/R3Audit.allocations");
                foreach(string old in new[]{"checks.txt","stop"})if(File.Exists(Root+"/"+old))File.Delete(Root+"/"+old);
                SessionState.SetBool(Own,true);configured=false;EditorApplication.EnterPlaymode();return;
            }
            if(!SessionState.GetBool(Own,false))return;
            if(!EditorApplication.isPlaying)return;
            if(!configured){var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);TrainerMenuPolishCapture.SelectSize(view,selected,new Vector2Int(1920,1080));view.maximized=true;view.Focus();configured=true;}
            if(File.Exists(Root+"/stop")||(File.Exists(Root+"/checks.txt")&&File.ReadAllText(Root+"/checks.txt").Contains("COMPLETE"))){
                SessionState.SetBool(Own,false);foreach(string flag in new[]{"Temp/R3Audit.run","Temp/R3Audit.manual","Temp/R3Audit.allocations"})if(File.Exists(flag))File.Delete(flag);
                var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.maximized=false;EditorApplication.ExitPlaymode();
            }
        }
    }
}
