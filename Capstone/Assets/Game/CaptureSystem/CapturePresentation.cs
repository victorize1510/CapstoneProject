using System;
using System.Collections;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.PetSummon;
using Capstone.Game.UISystem;
using Capstone.Game.WorldFeedback;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.CaptureSystem
{
    public readonly struct CapturePresentationResult
    {
        public readonly bool Success, IsCritical;
        public readonly int ShakeCount;
        public readonly PetController CapturedPet;
        public readonly string Message;
        public readonly Vector3 WorldPosition;
        internal readonly WildPetCaptureTarget Target;
        internal readonly bool Saved;
        internal readonly string ReceiptId, PetName;
        internal readonly Sprite PetIcon;
        internal CapturePresentationResult(CaptureRoll roll, WildPetCaptureTarget target, bool saved = false)
        {
            Success = roll.Success;
            IsCritical = roll.IsCritical;
            // So lac chi la trinh dien; khong roll lai va khong doi calculator.
            ShakeCount = roll.Success ? 3 : Mathf.Clamp(roll.ShakeCount, 1, 2);
            CapturedPet = roll.Success ? target.Pet : null;
            Target = target;
            Saved = saved;
            WorldPosition = target.Enemy.TargetPosition;
            PetName = target.Metadata.ResolveDisplayName(target.Stats.DisplayName);
            PetIcon = target.Stats.Icon;
            ReceiptId = "capture:" + target.Metadata.PersistentId;
            Message = roll.Success ? "Đã thu phục " + PetName : "Thú đã thoát khỏi bóng. Bạn có thể thử lại.";
        }
    }

    public sealed class CapturePresentation : MonoBehaviour
    {
        public enum MotionPhase { Idle, Windup, Flight, Contact, Absorb, Drop, Shake, Release, Complete }
        public bool IsPlaying { get; private set; }
        public MotionPhase Phase { get; private set; }
        public int CompletedShakes { get; private set; }
        public Vector3 BallPosition => ball != null ? ball.transform.position : default;
        public float ReleaseNormalized { get; private set; }
        GameObject ball;
        WildPetCaptureTarget target;
        CaptureVisualProxy proxy;
        CapturePresentationResult result;
        Renderer[] hidden = Array.Empty<Renderer>();
        bool[] visibility = Array.Empty<bool>();
        PetSummonHandSocket hand;
        PlayerVisualSwitcher switcher;
        bool switchLockedBefore;
        PetSummonVfxInstance energy;
        readonly RaycastHit[] groundHits = new RaycastHit[24];

        public void Play(CapturePresentationResult value, Transform origin, UIDocument document)
        {
            Cancel();
            result = value;
            target = value.Target;
            if (target == null) return;
            IsPlaying = true;
            CompletedShakes = 0;
            InventoryInputController.SetExternalGameplayInputBlocked(this, true);
            target.Lock(true);
            switcher = origin != null ? origin.GetComponent<PlayerVisualSwitcher>() : null;
            if (switcher != null) { switchLockedBefore = switcher.RuntimeSwitchLocked; switcher.SetRuntimeSwitchLocked(true); }
            StartCoroutine(Animate(origin));
        }

        IEnumerator Animate(Transform origin)
        {
            try
            {
                var movement = origin != null ? origin.GetComponent<BasicPlayerMovement>() : null;
                hand = origin != null ? origin.GetComponent<PetSummonHandSocket>() : null;
                if (movement == null || hand == null || CaptureAssets.Settings?.ballVisual == null)
                {
                    Debug.LogWarning("Thu phuc thieu movement, socket tay hoac model bong.", this);
                    yield break;
                }
                // Proxy chi co mesh, khong sua scale/material/xuong cua actor that.
                proxy = new CaptureVisualProxy(target, result.WorldPosition);
                proxy.Root.SetActive(result.Success);
                ball = Instantiate(CaptureAssets.Settings.ballVisual);
                ball.name = "Capture ball presentation";
                foreach (var script in ball.GetComponentsInChildren<MonoBehaviour>()) script.enabled = false;
                if (!hand.AttachBallToActiveHand(ball)) yield break;
                Phase = MotionPhase.Windup;
                Vector3 facing = result.WorldPosition - origin.position;
                facing.y = 0;
                Quaternion rotation = facing.sqrMagnitude > .01f ? Quaternion.LookRotation(facing) : origin.rotation;
                for (float t = 0; t < .12f; t += GameplayPause.PlayDelta)
                {
                    origin.rotation = Quaternion.RotateTowards(origin.rotation, rotation, 900 * GameplayPause.PlayDelta);
                    yield return null;
                }
                if (!movement.TryPlayScriptedThrow()
                    || !PlayerThrowPlayback.TryRelease(movement, switcher, 32, out float release)) yield break;
                ReleaseNormalized = release;
                float deadline = GameplayPause.PlayTime + 4;
                bool reached = false;
                while (GameplayPause.PlayTime < deadline && movement.IsScriptedActionPlaying)
                {
                    if (PlayerThrowPlayback.TryProgress(movement, switcher, out float progress) && progress >= release) { reached = true; break; }
                    yield return null;
                }
                if (!reached) yield break;
                ball = hand.DetachHeldBall();
                if (ball == null) yield break;
                // Chi reparent bong; scale tay khong duoc truyen sang dan dang bay.
                NormalizeBall();
                Vector3 start = ball.transform.position;
                Vector3 contact = result.WorldPosition;
                Vector3 ground = FindGround(contact, origin);
                Phase = MotionPhase.Flight;
                yield return Fly(start, contact, Mathf.Clamp(Vector3.Distance(start, contact) / 13, .28f, .8f), -18);
                Phase = MotionPhase.Contact;
                Vector3 towardPlayer = start - contact;
                towardPlayer.y = 0;
                towardPlayer = towardPlayer.sqrMagnitude > .01f ? towardPlayer.normalized : -origin.forward;
                Vector3 hover = contact + towardPlayer * .32f + Vector3.up * .18f;
                yield return Fly(contact, hover, .16f, -12);
                HideTarget();
                proxy.Root.SetActive(true);
                BeginEnergy(origin, ground);
                Phase = MotionPhase.Absorb;
                for (float t = 0; t < .65f; t += GameplayPause.PlayDelta)
                {
                    float clock = Mathf.Clamp01(t / .65f);
                    float pull = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .88f, clock));
                    SetProxy(Vector3.Lerp(contact, hover, pull), Mathf.Max(.01f, 1 - pull));
                    energy?.UpdatePresentation(clock * .65f, proxy.Root.transform.position, hover,
                        proxy.HalfExtents * (1 - pull), proxy.HalfExtents * (1 - pull), Quaternion.identity, proxy.Center, proxy.HalfExtents);
                    yield return null;
                }
                proxy.Root.SetActive(false);
                ResetEnergy();
                Phase = MotionPhase.Drop;
                Vector3 resting = ground + Vector3.up * .108f + towardPlayer * .25f;
                yield return Fly(hover, resting, Mathf.Clamp(Mathf.Sqrt(Mathf.Max(.1f, hover.y - resting.y) / 9), .2f, .55f), -18);
                yield return Fly(resting, resting + towardPlayer * .08f, .18f, -18);
                resting = ball.transform.position;
                yield return GameplayPause.WaitForSeconds(.18f);
                Phase = MotionPhase.Shake;
                for (int i = 0; i < result.ShakeCount; i++)
                {
                    for (float t = 0; t < .36f; t += GameplayPause.PlayDelta)
                    {
                        float f = t / .36f;
                        float wobble = Mathf.Sin(f * Mathf.PI * 2) * Mathf.Sin(f * Mathf.PI);
                        ball.transform.rotation = Quaternion.Euler(0, 0, wobble * 24);
                        ball.transform.position = resting + origin.right * (wobble * .035f);
                        yield return null;
                    }
                    CompletedShakes++;
                    ball.transform.SetPositionAndRotation(resting, Quaternion.identity);
                    yield return GameplayPause.WaitForSeconds(.18f);
                }
                if (!result.Success)
                {
                    Phase = MotionPhase.Release;
                    proxy.Root.SetActive(true);
                    for (float t = 0; t < .24f; t += GameplayPause.PlayDelta)
                    {
                        float f = Mathf.SmoothStep(0, 1, t / .24f);
                        SetProxy(Vector3.Lerp(resting, contact, f), Mathf.Max(.01f, f));
                        yield return null;
                    }
                }
                var sound = result.Success ? CaptureAssets.Settings?.successSound : CaptureAssets.Settings?.escapeSound;
                if (sound != null) SettingsSystem.GameAudioRouting.PlayWorldSound(sound, resting);
                Phase = MotionPhase.Complete;
                yield return GameplayPause.WaitForSeconds(.16f);
            }
            finally { Cleanup(); }
        }
        IEnumerator Fly(Vector3 start, Vector3 end, float duration, float gravity)
        {
            Vector3 velocity = (end - start) / duration - Vector3.up * (.5f * gravity * duration);
            for (float t = 0; t < duration; t += GameplayPause.PlayDelta)
            {
                if (ball == null) yield break;
                ball.transform.position = start + velocity * t + Vector3.up * (.5f * gravity * t * t);
                ball.transform.Rotate(Vector3.right, 300 * GameplayPause.PlayDelta, Space.World);
                yield return null;
            }
            if (ball != null) ball.transform.SetPositionAndRotation(end, Quaternion.identity);
        }
        void SetProxy(Vector3 position, float scale)
        {
            proxy.Root.transform.position = position;
            proxy.Root.transform.localScale = Vector3.one * scale;
        }
        void NormalizeBall()
        {
            var renderers = ball.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (diameter > .0001f) ball.transform.localScale *= .2145f / diameter;
        }
        Vector3 FindGround(Vector3 point, Transform origin)
        {
            float best = float.PositiveInfinity;
            Vector3 ground = new Vector3(point.x, target.transform.position.y, point.z);
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 2, Vector3.down, groundHits, 8, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.normal.y < .55f || hit.transform.IsChildOf(target.transform) || hit.transform.IsChildOf(origin) || hit.distance >= best) continue;
                ground = hit.point;
                best = hit.distance;
            }
            return ground;
        }
        void HideTarget()
        {
            if (result.Success || hidden.Length > 0 || target == null) return;
            hidden = target.GetComponentsInChildren<Renderer>();
            visibility = new bool[hidden.Length];
            for (int i = 0; i < hidden.Length; i++) { visibility[i] = hidden[i].enabled; hidden[i].enabled = false; }
        }
        void BeginEnergy(Transform origin, Vector3 ground)
        {
            var profile = origin.GetComponent<PetSummonReleaseEffect>()?.Profile;
            if (energy == null)
            {
                var go = new GameObject("Capture energy presentation");
                go.transform.SetParent(transform, false);
                energy = go.AddComponent<PetSummonVfxInstance>();
                energy.BuildLayout(true);
            }
            energy.Begin(true, profile, SettingsSystem.GameSettingsService.EffectsQuality, proxy.Center, proxy.HalfExtents);
            energy.SetGroundPoint(ground);
        }
        void ResetEnergy()
        {
            if (energy == null) return;
            energy.ResetPresentation();
            energy.gameObject.SetActive(false);
        }
        void Cleanup()
        {
            for (int i = 0; i < hidden.Length; i++) if (hidden[i] != null) hidden[i].enabled = visibility[i];
            hidden = Array.Empty<Renderer>();
            visibility = Array.Empty<bool>();
            proxy?.Dispose();
            proxy = null;
            if (target != null) { target.EndPresentationHold(); target.Lock(false); }
            target = null;
            if (hand != null && hand.HeldBall == ball) hand.DetachHeldBall();
            if (ball != null) { ball.SetActive(false); Destroy(ball); }
            ball = null;
            ResetEnergy();
            if (switcher != null) switcher.SetRuntimeSwitchLocked(switchLockedBefore);
            switcher = null;
            InventoryInputController.SetExternalGameplayInputBlocked(this, false);
            IsPlaying = false;
            Phase = MotionPhase.Idle;
            // Chi receipt da commit save duoc hien; dedup bao ve ca Cancel lap.
            if (result.Success && result.Saved)
                ItemAcquisitionFeedback.Publish(new ItemAcquisitionReceipt(result.ReceiptId, result.PetIcon, "Đã thu phục " + result.PetName));
            result = default;
        }
        public void Cancel() { StopAllCoroutines(); Cleanup(); }
        void OnDisable() => Cancel();
        void OnDestroy() { Cancel(); if (energy != null) Destroy(energy.gameObject); }
    }
}
