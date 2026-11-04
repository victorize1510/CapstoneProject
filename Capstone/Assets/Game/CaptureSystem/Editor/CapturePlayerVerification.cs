using System;
using System.IO;
using System.Text;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;
namespace Capstone.Game.CaptureSystem.Editor {
    public static class CapturePlayerVerification {
        const string Root=QaArtifactPaths.Root+"capture-stats-visibility-2026-11-04/";
        [MenuItem("Tools/Capstone/Capture/Verify release player scripts")]
        public static void Verify(){
            Directory.CreateDirectory(Root);
            File.WriteAllText(Root+"player-status.txt","RUNNING "+DateTime.Now);
            try{
                if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))throw new InvalidOperationException("Windows player module is unavailable.");
                string folder=Root+"player-scripts";Directory.CreateDirectory(folder);var timer=System.Diagnostics.Stopwatch.StartNew();
                PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings{group=BuildTargetGroup.Standalone,target=BuildTarget.StandaloneWindows64,options=ScriptCompilationOptions.None},folder);
                timer.Stop();var assemblies=Directory.GetFiles(folder,"Assembly-CSharp.dll",SearchOption.AllDirectories);if(assemblies.Length!=1)throw new InvalidOperationException("Player assembly not produced.");
                string metadata=Encoding.UTF8.GetString(File.ReadAllBytes(assemblies[0]));
                bool gameplay=metadata.Contains("CaptureCalculator")&&metadata.Contains("WildPetCaptureTarget")&&metadata.Contains("PetCaptureCoordinator");
                bool editorAbsent=!metadata.Contains("CaptureVerification")&&!metadata.Contains("CaptureAuthoringSetup")&&!metadata.Contains("CapturePlayerVerification");
                File.WriteAllText(Root+"player-compile.json",JsonUtility.ToJson(new Result{seconds=timer.Elapsed.TotalSeconds,gameplayTypesPresent=gameplay,editorVerificationTypesAbsent=editorAbsent,developmentBuild=false,standaloneExecuted=false},true));
                File.WriteAllLines(Root+"player-output-ledger.txt",Directory.GetFiles(folder,"*",SearchOption.AllDirectories));
                if(!gameplay||!editorAbsent)throw new InvalidOperationException("Release type-boundary check failed.");
                File.WriteAllText(Root+"player-status.txt","COMPLETE release Windows player scripts; no standalone executable was run.");
            }catch(Exception e){File.WriteAllText(Root+"player-status.txt","UNVERIFIED "+e);Debug.LogException(e);}
        }
        [Serializable]sealed class Result{public double seconds;public bool gameplayTypesPresent,editorVerificationTypesAbsent,developmentBuild,standaloneExecuted;}
    }
}

