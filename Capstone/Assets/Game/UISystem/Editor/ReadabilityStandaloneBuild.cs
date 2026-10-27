using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Capstone.Game.UISystem.Editor {
    static class ReadabilityStandaloneBuild {
        const string Root=ReadabilityQaIsolation.Root;static bool running;
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll(){
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Root+"build.request"))return;
            File.Delete(Root+"build.request");running=true;
            try{
                var scene=SceneManager.GetActiveScene();if(scene.name!="TestChar"||string.IsNullOrEmpty(scene.path))throw new Exception("Expected saved TestChar scene");
                if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))throw new Exception("Windows x64 support unavailable");
                Directory.CreateDirectory(Root+"build");File.WriteAllText(Root+"build-status.txt","BUILDING saved scene="+scene.path+"; dirty="+scene.isDirty+"; unsaved scene left untouched; backend="+PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scene.path},locationPathName=Root+"build/ReadabilityQA.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});var s=report.summary;
                File.WriteAllText(Root+"build-status.txt",$"{s.result}\nbytes={s.totalSize}\ntime={s.totalTime}\nerrors={s.totalErrors}\nwarnings={s.totalWarnings}\n"+string.Join("\n",report.steps.SelectMany(p=>p.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.content)));
            }catch(Exception e){File.WriteAllText(Root+"build-status.txt",e.ToString());}finally{running=false;}
        }
    }
}
