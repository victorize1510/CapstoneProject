using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Capstone.Game.PetSummon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class GirlFinalPlayerValidation
{
    [MenuItem("Tools/ToolCuaThang/Capstone/Player/Validate GirlFinal %&v", priority = 2)]
    public static void Validate()
    {
        var results = new List<string>();
        void Require(bool valid, string label) {
            results.Add((valid ? "PASS " : "FAIL ") + label);
            if (!valid) throw new InvalidOperationException(label);
        }
        string report = Path.Combine(Application.dataPath, "../Temp/GirlFinal-validation.txt");
        try {
            var scene = SceneManager.GetSceneByPath(GirlFinalPlayerSetup.ScenePath);
            var player = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BasicPlayerMovement>(true)).Single();
            var root=player.transform;
            var switcher=root.GetComponent<PlayerVisualSwitcher>();
            Require(root.name=="Player" && root.parent==null && root.GetComponent<Animator>()==null,"independent Player root with no Animator");
            Require(root.Find("Armature")==null && root.Find("Vefects_Vexa")==null,"legacy Vexa branches removed");
            Require(root.Find("Boy")!=null && root.Find("GirlFinal")!=null,"Boy and GirlFinal are sibling models");
            Require(switcher.TryGetVisualProfile(0,out var girl) && girl.displayName=="GirlFinal","female profile points to GirlFinal");
            Require(switcher.TryGetVisualProfile(1,out var boy) && boy.displayName=="Boy","male profile retained");
            Require(girl.root.GetComponentsInChildren<Animator>(true).Length==1 && boy.root.GetComponentsInChildren<Animator>(true).Length==1,"exactly one Animator per model");
            Require(girl.animator.transform==girl.root.transform.Find("Armature") && !girl.animator.isHuman,"Generic girl Animator at binding root");
            GirlFinalPlayerSetup.ValidateBindings(girl.animator.transform,girl.controller);
            Require(girl.controller.animationClips.Distinct().Count()==8,"all eight supplied clips wired");
            Require(girl.controller.animationClips.All(c=>AssetDatabase.GetAssetPath(c).StartsWith(GirlFinalPlayerSetup.AnimationFolder+"/")),"girl uses only original supplied FBX animations");
            Require(girl.throwReleaseFrame==GirlFinalPlayerSetup.GirlThrowReleaseFrame && boy.throwReleaseFrame==0,"girl releases at frame 46; Boy retains director timing");
            var states=((UnityEditor.Animations.AnimatorController)girl.controller).layers[0].stateMachine.states;
            AnimationClip StateClip(string name) => (AnimationClip)states.Single(s=>s.state.name==name).state.motion;
            Require(StateClip("SlowRun").name=="@Running" && StateClip("Sprint").name=="Running","normal @Running and Shift Running remain correctly assigned");
            Require(StateClip("IdleNeutral").name=="Idle" && StateClip("IdleSad").name=="Sad Idle" && StateClip("IdleBattle").name=="Dwarf Idle","normal, resting and battle idle clips remain correctly assigned");
            Require(player.animator==girl.animator && !player.useRootMotion,"movement references girl; CharacterController drives position");
            Require(Vector3.Distance(boy.localPosition,GirlFinalPlayerSetup.BoyPosition)<.000001f,"Boy sole alignment retained");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(GirlFinalPlayerSetup.PlayerPrefabPath);
            Require(prefab.name=="Player" && prefab.transform.Find("GirlFinal")!=null && prefab.transform.Find("Boy")!=null,"shared prefab matches Player structure");
            var hand=root.GetComponent<PetSummonHandSocket>();
            Require(hand!=null && hand.IsConfigured && hand.BindingCount==2,"both summon hand bindings configured");
            Require(girl.root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="RightHand").Find("BallSocket_PetSummon")!=null,"girl hand socket belongs to correct rig");
            var camera=Camera.main.GetComponent<BasicCameraFollow>();
            Require(camera.target==root && camera.freeLookRig!=null,"FreeLook still follows gameplay Player");
            // Compare the actual Animator to the imported transforms, using an isolated rig without gameplay scripts.
            var fixture=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GirlFinalPlayerSetup.ModelPath));
            try {
                foreach(var old in fixture.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(old);
                var armature=fixture.transform.Find("Armature");
                var animator=armature.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController=girl.controller;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                foreach(var state in ((UnityEditor.Animations.AnimatorController)girl.controller).layers[0].stateMachine.states) {
                    var clip=(AnimationClip)state.state.motion;
                    foreach(float phase in new[]{.05f,.5f,.95f}) {
                        animator.enabled=true;animator.Rebind();animator.Play(state.state.name,0,phase);animator.Update(0);
                        var bones=armature.GetComponentsInChildren<Transform>();
                        var poses=bones.Select(b=>b.localRotation).ToArray();
                        var positions=bones.Select(b=>b.localPosition).ToArray();
                        animator.enabled=false;clip.SampleAnimation(armature.gameObject,clip.length*phase);
                        for(int i=0;i<bones.Length;i++)
                            Require(Quaternion.Angle(poses[i],bones[i].localRotation)<.05f && Vector3.Distance(positions[i],bones[i].localPosition)<.000005f,state.state.name+" preserves source pose at "+phase+" / "+bones[i].name);
                    }
                }
            } finally { Object.DestroyImmediate(fixture); }
            File.WriteAllLines(report,results);
            Debug.Log("[GirlFinal] "+results.Count+" checks passed. Report: "+report);
        } catch(Exception e) {
            results.Add(e.ToString());File.WriteAllLines(report,results);Debug.LogException(e);
        }
    }
}
