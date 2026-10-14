using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.PetSummon.Editor
{
    // Explicit, temporary fixtures. Never joins the player's party or writes a save/scene.
    public sealed class PetSummonPlayModeValidation
    {
        static PetSummonPlayModeValidation activeRunner;
        PetSummonReleaseEffect effect;
        PetController pet;
        GameObject ball, owner;
        DummyEnemy combatEnemy;
        bool enemyLost;
        Quaternion lostHeading;

        static PetSummonPlayModeValidation()
        {
            EditorApplication.playModeStateChanged+=state => {
                if (state==PlayModeStateChange.ExitingPlayMode) StopFixture();
            };
            AssemblyReloadEvents.beforeAssemblyReload+=StopFixture;
        }

        static void StopFixture()
        {
            var runner=activeRunner; if (runner==null) return;
            if (runner.effect != null) { runner.effect.Cancel(); runner.effect.StopAllCoroutines(); }
            runner.CleanUp(true);
            if (runner.effect != null) UnityEngine.Object.DestroyImmediate(runner.effect.gameObject);
            activeRunner=null;
        }

        [MenuItem("Game Tools/Pets/Clear Temporary VFX Previews")]
        static void ClearStalePreviews()
        {
            if (Application.isPlaying) { Debug.LogWarning("Stop Play Mode before clearing temporary previews."); return; }
            foreach (var candidate in Resources.FindObjectsOfTypeAll<PetSummonReleaseEffect>())
                if (candidate.gameObject.name=="TemporaryPetVfxValidation" && !EditorUtility.IsPersistent(candidate)) {
                    candidate.Cancel(); UnityEngine.Object.DestroyImmediate(candidate.gameObject);
                }
            foreach (var item in Resources.FindObjectsOfTypeAll<GameObject>()) {
                if (item==null || EditorUtility.IsPersistent(item)) continue;
                bool fixture=item.name=="TemporaryPetVfxOwner" || item.name=="TemporaryPetVfxBall"
                    || item.name=="TemporaryVfxDragon01" || item.name=="TemporaryVfxDragon02" || item.name=="TemporaryVfxDragon03";
                bool stalePool=item.name=="PetSummon_VfxPool" && (item.hideFlags & HideFlags.DontSave)!=0;
                if (fixture || stalePool) UnityEngine.Object.DestroyImmediate(item);
            }
            activeRunner=null;
            Debug.Log("Temporary VFX preview resources cleared.");
        }

        [MenuItem("Game Tools/Pets/Run Play Mode VFX Validation")]
        static void RunAll() => Begin(0);
        [MenuItem("Game Tools/Pets/Validate Recall Combat Facing %#F9")]
        static void RunCombat() => Begin(-1);
        [MenuItem("Game Tools/Pets/Preview 3D VFX/Dusk")]
        static void PreviewDusk() => Begin(1);
        [MenuItem("Game Tools/Pets/Preview 3D VFX/Nightfall")]
        static void PreviewNightfall() => Begin(2);
        [MenuItem("Game Tools/Pets/Preview 3D VFX/Darkness %#F8")]
        static void PreviewDarkness() => Begin(3);

        static void Begin(int previewForm)
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().name != "TestChar") {
                Debug.LogWarning("Enter Play Mode in TestChar before running VFX fixtures."); return;
            }
            if (activeRunner != null && activeRunner.effect != null) {
                Debug.LogWarning("A temporary VFX fixture is already running. Resume it or stop Play Mode."); return;
            }
            var host=new GameObject("TemporaryPetVfxValidation") { hideFlags=HideFlags.DontSave };
            host.SetActive(false);
            var runner=new PetSummonPlayModeValidation(); activeRunner=runner;
            runner.effect=host.AddComponent<PetSummonReleaseEffect>();
            runner.effect.ConfigureProfile(AssetDatabase.LoadAssetAtPath<PetVFXProfile>(PetSummonVfxSetup.ProfilePath));
            host.SetActive(true);
            runner.effect.StartCoroutine(runner.Run(previewForm));
        }

        IEnumerator Run(int previewForm)
        {
            var player=UnityEngine.Object.FindFirstObjectByType<PetSummonDirector>();
            Require(player != null,"player director missing");
            owner=new GameObject("TemporaryPetVfxOwner") { hideFlags=HideFlags.DontSave };
            owner.transform.SetPositionAndRotation(player.transform.position,player.transform.rotation);
            ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name="TemporaryPetVfxBall"; ball.hideFlags=HideFlags.DontSave;
            ball.transform.localScale=Vector3.one*.22f;
            ball.GetComponent<Collider>().enabled=false;
            try {
                for (int form=1;form<=3;form++) {
                    if (previewForm > 0 && form != previewForm) continue;
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/HudSystem/Prefabs/Dragon0{form}Gameplay.prefab");
                    pet=UnityEngine.Object.Instantiate(prefab).GetComponent<PetController>();
                    pet.gameObject.name="TemporaryVfxDragon0"+form; pet.gameObject.hideFlags=HideFlags.DontSave;
                    Require(Array.IndexOf(UnityEngine.Object.FindObjectsByType<PetController>(FindObjectsInactive.Include,
                        FindObjectsSortMode.None),pet)<0,"fixture is visible to scene-pet/save discovery");
                    pet.GetComponent<Capstone.Game.HudSystem.PetEvolutionVisualController>().RestoreForm(form-1);
                    Require(form==1 || pet.animator != pet.GetComponent<Animator>(),"evolved fixture still uses base Animator");
                    pet.AssignOwner(owner.transform);
                    Vector3 originalScale=pet.transform.localScale;
                    Vector3 ground=FindLanding(pet,owner.transform);
                    pet.Withdraw();
                    foreach (PetVFXQuality quality in Enum.GetValues(typeof(PetVFXQuality))) {
                        if (previewForm != 0 && quality != PetVFXQuality.Medium) continue;
                        foreach (float scale in previewForm==0 && quality==PetVFXQuality.Medium
                            ? new[] { .25f,1f,2.5f } : new[] { 1f }) {
                            pet.transform.localScale=originalScale*scale;
                            ground=FindLanding(pet,owner.transform); pet.Withdraw();
                            effect.ConfigureQuality(quality);
                            ball.transform.position=ground+Vector3.up*.5f;
                            bool released=false;
                            IEnumerator spawn=effect.PlaySummon(pet,ground,ball.transform,()=> {
                                released=pet.TrySummonAt(ground,owner.transform.position-ground,true);
                                Require(released,"safe landing became invalid");
                            },ground);
                            yield return Pump(spawn,previewForm > 0,true);
                            Require(released && pet.IsSummoned,"spawn did not complete");
                            Require(effect.ActiveVfx == null,"spawn effect stayed active");
                            Require(ball.GetComponent<Renderer>().enabled,"spawn did not restore ball for reuse");
                            yield return null;
                            Require(pet.CanReceiveCommands,"Cast Spell did not finish");
                            if (previewForm<0) {
                                var target=new GameObject("TemporaryVfxCombatTarget") { hideFlags=HideFlags.DontSave };
                                combatEnemy=target.AddComponent<DummyEnemy>(); combatEnemy.enabled=false;
                                target.transform.position=pet.transform.position+Vector3.right*8;
                                enemyLost=false;
                                pet.CommandAttack(combatEnemy);
                                Require(pet.HasCombatTarget,"fixture could not acquire combat target");
                            }
                            ball.transform.position=owner.transform.position+Vector3.up*1.2f;
                            pet.SetRecallLocked(true);
                            bool withdrawn=false;
                            float started=Time.time;
                            IEnumerator recall=effect.PlayRecall(pet,ball.transform,()=> { withdrawn=true; pet.Withdraw(); });
                            yield return Pump(recall,previewForm > 0,false);
                            pet.SetRecallLocked(false);
                            Require(withdrawn && !pet.IsSummoned,"recall did not withdraw pet");
                            Require(effect.ActiveVfx == null,"recall effect stayed active");
                            Require(Time.time-started >= .65f && Time.time-started < .65f+Time.maximumDeltaTime*2,
                                "recall timing diverged from default 1x");
                            if (combatEnemy!=null) {
                                Require(enemyLost,"combat recall did not exercise target loss");
                                UnityEngine.Object.Destroy(combatEnemy.gameObject); combatEnemy=null;
                            }
                            Debug.Log($"Play Mode VFX passed: Dragon0{form}, {quality}, scale={scale}; animator={pet.animator.name}; complete Cast Spell, spawn and recall.");
                        }
                    }
                    UnityEngine.Object.Destroy(pet.gameObject); pet=null;
                    yield return null;
                }
                Debug.Log("Play Mode VFX validation passed: "+(previewForm==0?"all three forms and all qualities"
                    :previewForm<0?"moving combat target, target loss and command lock for all three forms":"3D frame preview"));
            }
            finally {
                CleanUp();
                if (effect != null) UnityEngine.Object.Destroy(effect.gameObject);
                if (ReferenceEquals(activeRunner,this)) activeRunner=null;
            }
        }

        IEnumerator Pump(IEnumerator sequence,bool pause,bool spawning)
        {
            bool paused=false;
            Vector3 ballOrigin=ball.transform.position;
            float started=Time.time;
            try {
                while (sequence.MoveNext()) {
                    if (!pause && !spawning)
                        ball.transform.position=ballOrigin+owner.transform.right*(.2f*Mathf.Sin((Time.time-started)*6));
                    if (!spawning && combatEnemy!=null) {
                        combatEnemy.transform.position=pet.transform.position+Vector3.right*8
                            +Vector3.forward*(1.5f*Mathf.Sin((Time.time-started)*8));
                        if (!enemyLost && effect.CaptureProgress>=.45f) {
                            enemyLost=true; lostHeading=pet.transform.rotation;
                            // Mark the fixture lost without damage/rewards or affecting the real enemy registry.
                            typeof(DummyEnemy).GetField("alive",System.Reflection.BindingFlags.Instance
                                | System.Reflection.BindingFlags.NonPublic).SetValue(combatEnemy,false);
                        }
                    }
                    float progress=spawning?effect.PresentationScale:effect.CaptureProgress;
                    if (pause && !paused && effect.ActiveVfx != null && progress>=.35f) {
                        paused=true; EditorApplication.isPaused=true;
                        Selection.activeGameObject=pet.gameObject;
                        Debug.Log("VFX frame preview paused: "+(spawning?"Spawn":"Recall")+"; press Pause to continue.");
                    }
                    yield return sequence.Current;
                    // Compare with the pose rendered this frame, after Animator and VFX LateUpdate.
                    yield return endOfFrame;
                    if (effect.ActiveVfx != null) ValidateLivePose(spawning);
                    if (!spawning && combatEnemy!=null && pet.IsSummoned) {
                        if (enemyLost) Require(Quaternion.Angle(lostHeading,pet.transform.rotation)<.001f,
                            "dead target changed last recall heading");
                        else {
                            Vector3 desired=combatEnemy.TargetPosition-effect.PresentationCenter; desired.y=0;
                            Require(Vector3.Angle(pet.transform.forward,desired)<20,
                                "recalling pet stopped facing moving combat target");
                        }
                        Require(!pet.CanReceiveCommands,"combat recall accepted new commands");
                    }
                }
            }
            finally { (sequence as IDisposable)?.Dispose(); }
        }

        readonly WaitForEndOfFrame endOfFrame=new WaitForEndOfFrame();
        readonly Mesh verificationMesh=new Mesh { name="SummonValidationPose",hideFlags=HideFlags.DontSave };
        readonly MaterialPropertyBlock verificationProperties=new MaterialPropertyBlock();
        readonly System.Collections.Generic.List<Vector3> verificationVertices=new System.Collections.Generic.List<Vector3>();

        void ValidateLivePose(bool spawning)
        {
            var visual=pet.GetComponent<Capstone.Game.HudSystem.PetEvolutionVisualController>();
            foreach (var skin in pet.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                if (skin.name=="PetSummon_BodyGlow" || !visual.IsCurrentVisualRenderer(skin)) continue;
                skin.BakeMesh(verificationMesh,true); verificationMesh.RecalculateBounds();
                verificationMesh.GetVertices(verificationVertices);
                Quaternion inverse=Quaternion.Inverse(pet.transform.rotation);
                Bounds posed=default; bool found=false;
                foreach (Vector3 vertex in verificationVertices) {
                    Vector3 point=inverse*(skin.transform.TransformPoint(vertex)-pet.transform.position);
                    if (!found) { posed=new Bounds(point,Vector3.zero); found=true; } else posed.Encapsulate(point);
                }
                Vector3 expected=pet.transform.position+pet.transform.rotation*posed.center;
                var overlay=skin.transform.Find("PetSummon_BodyGlow").GetComponent<Renderer>();
                overlay.GetPropertyBlock(verificationProperties,0);
                Vector3 actual=verificationProperties.GetVector("_BodyCenter");
                Require(Vector3.Distance(expected,actual)<.02f,"body anchor retained an earlier animation pose");
                if (!spawning && effect.CaptureProgress < .99f && effect.ActiveVfx.Shell.enabled) {
                    var shell=effect.ActiveVfx.Shell.transform;
                    Vector3 axes=shell.localScale*.5f/Mathf.Max(.001f,effect.PresentationScale);
                    foreach (Vector3 vertex in verificationVertices) {
                        Vector3 world=skin.transform.TransformPoint(vertex);
                        Vector3 local=inverse*(world-actual);
                        Require(new Vector3(local.x/axes.x,local.y/axes.y,local.z/axes.z).sqrMagnitude<=1.01f,
                            "animated pose protrudes through fitted halo");
                        Vector4 settings=verificationProperties.GetVector("_RecallWarp");
                        Vector4 axis=verificationProperties.GetVector("_RecallAxis");
                        var warp=new PetRecallWarp {
                            enabled=settings.x>.5f,center=actual,ball=verificationProperties.GetVector("_RecallBall"),
                            axis=new Vector3(axis.x,axis.y,axis.z),axialExtent=axis.w,
                            time=settings.y,delay=settings.z,twist=settings.w
                        };
                        Require(overlay.bounds.Contains(warp.TransformPoint(world)),"animated funnel escaped culling bounds");
                    }
                    if (effect.ActiveVfx.Beam.enabled) Require(Vector3.Distance(effect.ActiveVfx.Beam.GetPosition(0),
                        PetSummonReleaseEffect.ResolveBallCenter(ball.transform,ball.GetComponent<SphereCollider>()))<.001f,
                        "recall beam did not follow moving ball");
                }
            }
            if (spawning && effect.ActiveVfx.Core.enabled)
                Require(effect.ActiveVfx.Core.bounds.min.y>=pet.transform.position.y-.01f,
                    "spawn flash sank below landing ground");
        }

        static Vector3 FindLanding(PetController pet,Transform owner)
        {
            for (int ring=0;ring<3;ring++) for (int i=0;i<12;i++) {
                Vector3 offset=Quaternion.Euler(0,i*30,0)*owner.forward*(3+ring);
                if (pet.TrySummonAt(owner.position+offset,-offset,true)) return pet.transform.position;
            }
            throw new InvalidOperationException("VFX fixture could not find a safe landing near player.");
        }

        static void Require(bool value,string message) {
            if (!value) throw new InvalidOperationException("Play Mode VFX: "+message);
        }

        void CleanUp(bool immediate=false)
        {
            if (effect != null) effect.Cancel();
            if (verificationMesh!=null) DestroyFixture(verificationMesh,immediate);
            if (pet != null) { pet.SetRecallLocked(false); DestroyFixture(pet.gameObject,immediate); pet=null; }
            if (ball != null) { DestroyFixture(ball,immediate); ball=null; }
            if (owner != null) { DestroyFixture(owner,immediate); owner=null; }
            if (combatEnemy!=null) { DestroyFixture(combatEnemy.gameObject,immediate); combatEnemy=null; }
        }
        static void DestroyFixture(UnityEngine.Object item,bool immediate) {
            if (immediate) UnityEngine.Object.DestroyImmediate(item); else UnityEngine.Object.Destroy(item);
        }
    }
}
