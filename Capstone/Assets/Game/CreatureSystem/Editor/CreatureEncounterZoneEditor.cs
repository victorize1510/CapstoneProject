using UnityEditor;
using UnityEngine;

namespace Capstone.Game.CreatureSystem.Editor
{
    [CustomEditor(typeof(CreatureEncounterZone))]
    public sealed class CreatureEncounterZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            var zone = (CreatureEncounterZone)target;
            using (new EditorGUI.DisabledScope(Application.isPlaying)) {
                if (GUILayout.Button("Kiem tra cau hinh Zone")) {
                    bool valid = zone.ValidateConfiguration(out var reason);
                    if (valid) Debug.Log("[Creature Zone] " + reason, zone);
                    else Debug.LogError("[Creature Zone] " + reason, zone);
                }
            }
            if (GUILayout.Button("Kiem tra coverage NavMesh (khong bake)")) Debug.Log("[Creature Zone] " + zone.ProbeNavMeshCoverage(), zone);
            foreach (string issue in zone.ConfigurationIssues) EditorGUILayout.HelpBox("BLOCKED: " + issue, MessageType.Warning);
            if (!Application.isPlaying) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Slot dang giu / muc tieu", zone.OccupiedSlots + " / " + zone.TargetPopulation);
            EditorGUILayout.LabelField("So loai du dieu kien", zone.EligibleSpeciesCount.ToString());
            EditorGUILayout.LabelField("Tong da spawn", zone.TotalSpawned.ToString());
            EditorGUILayout.HelpBox(zone.LastStatus, MessageType.Info);
            EditorGUILayout.HelpBox(zone.LastPlacementReport, MessageType.None);
            if (zone.isActiveAndEnabled) Repaint();
        }
    }
}
