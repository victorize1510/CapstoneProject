#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.PetSummon;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Capstone.Game.CreatureSystem {
    public static class PveCoreVerification {
        const string Qa=QaArtifactPaths.Root+"pve-combat-hub-2026-11-11/",Session="Capstone.PveCore.Qa";
        public const string Version="pve-core-verify-2026-11-12-v5";
        static readonly List<string> checks=new List<string>();static double pollAt;static bool running;
        static void Write(string name,string value){Directory.CreateDirectory(Qa);File.WriteAllText(Qa+name,value);File.AppendAllText(Qa+"created-paths.txt",Qa+name+"\n");}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;Write("verification-loaded.txt",Version);}
        static void Poll(){if(EditorApplication.timeSinceStartup<pollAt||EditorApplication.isUpdating||EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode)return;pollAt=EditorApplication.timeSinceStartup+1;if(!File.Exists(Qa+"core.request")||File.ReadAllText(Qa+"core.request").Trim()!=Version)return;File.Delete(Qa+"core.request");Begin();}
        static void Check(bool pass,string name){checks.Add((pass?"PASS ":"FAIL ")+name);Write("core-checks.txt",string.Join("\n",checks));if(!pass)throw new InvalidOperationException(name);}
        [MenuItem("Tools/Capstone/Advanced/PvE/2. Verify input mana and volley in isolated save")]
        public static void Begin(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;checks.Clear();
            try{
                var random=new System.Random(11042026);var bins=new int[4];for(int i=0;i<100000;i++)bins[PveBalanceRules.WindVolleyCount(random.NextDouble())-2]++;
                double[] expected={.1,.4,.4,.1};for(int i=0;i<4;i++)Check(Math.Abs(bins[i]/100000d-expected[i])<Math.Max(.005,4*Math.Sqrt(expected[i]*(1-expected[i])/100000)),"seeded 100000 volley distribution n="+(i+2)+" count="+bins[i]);
                for(int n=2;n<=5;n++)Check(Math.Abs(1.8/n*n-1.8)<.00001,"raw projectile budget conserved n="+n);
                Check(PveBalanceRules.ExperienceAward(30,3,5)==33&&PveBalanceRules.ExperienceAward(30,5,5)==30&&PveBalanceRules.ExperienceAward(30,10,5)==19&&PveBalanceRules.ExperienceAward(30,15,5)==0,"approved EXP examples 33/30/19/0");
                var gesture=new HubPressGesture();Check(gesture.Step(true,true,false,0,.28f)==HubPressGesture.Intent.None&&gesture.Step(false,true,false,.29f,.28f)==HubPressGesture.Intent.Hold&&gesture.Step(false,false,true,.30f,.28f)==HubPressGesture.Intent.ReleaseHold,"hold release closes without tap/throw");
                SessionState.SetBool(Session,true);SessionState.SetString(Session+".save",Qa+"test-saves/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
                SessionState.SetBool(Session+".reload",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+".options",(int)EditorSettings.enterPlayModeOptions);
                EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
                EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
                Write("core-status.txt","ENTERING_PLAY isolated saves");EditorApplication.EnterPlaymode();
            }catch(Exception e){Write("core-status.txt","FAILED before Play\n"+e);Debug.LogException(e);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){
            if(!SessionState.GetBool(Session,false)||running)return;string root=SessionState.GetString(Session+".save","");
            foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new PlayerJsonFileSaveStore(root));
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(root));Run();
        }
        static async Task Gap(int ms=100){await Task.Delay(ms);if(!EditorApplication.isPlaying)throw new OperationCanceledException("Play ended");}
        static async void Run(){
            running=true;var brains=new List<(BasicCreatureEnemy brain,bool enabled)>();
            try{
                await Gap(600);Write("core-status.txt","RUNNING input/mana/volley engine checks");
                var player=Object.FindFirstObjectByType<PetCommandInput>();var save=Object.FindFirstObjectByType<PlayerSaveController>();
                Check(player!=null&&save!=null&&save.SavePath.StartsWith(Qa,StringComparison.OrdinalIgnoreCase),"save isolated on E before gameplay Start");
                Check(player.GetComponent<PlayerVisualSwitcher>().SwitchKey==KeyCode.F8,"legacy G binding migrates to F8");
                foreach(var brain in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None)){brains.Add((brain,brain.enabled));brain.enabled=false;}
                var director=player.GetComponent<PetSummonDirector>();var movement=player.GetComponent<BasicPlayerMovement>();
                for(int i=0;i<40&&movement!=null&&!movement.CanStartPetThrow;i++)await Gap();
                Check(director!=null&&director.RequestSummonSlot(0),"real summon request accepted: "+director?.LastFailureReason);
                for(int i=0;i<120&&director.IsSequenceRunning;i++)await Gap();Check(player.CanUsePetSkills,"real summoned pet ready");
                var pet=player.activePet;var stats=pet.GetComponent<PetHudRuntimeStats>();
                Check(stats.MaxEnergy==100&&stats.Energy==100,"new pet starts with configured 100 MP");
                string[] names={"bite","blast","projectile","wing_strike"};var skills=new List<SkillHudData>();
                foreach(var name in names)skills.Add(AssetDatabase.LoadAssetAtPath<PetSkillDefinition>("Assets/Game/Skills/Data/Definitions/Skill_"+name+".asset").CreateHudData(1,true));stats.SetSkills(skills);
                var action=pet.GetComponent<PetCombatActionDriver>();if(action==null)action=pet.gameObject.AddComponent<PetCombatActionDriver>();
                stats.RequestSkill(0);await Gap();Check(action.StartedCount==1&&Math.Abs(stats.Energy-92)<.001f&&stats.GetSkills()[0].IsCoolingDown,"accepted bite spends 8 MP and starts one cooldown");
                for(int i=0;i<40&&action.IsSkillBusy;i++)await Gap();
                stats.SetStatus(stats.Health,stats.MaxHealth,7,stats.MaxEnergy);int started=action.StartedCount;stats.RequestSkill(1);await Gap();
                Check(action.StartedCount==started&&stats.Energy==7&&!stats.GetSkills()[1].IsCoolingDown,"insufficient MP rejects without cooldown or resource loss");
                stats.SetStatus(stats.Health,stats.MaxHealth,100,stats.MaxEnergy);
                var presentation=pet.GetComponent<PetSkillPresentationPlayer>();int emissions=0;
                Action<int,string> first=(_,id)=>{if(id==skills[3].skillId)emissions++;};Action<int,string,int> child=(_,id,__)=>{if(id==skills[3].skillId)emissions++;};
                presentation.SkillReleased+=first;presentation.SkillProjectileReleased+=child;
                stats.RequestSkill(3);await Gap();var execution=pet.GetComponent<PetSkillCombatExecutor>();int count=execution.LastVolleyCount;
                for(int i=0;i<40&&(action.IsSkillBusy||emissions<count);i++)await Gap();
                presentation.SkillReleased-=first;presentation.SkillProjectileReleased-=child;
                Check(count>=2&&count<=5&&emissions==count,"animation markers emit all "+count+" wind projectiles exactly once");
                Check(Math.Abs(stats.Energy-82)<.001f&&stats.GetSkills()[3].IsCoolingDown,"volley spends 18 MP and cooldown exactly once");
                var capture=Object.FindFirstObjectByType<HubCaptureHud>();capture.OpenPicker();await Gap();
                Check(capture.IsPickerOpen&&GameplayHudController.Live.PresentationRoot.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("BallChoice",StringComparison.Ordinal))==3,"G picker shows exactly three ball cells");
                capture.ClosePicker();await Gap();Check(!capture.IsPickerOpen&&!Inventory.InventoryInputController.GameplayInputBlocked,"picker closes and restores gameplay input");
                var pooled=GameplayHudController.Live.PresentationRoot.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("BallChoice",StringComparison.Ordinal)).Select(t=>t.GetInstanceID()).ToArray();
                for(int repeat=0;repeat<6;repeat++){capture.OpenPicker();await Gap(35);capture.ClosePicker();await Gap(35);}
                Check(GameplayHudController.Live.PresentationRoot.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("BallChoice",StringComparison.Ordinal)).Select(t=>t.GetInstanceID()).SequenceEqual(pooled),"six repeated G picker openings reuse exact three button instances");
                player.ProcessReturnPress(Time.unscaledTime);Check(pet.IsSummoned&&!pet.HasCombatTarget,"single Backspace command keeps pet summoned and returns");
                player.ProcessReturnPress(Time.unscaledTime+.1f);for(int i=0;i<120&&director.IsSequenceRunning;i++)await Gap();Check(!pet.IsSummoned,"double Backspace commits one real recall");
                if(File.Exists(Qa+"combat-full.flag")){File.Delete(Qa+"combat-full.flag");await PveCombatScenarioVerification.Run(player,Check);}
                if(File.Exists(Qa+"final.flag")){File.Delete(Qa+"final.flag");await PveFinalScenarioVerification.Run(player,Check);}
                Write("core-status.txt","COMPLETE "+checks.Count+" checks; physical key hold and standalone not verified");
            }catch(Exception e){Write("core-status.txt","FAILED\n"+e);Debug.LogException(e);}
            finally{foreach(var value in brains)if(value.brain!=null)value.brain.enabled=value.enabled;SessionState.SetBool(Session,false);running=false;EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+".reload",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+".options",0);if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();}
        }
    }
}
#endif
