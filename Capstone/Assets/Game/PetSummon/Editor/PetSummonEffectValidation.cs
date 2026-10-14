using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.PetSummon.Editor
{
    public static class PetSummonEffectValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly Type Effect = typeof(PetSummonReleaseEffect);
        static void Require(bool value,string message) {
            if (!value) throw new InvalidOperationException("3D summon VFX: "+message);
        }
        static object Field(object value,string name) => value.GetType().GetField(name,Private|BindingFlags.Public).GetValue(value);

        [MenuItem("Game Tools/Pets/Validate Summon Effects")]
        public static void Run()
        {
            if (Application.isPlaying) { Debug.LogWarning("Stop Play Mode before validating summon effects."); return; }
            var profile=AssetDatabase.LoadAssetAtPath<PetVFXProfile>(PetSummonVfxSetup.ProfilePath);
            Require(profile != null,"install 3D VFX resources first");
            Require(profile.bodyShader != null && profile.energyShader != null,"missing shader references");
            Require(!ShaderUtil.ShaderHasError(profile.bodyShader) && !ShaderUtil.ShaderHasError(profile.energyShader),"shader compilation errors");
            ValidateEllipsoidContact();
            ValidateWarpAndHalo(profile);
            ValidateGroundClearance();
            var effectObject=new GameObject("SummonVFXValidation");
            var effect=effectObject.AddComponent<PetSummonReleaseEffect>(); effect.ConfigureProfile(profile);
            var owner=new GameObject("SummonVFXValidationOwner");
            var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name="SummonVFXValidationBall"; ball.transform.position=new Vector3(-25,1.2f,2);
            ball.transform.localScale=Vector3.one*.25f;
            var collider=ball.GetComponent<SphereCollider>(); collider.center=new Vector3(.2f,.1f,-.15f);
            Vector3 ballCenter=PetSummonReleaseEffect.ResolveBallCenter(ball.transform,collider);
            try {
                for (int form=1;form<=3;form++) {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/HudSystem/Prefabs/Dragon0{form}Gameplay.prefab");
                    Require(prefab != null,"missing dragon form "+form);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    try {
                        // These visual prefabs can have no authored collider until runtime initialization.
                        if (instance.GetComponentsInChildren<Collider>(true).Length == 0) instance.AddComponent<SphereCollider>();
                        int colliderCount=instance.GetComponentsInChildren<Collider>(true).Length;
                        var pet=instance.GetComponent<PetController>(); pet.AssignOwner(owner.transform);
                        var visual=instance.GetComponent<Capstone.Game.HudSystem.PetEvolutionVisualController>();
                        visual.RestoreForm(form-1);
                        Require(form==1 || pet.animator != instance.GetComponent<Animator>(),"evolved fixture still uses base Animator");
                        PreparePet(pet); instance.transform.position=new Vector3(form*2,0,0);
                        foreach (PetVFXQuality quality in Enum.GetValues(typeof(PetVFXQuality))) {
                            effect.ConfigureQuality(quality);
                            ValidateSpawn(effect,pet,ball,ballCenter);
                            ValidateRecall(effect,pet,ball,ballCenter,quality);
                        }
                        ValidateSequenceReplacement(effect,pet,ball);
                        ValidateModelScaleResponse(effect,pet,ball);
                        // Pool reuse across 20 cycles; neither root instances nor material counts may grow.
                        effect.ConfigureQuality(PetVFXQuality.Medium);
                        var sequence=effect.PlayRecall(pet,ball.transform,()=>{}); sequence.MoveNext();
                        var recalled=effect.ActiveVfx; effect.Cancel(); (sequence as IDisposable)?.Dispose();
                        int materials=effect.CachedMaterialCount;
                        for (int cycle=0;cycle<20;cycle++) {
                            sequence=effect.PlayRecall(pet,ball.transform,()=>{}); sequence.MoveNext();
                            Require(effect.ActiveVfx == recalled,"pool created a new recall instance");
                            effect.PreviewAt(.32f);
                            effect.Cancel(); effect.Cancel(); (sequence as IDisposable)?.Dispose();
                            Require(effect.CachedMaterialCount == materials,"material cache grew on repeated use");
                            Require(!recalled.gameObject.activeSelf,"pooled recall effect stayed active");
                            Require(instance.GetComponentsInChildren<Collider>(true).Length == colliderCount,"fixture changed colliders");
                        }
                        Debug.Log("3D summon form passed: "+prefab.name+"; animator="+pet.animator.name
                            +"; all qualities, contact, dissolve, texture and 20 pooled cycles.");
                    }
                    finally { effect.Cancel(); Object.DestroyImmediate(instance); }
                }
                ValidateDestroyedPet(effect,ball);
                ValidateFacing(owner.transform);
                Debug.Log("3D summon VFX validation passed: all three forms, three quality presets, ball-centred spawn, ellipsoid contact, funnel warp, cancellation and pooling.");
            }
            finally { effect.Cancel(); Object.DestroyImmediate(effectObject); Object.DestroyImmediate(owner); Object.DestroyImmediate(ball); }
        }

        static void PreparePet(PetController pet)
        {
            typeof(PetController).GetProperty("IsSummoned").SetValue(pet,true);
            var visual=pet.GetComponentInChildren<Capstone.Game.HudSystem.PetEvolutionVisualController>(true);
            foreach (Renderer renderer in pet.GetComponentsInChildren<Renderer>(true))
                if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    && renderer.GetComponentInParent<AAMAP.MapIcon>(true) == null
                    && (visual == null || visual.IsCurrentVisualRenderer(renderer))) pet.SetVisualRendererEnabled(renderer,true);
        }

        static void ValidateEllipsoidContact()
        {
            Vector3 center=new Vector3(1,2,3);
            foreach (float scale in new[] { .25f,1f,2.5f }) foreach (Vector3 direction in new[] { Vector3.right,Vector3.up,new Vector3(2,-4,6).normalized }) {
                Vector3 axes=new Vector3(4,.5f,2)*scale;
                Quaternion rotation=Quaternion.Euler(25,67,40);
                Vector3 point=PetSummonVfxInstance.ResolveEllipsoidContact(center+direction*10,center,axes,rotation);
                Vector3 local=Quaternion.Inverse(rotation)*(point-center);
                Require(Mathf.Abs(new Vector3(local.x/axes.x,local.y/axes.y,local.z/axes.z).magnitude-1)<.0001f,
                    "contact is not on ellipsoid surface");
                Require(Vector3.Dot((point-center).normalized,direction)>.9999f,"contact does not face the ball");
                Vector3 inside=PetSummonVfxInstance.ResolveEllipsoidContact(center+direction*.01f,center,axes,rotation);
                Require(Vector3.Distance(point,inside)<.001f,"contact failed when ball is inside halo");
            }
        }

        static void ValidateWarpAndHalo(PetVFXProfile profile)
        {
            Vector3 center=Vector3.zero,ball=Vector3.right*8,extents=new Vector3(2,1,1);
            Vector3 near=new Vector3(2,.5f,0),far=new Vector3(-2,.5f,0);
            var warp=PetRecallWarp.Create(center,ball,extents,Quaternion.identity,.18f,profile);
            Require(Vector3.Distance(warp.TransformPoint(near),near)<.0001f,"warp changed the initial pose");
            warp.time=.28f;
            float nearPull=Vector3.Dot(warp.TransformPoint(near)-near,Vector3.right)/(8-near.x);
            float farPull=Vector3.Dot(warp.TransformPoint(far)-far,Vector3.right)/(8-far.x);
            Require(nearPull>farPull+.01f,"near side was not pulled before far side");
            Require(warp.delay<=.08f && warp.twist<=60*Mathf.Deg2Rad+.0001f,"warp exceeds approved defaults");
            warp.time=.56f;
            Require(Vector3.Distance(warp.TransformPoint(near),ball)<.0001f
                && Vector3.Distance(warp.TransformPoint(far),ball)<.0001f,"warp did not enter ball by .56s");
            warp=PetRecallWarp.Create(center,center,extents,Quaternion.identity,.32f,profile);
            Vector3 close=warp.TransformPoint(far);
            Require(warp.delay==0 && warp.twist==0 && !float.IsNaN(close.x),"coincident ball creates reverse/invalid funnel");
            Vector3 grown=PetRecallWarp.SmoothHalo(Vector3.one,Vector3.one*2.1f,Vector3.one*2,.016f);
            Require(grown==Vector3.one*2.1f,"halo expansion is delayed");
            Vector3 shrunk=PetRecallWarp.SmoothHalo(Vector3.one*1.1f,Vector3.one*1.05f,Vector3.one,.08f);
            Require(Vector3.Distance(shrunk,Vector3.one*(1.05f+.05f/Mathf.Exp(1)))<.0001f,"halo shrink is not .08s exponential");
            Require(PetRecallWarp.SmoothHalo(Vector3.one*10,Vector3.one*1.05f,Vector3.one,.001f).x<=1.10001f,
                "halo instantaneous excess exceeds 10 percent");
        }

        static void ValidateSpawn(PetSummonReleaseEffect effect,PetController pet,GameObject ball,Vector3 center)
        {
            Vector3 scale=pet.transform.localScale;
            IEnumerator sequence=effect.PlaySummon(pet,pet.transform.position,ball.transform,()=>{});
            try {
                Require(sequence.MoveNext(),"spawn did not start");
                var instance=effect.ActiveVfx;
                Require(instance != null && instance.Beam == null,"spawn contains a beam");
                Require(Vector3.Distance(effect.PresentationCenter,center)<.0001f,"spawn is detached from real ball center");
                Require(Mathf.Abs(effect.PresentationScale-.04f)<.0001f,"spawn did not start small");
                Require(!ball.GetComponent<Renderer>().enabled,"ball remained visible during flash");
                foreach (float time in new[] { 0f,.26f,.5f,.64f,.79f }) {
                    effect.PreviewAt(time); ValidateBody(effect,pet,time<.72f);
                    ValidateNoSurroundingHalo(instance);
                    ValidateFlashEnvelope(effect,pet);
                    if (instance.Core.enabled)
                        Require(instance.Core.bounds.min.y>=pet.transform.position.y-.001f,"spawn flash is below landing ground");
                    Require(pet.transform.localScale == scale,"spawn scaled gameplay root");
                    if (instance.Shockwave.enabled) {
                        Vector3 envelope=(Vector3)Field(effect,"bodyHalfExtents")*(1+effect.Profile.burstPadding);
                        Vector3 wave=instance.Shockwave.transform.localScale*.5f;
                        Require(wave.x<=envelope.x+.001f && wave.y<=envelope.y+.001f && wave.z<=envelope.z+.001f,
                            "shockwave exceeds eight percent model envelope");
                    }
                }
                effect.Cancel(); effect.Cancel();
                Require(ball.GetComponent<Renderer>().enabled,"cancel failed to restore the ball");
                Require(!instance.gameObject.activeSelf,"spawn instance did not return to pool");
                Require(!instance.Shockwave.enabled && !instance.BallPulse.enabled,"spawn cancellation left wave/pulse visible");
                ValidateOriginals(pet);
            }
            finally { (sequence as IDisposable)?.Dispose(); effect.Cancel(); }
        }

        static void ValidateRecall(PetSummonReleaseEffect effect,PetController pet,GameObject ball,Vector3 center,PetVFXQuality quality)
        {
            pet.SetRecallLocked(true);
            var sequence=effect.PlayRecall(pet,ball.transform,()=>{});
            try {
                Require(sequence.MoveNext(),"recall did not start");
                var instance=effect.ActiveVfx;
                foreach (float time in new[] { .18f,.32f,.48f }) {
                    effect.PreviewAt(time);
                    Vector3 halo=(Vector3)Field(effect,"haloHalfExtents");
                    ValidateNoSurroundingHalo(instance);
                    Require(instance.Beam.enabled,"beam missing outside ellipsoid");
                    Vector3 endpoint=instance.Beam.GetPosition(instance.Beam.positionCount-1);
                    Vector3 expected=PetSummonVfxInstance.ResolveEllipsoidContact(center,effect.PresentationCenter,
                        halo*effect.PresentationScale,pet.transform.rotation);
                    Require(Vector3.Distance(endpoint,expected)<.001f,"beam misses recall envelope");
                    Require(Vector3.Distance(instance.Beam.GetPosition(0),center)<.0001f,"beam misses held ball");
                    ValidateBody(effect,pet,true);
                    ValidateFlashEnvelope(effect,pet);
                    ValidatePoseEnvelope(effect,pet,time);
                }
                effect.PreviewAt(.32f); // Warm all late-frame paths before allocation measurement.
                int strands=0;
                foreach (var strand in instance.FunnelStrands) if (strand.enabled) {
                    strands++;
                    Require(Vector3.Distance(strand.GetPosition(strand.positionCount-1),center)<.0001f,"funnel misses ball");
                }
                Require(strands==(quality==PetVFXQuality.Low?1:2),"wrong funnel strand budget");
                long before=GC.GetAllocatedBytesForCurrentThread();
                for (int i=0;i<100;i++) effect.PreviewAt(.32f);
                long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
                Require(allocated==0,"presentation update allocated "+allocated+" managed bytes");
                Vector3 savedBall=ball.transform.position;
                effect.PreviewAt(.18f);
                var collider=ball.GetComponent<SphereCollider>();
                ball.transform.position=effect.PresentationCenter+Vector3.right*.01f
                    - collider.transform.TransformVector(collider.center);
                effect.PreviewAt(.18f);
                Require(instance.Beam.enabled,"nearby ball inside halo hid the recall beam");
                Vector3 innerEndpoint=instance.Beam.GetPosition(instance.Beam.positionCount-1);
                Vector3 expectedInner=PetSummonVfxInstance.ResolveEllipsoidContact(ball.transform.TransformPoint(collider.center),
                    effect.PresentationCenter,(Vector3)Field(effect,"haloHalfExtents")*effect.PresentationScale,pet.transform.rotation);
                Require(Vector3.Distance(innerEndpoint,expectedInner)<.001f,"inner beam misses recall envelope");
                ball.transform.position=savedBall;
                effect.Cancel(); ValidateOriginals(pet);
                foreach (var strand in instance.FunnelStrands) Require(!strand.enabled,"cancel left funnel visible");
            }
            finally { (sequence as IDisposable)?.Dispose(); effect.Cancel(); pet.SetRecallLocked(false); }
        }

        static void ValidateNoSurroundingHalo(PetSummonVfxInstance instance)
        {
            Require(!instance.Shell.enabled,"surrounding shell is visible");
            foreach (var orbit in instance.Orbits) Require(!orbit.enabled,"surrounding orbit is visible");
        }

        static void ValidateBody(PetSummonReleaseEffect effect,PetController pet,bool originalHidden)
        {
            var bodies=(IEnumerable)Field(effect,"bodies"); var block=new MaterialPropertyBlock();
            foreach (object binding in bodies) {
                var source=(Renderer)Field(binding,"source"); var glow=(Renderer)Field(binding,"glow");
                Require(source.enabled != originalHidden,"native body visibility mismatch");
                Require(source.GetComponentInParent<AAMAP.MapIcon>(true)==null,"minimap included in body presentation");
                glow.GetPropertyBlock(block,0);
                Require(Mathf.Abs(block.GetFloat("_PresentationScale")-effect.PresentationScale)<.0001f,"shader scale mismatch");
                Material[] originals=source.sharedMaterials, overlays=glow.sharedMaterials;
                for (int i=0;i<overlays.Length;i++) {
                    if (originals.Length==0) continue;
                    Material material=originals[Mathf.Min(i,originals.Length-1)]; if (material==null) continue;
                    string map=material.HasProperty("_BaseMap")?"_BaseMap":material.HasProperty("_MainTex")?"_MainTex":null;
                    if (map == null) continue;
                    Require(material.GetTexture(map)==overlays[i].GetTexture("_BaseMap"),"wrong texture");
                    Require(material.GetTextureScale(map)==overlays[i].GetTextureScale("_BaseMap")
                        && material.GetTextureOffset(map)==overlays[i].GetTextureOffset("_BaseMap"),"UV transform lost");
                }
            }
        }

        static void ValidatePoseEnvelope(PetSummonReleaseEffect effect,PetController pet,float time)
        {
            Vector3 center=(Vector3)Field(effect,"bodyCenter");
            Vector3 halo=(Vector3)Field(effect,"haloHalfExtents");
            var warp=(PetRecallWarp)Field(effect,"recallWarp");
            foreach (object binding in (IEnumerable)Field(effect,"bodies")) {
                var source=(Renderer)Field(binding,"source"); var glow=(Renderer)Field(binding,"glow");
                foreach (Vector3 vertex in (System.Collections.Generic.List<Vector3>)Field(binding,"poseVertices")) {
                    Vector3 world=source.transform.TransformPoint(vertex);
                    Vector3 local=Quaternion.Inverse(pet.transform.rotation)*(world-center);
                    Require(new Vector3(local.x/halo.x,local.y/halo.y,local.z/halo.z).sqrMagnitude<=1.0001f,
                        "posed mesh protrudes through ellipsoid");
                    Require(glow.bounds.Contains(warp.TransformPoint(world)),"funnel vertex escapes camera culling bounds");
                }
            }
        }

        static void ValidateSequenceReplacement(PetSummonReleaseEffect effect,PetController pet,GameObject ball)
        {
            IEnumerator oldSequence=effect.PlayRecall(pet,ball.transform,()=>{});
            IEnumerator newSequence=null;
            float originalSpeed=pet.animator.speed;
            try {
                Require(oldSequence.MoveNext(),"first replacement sequence did not start");
                newSequence=effect.PlayRecall(pet,ball.transform,()=>{});
                Require(newSequence.MoveNext(),"replacement sequence did not start");
                effect.PreviewAt(.32f);
                var current=effect.ActiveVfx;
                Vector3 center=effect.PresentationCenter;
                Require(!oldSequence.MoveNext(),"cancelled coroutine continued running");
                Require(effect.ActiveVfx==current && current.gameObject.activeSelf,"old coroutine cleaned up new effect");
                Require(Mathf.Abs((float)Field(effect,"effectTime")-.32f)<.0001f
                    && effect.PresentationCenter==center,"old coroutine mutated replacement presentation");
                // Recall does not own Animator speed and must not undo another system's update.
                pet.animator.speed=.9f;
                effect.Cancel();
                Require(Mathf.Abs(pet.animator.speed-.9f)<.0001f,"recall cleanup overwrote Animator speed");
                ValidateOriginals(pet);
            }
            finally {
                (oldSequence as IDisposable)?.Dispose(); (newSequence as IDisposable)?.Dispose();
                effect.Cancel(); pet.animator.speed=originalSpeed;
            }
        }

        static void ValidateFlashEnvelope(PetSummonReleaseEffect effect,PetController pet)
        {
            var core=effect.ActiveVfx.Core;
            if (!core.enabled) return;
            Vector3 model=(Vector3)Field(effect,"bodyHalfExtents")*effect.PresentationScale;
            Vector3 visible=core.transform.localScale*.5f;
            float margin=1+effect.Profile.burstPadding;
            Require(visible.x<=model.x*margin+.0001f && visible.y<=model.y*margin+.0001f
                && visible.z<=model.z*margin+.0001f,"flash exceeds model envelope plus padding");
            Require(Quaternion.Angle(core.transform.rotation,pet.transform.rotation)<.001f,"flash lost model orientation");
        }

        static void ValidateGroundClearance()
        {
            foreach (float scale in new[] { .25f,1f,2.5f })
                foreach (Quaternion rotation in new[] { Quaternion.identity,Quaternion.Euler(25,67,40) }) {
                    Vector3 extents=new Vector3(2,1,3)*scale;
                    float floor=-94f;
                    Vector3 center=PetSummonVfxInstance.KeepFlashAboveGround(new Vector3(3,floor+.1f,5),extents,rotation,floor);
                    for (int latitude=0;latitude<=12;latitude++) for (int longitude=0;longitude<24;longitude++) {
                        float a=latitude*Mathf.PI/12, b=longitude*Mathf.PI/12;
                        Vector3 surface=center+rotation*Vector3.Scale(extents,
                            new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b)));
                        Require(surface.y>=floor,"rotated flash clips below ground");
                    }
                    Vector3 airborne=new Vector3(3,100,5);
                    Require(PetSummonVfxInstance.KeepFlashAboveGround(airborne,extents,rotation,floor)==airborne,
                        "ground clearance moved an already airborne flash");
                }
        }

        static void ValidateModelScaleResponse(PetSummonReleaseEffect effect,PetController pet,GameObject ball)
        {
            Transform previousParent=pet.transform.parent;
            Vector3 previousScale=pet.transform.localScale, previousPosition=pet.transform.localPosition;
            Quaternion previousRotation=pet.transform.localRotation;
            var parent=new GameObject("VfxScaleValidationParent");
            parent.transform.SetParent(previousParent,false);
            pet.transform.SetParent(parent.transform,false);
            pet.transform.localRotation=Quaternion.identity;
            try {
                Vector3 baseline=MeasureModel(effect,pet,ball);
                foreach (float factor in new[] { .25f,1f,2.5f }) {
                    pet.transform.localScale=previousScale*factor;
                    Vector3 measured=MeasureModel(effect,pet,ball);
                    Require(Vector3.Distance(measured,baseline*factor)<.002f,
                        $"model sizing ignored scale {factor}: measured={measured:F4}, expected={baseline*factor:F4}");
                }
                pet.transform.localScale=previousScale;
                pet.transform.localRotation=Quaternion.Euler(0,67,0);
                Require(Vector3.Distance(MeasureModel(effect,pet,ball),baseline)<.002f,
                    "turning the pet inflated the flash");
                pet.transform.localRotation=Quaternion.identity;
                parent.transform.localScale=new Vector3(2,.75f,1.5f);
                Require(Vector3.Distance(MeasureModel(effect,pet,ball),Vector3.Scale(baseline,parent.transform.localScale))<.002f,
                    "model sizing ignored nonuniform parent scale");
            }
            finally {
                effect.Cancel(); pet.transform.SetParent(previousParent,false);
                pet.transform.localScale=previousScale; pet.transform.localRotation=previousRotation;
                pet.transform.localPosition=previousPosition; Object.DestroyImmediate(parent);
            }
        }

        static Vector3 MeasureModel(PetSummonReleaseEffect effect,PetController pet,GameObject ball)
        {
            var sequence=effect.PlayRecall(pet,ball.transform,()=>{});
            try {
                Require(sequence.MoveNext(),"scale measurement did not start");
                effect.PreviewAt(.32f); ValidateFlashEnvelope(effect,pet);
                return (Vector3)Field(effect,"bodyHalfExtents");
            }
            finally { effect.Cancel(); (sequence as IDisposable)?.Dispose(); }
        }

        static void ValidateDestroyedPet(PetSummonReleaseEffect effect,GameObject ball)
        {
            GameObject model=GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name="DestroyedPetVfxValidation";
            var pet=model.AddComponent<PetController>();
            PreparePet(pet);
            IEnumerator sequence=effect.PlayRecall(pet,ball.transform,()=>{});
            try {
                Require(sequence.MoveNext(),"destroyed-pet sequence did not start");
                var current=effect.ActiveVfx;
                Object.DestroyImmediate(model);
                effect.Cancel();
                Require(effect.ActiveVfx==null && !current.gameObject.activeSelf,"destroyed pet left active VFX");
            }
            finally {
                effect.Cancel(); (sequence as IDisposable)?.Dispose();
                if (model!=null) Object.DestroyImmediate(model);
            }
        }

        static void ValidateOriginals(PetController pet)
        {
            foreach (Renderer renderer in pet.GetComponentsInChildren<Renderer>(true))
                if (renderer.name != "PetSummon_BodyGlow" && pet.ShouldShowVisualRenderer(renderer))
                    Require(renderer.enabled,"current form stayed hidden after cancel");
        }

        static void ValidateFacing(Transform owner)
        {
            var root=new GameObject("RecallFacingValidationPet"); var enemyObject=new GameObject("RecallFacingValidationEnemy");
            try {
                var pet=root.AddComponent<PetController>(); var enemy=enemyObject.AddComponent<DummyEnemy>();
                pet.AssignOwner(owner); typeof(PetController).GetField("target",Private).SetValue(pet,enemy);
                typeof(PetController).GetField("hasPendingHit",Private).SetValue(pet,true); pet.SetRecallLocked(true);
                Require((bool)Field(pet,"recallWasInCombat"),"combat facing lost before lock");
                Require(!(bool)Field(pet,"hasPendingHit"),"recall retained pending damage");
                pet.SetRecallVisualCenter(new Vector3(1,0,0));
                typeof(DummyEnemy).GetField("alive",Private).SetValue(enemy,false);
                Quaternion last=pet.transform.rotation;
                typeof(PetController).GetMethod("UpdateRecallFacing",Private).Invoke(pet,null);
                typeof(DummyEnemy).GetField("alive",Private).SetValue(enemy,true);
                enemy.transform.position=new Vector3(50,0,20);
                typeof(PetController).GetMethod("UpdateRecallFacing",Private).Invoke(pet,null);
                Require(Quaternion.Angle(last,pet.transform.rotation)<.001f,"lost/dead target changed final heading");
                pet.SetRecallLocked(false); pet.SetRecallLocked(true);
                Require(!(bool)Field(pet,"recallWasInCombat"),"idle recall retained old enemy");
                Require(!(bool)Field(pet,"hasRecallVisualCenter"),"recall retained old visual center");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(enemyObject); }
        }

        static double previewDeadline;
        [MenuItem("Game Tools/Pets/Pause Next Summon Effect")]
        static void PauseNextEffect()
        {
            if (!EditorApplication.isPlaying) { Debug.Log("Enter Play Mode before arming frame preview."); return; }
            previewDeadline=EditorApplication.timeSinceStartup+60;
            EditorApplication.update-=WatchEffect; EditorApplication.update+=WatchEffect;
        }
        static void WatchEffect()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup>previewDeadline) { EditorApplication.update-=WatchEffect; return; }
            foreach (var effect in Object.FindObjectsByType<PetSummonReleaseEffect>(FindObjectsSortMode.None)) {
                if (effect.ActiveVfx == null) continue;
                bool spawn=(bool)Field(effect,"spawning"); float progress=spawn?effect.PresentationScale:effect.CaptureProgress;
                if (progress<.3f || progress>.75f) continue;
                EditorApplication.update-=WatchEffect; EditorApplication.isPaused=true;
                Debug.Log("3D summon frame preview: "+(spawn?"Spawn":"Recall")+" "+progress.ToString("F3")); return;
            }
        }
    }
}
