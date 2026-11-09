using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Skills;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem.Editor {
    /// <summary>Repeatable authoring, isolated from Trung's art and existing individual saves.</summary>
    public static class BatRuntimeSetup {
        public const string Root = "Assets/Game/CreatureSystem/Runtime/Bats/";
        public const string CatalogPath = "Assets/Game/CreatureSystem/Runtime/CreatureRuntimeCatalog.asset";
        public const string FamilyPath = "Assets/Game/CreatureSystem/Data/Families/Family_bat.asset";
        const string Skills = "Assets/Game/Skills/";
        const string Template = "Assets/Game/CreatureSystem/Runtime/Dragons/OwnedPet/OwnedPet_Dragon01.prefab";
        static readonly int[][] Bases = { new[] {45,55,35,35,40,90}, new[] {60,80,50,50,55,115}, new[] {75,105,65,65,70,140} };
        static readonly int[] Catch = {210,100,45};
        static readonly string[] States = {"Idle", "Fly Forward In Place", "Bite Attack", "Cast Spell", "Projectile Attack", "Take Damage", "Die"};
        [MenuItem("Tools/Capstone/Creatures/9. Build Bat family and basic combat")]
        public static void Build() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dừng Play Mode trước khi dựng asset.");
            var family = AssetDatabase.LoadAssetAtPath<CreatureFamilyDefinition>(FamilyPath);
            if (family == null || family.forms.Count != 3) throw new InvalidOperationException("Thiếu ba dạng dơi đã kiểm kê.");
            var catalog = AssetDatabase.LoadAssetAtPath<PetSkillCatalog>(Skills + "Resources/PetSkills/PetSkillCatalog.asset");
            var vfx = BuildEffect();
            var audio = new[] { Audio("Hand1"), Audio("Ray2"), Audio("Explosion1") };
            var definitions = new List<PetSkillDefinition>();
            definitions.Add(Define(catalog, "bite.normal", "Cắn", "Cắn mục tiêu ở cự ly gần.", PetElement.Normal, "Bite Attack", 1.6f, PetSkillDelivery.Melee, CreatureDamageKind.Physical, 1, 3, vfx, audio));
            definitions.Add(Define(catalog, "orb.dark", "Cầu bóng tối", "Phóng một cầu năng lượng bóng tối về mục tiêu đã khóa.", PetElement.Dark, "Projectile Attack", 3.6f, PetSkillDelivery.Projectile, CreatureDamageKind.Magical, 1.2f, 12, vfx, audio));
            definitions.Add(Define(catalog, "pulse.dark", "Xung bóng tối", "Giải phóng năng lượng bóng tối lên mục tiêu trong tầm nhìn.", PetElement.Dark, "Cast Spell", 5, PetSkillDelivery.Targeted, CreatureDamageKind.Magical, 1.35f, 9, vfx, audio));
            // Preserve IDs, elements, learn levels, icons and authored timing of the existing four dragon skills.
            foreach (var tuple in new[] { ("bite",PetSkillDelivery.Melee,CreatureDamageKind.Physical,1f,3f), ("blast",PetSkillDelivery.Targeted,CreatureDamageKind.Magical,1.15f,10f), ("projectile",PetSkillDelivery.Projectile,CreatureDamageKind.Magical,1f,14f), ("wing strike",PetSkillDelivery.Melee,CreatureDamageKind.Physical,1.2f,3.5f) }) {
                if (!catalog.TryGet(tuple.Item1, out var definition)) throw new InvalidOperationException("Thiếu skill cũ: " + tuple.Item1);
                if (definition.combat == null) { WireCombat(definition, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, vfx, audio); Save(definition); }
            }
            for (int i = 0; i < 3; i++) {
                var form = family.forms[i];
                var stat = Asset<PetStatProfile>("Assets/Resources/PetStats/Bat" + form.formId + ".asset");
                var so = new SerializedObject(stat); Set(so,"definitionId","bat"); Set(so,"formId",form.formId);
                string[] axes = {"hp","attack","magicAttack","defense","magicDefense","speed"};
                for (int j=0;j<6;j++) Set(so,axes[j],Bases[i][j]);
                Set(so,"criticalRate",6.25f); Set(so,"criticalDamage",50f); Set(so,"isCatchable",true); Set(so,"baseCatchRate",Catch[i]); so.ApplyModifiedPropertiesWithoutUndo(); Save(stat); form.statProfile=stat;
                var set=Asset<PetSpeciesSkillSet>(Root+"Skills/Bat"+form.formId+".asset");
                set.speciesId=form.displayName;set.catalog=catalog;set.learnset.Clear();set.defaultLoadout.Clear();
                for(int j=0;j<3;j++)set.learnset.Add(new PetSpeciesSkillEntry{skill=definitions[j],learnAtLevel=j==2?12:1});
                set.defaultLoadout.AddRange(definitions.Take(2));set.Invalidate();Save(set);form.skillSet=set;
                if(!catalog.speciesSets.Contains(set))catalog.speciesSets.Add(set);
                var profile=Asset<CreatureAnimationProfile>(Root+"Animation/Bat"+form.formId+".asset");profile.definitionId="bat";profile.formId=form.formId;profile.actions.Clear();profile.skills.Clear();
                Bind(profile,CreatureAction.Idle,"Idle");Bind(profile,CreatureAction.Move,"Fly Forward In Place");Bind(profile,CreatureAction.Fly,"Fly Forward In Place");Bind(profile,CreatureAction.Attack,"Bite Attack");Bind(profile,CreatureAction.Bite,"Bite Attack");Bind(profile,CreatureAction.Cast,"Cast Spell");Bind(profile,CreatureAction.Projectile,"Projectile Attack");Bind(profile,CreatureAction.Hit,"Take Damage");Bind(profile,CreatureAction.Death,"Die");
                profile.skills.Add(new CreatureSkillAction{skillId="bite.normal",action=CreatureAction.Bite});profile.skills.Add(new CreatureSkillAction{skillId="orb.dark",action=CreatureAction.Projectile});profile.skills.Add(new CreatureSkillAction{skillId="pulse.dark",action=CreatureAction.Cast});Save(profile);form.animationProfile=profile;
                form.visualPrefab=Visual(form);
            }
            foreach(var form in family.forms){form.petPrefab=Actor(family,form,CreatureRole.OwnedPet);form.wildPrefab=Actor(family,form,CreatureRole.Wild);form.enemyPrefab=Actor(family,form,CreatureRole.Enemy);}
            family.evolutionRulesReviewed=true;Save(family);catalog.Invalidate();Save(catalog);
            // Upgrade only production dragon actor roots. Never modify art, pilot or user scene individuals here.
            for(int i=1;i<=3;i++)foreach(string role in new[]{"OwnedPet","Wild"}){
                string path=DragonDemoSceneIntegration.Root+role+"/"+role+"_Dragon"+i.ToString("00")+".prefab";
                var go=PrefabUtility.LoadPrefabContents(path);try{WireActorCombat(go,role=="OwnedPet"?CreatureRole.OwnedPet:CreatureRole.Wild);PrefabUtility.SaveAsPrefabAsset(go,path);}finally{PrefabUtility.UnloadPrefabContents(go);}
            }
            var runtime=Asset<PetPrefabCatalog>(CatalogPath);var data=new SerializedObject(runtime);var entries=data.FindProperty("entries");entries.arraySize=6;
            var dragon=AssetDatabase.LoadAssetAtPath<CreatureFamilyDefinition>("Assets/Game/CreatureSystem/Data/Families/Family_Dragon_Dusk.asset");
            for(int i=0;i<6;i++){var entry=entries.GetArrayElementAtIndex(i);var f=i<3?dragon.forms[i]:family.forms[i-3];entry.FindPropertyRelative("definitionId").stringValue=i<3?"Dragon Dusk":"bat";entry.FindPropertyRelative("formId").stringValue=f.formId;entry.FindPropertyRelative("prefab").objectReferenceValue=f.petPrefab.GetComponent<PetController>();}data.ApplyModifiedPropertiesWithoutUndo();Save(runtime);
            Debug.Log("Bat runtime built: three forms, nine role roots, three shared skills, basic enemy brain and six-form catalog. No source prefab or live save edited.");
        }
        static PetSkillDefinition Define(PetSkillCatalog catalog,string id,string name,string description,PetElement element,string state,float cooldown,PetSkillDelivery delivery,CreatureDamageKind kind,float power,float range,GameObject vfx,AudioClip[] audio){
            var d=Asset<PetSkillDefinition>(Root+"Skills/Skill_"+id+".asset");d.skillId=id;d.displayName=name;d.description=description;d.element=element;d.animatorStates=new[]{state};d.cooldownSeconds=cooldown;d.animationDuration=.9f;d.windupSeconds=.1f;d.recoverySeconds=.25f;
            if(catalog.TryGet(id=="bite.normal"?"bite":"projectile",out var old))d.icon=old.icon;
            WireCombat(d,delivery,kind,power,range,vfx,audio);Save(d);if(!catalog.skills.Contains(d))catalog.skills.Add(d);catalog.Invalidate();return d;
        }
        static void WireCombat(PetSkillDefinition d,PetSkillDelivery delivery,CreatureDamageKind kind,float power,float range,GameObject vfx,AudioClip[] audio){
            var combat=Asset<PetSkillCombatProfile>(Root+"Combat/Combat_"+d.Id.Replace(' ','_')+".asset");combat.delivery=delivery;combat.damageKind=kind;combat.power=power;combat.range=range;combat.projectileSpeed=9;combat.lifetime=3;combat.projectileVisual=delivery==PetSkillDelivery.Projectile?vfx:null;combat.visualScale=.25f;Save(combat);d.combat=combat;
            var p=d.presentation!=null?d.presentation:Asset<PetSkillPresentationProfile>(Root+"Skills/Presentation_"+d.Id+".asset");
            p.releaseMode=PetSkillReleaseMode.Timed;p.releaseDelaySeconds=.25f;p.impactWindowSeconds=4;
            p.cast=new PetSkillCue{socket="Body",followSocket=true,sound=audio[1],volume=.15f,lifetimeSeconds=.6f};
            p.release=new PetSkillCue{socket="Mouth",sound=audio[0],volume=.35f,lifetimeSeconds=.9f};
            p.impact=new PetSkillCue{effectPrefab=vfx,scale=.3f,sound=audio[2],volume=.3f,lifetimeSeconds=1.2f};Save(p);d.presentation=p;
        }
        static AudioClip Audio(string name){string path="Assets/UnityAssset/Trung/KriptoFX/Realistic Effects Pack v4/Effects/Audio/"+name+".wav";var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(clip==null)throw new InvalidOperationException("Missing combat audio: "+path);return clip;}
        static GameObject BuildEffect(){
            string source="Assets/UnityAssset/Trung/Eric VFX Studio/Free Game VFX/Prefab/FX_Purple_Hit_02.prefab",path=Root+"Effects/PurpleSkillCue.prefab";
            var root=PrefabUtility.LoadPrefabContents(source);try{
                foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))if(script!=null)Object.DestroyImmediate(script);
                foreach(var collider in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
                foreach(var particle in root.GetComponentsInChildren<ParticleSystem>(true)){var main=particle.main;main.stopAction=ParticleSystemStopAction.None;main.loop=false;main.playOnAwake=true;}
                root.name="PurpleSkillCue";Folder(Path.GetDirectoryName(path).Replace('\\','/'));return PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static GameObject Visual(CreatureFormDefinition form){
            var root=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(form.sourcePrefab));
            try{
                root.SetActive(false);
                foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true).OrderBy(s=>s is PetHudRuntimeStats?1:0).ToArray())if(script!=null)Object.DestroyImmediate(script);
                foreach(var c in root.GetComponentsInChildren<Component>(true).ToArray())if(c!=null&&!(c is Transform)&&!(c is Animator)&&!(c is Renderer)&&!(c is MeshFilter)&&!(c is LODGroup))Object.DestroyImmediate(c);
                var animator=root.GetComponentInChildren<Animator>(true);if(animator==null)throw new InvalidOperationException("Missing Bat Animator.");
                var source=animator.runtimeAnimatorController as AnimatorController;if(source==null)throw new InvalidOperationException("Missing source controller.");
                string path=Root+"Animation/Bat"+form.formId+".controller";Folder(Path.GetDirectoryName(path).Replace('\\','/'));
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
                var machine=controller.layers[0].stateMachine;
                foreach(var old in machine.states)machine.RemoveState(old.state);
                foreach(string name in States){var state=source.layers[0].stateMachine.states.FirstOrDefault(s=>s.state.name==name).state;if(state==null||state.motion==null)throw new InvalidOperationException("Missing real clip/state "+name+" in "+source.name);var created=machine.AddState(name);created.motion=state.motion;if(name=="Idle")machine.defaultState=created;}
                Save(controller);animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.enabled=true;
                var binding=animator.gameObject.AddComponent<CreatureVisualBinding>();binding.animationProfile=form.animationProfile;binding.skillSet=form.skillSet;
                var bones=root.GetComponentsInChildren<Transform>(true);var jaw=bones.FirstOrDefault(t=>t.name=="RigJaw");if(jaw==null)throw new InvalidOperationException("Missing actual RigJaw socket.");
                binding.sockets.Add(new PetSkillSocket{key="Mouth",anchor=jaw});binding.sockets.Add(new PetSkillSocket{key="Body",anchor=bones.FirstOrDefault(t=>t.name=="RigSpine")??bones.FirstOrDefault(t=>t.name=="RigHub")??root.transform});binding.sockets.Add(new PetSkillSocket{key="Ground",anchor=root.transform});
                // A unit model root matches evolution's contract; the child preserves authored form scale.
                var wrapper=new GameObject("Visual_Bat"+form.formId);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wrapper,root.scene);root.transform.SetParent(wrapper.transform,false);root.transform.localPosition=Vector3.zero;root.transform.localRotation=Quaternion.identity;root.SetActive(true);
                string output=Root+"Visual/Visual_Bat"+form.formId+".prefab";Folder(Path.GetDirectoryName(output).Replace('\\','/'));return PrefabUtility.SaveAsPrefabAsset(wrapper,output);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static GameObject Actor(CreatureFamilyDefinition family,CreatureFormDefinition form,CreatureRole role){
            var go=PrefabUtility.LoadPrefabContents(Template);
            try{
                go.SetActive(false);foreach(Transform child in go.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(form.visualPrefab,go.scene);visual.transform.SetParent(go.transform,false);
                var animator=visual.GetComponentInChildren<Animator>(true);var pet=go.GetComponent<PetController>();pet.owner=null;pet.useSelfInput=false;pet.animator=animator;
                var so=new SerializedObject(pet);Set(so,"wildEncounter",role==CreatureRole.Wild);so.ApplyModifiedPropertiesWithoutUndo();
                var metadata=go.GetComponent<PetCollectionMetadata>();so=new SerializedObject(metadata);
                foreach(string field in new[]{"petId","nickname","captureDate","captureLocation","captureBallId","trainerName","codexNumber","codexDescription"})Set(so,field,"");
                foreach(string field in new[]{"favorite","released","geneticsInitialized","captureGeneticsRolled","codexRegistered","hasNextEvolution"})Set(so,field,false);
                Set(so,"obtainedOrder",0);
                Set(so,"definitionId","bat");Set(so,"species",form.displayName);Set(so,"currentFormId",form.formId);Set(so,"evolutionStage",int.Parse(form.formId)-1);Set(so,"element",(int)PetElement.Dark);Set(so,"secondaryElement",(int)PetElement.Flying);Set(so,"experience",0);Set(so,"experienceToNextLevel",0);
                so.FindProperty("promptedEvolutionIds").ClearArray();so.FindProperty("resourceInvestments").ClearArray();
                var rules=so.FindProperty("evolutionRules");rules.arraySize=2;
                for(int i=0;i<2;i++){var rule=rules.GetArrayElementAtIndex(i);Set(rule,"evolutionId","bat."+(i+1).ToString("00")+"-to-"+(i+2).ToString("00")+".level");Set(rule,"sourceStage",i);Set(rule,"targetFormId",family.forms[i+1].formId);Set(rule,"targetDisplayName",family.forms[i+1].displayName);Set(rule,"targetSpecies",family.forms[i+1].displayName);Set(rule,"targetIcon",family.forms[i+1].avatar);Set(rule,"targetVisualPrefab",family.forms[i+1].visualPrefab);Set(rule,"targetElement",(int)PetElement.Dark);Set(rule,"targetSecondaryElement",(int)PetElement.Flying);Set(rule,"trigger",0);Set(rule,"requiredLevel",i==0?18:36);Set(rule,"requiredItemId","");Set(rule,"requiredItemQuantity",1);Set(rule,"maxHealthMultiplier",1f);foreach(string bonus in new[]{"attackBonus","defenseBonus","magicAttackBonus","magicDefenseBonus","speedBonus"})Set(rule,bonus,0);}
                so.ApplyModifiedPropertiesWithoutUndo();
                var stats=go.GetComponent<PetHudRuntimeStats>();so=new SerializedObject(stats);Set(so,"displayName",form.displayName);Set(so,"icon",form.avatar);Set(so,"level",5);Set(so,"speciesSkillSet",form.skillSet);Set(so,"animator",animator);Set(so,"petController",pet);Set(so,"usePrototypeSkillsWhenEmpty",false);Set(so,"health",1f);Set(so,"maxHealth",1f);so.FindProperty("skills").ClearArray();so.FindProperty("learnedSkills").ClearArray();so.ApplyModifiedPropertiesWithoutUndo();
                var evolution=go.GetComponent<PetEvolutionVisualController>();so=new SerializedObject(evolution);Set(so,"visualRoot",go.transform);Set(so,"baseVisual",visual);Set(so,"baseVisualPrefab",form.visualPrefab);so.ApplyModifiedPropertiesWithoutUndo();
                var presentation=go.GetComponent<CreatureRolePresentation>();presentation.role=role;
                if(role==CreatureRole.Enemy){Object.DestroyImmediate(evolution);Object.DestroyImmediate(pet);}
                if(role!=CreatureRole.OwnedPet){var health=go.GetComponent<DummyEnemy>();if(health==null)health=go.AddComponent<DummyEnemy>();health.autoRevive=false;health.writeHealthToName=false;health.tintFeedback=false;var e=new SerializedObject(health);Set(e,"experienceReward",30);var ev=e.FindProperty("effortYield");foreach(string a in new[]{"hp","attack","defense","magicAttack","magicDefense","speed"})Set(ev,a,a=="speed"?1:0);e.ApplyModifiedPropertiesWithoutUndo();if(role==CreatureRole.Wild&&go.GetComponent<WildPetCaptureTarget>()==null)go.AddComponent<WildPetCaptureTarget>();}
                var capsule=go.GetComponent<CapsuleCollider>();if(capsule==null)capsule=go.AddComponent<CapsuleCollider>();var renderers=visual.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);Vector3 size=go.transform.InverseTransformVector(bounds.size);capsule.center=go.transform.InverseTransformPoint(bounds.center);capsule.radius=Mathf.Clamp(Mathf.Min(size.x,size.z)*.23f,.2f,.65f);capsule.height=Mathf.Max(capsule.radius*2,size.y);
                WireActorCombat(go,role);var agent=go.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent==null)agent=go.AddComponent<UnityEngine.AI.NavMeshAgent>();agent.enabled=false;agent.radius=capsule.radius;agent.height=capsule.height;agent.baseOffset=0;
                go.name=role+"_Bat"+form.formId;go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;go.SetActive(true);string path=Root+role+"/"+go.name+".prefab";Folder(Path.GetDirectoryName(path).Replace('\\','/'));return PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void WireActorCombat(GameObject go,CreatureRole role){
            if(go.GetComponent<CreatureCombatant>()==null)go.AddComponent<CreatureCombatant>();
            if(role==CreatureRole.Enemy){var executor=go.GetComponent<PetSkillCombatExecutor>();if(executor!=null)Object.DestroyImmediate(executor);if(go.GetComponent<BasicCreatureEnemy>()==null)go.AddComponent<BasicCreatureEnemy>();}
            else {if(go.GetComponent<PetSkillCombatExecutor>()==null)go.AddComponent<PetSkillCombatExecutor>();var player=go.GetComponent<PetSkillPresentationPlayer>();var binding=go.GetComponentInChildren<CreatureVisualBinding>(true);player.sockets=new List<PetSkillSocket>(binding.sockets);}
            var health=go.GetComponent<DummyEnemy>();if(health!=null)health.tintFeedback=false;
        }
        static void Bind(CreatureAnimationProfile p,CreatureAction a,string state)=>p.actions.Add(new CreatureActionBinding{action=a,state=state});
        static void Save(Object value){EditorUtility.SetDirty(value);AssetDatabase.SaveAssetIfDirty(value);}
        static T Asset<T>(string path)where T:ScriptableObject{var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset!=null)return asset;Folder(Path.GetDirectoryName(path).Replace('\\','/'));asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;}
        static void Folder(string folder){string current="Assets";foreach(string part in folder.Split('/').Skip(1)){if(!AssetDatabase.IsValidFolder(current+"/"+part))AssetDatabase.CreateFolder(current,part);current+="/"+part;}}
        static void Assign(SerializedProperty p,object value){if(p==null)throw new InvalidOperationException("Missing serialized field.");if(value is string s)p.stringValue=s;else if(value is bool b)p.boolValue=b;else if(value is int i)p.intValue=i;else if(value is float f)p.floatValue=f;else p.objectReferenceValue=value as Object;}
        static void Set(SerializedObject so,string key,object value)=>Assign(so.FindProperty(key),value);
        static void Set(SerializedProperty so,string key,object value)=>Assign(so.FindPropertyRelative(key),value);
    }
}
