using System;
using Capstone.Game.CreatureSystem;
using Capstone.Game.UISystem;
using UnityEngine;

// On the active visual's Animator so clip events and runtime timing share one playback gate.
[DisallowMultipleComponent, RequireComponent(typeof(Animator), typeof(AudioSource))]
public sealed class PetCryEventReceiver : MonoBehaviour
{
    enum Occasion { None, Ambient, Summon, Skill, Death }
    enum TimingMode { Normalized, ClipSeconds, AnimationEvent }

    [Serializable]
    struct Timing
    {
        public bool enabled;
        [SerializeField] TimingMode mode;
        [SerializeField, Range(0, 1)] float normalized;
        [SerializeField, Min(0)] float clipSeconds;

        public Timing(float normalized)
        {
            enabled = true;
            mode = TimingMode.Normalized;
            this.normalized = normalized;
            clipSeconds = 0;
        }

        public bool UsesEvent => mode == TimingMode.AnimationEvent;
        public bool UsesClipSeconds => mode == TimingMode.ClipSeconds;

        public bool Reached(AnimatorStateInfo state, float clipLength) => mode == TimingMode.ClipSeconds
            ? state.normalizedTime >= Mathf.Min(.99f, clipSeconds / Mathf.Max(.01f, clipLength))
            : state.normalizedTime >= normalized;
    }

    [SerializeField] AudioSource crySource;
    [Header("Peaceful cry")]
    [SerializeField] CreatureAction ambientAction = CreatureAction.Cast;
    [SerializeField] Timing ambient = new Timing(.25f);
    [SerializeField] Vector2 ambientInterval = new Vector2(12, 25);
    [SerializeField, Min(.1f)] float ambientHoldLimit = 4;
    [Header("Summon cry")]
    [SerializeField] CreatureAction summonAction = CreatureAction.Cast;
    [SerializeField] Timing summon = new Timing(.17f);
    [SerializeField, Min(.1f)] float summonHoldLimit = .45f;
    [Header("Skill cry")]
    [SerializeField] Timing skill = new Timing(.25f);
    [SerializeField] Timing projectileSkill = new Timing(.25f);
    [SerializeField] CreatureAction[] skillActions = { CreatureAction.Cast, CreatureAction.Projectile };
    [Header("Death cry")]
    [SerializeField] Timing death = new Timing(.12f);
    [SerializeField, Range(0, 1)] float deathPose = .72f;
    [SerializeField, Min(.1f)] float deathHoldLimit = 1.5f;
    [Header("Playback")]
    [SerializeField, Min(0)] float minimumCryInterval = .12f;

    Animator animator;
    Occasion occasion;
    Timing timing;
    int stateHash;
    float startedAt, startDeadline, enteredAt, nextAmbient, lastCryAt = float.NegativeInfinity;
    bool entered, fired, finished;
    float playedAt = float.NegativeInfinity, playedDuration;
    AnimatorCullingMode cullingBeforeDeath;
    bool forcedDeathEvaluation;
    readonly System.Collections.Generic.List<AnimatorClipInfo> clips = new System.Collections.Generic.List<AnimatorClipInfo>(2);

    internal bool IsCosmetic => occasion == Occasion.Ambient || occasion == Occasion.Summon;
    internal float SummonHoldLimit => summonHoldLimit;
    internal bool DeathReady => occasion != Occasion.Death ||
        finished && (!fired || !crySource.isPlaying || GameplayPause.PlayTime >= playedAt + playedDuration + .15f);

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (crySource == null)
            crySource = GetComponent<AudioSource>();
        Capstone.Game.SettingsSystem.GameAudioRouting.Route(crySource, Capstone.Game.SettingsSystem.GameAudioBus.PetCry);
    }

    void OnDisable() => ResetPresence();

    internal void ResetPresence()
    {
        Clear(true);
        nextAmbient = 0;
        lastCryAt = float.NegativeInfinity;
    }

    void Clear(bool stopVoice)
    {
        if (stopVoice && crySource != null)
            crySource.Stop();
        if (forcedDeathEvaluation && animator != null)
            animator.cullingMode = cullingBeforeDeath;
        forcedDeathEvaluation = false;
        occasion = Occasion.None;
    }

    void Arm(Occasion next, Timing marker, int hash, float delay = 0)
    {
        Clear(IsCosmetic || next == Occasion.Death);
        if ((!marker.enabled || crySource == null || crySource.clip == null) && next != Occasion.Death)
            return;
        occasion = next;
        timing = marker;
        stateHash = hash;
        startedAt = Time.time + delay;
        startDeadline = startedAt + .32f;
        entered = fired = finished = false;
    }

    internal void CancelAmbient()
    {
        if (occasion == Occasion.Ambient)
            Clear(true);
        nextAmbient = 0;
    }

    internal void CancelSkill()
    {
        if (occasion == Occasion.Skill)
            Clear(false);
    }

    internal bool TickAmbient(CreatureAnimationDriver driver)
    {
        if (occasion == Occasion.Ambient)
        {
            if (!finished)
                return true;
            Clear(false);
        }
        if (!ambient.enabled || crySource == null || crySource.clip == null || occasion != Occasion.None)
            return false;
        if (nextAmbient <= 0)
        {
            float min = Mathf.Max(.1f, ambientInterval.x);
            nextAmbient = Time.time + UnityEngine.Random.Range(min, Mathf.Max(min, ambientInterval.y));
        }
        if (Time.time < nextAmbient || driver == null)
            return false;

        nextAmbient = 0;
        if (!driver.TryGetActionHash(ambientAction, out int hash) || !driver.TryPlay(ambientAction))
            return false;
        Arm(Occasion.Ambient, ambient, hash);
        return true;
    }

    internal bool TryGetSummonState(CreatureAnimationDriver driver, out int hash)
    {
        hash = 0;
        return summon.enabled && crySource != null && crySource.clip != null && driver != null
            && driver.TryGetActionHash(summonAction, out hash);
    }

    internal void BeginSummon(int hash) => Arm(Occasion.Summon, summon, hash);

    internal void EndSummon()
    {
        if (occasion == Occasion.Summon)
        {
            if (entered && timing.enabled)
                PlayOnce();
            Clear(false);
        }
    }

    internal void BeginSkill(CreatureAnimationDriver driver, int hash, float delay = 0)
    {
        if (IsCosmetic)
            Clear(true);
        CancelSkill();
        if (driver == null)
            return;
        foreach (var action in skillActions)
        {
            if (!driver.TryGetActionHash(action, out int actionHash) || actionHash != hash)
                continue;
            Arm(Occasion.Skill, action == CreatureAction.Projectile ? projectileSkill : skill, hash, delay);
            return;
        }
    }

    internal void BeginDeath(int hash)
    {
        if (occasion == Occasion.Death)
            return;
        Arm(Occasion.Death, death, hash);
        if (animator != null && animator.isActiveAndEnabled)
        {
            cullingBeforeDeath = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            forcedDeathEvaluation = true;
        }
    }

    void LateUpdate()
    {
        if (occasion == Occasion.None || finished || GameplayPause.IsPaused || Time.time < startedAt)
            return;

        if (!TryReadState(out var state, out bool next))
        {
            if (!entered && Time.time < startDeadline)
                return;
            if (occasion == Occasion.Death && timing.enabled)
                PlayOnce();
            FinishTiming();
            return;
        }

        if (!entered)
        {
            entered = true;
            enteredAt = Time.time;
        }

        float cap = occasion == Occasion.Death ? deathHoldLimit : occasion == Occasion.Ambient ? ambientHoldLimit
            : occasion == Occasion.Summon ? summonHoldLimit : 5f;
        bool timedOut = Time.time - enteredAt >= cap;
        if (timing.enabled && !fired && (!timing.UsesEvent && timing.Reached(state, timing.UsesClipSeconds ? ClipLength(state, next) : 0)
            || occasion == Occasion.Death && timedOut))
            PlayOnce();

        float end = occasion == Occasion.Death ? deathPose : 1f;
        // Death waits for its marker as well as the pose, including markers authored near the end.
        if (timedOut || state.normalizedTime >= end && (occasion != Occasion.Death || fired || !timing.enabled))
            FinishTiming();
    }

    float ClipLength(AnimatorStateInfo state, bool next)
    {
        if (next)
            animator.GetNextAnimatorClipInfo(0, clips);
        else
            animator.GetCurrentAnimatorClipInfo(0, clips);
        return clips.Count > 0 && clips[0].clip != null ? clips[0].clip.length : state.length;
    }

    bool TryReadState(out AnimatorStateInfo state, out bool next)
    {
        state = default;
        next = false;
        if (stateHash == 0 || animator == null || !animator.isActiveAndEnabled)
            return false;
        next = animator.IsInTransition(0);
        state = next ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        return state.fullPathHash == stateHash;
    }

    void FinishTiming()
    {
        finished = true;
        if (occasion == Occasion.Skill)
            Clear(false);
    }

    // Existing and future PlayPetCry events must belong to the action that armed this receiver.
    public void PlayPetCry(AnimationEvent marker)
    {
        if (occasion == Occasion.None || finished || !timing.enabled || !timing.UsesEvent || GameplayPause.IsPaused
            || Time.time < startedAt || marker == null || !marker.isFiredByAnimator
            || marker.animatorStateInfo.fullPathHash != stateHash || !TryReadState(out _, out _))
            return;
        PlayOnce();
    }

    void PlayOnce()
    {
        if (fired || crySource == null || crySource.clip == null || !crySource.isActiveAndEnabled)
            return;
        if (occasion != Occasion.Death && GameplayPause.PlayTime - lastCryAt < minimumCryInterval)
            return;
        fired = true;
        playedAt = lastCryAt = GameplayPause.PlayTime;
        playedDuration = crySource.clip.length / Mathf.Max(.01f, Mathf.Abs(crySource.pitch));
        crySource.Play();
    }
}
