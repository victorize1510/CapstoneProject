#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.Skills;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Capstone.Game.WorldFeedback {
    public static class CombatVisualsEngineChecks {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static readonly StringBuilder results = new StringBuilder();
        static void Check(bool value, string name) {
            results.AppendLine((value ? "PASS " : "FAIL ") + name);
            CombatVisualsVerification.Write("engine-checks.txt", results.ToString());
            if (!value) throw new InvalidOperationException(name);
        }
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Any).Invoke(target, args);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Any).SetValue(target, value);
        static object Get(object target, string field) => target.GetType().GetField(field, Any).GetValue(target);
        public static async Task Run() {
            results.Clear(); var input = Object.FindFirstObjectByType<PetCommandInput>();
            var save = Object.FindFirstObjectByType<PlayerSaveController>();
            Check(input != null && save != null && save.SavePath.StartsWith(CombatVisualsVerification.Root, StringComparison.OrdinalIgnoreCase), "save isolated before any fixture");
            var enabled = new List<BasicCreatureEnemy>();
            int fps = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
            try {
                foreach (var ai in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None)) if (ai.enabled) { ai.enabled = false; enabled.Add(ai); }
                var numeric = Resources.Load<Font>("WorldFeedback/Fonts/Rajdhani-SemiBold");
                Check(numeric != null, "licensed numeric font loaded");
                foreach (char glyph in "0123456789.-+×%") Check(numeric.HasCharacter(glyph), "numeric glyph U+" + ((int)glyph).ToString("X4"));
                foreach (string id in new[] { "bite", "blast", "projectile", "wing_strike" }) {
                    var skill = AssetDatabase.LoadAssetAtPath<PetSkillDefinition>("Assets/Game/Skills/Data/Definitions/Skill_" + id + ".asset");
                    Check(skill.icon != null && skill.icon.texture.wrapMode == TextureWrapMode.Clamp, "semantic sprite assigned and clamped " + id);
                    Check(skill.presentation.releaseMode == PetSkillReleaseMode.NormalizedAnimation, "animation-owned release " + id);
                }
                var fade = Object.FindFirstObjectByType<CameraVisibility>();
                Check(fade != null && fade.GameplayCamera == input.commandCamera, "fade bound to actual command camera");
                CameraAndMaterials(fade);
                DamageChecks(input);
                foreach (int target in new[] { 30, 60 }) {
                    QualitySettings.vSyncCount = 0; Application.targetFrameRate = target;
                    CombatVisualsVerification.Write("engine-status.txt", "RUNNING three forms/four skills at requested " + target + " FPS (effective timing logged separately)");
                    await PveCombatScenarioVerification.Run(input, (pass, name) => Check(pass, "fps-request=" + target + " " + name), target==60, CombatVisualsVerification.Root + target + "-");
                }
                await Task.Delay(1200);
                Check(CombatAudioVoices.LiveCount <= 8 && CombatEffectPool.ActiveCount <= 64 && CombatEffectPool.IdleCount <= 24, "audio/VFX budgets bounded after three-form scenarios");
                CombatVisualsVerification.Write("engine-status.txt", "PASS all requested engine checks; native hold gestures, standalone and GPU timings are separate evidence");
            } catch (Exception e) { CombatVisualsVerification.Write("engine-status.txt", "FAILED\n" + e); throw; }
            finally {
                Application.targetFrameRate = fps; QualitySettings.vSyncCount = vsync;
                foreach (var ai in enabled) if (ai != null) ai.enabled = true;
            }
        }
        static void CameraAndMaterials(CameraVisibility fade) {
            var source = fade.GetComponent<BasicCameraFollow>();
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "QA scenery material fixture";
            go.transform.position = new Vector3(10000, 0, 3); go.transform.localScale = new Vector3(4, 3, .3f); go.layer = 31;
            var renderer = go.GetComponent<Renderer>();
            var cameraObject = new GameObject("QA isolated shader camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(10000, 0, 0); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.02f, .035f, .05f); camera.cullingMask = 1 << 31; camera.nearClipPlane = .1f; camera.farClipPlane = 20;
            camera.fieldOfView = 50; camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            var originalCamera = Get(fade, "cameraSource");
            var originalA = Get(fade, "focusA"); var originalB = Get(fade, "focusB");
            var rt = new RenderTexture(192, 192, 24); rt.Create();
            var originalTarget = source.target;
            var actor = new GameObject("QA actor mesh fixture"); actor.transform.SetParent(source.target, false);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube); mesh.transform.SetParent(actor.transform); mesh.transform.position = new Vector3(10000, 0, 5);
            Check(!CameraVisibility.Eligible(mesh.GetComponent<Renderer>()), "actor mesh excluded even when it uses an opaque Lit material");
            Object.DestroyImmediate(actor);
            try {
                Set(fade, "cameraSource", camera); Set(fade, "focusA", new Vector4(10000, 0, 5, .65f)); Set(fade, "focusB", Vector4.zero);
                Check(CameraVisibility.Eligible(renderer), "ordinary scenery eligible");
                foreach (string family in new[] { "Lit", "Simple Lit", "Unlit" }) {
                    var material = new Material(Shader.Find("Universal Render Pipeline/" + family));
                    material.SetColor("_BaseColor", new Color(.5f, .65f, .7f));
                    if (material.HasProperty("_Parallax")) material.SetFloat("_Parallax", .043f);
                    if (material.HasProperty("_DetailNormalMapScale")) material.SetFloat("_DetailNormalMapScale", 1.37f);
                    renderer.sharedMaterial = material;
                    var entry = Call(fade, "Cache", renderer, false);
                    Check(entry != null, family + " has its own adapter");
                    var variants = (Material[])Get(entry, "Faded");
                    Check(variants[0].shader.name.EndsWith(family.Replace(" ", "")), "shader family preserved " + family);
                    Check(!ShaderUtil.ShaderHasError(variants[0].shader), "shader imported without errors " + family);
                    if (material.HasProperty("_Parallax")) Check(Mathf.Approximately(variants[0].GetFloat("_Parallax"), .043f), "parallax not reused for fade");
                    if (material.HasProperty("_DetailNormalMapScale")) Check(Mathf.Approximately(variants[0].GetFloat("_DetailNormalMapScale"), 1.37f), "detail normal scale preserved");
                    Set(entry, "Amount", 0f); var solid = Render(camera, rt);
                    Set(entry, "Amount", 1f); var cutaway = Render(camera, rt);
                    int changed = 0, outside = 0;
                    for (int y = 0; y < 192; y++) for (int x = 0; x < 192; x++) {
                        var a = solid.GetPixel(x, y); var b = cutaway.GetPixel(x, y);
                        if (Mathf.Max(Mathf.Abs(a.r-b.r), Mathf.Abs(a.g-b.g), Mathf.Abs(a.b-b.b)) <= .015f) continue;
                        changed++; if (x < 40 || x > 152 || y < 40 || y > 152) outside++;
                    }
                    Check(changed > 600 && outside == 0, family + " actual URP cutaway localized pixels=" + changed + " outside=" + outside);
                    Check(renderer.sharedMaterial == material, "render end restores original material " + family);
                    if (family == "Lit") {
                        FileWritePng("shader-solid.png", solid);
                        FileWritePng("shader-cutaway.png", cutaway);
                    }
                    Object.DestroyImmediate(solid); Object.DestroyImmediate(cutaway);
                    var external = new Material(material); renderer.sharedMaterial = external;
                    Call(fade, "Apply"); Check(renderer.sharedMaterial == external, "runtime material replacement not overwritten " + family);
                    Object.DestroyImmediate(material); Object.DestroyImmediate(external);
                }
                Physics.SyncTransforms();
                Check(Physics.Raycast(camera.transform.position, Vector3.forward, out _, 4, 1 << 31), "faded wall remains a physical collider");
                var pivot = new GameObject("QA camera collision pivot"); pivot.transform.position = camera.transform.position;
                source.target = pivot.transform;
                Vector3 safe = source.ConstrainCameraPosition(pivot.transform.position, pivot.transform.position + Vector3.forward * 6);
                Check(safe.z < 3 && safe.z > 0, "camera collision retracts before fixture wall, not through it");
                go.transform.position = new Vector3(10000, 0, .65f); Physics.SyncTransforms();
                safe = source.ConstrainCameraPosition(pivot.transform.position, pivot.transform.position + Vector3.forward * 6);
                Check(Vector3.Cross(safe-pivot.transform.position,Vector3.forward).sqrMagnitude < .000001f
                    && !Physics.CheckSphere(safe,.28f,1<<31,QueryTriggerInteraction.Ignore), "near wall retracts on the same boom without switching shoulder");
                Object.DestroyImmediate(pivot);
                fade.enabled = false;
                Check(fade.CachedRendererCount == 0 && fade.MaterialCloneCount == 0 && Shader.GetGlobalFloat("_CapstoneOcclusionView") == 0, "disable clears clones and camera globals");
                fade.enabled = true;
            } finally {
                source.target = originalTarget; Set(fade, "cameraSource", originalCamera); Set(fade, "focusA", originalA); Set(fade, "focusB", originalB);
                Call(fade, "Clear"); Object.DestroyImmediate(go); Object.DestroyImmediate(cameraObject); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
        static Texture2D Render(Camera camera, RenderTexture target) {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(camera, request);
            var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0,0,target.width,target.height), 0,0); texture.Apply(); RenderTexture.active = previous; return texture;
        }
        static void FileWritePng(string name, Texture2D texture) {
            System.IO.File.WriteAllBytes(CombatVisualsVerification.Root + name, texture.EncodeToPNG());
            System.IO.File.AppendAllText(CombatVisualsVerification.Root + "created-paths.txt", CombatVisualsVerification.Root + name + "\n");
        }
        static void DamageChecks(PetCommandInput input) {
            Vector3 point = input.commandCamera.transform.position + input.commandCamera.transform.forward * 3;
            FloatingCombatText.ShowDamage(new CombatDamageResult(new CombatHit(null,"qa",1,99,CreatureDamageKind.Physical,true,false,point),null,6.89f,false));
            var labels = Object.FindFirstObjectByType<FloatingCombatText>();
            Check(labels != null && Mathf.Approximately(labels.LastDamage,6.89f) && labels.LastText == "6.9", "number uses actual HP lost, not 99 raw damage");
            int spawned = labels.SpawnedCount;
            FloatingCombatText.ShowDamage(new CombatDamageResult(new CombatHit(null,"qa",1,99,CreatureDamageKind.Physical,false,false,point),null,0,false));
            Check(labels.SpawnedCount == spawned, "zero HP loss emits no damage number");
            for(int i=0;i<50;i++) FloatingCombatText.ShowDamage(point,8,CreatureDamageKind.Physical);
            Check(labels.ActiveCount <= 32, "damage text pool remains capped at 32 under burst");
        }
    }
}
#endif
