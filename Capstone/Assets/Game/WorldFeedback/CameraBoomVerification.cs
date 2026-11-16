#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CreatureSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Capstone.Game.WorldFeedback {
    /// <summary>Explicit opt-in only. Tests/save/captures remain outside the repository.</summary>
    public static class CameraBoomVerification {
        public const string Root=QaArtifactPaths.Root+"camera-boom-tools-2026-11-14/";
        const string Session="Capstone.CameraBoomQA";
        const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        static readonly List<string> checks=new List<string>();static bool running;
        static string Stage=>SessionState.GetString(Session+"stage","baseline");
        static void Write(string file,string data){Directory.CreateDirectory(Root);File.WriteAllText(Root+file,data);File.AppendAllText(Root+"created-paths.txt",Root+file+"\n");}
        [MenuItem("Tools/Capstone/Advanced/Camera/Audit Toolbox menus")]
        static void VerifyTools(){
            var menus=new List<string>();foreach(var method in TypeCache.GetMethodsWithAttribute<MenuItem>())foreach(var item in method.GetCustomAttributes<MenuItem>())if(!item.validate&&item.menuItem.StartsWith("Tools/Capstone/",StringComparison.Ordinal))menus.Add(item.menuItem);
            int duplicates=menus.GroupBy(s=>s).Count(g=>g.Count()>1);int normal=menus.Count(s=>!s.StartsWith("Tools/Capstone/Advanced/",StringComparison.Ordinal));
            Write("registered-tools.txt",string.Join("\n",menus.OrderBy(s=>s)));
            bool opened=EditorApplication.ExecuteMenuItem("Tools/Capstone/Toolbox");
            Write("tools-status.txt",(duplicates==0&&normal==3&&opened?"COMPLETE":"FINDINGS")+" registered="+menus.Count+" normal="+normal+" advanced="+(menus.Count-normal)+" duplicates="+duplicates+" toolboxOpened="+opened);
        }
        [MenuItem("Tools/Capstone/Advanced/Camera/Verify isolated camera regression")]
        public static void Verify()=>Begin("after");
        static void Begin(string stage){
            if(EditorApplication.isPlayingOrWillChangePlaymode||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="DemoScene")return;
            checks.Clear();SessionState.SetBool(Session,true);SessionState.SetString(Session+"stage",stage);SessionState.SetString(Session+"save",Root+"test-saves/"+stage+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            SessionState.SetBool(Session+"reload",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+"options",(int)EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();Write(stage+"-status.txt","ENTERING_PLAY");EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(!SessionState.GetBool(Session,false)||running)return;
            string path=SessionState.GetString(Session+"save","");foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new PlayerJsonFileSaveStore(path));
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(path));Run();
        }
        static void Check(bool result,string text){checks.Add((result?"PASS ":"FAIL ")+text);Write(Stage+"-checks.txt",string.Join("\n",checks));}
        static async Task Gap(int ms=35){await Task.Delay(ms);if(!EditorApplication.isPlaying)throw new OperationCanceledException("Play ended");}
        static async Task Shot(string name){string file=Root+Stage+"-"+name+".png";File.AppendAllText(Root+"created-paths.txt",file+"\n");ScreenCapture.CaptureScreenshot(file);await Gap(100);}
        static GameObject Wall(Vector3 position,Quaternion rotation,Vector3 size){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Camera QA temporary wall";go.transform.SetPositionAndRotation(position,rotation);go.transform.localScale=size;Physics.SyncTransforms();return go;}
        static async void Run(){
            running=true;var fixtures=new List<GameObject>();var brains=new List<(BasicCreatureEnemy ai,bool enabled)>();BasicCameraFollow source=null;bool sourceEnabled=false;EditorWindow view=null;PropertyInfo size=null;int oldSize=-1;
            try{
                await Gap(1000);Write(Stage+"-status.txt","RUNNING");
                var save=Object.FindFirstObjectByType<PlayerSaveController>();Check(save!=null&&save.SavePath.StartsWith(Root,StringComparison.OrdinalIgnoreCase),"save isolated on E before gameplay Start");
                source=Object.FindFirstObjectByType<BasicCameraFollow>();if(source==null||source.freeLookRig==null||!source.freeLookRig.IsReady)throw new InvalidOperationException("DemoScene rig missing");
                foreach(var ai in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None)){brains.Add((ai,ai.enabled));ai.enabled=false;}
                RunProbes(fixtures);
                var rig=source.freeLookRig;sourceEnabled=source.enabled;source.enabled=false;
                Vector3 root=source.target.position;float yaw=(float)typeof(BasicCameraFollow).GetField("yaw",Any).GetValue(source),pitch=(float)typeof(BasicCameraFollow).GetField("pitch",Any).GetValue(source);float distance=Mathf.Max(3,rig.ZoomDistance);
                var playbackObject=new GameObject("Camera QA pose driver");fixtures.Add(playbackObject);var playback=playbackObject.AddComponent<CameraBoomQaPlayback>();playback.rig=rig;playback.position=root;playback.yaw=yaw;playback.pitch=pitch;playback.distance=distance;
                Action<bool> render=locked=>playback.locked=locked;
                for(int i=0;i<14;i++){render(false);await Gap();}
                Vector3 origin=rig.CollisionOrigin,clear=source.transform.position,direction=(clear-origin).normalized;
                var wall=Wall(origin+direction*.85f,Quaternion.LookRotation(direction),new Vector3(12,8,.12f));fixtures.Add(wall);
                var viewType=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");view=EditorWindow.GetWindow(viewType);size=viewType.GetProperty("selectedSizeIndex",Any);oldSize=(int)size.GetValue(view);
                var select=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture")).First(t=>t!=null).GetMethod("SelectSize",Any);
                foreach(var res in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    select.Invoke(null,new object[]{view,size,res});view.Focus();for(int i=0;i<12;i++){render(false);await Gap();}
                    float angle=Vector3.Angle(direction,source.transform.position-origin);Check(angle<1,"actual free-camera obstruction preserves boom bearing "+res+" angle="+angle.ToString("F3"));
                    Check(Screen.width==res.x&&Screen.height==res.y,"actual resolution "+res);await Shot("wall-"+res.x);
                }
                var extension=rig.freeCamera.GetComponent<PlayerCameraCollision>();var stored=typeof(PlayerCameraCollision).GetField("recovery",Any).GetValue(extension);Write("pipeline-diagnostics.txt","mainComponents="+string.Join(",",source.GetComponents<Component>().Select(c=>c.GetType().Name))+" freeRig="+(extension.rig==rig)+" live="+rig.brain.ActiveVirtualCamera?.Name+" bodyFrame="+typeof(PlayerCameraCollision).GetField("bodyFrame",Any).GetValue(extension)+" frame="+Time.frameCount+" bodyDistance="+Vector3.Distance(origin,(Vector3)typeof(PlayerCameraCollision).GetField("bodyPosition",Any).GetValue(extension))+" recoveryDistance="+typeof(CameraBoomRecovery).GetField("distance",Any).GetValue(stored));
                playback.ResetMeasurements();wall.SetActive(false);float before=Vector3.Distance(origin,source.transform.position);float largest=0,previous=before;var trace=new System.Text.StringBuilder("observerFrame,deltaTime,distance,stepSinceObservation,smoothing\n");
                for(int i=0;i<65;i++){render(false);await Gap();float now=Vector3.Distance(origin,source.transform.position);largest=Mathf.Max(largest,now-previous);trace.AppendLine(FormattableString.Invariant($"{Time.frameCount},{Time.deltaTime},{now},{now-previous},{source.UserSmoothingEnabled}"));previous=now;}
                Write(Stage+"-recovery.csv",trace.ToString());
                Check(Vector3.Distance(clear,source.transform.position)<.1f,"camera recovers original distance after obstacle removed");
                Check(playback.MaxOutwardStep<.8f,"outward recovery has no one-frame distance jump max="+playback.MaxOutwardStep.ToString("F3")+" observedFrames="+playback.ObservedFrames);
                for(int i=0;i<16;i++){render(true);await Gap();}Vector3 lockRay=(source.transform.position-origin).normalized;
                wall.transform.SetPositionAndRotation(origin+lockRay*.85f,Quaternion.LookRotation(lockRay));wall.SetActive(true);Physics.SyncTransforms();for(int i=0;i<16;i++)await Gap();
                Check(Vector3.Angle(lockRay,source.transform.position-origin)<1,"actual lock-camera obstruction preserves requested shoulder");await Shot("lock-1366");
                Write(Stage+"-status.txt",(checks.Any(c=>c.StartsWith("FAIL"))?"FINDINGS ":"COMPLETE ")+checks.Count+" checks; engine-driven camera poses, not physical camera input");
            }catch(Exception e){Write(Stage+"-status.txt","FAILED\n"+e);Debug.LogException(e);}
            finally{foreach(var go in fixtures)if(go!=null)Object.Destroy(go);foreach(var pair in brains)if(pair.ai!=null)pair.ai.enabled=pair.enabled;if(source!=null){source.enabled=sourceEnabled;source.freeLookRig.freeCamera.PreviousStateIsValid=false;source.freeLookRig.lockCamera.PreviousStateIsValid=false;}if(view!=null&&oldSize>=0)size.SetValue(view,oldSize);SessionState.SetBool(Session,false);running=false;EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+"reload",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+"options",0);if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();}
        }
        static void RunProbes(List<GameObject> fixtures){
            var go=new GameObject("Camera QA solver",typeof(Camera));fixtures.Add(go);go.GetComponent<Camera>().enabled=false;var source=go.AddComponent<BasicCameraFollow>();source.enabled=false;source.target=go.transform;
            Vector3 origin=new Vector3(3000,100,3000),desired=origin+Vector3.back*5;
            Check((source.ConstrainCameraPosition(origin,desired)-desired).sqrMagnitude<.00001f,"clear boom retains requested pose");
            var wall=Wall(origin+Vector3.back*.85f,Quaternion.identity,new Vector3(8,6,.12f));fixtures.Add(wall);
            Vector3 actual=source.ConstrainCameraPosition(origin,desired);float lateral=Vector3.Cross(actual-origin,Vector3.back).magnitude;
            Check(lateral<.001f,"close wall cannot rotate camera sideways lateral="+lateral.ToString("F4"));
            Check(actual.z>wall.transform.position.z,"close wall cannot put camera behind the wall");
            wall.transform.position=origin+Vector3.back*2;Physics.SyncTransforms();actual=source.ConstrainCameraPosition(origin,desired);
            Check(actual.z>wall.transform.position.z+.25f&&Vector3.Cross(actual-origin,Vector3.back).magnitude<.001f,"ordinary wall retracts along requested boom with lens margin");
            wall.GetComponent<Collider>().isTrigger=true;Physics.SyncTransforms();Check((source.ConstrainCameraPosition(origin,desired)-desired).sqrMagnitude<.00001f,"trigger ignored");wall.SetActive(false);
            if(Stage!="after")return;
            var own=Wall(origin+Vector3.back*2,Quaternion.identity,Vector3.one);fixtures.Add(own);own.transform.SetParent(go.transform,true);Physics.SyncTransforms();Check((source.ConstrainCameraPosition(origin,desired)-desired).sqrMagnitude<.00001f,"controlled player collider does not retract camera");own.SetActive(false);
            var body=Wall(origin+Vector3.back*2,Quaternion.identity,Vector3.one);fixtures.Add(body);body.AddComponent<DummyEnemy>().enabled=false;Physics.SyncTransforms();Check((source.ConstrainCameraPosition(origin,desired)-desired).sqrMagnitude<.00001f,"moving enemy collider does not push gameplay camera");body.SetActive(false);
            var recovery=new CameraBoomRecovery();float close=recovery.Step(.15f,5,origin,1f/60,.24f);float first=recovery.Step(5,5,origin,1f/60,.24f);
            Check(close==.15f&&first>.15f&&first<.4f,"pure recovery eases distance instead of teleporting");
            for(int i=0;i<90;i++)recovery.Step(5,5,origin,1f/60,.24f);
            Check(recovery.Step(.2f,5,origin,1f/60,.24f)==.2f,"new obstruction pulls in on same frame");
            Check(recovery.Step(5,5,origin+Vector3.right*20,1f/60,.24f)==5,"target warp resets radial history");
            recovery=default;recovery.Step(.2f,5,origin,1f/60,.24f);Check(recovery.Step(5,5,origin,10,.24f)<1,"long UI/focus pause cannot skip recovery into a zoom-out snap");
            var solve=new CameraBoomCollision(_=>false);wall.GetComponent<Collider>().isTrigger=false;wall.SetActive(true);
            for(int yaw=-180;yaw<180;yaw+=60){Vector3 ray=Quaternion.Euler(14,yaw,0)*Vector3.back;wall.transform.SetPositionAndRotation(origin+ray*2,Quaternion.LookRotation(ray));Physics.SyncTransforms();actual=solve.Resolve(origin,origin+ray*5,.3f,.12f,~0);Check(Vector3.Cross(actual-origin,ray).magnitude<.001f&&(actual-origin).magnitude<2,"radial collision at yaw "+yaw);}
            // The sweep starts intersecting a nearby wall, while the point pivot
            // remains outside it. This is common beside doorframes/corners.
            wall.transform.SetPositionAndRotation(origin+Vector3.back*.2f,Quaternion.identity);Physics.SyncTransforms();
            actual=solve.Resolve(origin,desired,.3f,.12f,~0);
            Check(Vector3.Cross(actual-origin,Vector3.back).magnitude<.001f&&actual.z>=origin.z-.14f,"start-sphere overlap never crosses the nearby wall or changes shoulder");
            wall.transform.position=origin;Physics.SyncTransforms();actual=solve.Resolve(origin,desired,.3f,.12f,~0);
            Check(!solve.HasClearPosition&&actual==origin,"pivot inside solid reports no radial solution instead of teleporting");
            wall.SetActive(false);solve.Resolve(origin,desired,.3f,.12f,~0);
            Check(solve.HasClearPosition&&!solve.QuerySaturated,"solver clears obstruction diagnostics after geometry is removed");
            long allocated=GC.GetAllocatedBytesForCurrentThread();var clock=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<1000;i++)solve.Resolve(origin,desired,.3f,.12f,~0);clock.Stop();long used=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Write("solver-measurement.txt","1000 warmed probes; milliseconds="+clock.Elapsed.TotalMilliseconds+" allocatedBytes="+used+" (includes Stopwatch allocation)");Check(used<1024,"1000 warm probes have no per-query managed allocation");
        }
    }
    [DefaultExecutionOrder(100)]
    internal sealed class CameraBoomQaPlayback:MonoBehaviour {
        internal PlayerFreeLookRig rig;internal Vector3 position;internal float yaw,pitch,distance;internal bool locked;
        internal float MaxOutwardStep;internal int ObservedFrames;float previousDistance;
        internal void ResetMeasurements(){MaxOutwardStep=0;ObservedFrames=0;previousDistance=Vector3.Distance(rig.CollisionOrigin,rig.source.transform.position);}
        void LateUpdate(){if(rig==null||!rig.IsReady)return;rig.Render(position,yaw,pitch,distance,locked,Quaternion.Euler(0,yaw,0),rig.source.lockCameraOffset,position+Vector3.up*rig.source.lookHeight);float now=Vector3.Distance(rig.CollisionOrigin,rig.source.transform.position);MaxOutwardStep=Mathf.Max(MaxOutwardStep,now-previousDistance);previousDistance=now;ObservedFrames++;}
    }
}
#endif
