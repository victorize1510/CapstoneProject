using UnityEngine;
using UnityEngine.Rendering;

namespace Capstone.Game.PetSummon
{
    // One reusable presentation instance. It never owns a gameplay collider or Animator.
    public sealed class PetSummonVfxInstance : MonoBehaviour
    {
        [SerializeField] MeshRenderer shell;
        [SerializeField] MeshRenderer core;
        [SerializeField] MeshRenderer contact;
        [SerializeField] LineRenderer[] orbits;
        [SerializeField] LineRenderer groundRing;
        [SerializeField] LineRenderer beam;
        [SerializeField] LineRenderer[] funnelStrands;
        [SerializeField] MeshRenderer shockwave;
        [SerializeField] MeshRenderer ballPulse;
        [SerializeField] ParticleSystem burst;
        [SerializeField] ParticleSystem materialize;
        [SerializeField] ParticleSystem sparkles;
        [SerializeField] Light energyLight;
        readonly ParticleSystem.Particle[] attractionBuffer = new ParticleSystem.Particle[48];
        readonly Vector3[] unitCircle = new Vector3[128];
        readonly Vector3[] circlePositions = new Vector3[128];
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int GroundId = Shader.PropertyToID("_GroundY");
        static readonly int ClipGroundId = Shader.PropertyToID("_ClipGround");
        MaterialPropertyBlock properties;
        Material shellMaterial, coreMaterial, particleMaterial, ribbonMaterial, waveMaterial;
        PetVFXProfile profile;
        PetVFXQuality quality;
        Color primary, secondary;
        bool recall, emittedBody, emittedFinish;
        int count, segments, cachedSegments;
        float particleRadius, burstMargin, spawnFloorY;
        Vector3 groundPoint;
        Quaternion bodyRotation;

        public MeshRenderer Shell => shell;
        public MeshRenderer Core => core;
        public LineRenderer Beam => beam;
        public LineRenderer[] Orbits => orbits;
        public LineRenderer[] FunnelStrands => funnelStrands;
        public MeshRenderer Shockwave => shockwave;
        public MeshRenderer BallPulse => ballPulse;

        public bool BuildLayout(bool forRecall)
        {
            bool changed = shell == null;
            if (shell == null) {
                shell = Sphere("EnergyShell"); core = Sphere("EnergyCore");
                if (forRecall) contact = Sphere("ContactFlare");
                orbits = new LineRenderer[3];
                for (int i = 0; i < 3; i++) orbits[i] = Line("EnergyOrbit" + (i + 1), true);
                groundRing = Line("ExpandingGroundRing", true);
                if (forRecall) beam = Line("ReturnBeam", false);
                burst = Particles(forRecall ? "AttractionParticles" : "ParticleBurst");
                materialize = Particles(forRecall ? "DissolveParticles" : "MaterializeParticles");
                sparkles = Particles(forRecall ? "FinalSparkle" : "FinishSparkles");
                var lightObject = new GameObject("EnergyLight"); lightObject.transform.SetParent(transform, false);
                energyLight = lightObject.AddComponent<Light>(); energyLight.type = LightType.Point;
                energyLight.shadows = LightShadows.None;
            }
            if (forRecall && (funnelStrands == null || funnelStrands.Length != 2)) {
                funnelStrands = new[] { Line("FunnelStrand1", false), Line("FunnelStrand2", false) };
                changed = true;
            }
            if (!forRecall && shockwave == null) { shockwave = Sphere("BurstWave"); changed = true; }
            if (ballPulse == null) { ballPulse = Sphere("BallPulse"); changed = true; }
            changed |= ConfigureParticleStreak(burst);
            changed |= ConfigureParticleStreak(materialize);
            changed |= ConfigureParticleStreak(sparkles);
            if (changed) ResetPresentation();
            return changed;
        }

        MeshRenderer Sphere(string objectName)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = objectName; go.transform.SetParent(transform, false);
            Collider collider = go.GetComponent<Collider>(); collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return renderer;
        }

        LineRenderer Line(string objectName, bool loop)
        {
            var go = new GameObject(objectName); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); line.loop = loop; line.useWorldSpace = true;
            line.positionCount = loop ? 128 : 2; line.numCornerVertices = 2; line.numCapVertices = 3;
            line.textureMode = LineTextureMode.Stretch; line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false; return line;
        }

        ParticleSystem Particles(string objectName)
        {
            var go = new GameObject(objectName); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 48;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere;
            var colour = ps.colorOverLifetime; colour.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.7f, .5f), new GradientAlphaKey(0, 1) });
            colour.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return ps;
        }

        static bool ConfigureParticleStreak(ParticleSystem system)
        {
            var renderer=system.GetComponent<ParticleSystemRenderer>();
            var size=system.sizeOverLifetime;
            bool changed=renderer.renderMode!=ParticleSystemRenderMode.Stretch
                || renderer.lengthScale!=2.2f || renderer.velocityScale!=.12f || !size.enabled;
            if (!changed) return false;
            renderer.renderMode=ParticleSystemRenderMode.Stretch;
            renderer.lengthScale=2.2f; renderer.velocityScale=.12f;
            size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            return true;
        }

        public void Warmup(PetVFXProfile settings)
        {
            properties ??= new MaterialPropertyBlock();
            BuildLayout(beam != null || contact != null);
            if (shellMaterial != null) return;
            Shader shader = settings != null && settings.energyShader != null
                ? settings.energyShader : Shader.Find("Capstone/PetSummonEnergy");
            if (shader == null) throw new System.InvalidOperationException("Pet summon energy shader is missing.");
            shellMaterial = MakeMaterial(shader, 0); coreMaterial = MakeMaterial(shader, 1);
            particleMaterial = MakeMaterial(shader, 2); ribbonMaterial = MakeMaterial(shader, 3);
            waveMaterial = MakeMaterial(shader,4);
            shell.sharedMaterial = shellMaterial; core.sharedMaterial = coreMaterial;
            if (contact != null) contact.sharedMaterial = coreMaterial;
            for (int i = 0; i < orbits.Length; i++) orbits[i].sharedMaterial = ribbonMaterial;
            groundRing.sharedMaterial = ribbonMaterial;
            if (beam != null) beam.sharedMaterial = ribbonMaterial;
            if (funnelStrands != null) foreach (var strand in funnelStrands) strand.sharedMaterial = ribbonMaterial;
            if (shockwave != null) shockwave.sharedMaterial = waveMaterial;
            if (ballPulse != null) ballPulse.sharedMaterial = coreMaterial;
            burst.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
            materialize.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
            sparkles.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
        }

        static Material MakeMaterial(Shader shader, float shape)
        {
            var material = new Material(shader) { hideFlags = HideFlags.DontSave, name = "PetSummon_PooledEnergy" };
            material.SetFloat(ShapeId, shape); return material;
        }

        public void SetGroundPoint(Vector3 position) {
            spawnFloorY = position.y;
            groundPoint = position + Vector3.up * .025f;
        }

        public void Begin(bool forRecall, PetVFXProfile settings, PetVFXQuality preset, Vector3 center,
            Vector3 halfExtents, float speed = 1f)
        {
            Warmup(settings); ResetPresentation();
            profile = settings; quality = preset; recall = forRecall;
            Vector3 modelHalfExtents = Vector3.Max(halfExtents,Vector3.one*.001f);
            particleRadius = Mathf.Min(modelHalfExtents.x,Mathf.Min(modelHalfExtents.y,modelHalfExtents.z));
            burstMargin = 1+Mathf.Clamp(settings != null ? settings.burstPadding : .08f,0,.2f);
            bodyRotation = Quaternion.identity;
            primary = settings != null ? settings.primaryColor : new Color(.72f, 1, 1, 1);
            secondary = settings != null ? settings.secondaryColor : new Color(.45f, 1, .78f, 1);
            count = Mathf.RoundToInt(PetVFXProfile.ParticleBudget(preset) * (settings != null ? settings.particleAmount : 1));
            segments = PetVFXProfile.SegmentCount(preset);
            if (segments != cachedSegments) {
                for (int i=0;i<segments;i++) {
                    float angle=i*Mathf.PI*2/segments;
                    unitCircle[i]=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                }
                cachedSegments=segments;
            }
            for (int i = 0; i < orbits.Length; i++) orbits[i].positionCount = segments;
            groundRing.positionCount = segments;
            if (beam != null) beam.positionCount = 2;
            if (funnelStrands != null) foreach (var strand in funnelStrands) strand.positionCount = segments;
            transform.position = center; gameObject.SetActive(true);
            var main = burst.main; main.simulationSpeed = Mathf.Max(.1f,speed);
            main = materialize.main; main.simulationSpeed = Mathf.Max(.1f,speed);
            main = sparkles.main; main.simulationSpeed = Mathf.Max(.1f,speed);
            // Travel stays within the model envelope, rather than inheriting the wide recall sphere's radius.
            if (!recall) Emit(burst, center, particleRadius * .04f, count / 2, .45f, particleRadius * 1.25f);
        }

        public void UpdatePresentation(float time, Vector3 center, Vector3 ball,
            Vector3 halfExtents, Vector3 haloExtents, Quaternion rotation, Vector3 sourceCenter,Vector3 sourceExtents)
        {
            transform.position = center;
            bodyRotation = rotation;
            float finish = FadeOut(time, recall ? .56f : .52f, recall ? .65f : .64f);
            float appear = FadeIn(time, 0, .08f);
            // Legacy prefab modules stay disabled, including on pooled reuse. The fitted envelope
            // remains a mathematical anchor for Recall; it no longer draws a shell or orbit lines.
            shell.enabled = false;
            for (int i = 0; i < orbits.Length; i++) orbits[i].enabled = false;
            if (recall) UpdateRecall(time, center, haloExtents, ball, halfExtents,sourceCenter,sourceExtents);
            else UpdateSpawn(time, center,halfExtents,ball,sourceExtents);
            energyLight.enabled = quality != PetVFXQuality.Low && finish > .001f;
            if (energyLight.enabled) {
                energyLight.color = primary;
                energyLight.range = Mathf.Max(.01f,Mathf.Max(halfExtents.x,Mathf.Max(halfExtents.y,halfExtents.z))*1.8f);
                energyLight.intensity = (recall ? .8f : 1.4f) * appear * finish;
            }
        }

        void UpdateSpawn(float time, Vector3 center, Vector3 halfExtents,Vector3 ball,Vector3 sourceExtents)
        {
            float flash = FadeOut(time, .12f, .46f);
            // The shared presentation centre already travels from the ball to the animated body.
            // Keep the visible ellipsoid above the landing surface as it expands.
            Vector3 flashExtents=halfExtents*burstMargin;
            Vector3 flashCenter=KeepFlashAboveGround(center,flashExtents,bodyRotation,spawnFloorY);
            SetEllipsoid(core,flashCenter,flashExtents,
                bodyRotation,Color.Lerp(Color.white,primary,.2f),flash*.82f);
            float waveSize=Mathf.Lerp(.04f,1,FadeIn(time,0,.12f));
            SetEllipsoid(shockwave,center,sourceExtents*(burstMargin*waveSize),bodyRotation,
                primary,FadeIn(time,0,.025f)*FadeOut(time,.12f,.46f)*.75f,true);
            SetSphere(ballPulse,ball,particleRadius*Mathf.Lerp(.08f,.4f,FadeIn(time,0,.04f)),
                Color.white,FadeOut(time,.04f,.12f)*.95f,true);
            groundRing.enabled = time >= .18f && time < .64f;
            if (groundRing.enabled) Ellipse(groundRing, groundPoint,
                new Vector3(sourceExtents.x,0,sourceExtents.z)*burstMargin
                    * Mathf.Lerp(.2f,1,Mathf.Clamp01((time-.18f)/.46f)),
                bodyRotation,Quaternion.Euler(90, 0, 0), particleRadius*.008f, secondary,
                FadeIn(time,.18f,.24f)*FadeOut(time,.38f,.64f)*.45f);
            if (time >= .22f && !emittedBody) {
                emittedBody = true; Emit(materialize,center,particleRadius*.65f,count/4,.35f,particleRadius*.15f);
            }
            if (time >= .64f && !emittedFinish) {
                emittedFinish = true; Emit(sparkles,center,particleRadius*.7f,count/4,.16f,particleRadius*.2f);
            }
            if (beam != null) beam.enabled = false;
            if (contact != null) contact.enabled = false;
            ClampParticlesAboveGround(burst); ClampParticlesAboveGround(materialize); ClampParticlesAboveGround(sparkles);
        }

        public static Vector3 KeepFlashAboveGround(Vector3 center,Vector3 halfExtents,Quaternion rotation,float groundY)
        {
            // Projection of a rotated ellipsoid onto world Y; does not enlarge the flash.
            float x=(rotation*Vector3.right).y*halfExtents.x;
            float y=(rotation*Vector3.up).y*halfExtents.y;
            float z=(rotation*Vector3.forward).y*halfExtents.z;
            float verticalRadius=Mathf.Sqrt(x*x+y*y+z*z);
            center.y=Mathf.Max(center.y,groundY+verticalRadius+.005f);
            return center;
        }

        void UpdateRecall(float time, Vector3 center, Vector3 haloExtents, Vector3 ball,Vector3 halfExtents,
            Vector3 sourceCenter,Vector3 sourceExtents)
        {
            float radius=Mathf.Min(haloExtents.x,Mathf.Min(haloExtents.y,haloExtents.z));
            groundRing.enabled = false;
            SetSphere(ballPulse,ball,particleRadius*.15f,Color.white,
                FadeIn(time,.56f,.575f)*FadeOut(time,.585f,.65f)*.85f);
            SetEllipsoid(core,center,halfExtents*(.12f*burstMargin),bodyRotation,primary,
                FadeIn(time,.25f,.42f)*FadeOut(time,.56f,.65f)*.65f);
            float reach = FadeIn(time,.08f,.18f);
            Vector3 endpoint = ResolveEllipsoidContact(ball, center, haloExtents,bodyRotation);
            // A nearby player can be inside a wide-winged dragon's halo. Still meet the nearest surface.
            bool visible = radius > .005f && reach > 0 && (endpoint-ball).sqrMagnitude > .000001f;
            beam.enabled = visible;
            contact.enabled = visible && reach >= .999f;
            if (visible) {
                beam.widthMultiplier = Mathf.Max(.002f, particleRadius * .018f);
                float beamAlpha = reach*FadeOut(time,.50f,.56f);
                beam.startColor = beam.endColor = new Color(primary.r, primary.g, primary.b, beamAlpha);
                beam.SetPosition(0,ball);
                beam.SetPosition(1,Vector3.Lerp(ball,endpoint,reach));
                if (contact.enabled) SetSphere(contact, endpoint, Mathf.Min(radius * .1f, particleRadius * .07f),
                    Color.white, beamAlpha*.85f);
            }
            if (time >= .18f && !emittedBody) {
                emittedBody = true; Emit(burst,center,particleRadius,count/2,.38f,0);
                Emit(materialize,center,particleRadius*.65f,count/4,.3f,0);
            }
            UpdateFunnel(time,ball,endpoint,haloExtents,sourceCenter,sourceExtents);
            float progress=FadeIn(time,.18f,.56f);
            Attract(burst, ball, progress); Attract(materialize, ball, progress);
            if (time >= .56f && !emittedFinish) {
                emittedFinish = true; Emit(sparkles,ball,particleRadius*.01f,count/4,.09f,particleRadius*.1f);
            }
        }

        public static Vector3 ResolveEllipsoidContact(Vector3 origin, Vector3 center, Vector3 halfExtents,Quaternion rotation)
        {
            Vector3 axes=Vector3.Max(halfExtents,Vector3.one*.000001f);
            Vector3 local=Quaternion.Inverse(rotation)*(origin-center);
            if (local.sqrMagnitude<.0000000001f) local=Vector3.right;
            float length=new Vector3(local.x/axes.x,local.y/axes.y,local.z/axes.z).magnitude;
            return center+rotation*(local/Mathf.Max(.000001f,length));
        }

        void UpdateFunnel(float time,Vector3 ball,Vector3 start,Vector3 haloExtents,
            Vector3 sourceCenter,Vector3 sourceExtents)
        {
            if (funnelStrands == null) return;
            var warp=PetRecallWarp.Create(sourceCenter,ball,sourceExtents,bodyRotation,time,profile);
            float alpha=FadeIn(time,.18f,.25f)*FadeOut(time,.50f,.56f)*.8f;
            // With the ball inside the remaining halo, its forward surface lies beyond the ball.
            // Suppress the decorative tail rather than drawing a coil back in the opposite direction.
            bool outward=Vector3.Dot(ball-start,warp.axis)>.001f;
            Vector3 delta=ball-start;
            float distance=delta.magnitude;
            Vector3 axis=distance>.0001f?delta/distance:warp.axis;
            Vector3 side=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized;
            Vector3 up=Vector3.Cross(axis,side);
            float width=Mathf.Min(haloExtents.x,Mathf.Min(haloExtents.y,haloExtents.z))*.3f;
            width=Mathf.Min(width,distance*.15f);
            int strands=quality==PetVFXQuality.Low?1:2;
            for (int arm=0;arm<funnelStrands.Length;arm++) {
                var line=funnelStrands[arm];
                line.enabled=arm<strands && outward && warp.enabled && distance>.001f && alpha>.001f && width>.0001f;
                if (!line.enabled) continue;
                line.widthMultiplier=Mathf.Max(.001f,particleRadius*.012f);
                Color colour=arm==0?primary:secondary; colour.a=alpha; line.startColor=line.endColor=colour;
                for (int i=0;i<segments;i++) {
                    float u=i/(float)(segments-1);
                    float angle=u*Mathf.PI*3-time*12+arm*Mathf.PI;
                    float envelope=width*Mathf.Sin(u*Mathf.PI)*Mathf.Pow(1-u,1.4f);
                    circlePositions[i]=Vector3.Lerp(start,ball,u)
                        +(side*Mathf.Cos(angle)+up*Mathf.Sin(angle))*envelope;
                }
                line.SetPositions(circlePositions);
            }
        }

        void ClampParticlesAboveGround(ParticleSystem system)
        {
            int alive=system.GetParticles(attractionBuffer);
            for (int i=0;i<alive;i++) if (attractionBuffer[i].position.y<spawnFloorY+.005f) {
                Vector3 position=attractionBuffer[i].position; position.y=spawnFloorY+.005f;
                attractionBuffer[i].position=position;
                Vector3 velocity=attractionBuffer[i].velocity; velocity.y=Mathf.Max(0,velocity.y);
                attractionBuffer[i].velocity=velocity;
            }
            system.SetParticles(attractionBuffer,alive);
        }

        static float FadeIn(float time, float start, float end)
            => Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,time));

        static float FadeOut(float time, float start, float end) => 1-FadeIn(time,start,end);

        void Attract(ParticleSystem system, Vector3 center, float progress)
        {
            int alive = system.GetParticles(attractionBuffer);
            for (int i = 0; i < alive; i++) {
                Vector3 offset = center - attractionBuffer[i].position;
                attractionBuffer[i].velocity = offset * Mathf.Lerp(9, 24, progress);
                if (offset.sqrMagnitude < .0004f) attractionBuffer[i].remainingLifetime = 0;
            }
            system.SetParticles(attractionBuffer, alive);
        }

        void Emit(ParticleSystem ps, Vector3 center, float radius, int amount, float lifetime, float speed)
        {
            ps.transform.position = center;
            var main = ps.main; main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
            main.startSize = Mathf.Clamp(particleRadius*.025f,.001f,.09f);
            main.startColor = secondary;
            var shape = ps.shape; shape.radius = Mathf.Max(.005f, radius); shape.radiusThickness = .1f;
            ps.Play(false); ps.Emit(Mathf.Max(0, amount));
        }

        void Ellipse(LineRenderer line, Vector3 center, Vector3 axes, Quaternion rotation,Quaternion plane,
            float width, Color color, float alpha)
        {
            line.widthMultiplier = width; color.a = alpha; line.startColor = line.endColor = color;
            for (int i = 0; i < segments; i++)
                circlePositions[i]=center+rotation*Vector3.Scale(axes,plane*unitCircle[i]);
            // SetPositions consumes positionCount entries from this reusable maximum-sized buffer.
            line.SetPositions(circlePositions);
        }

        void SetSphere(MeshRenderer renderer, Vector3 center, float radius, Color colour, float alpha,bool clipGround=false)
            => SetEllipsoid(renderer,center,Vector3.one*radius,Quaternion.identity,colour,alpha,clipGround);

        void SetEllipsoid(MeshRenderer renderer,Vector3 center,Vector3 halfExtents,Quaternion rotation,Color colour,float alpha,bool clipGround=false)
        {
            renderer.enabled = halfExtents.sqrMagnitude > .000001f && alpha > .001f;
            if (!renderer.enabled) return;
            renderer.transform.SetPositionAndRotation(center,rotation);
            renderer.transform.localScale = halfExtents*2;
            properties.Clear();
            colour.a = alpha; properties.SetColor(BaseColorId, colour);
            properties.SetFloat(GroundId,spawnFloorY); properties.SetFloat(ClipGroundId,clipGround?1:0);
            renderer.SetPropertyBlock(properties);
        }

        public void ResetPresentation()
        {
            emittedBody = emittedFinish = false;
            if (shell == null) return;
            shell.enabled = core.enabled = groundRing.enabled = false;
            if (contact != null) contact.enabled = false;
            if (beam != null) beam.enabled = false;
            if (shockwave != null) shockwave.enabled = false;
            if (ballPulse != null) ballPulse.enabled = false;
            if (funnelStrands != null) foreach (var strand in funnelStrands) strand.enabled = false;
            for (int i = 0; i < orbits.Length; i++) orbits[i].enabled = false;
            burst.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            materialize.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparkles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            energyLight.enabled = false;
        }

        void OnDestroy()
        {
            DestroyResource(shellMaterial); DestroyResource(coreMaterial);
            DestroyResource(particleMaterial); DestroyResource(ribbonMaterial);
            DestroyResource(waveMaterial);
        }

        static void DestroyResource(Object item) {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
    }
}
