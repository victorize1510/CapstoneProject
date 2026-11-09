using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Resolves semantic actions once per visual binding. Gameplay callers retain their own command/cooldown gates.</summary>
    [DisallowMultipleComponent]
    public sealed class CreatureAnimationDriver : MonoBehaviour {
        struct Resolved { public int hash; public string[] names; }
        readonly Dictionary<CreatureAction, Resolved> actions = new Dictionary<CreatureAction, Resolved>();
        readonly Dictionary<string, CreatureAction> skills = new Dictionary<string, CreatureAction>(StringComparer.OrdinalIgnoreCase);
        Animator animator;
        public Animator BoundAnimator => animator;
        public CreatureAnimationProfile Profile { get; private set; }
        public string LastError { get; private set; }
        public int BindingRevision { get; private set; }
        public void Bind(Animator next) {
            animator = next;
            var binding = next != null ? next.GetComponent<CreatureVisualBinding>() : null;
            Profile = binding != null ? binding.animationProfile : null;
            actions.Clear(); skills.Clear(); LastError = string.Empty; BindingRevision++;
            if (Profile == null || animator == null) return;
            var duplicateActions = new HashSet<CreatureAction>();
            foreach (var item in Profile.actions) {
                if (item == null || item.action == CreatureAction.None || string.IsNullOrWhiteSpace(item.state)) continue;
                if (duplicateActions.Contains(item.action)) continue;
                if (actions.ContainsKey(item.action)) { actions.Remove(item.action); duplicateActions.Add(item.action); continue; }
                string path = item.state.Contains(".") ? item.state : "Base Layer." + item.state;
                int hash = Animator.StringToHash(path);
                if (!animator.HasState(0, hash)) { LastError = $"{Profile.name}: thiếu state {path}."; continue; }
                actions[item.action] = new Resolved { hash = hash, names = new[] { path } };
            }
            var duplicateSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Profile.skills) {
                if (item == null || string.IsNullOrWhiteSpace(item.skillId)) continue;
                string id = item.skillId.Trim();
                if (duplicateSkills.Contains(id)) continue;
                if (skills.ContainsKey(id)) { skills.Remove(id); duplicateSkills.Add(id); continue; }
                skills[id] = item.action;
            }
        }
        public bool TryGetStates(CreatureAction action, out string[] states) {
            if (actions.TryGetValue(action, out var result)) { states = result.names; LastError = string.Empty; return true; }
            states = Array.Empty<string>();
            LastError = $"{(Profile != null ? Profile.name : name)}: chưa cấu hình hành động {action}. Hãy kiểm tra Animator/Profile.";
            return false;
        }
        public bool TryGetSkillStates(string skillId, out string[] states) {
            states = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(skillId) && skills.TryGetValue(skillId.Trim(), out var action)) return TryGetStates(action, out states);
            LastError = $"{(Profile != null ? Profile.name : name)}: chưa gán hành động cho kỹ năng {skillId}.";
            return false;
        }
        public bool TryPlay(CreatureAction action, float fade = .08f) {
            if (animator == null || !animator.isActiveAndEnabled) { LastError = "Animator chưa sẵn sàng để phát hành động."; return false; }
            if (!TryGetStates(action, out _)) return false;
            animator.CrossFadeInFixedTime(actions[action].hash, Mathf.Max(0, fade), 0, 0);
            return true;
        }
    }
}
