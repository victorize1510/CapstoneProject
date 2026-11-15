using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.Skills {
    /// <summary>Session-only deadlines keyed by individual and skill, never by loadout slot.</summary>
    public static class PetSkillCooldowns {
        readonly struct Key : IEquatable<Key> {
            readonly string pet, skill;
            public Key(string pet, string skill) { this.pet = pet; this.skill = skill; }
            public bool Equals(Key other) => pet == other.pet && skill == other.skill;
            public override bool Equals(object other) => other is Key key && Equals(key);
            public override int GetHashCode() => ((pet?.GetHashCode() ?? 0) * 397) ^ (skill?.GetHashCode() ?? 0);
        }
        static readonly Dictionary<Key, float> ends = new Dictionary<Key, float>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { ends.Clear(); }
        public static float Remaining(string pet, string skill) => ends.TryGetValue(new Key(pet, skill), out float end) ? Mathf.Max(0, end - Time.time) : 0;
        internal static void Set(string pet, string skill, float seconds) {
            var key = new Key(pet, skill);
            if (seconds <= 0) ends.Remove(key); else ends[key] = Time.time + seconds;
        }
    }
}
