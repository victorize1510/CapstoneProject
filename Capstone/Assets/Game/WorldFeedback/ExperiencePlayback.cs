using System;
using System.Collections.Generic;
using Capstone.Game.CaptureSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.WorldFeedback {
    internal static class ExperiencePlayback {
        internal readonly struct Point {
            internal readonly int Level, Experience, Required;
            internal float Fraction => Required > 0 ? Mathf.Clamp01((float)Experience / Required) : 0;
            internal Point(int level, int experience, int required) {
                Level = level;
                Experience = experience;
                Required = required;
            }
        }

        const float Fill = .45f;
        const float Step = Fill + .15f;
        sealed class Gain {
            internal Point Before, After;
            internal Func<int, int> Requirement;
            internal float Start, End;
        }

        sealed class Track {
            internal string Name;
            internal readonly Queue<Gain> Gains = new Queue<Gain>();
            internal float End;
        }

        static readonly Dictionary<Object, Track> tracks = new Dictionary<Object, Track>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Clear() => tracks.Clear();

        internal static void Publish(Object owner, string name, Point before, Point after, Func<int, int> requirement) {
            if (!Application.isPlaying || owner == null) return;
            CaptureEventBatch.Publish(owner, "experience-playback:" + Guid.NewGuid(), () => {
                if (owner == null) return;
                if (!tracks.TryGetValue(owner, out Track track)) tracks[owner] = track = new Track();
                track.Name = name;
                float start = Mathf.Max(Time.unscaledTime, track.End);
                float end = start + (after.Level - before.Level) * Step + (after.Experience > 0 ? Fill : 0);
                track.Gains.Enqueue(new Gain { Before = before, After = after, Requirement = requirement, Start = start, End = end });
                track.End = end;
                AcquisitionToastView.Ensure();
            });
        }

        internal static bool TrySample(Object owner, out Point point) {
            point = default;
            if (owner == null || !tracks.TryGetValue(owner, out Track track) || track.Gains.Count == 0) return false;
            float now = Time.unscaledTime;
            while (track.Gains.Count > 1 && now >= track.Gains.Peek().End) track.Gains.Dequeue();
            Gain gain = track.Gains.Peek();
            if (now > gain.End + .6f) return false;
            if (now >= gain.End) { point = gain.After; return true; }

            float elapsed = Mathf.Max(0, now - gain.Start);
            int completed = Mathf.Min(gain.After.Level - gain.Before.Level, Mathf.FloorToInt(elapsed / Step));
            int level = gain.Before.Level + completed;
            int start = completed == 0 ? gain.Before.Experience : 0;
            int required = level == gain.After.Level ? gain.After.Required :
                completed == 0 ? gain.Before.Required : gain.Requirement(level);
            int target = level == gain.After.Level ? gain.After.Experience : required;
            float t = Mathf.Clamp01((elapsed - completed * Step) / Fill);
            point = new Point(level, Mathf.RoundToInt(Mathf.Lerp(start, target, t)), required);
            return true;
        }

        internal static void ReadVisible(List<(string name, Point point)> results, List<Object> expired) {
            results.Clear();
            expired.Clear();
            foreach (var pair in tracks) {
                if (TrySample(pair.Key, out Point point)) results.Add((pair.Value.Name, point));
                else expired.Add(pair.Key);
            }
            foreach (var owner in expired) tracks.Remove(owner);
        }
    }
}
