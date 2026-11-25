using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace Capstone.Game.UISystem {
    /// <summary>Full-screen menu pause. Transient HUD/input locks are deliberately not pause owners.</summary>
    public static class GameplayPause {
        static readonly HashSet<Object> owners = new HashSet<Object>();
        static readonly List<Object> expired = new List<Object>();
        static readonly List<(NavMeshAgent agent, bool stopped)> agents = new List<(NavMeshAgent,bool)>();
        static readonly List<(Animator animator, float speed)> animators = new List<(Animator,float)>();
        static readonly List<ParticleSystem> particles = new List<ParticleSystem>();
        static readonly List<AudioSource> voices = new List<AudioSource>();
        static readonly List<(VisualEffect effect,bool paused)> graphs = new List<(VisualEffect,bool)>();
        static float previousScale=1, pausedAt, pausedDuration;
        static GameplayPauseLifetime lifetime;
        public static bool IsPaused => owners.Count>0;
        public static float PlayTime => Time.unscaledTime-pausedDuration-(IsPaused?Time.unscaledTime-pausedAt:0);
        public static float PlayDelta => IsPaused?0:Time.unscaledDeltaTime;
        public static System.Collections.IEnumerator WaitForSeconds(float seconds){float until=PlayTime+Mathf.Max(0,seconds);while(PlayTime<until)yield return null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){ReleaseAll();pausedDuration=0;lifetime=null;SceneManager.sceneUnloaded-=SceneUnloaded;SceneManager.sceneUnloaded+=SceneUnloaded;}
        public static void SetPaused(Object owner,bool value){
            if(owner==null)return;
            if(owner is Behaviour behaviour&&!behaviour.isActiveAndEnabled)value=false;
            if(value){
                if(owners.Contains(owner))return;
                bool first=owners.Count==0;owners.Add(owner);
                if(!first)return;
                previousScale=Time.timeScale;pausedAt=Time.unscaledTime;Time.timeScale=0;
                if(lifetime==null){var go=new GameObject("Gameplay pause lifetime");Object.DontDestroyOnLoad(go);lifetime=go.AddComponent<GameplayPauseLifetime>();}
                FreezeWorld();
            }else if(owners.Remove(owner)&&owners.Count==0)Resume();
        }
        static void FreezeWorld(){
            foreach(var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))if(agent.enabled&&agent.isOnNavMesh){agents.Add((agent,agent.isStopped));agent.isStopped=true;}
            foreach(var animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)){
                if(animator.GetComponentInParent<PetController>()==null&&animator.GetComponentInParent<BasicPlayerMovement>()==null&&animator.GetComponentInParent<CreatureSystem.CreatureRolePresentation>()==null)continue;
                animators.Add((animator,animator.speed));animator.speed=0;
            }
            foreach(var particle in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))if(particle.isPlaying&&particle.GetComponentInParent<Canvas>()==null){particles.Add(particle);particle.Pause(false);}
            foreach(var audio in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))if(audio.isPlaying&&audio.spatialBlend>.01f&&audio.GetComponentInParent<Canvas>()==null){voices.Add(audio);audio.Pause();}
            foreach(var effect in Object.FindObjectsByType<VisualEffect>(FindObjectsSortMode.None))if(effect.GetComponentInParent<Canvas>()==null){graphs.Add((effect,effect.pause));effect.pause=true;}
        }
        static void Resume(){
            pausedDuration+=Time.unscaledTime-pausedAt;Time.timeScale=previousScale;
            foreach(var entry in agents)if(entry.agent!=null&&entry.agent.enabled&&entry.agent.isOnNavMesh)entry.agent.isStopped=entry.stopped;
            foreach(var entry in animators)if(entry.animator!=null&&Mathf.Approximately(entry.animator.speed,0))entry.animator.speed=entry.speed;
            foreach(var particle in particles)if(particle!=null&&particle.gameObject.activeInHierarchy&&particle.isPaused)particle.Play(false);
            foreach(var audio in voices)if(audio!=null&&audio.isActiveAndEnabled)audio.UnPause();
            foreach(var entry in graphs)if(entry.effect!=null)entry.effect.pause=entry.paused;
            agents.Clear();animators.Clear();particles.Clear();voices.Clear();graphs.Clear();
        }
        internal static void Prune(){expired.Clear();foreach(var owner in owners)if(owner==null||(owner is Behaviour b&&!b.isActiveAndEnabled))expired.Add(owner);foreach(var owner in expired)owners.Remove(owner);if(expired.Count>0&&owners.Count==0)Resume();}
        static void SceneUnloaded(Scene scene){Prune();}
        internal static void ReleaseAll(){if(owners.Count==0)return;owners.Clear();Resume();}
    }
}

