using System;
using System.IO;
using Capstone.Game.CaptureSystem;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.CreatureSystem.Editor {
    // Legacy authoring commands are explicit menu actions, never reload/update hooks.
    // Optional QA storage must not poison type initialization or reapply old assets.
    public static class PveGameplaySetup {
        public const string Qa=QaArtifactPaths.Root+"pve-combat-hub-2026-11-11/";
        public const string Version="pve-input-mana-volley-2026-11-12-v2";
        public const string NavigationVersion="pve-demo-navigation-2026-11-12-v2";
        internal static bool TryPrepareQaOutput(string directory, out string error) {
            try { Directory.CreateDirectory(directory); error=string.Empty; return true; }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException) {
                error=exception.Message;
                return false;
            }
        }
        static bool PrepareQaOutput() {
            if(TryPrepareQaOutput(Qa,out string error))return true;
            Debug.LogError("Không thể tạo thư mục QA: "+Qa+"\nHãy kiểm tra ổ E và quyền truy cập. Lệnh authoring chưa được thực hiện.\n"+error);
            return false;
        }
        [MenuItem("Tools/Capstone/Advanced/PvE/3. Build bounded DemoScene navigation")]
        public static void BuildDemoNavigation(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Dừng Play Mode trước.");
            var scene=EditorSceneManager.GetActiveScene();if(scene.name!="DemoScene")throw new InvalidOperationException("Chỉ bake phần scene DemoScene đang mở.");
            var player=UnityEngine.Object.FindFirstObjectByType<PetCommandInput>();if(player==null)throw new InvalidOperationException("Thiếu player trong scene.");
            if(!PrepareQaOutput())return;
            Directory.CreateDirectory(Qa+"scene-backups");EditorSceneManager.SaveScene(scene,Qa+"scene-backups/before-navigation.unity",true);
            var root=GameObject.Find("Demo PvE Navigation");if(root==null){root=new GameObject("Demo PvE Navigation");Undo.RegisterCreatedObjectUndo(root,"Demo PvE Navigation");}
            var surface=root.GetComponent<NavMeshSurface>();if(surface==null)surface=Undo.AddComponent<NavMeshSurface>(root);
            root.transform.position=player.transform.position;surface.collectObjects=CollectObjects.Volume;surface.size=new Vector3(80,16,80);surface.center=new Vector3(0,2,0);
            surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.ignoreNavMeshAgent=true;surface.ignoreNavMeshObstacle=true;
            surface.overrideVoxelSize=true;surface.voxelSize=.2f;surface.overrideTileSize=true;surface.tileSize=128;
            var modifier=player.GetComponent<NavMeshModifier>();if(modifier==null)modifier=Undo.AddComponent<NavMeshModifier>(player.gameObject);modifier.ignoreFromBuild=true;
            var clock=System.Diagnostics.Stopwatch.StartNew();surface.BuildNavMesh();
            if(surface.navMeshData==null)throw new InvalidOperationException("Bake không tạo NavMeshData.");
            string dir="Assets/Game/CreatureSystem/Runtime/DemoNavigation";Directory.CreateDirectory(dir);AssetDatabase.Refresh();string path=dir+"/NavMesh_DemoPvE.asset";
            var previous=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if(previous==null){AssetDatabase.CreateAsset(surface.navMeshData,path);Save(surface.navMeshData);}
            else{var generated=surface.navMeshData;surface.RemoveData();EditorUtility.CopySerialized(generated,previous);surface.navMeshData=previous;Save(previous);surface.AddData();UnityEngine.Object.DestroyImmediate(generated);}
            Vector3 feet=player.transform.position;
            var groundHits=Physics.RaycastAll(feet+Vector3.up*2,Vector3.down,12,~0,QueryTriggerInteraction.Ignore);
            Array.Sort(groundHits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var ground in groundHits)if(!ground.transform.IsChildOf(player.transform)&&ground.normal.y>.6f){feet=ground.point;break;}
            bool sampled=NavMesh.SamplePosition(feet,out var nav,3,NavMesh.AllAreas);
            bool near=sampled&&Mathf.Abs(nav.position.y-feet.y)<.6f&&Vector2.Distance(new Vector2(nav.position.x,nav.position.z),new Vector2(feet.x,feet.z))<2;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,Qa+"scene-backups/after-navigation.unity",true);
            File.WriteAllText(Qa+"navigation-status.txt","BAKED seconds="+clock.Elapsed.TotalSeconds+" triangles="+NavMesh.CalculateTriangulation().indices.Length/3+" playerSample="+near+" player="+player.transform.position+" ground="+feet+" nav="+(sampled?nav.position.ToString():"missing"));
            if(!near)throw new InvalidOperationException("NavMesh chưa phủ chân player; không báo đạt chuyển động.");
            File.WriteAllText(Qa+"authoring-status.txt","APPLIED "+NavigationVersion);
        }
        [MenuItem("Tools/Capstone/Advanced/PvE/1. Apply approved input mana and volley assets")]
        public static void Apply(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Dừng Play Mode trước.");
            if(!PrepareQaOutput())return;
            string[] names={"bite","blast","projectile","wing_strike"};float[] cost={8,15,12,18};
            for(int i=0;i<names.Length;i++){
                var skill=AssetDatabase.LoadAssetAtPath<PetSkillDefinition>("Assets/Game/Skills/Data/Definitions/Skill_"+names[i]+".asset");
                if(skill==null||skill.combat==null||skill.presentation==null)throw new InvalidOperationException("Thiếu skill: "+names[i]);
                skill.manaCost=cost[i];skill.combat.weightedWindVolley=i==3;skill.combat.burstEndNormalized=.78f;
                if(i==3){skill.combat.power=1.8f;skill.animationDuration=1.05f;skill.presentation.releaseNormalized=.30f;skill.presentation.chainNormalized=.94f;skill.description="Phóng 2–5 lưỡi gió, tổng sức mạnh 180% ATK chia theo số phát. Đạn hụt không chuyển damage sang đạn khác. Tốn 18 MP.";}
                if(i==2){skill.combat.power=1.6f;skill.description="Phóng một đạn gây 160% SP.ATK. Tốn 12 MP; đạn dừng khi trúng mục tiêu hoặc vật cản.";}
                Save(skill);Save(skill.combat);Save(skill.presentation);
            }
            for(int form=1;form<=3;form++){
                var set=AssetDatabase.LoadAssetAtPath<PetSpeciesSkillSet>("Assets/Game/Skills/Data/Species/Dragon"+form.ToString("00")+".asset");
                if(set==null)throw new InvalidOperationException("Thiếu learnset rồng "+form);
                set.startingMana=100;foreach(var entry in set.learnset)if(entry.skill!=null&&entry.skill.combat!=null&&entry.skill.combat.weightedWindVolley)entry.animationDuration=1.05f;
                set.Invalidate();Save(set);MakeWingVariant(form);
            }
            const string catalogPath="Assets/Resources/Capture/BallPresentations.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<CaptureBallPresentationCatalog>(catalogPath);
            if(catalog==null){catalog=ScriptableObject.CreateInstance<CaptureBallPresentationCatalog>();AssetDatabase.CreateAsset(catalog,catalogPath);}
            var old=catalog.choices;catalog.choices=new CaptureBallPresentation[3];
            string[] ids={"BallCommon","BallRare","BallUltra"},labels={"Thường","Rare","Ultra"};
            for(int i=0;i<3;i++)catalog.choices[i]=new CaptureBallPresentation{key=ids[i],displayName=labels[i],icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/WorldFeedback/Art/"+ids[i]+"/"+ids[i]+"_Icon.png"),item=i==0?CaptureAssets.LoadBall():old!=null&&i<old.Length?old[i]?.item:null};
            Save(catalog);
            ConfigureLoot();
            foreach(var player in UnityEngine.Object.FindObjectsByType<PlayerVisualSwitcher>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
                var serialized=new SerializedObject(player);var key=serialized.FindProperty("switchKey");
                if(key.intValue==(int)KeyCode.G){Undo.RecordObject(player,"Move legacy G visual switch to F8");key.intValue=(int)KeyCode.F8;serialized.ApplyModifiedProperties();}
            }
            File.WriteAllText(Qa+"authoring-status.txt","APPLIED "+Version+" "+DateTime.UtcNow.ToString("O"));
        }
        static void ConfigureLoot(){
            const string dir="Assets/Game/CreatureSystem/Resources/Pve";Directory.CreateDirectory(dir);AssetDatabase.Refresh();
            const string path=dir+"/LootCatalog.asset";var catalog=AssetDatabase.LoadAssetAtPath<PveLootCatalog>(path);
            if(catalog==null){catalog=ScriptableObject.CreateInstance<PveLootCatalog>();AssetDatabase.CreateAsset(catalog,path);}
            if(catalog.potion==null)catalog.potion=AssetDatabase.LoadAssetAtPath<Capstone.Game.Inventory.MonsterItemDefinition>("Assets/Game/Inventory/Potion.asset");
            if(catalog.healingApple==null)catalog.healingApple=AssetDatabase.LoadAssetAtPath<Capstone.Game.Inventory.MonsterItemDefinition>("Assets/Game/Inventory/HealingApple.asset");
            if(catalog.manaPotion==null)catalog.manaPotion=AssetDatabase.LoadAssetAtPath<Capstone.Game.Inventory.MonsterItemDefinition>("Assets/Game/Inventory/ManaPotion.asset");
            catalog.goldIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ThangTest/UIThang/GUIArt/Resources/UI/GuiKit/Icons/coin.png");
            const string materialPath=dir+"/LootGlow.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(shader==null)throw new InvalidOperationException("Thiếu URP particle shader.");material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Surface",1);material.SetFloat("_Blend",2);material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.One);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;Save(material);
            catalog.glowMaterial=material;Save(catalog);
        }
        static void MakeWingVariant(int form){
            string controllerPath="Assets/Game/CreatureSystem/Runtime/Dragons/Animation/Dragon"+form.ToString("00")+".controller";
            var visual=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CreatureSystem/Runtime/Dragons/Visual/Visual_Dragon"+form.ToString("00")+".prefab");
            var controller=visual!=null?visual.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController as AnimatorController:null;
            string actual=controller!=null?AssetDatabase.GetAssetPath(controller):"";
            bool owned=actual.StartsWith("Assets/Game/",StringComparison.Ordinal)||actual=="Assets/Animations/Thắng/Pets/Dragon_Dusk_PetRuntime.controller";
            if(controller==null||!owned)throw new InvalidOperationException("Không sửa controller nguồn cho rồng "+form+": "+actual);
            ConfigureWing(controller.layers[0].stateMachine,form);ConfigureDowned(controller,form);Save(controller);
        }
        static void ConfigureDowned(AnimatorController controller,int form){
            var machine=controller.layers[0].stateMachine;AnimatorState fallen=null,downed=null;
            foreach(var entry in machine.states){if(entry.state.name=="Die")fallen=entry.state;if(entry.state.name=="Downed")downed=entry.state;}
            if(fallen==null||!(fallen.motion is AnimationClip source))throw new InvalidOperationException("Thiếu animation gục cho rồng "+form);
            string path="Assets/Game/CreatureSystem/Runtime/Dragons/Animation/Pve/Downed_Dragon"+form.ToString("00")+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=UnityEngine.Object.Instantiate(source);clip.name="Downed Dragon "+form;AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);AssetDatabase.CreateAsset(clip,path);}
            if(downed==null)downed=machine.AddState("Downed");downed.motion=clip;downed.speed=1;Save(downed);
            var profile=AssetDatabase.LoadAssetAtPath<CreatureAnimationProfile>("Assets/Game/CreatureSystem/Runtime/Dragons/Animation/Dragon"+form.ToString("00")+".asset");
            profile.actions.RemoveAll(entry=>entry.action==CreatureAction.Downed);profile.actions.Add(new CreatureActionBinding{action=CreatureAction.Downed,state="Downed"});Save(profile);
        }
        static void ConfigureWing(AnimatorStateMachine machine,int form){
            foreach(var child in machine.states)if(child.state.name=="Wing Attack"){
                string dir="Assets/Game/CreatureSystem/Runtime/Dragons/Animation/Pve";Directory.CreateDirectory(dir);AssetDatabase.Refresh();
                string path=dir+"/WingVolley_Dragon"+form.ToString("00")+".anim";
                var variant=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(variant==null){
                    var source=child.state.motion as AnimationClip;if(source==null||source.length<=0)throw new InvalidOperationException("Thiếu clip cánh.");
                    variant=UnityEngine.Object.Instantiate(source);variant.name="Wind Volley Dragon "+form;
                    float ratio=1.05f/source.length;
                    foreach(var binding in AnimationUtility.GetCurveBindings(source)){
                        var curve=AnimationUtility.GetEditorCurve(source,binding);var keys=curve.keys;
                        for(int i=0;i<keys.Length;i++){keys[i].time*=ratio;keys[i].inTangent/=ratio;keys[i].outTangent/=ratio;}curve.keys=keys;AnimationUtility.SetEditorCurve(variant,binding,curve);
                    }
                    foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(source)){
                        var keys=AnimationUtility.GetObjectReferenceCurve(source,binding);for(int i=0;i<keys.Length;i++)keys[i].time*=ratio;AnimationUtility.SetObjectReferenceCurve(variant,binding,keys);
                    }
                    AnimationUtility.SetAnimationEvents(variant,Array.Empty<AnimationEvent>());
                    var settings=AnimationUtility.GetAnimationClipSettings(variant);settings.startTime=0;settings.stopTime=1.05f;settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(variant,settings);
                    AssetDatabase.CreateAsset(variant,path);
                }
                child.state.motion=variant;child.state.speed=1;Save(child.state);
            }
            foreach(var child in machine.stateMachines)ConfigureWing(child.stateMachine,form);
        }
        static void Save(UnityEngine.Object value){EditorUtility.SetDirty(value);AssetDatabase.SaveAssetIfDirty(value);}
    }
}
