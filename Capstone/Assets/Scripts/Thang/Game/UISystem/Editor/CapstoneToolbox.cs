using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Capstone.Game.UISystem.Editor
{
    /// <summary>One searchable entry point; old setup/QA commands remain available under Advanced.</summary>
    public sealed class CapstoneToolbox : EditorWindow
    {
        const string AdvancedPrefix = "Tools/Capstone/Advanced/";
        sealed class Command { public string path, label; }
        readonly List<Command> commands = new List<Command>();
        static readonly string[] groups = { "Tất cả", "Camera", "Pet / Combat", "UI", "Map", "Save / Inventory" };
        string search = "";
        int group;
        bool advanced;
        Vector2 scroll;
        BasicCameraFollow sceneCamera;
        double nextLookup;

        [MenuItem("Tools/Capstone/Toolbox", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<CapstoneToolbox>("Capstone Tools");
            window.minSize = new Vector2(530, 360);
        }

        [MenuItem("Tools/Capstone/Camera/Chọn camera hiện tại", priority = 10)]
        public static void SelectCamera()
        {
            var source = FindFirstObjectByType<BasicCameraFollow>();
            if (source == null) { Debug.LogWarning("Scene chưa có BasicCameraFollow."); return; }
            Selection.activeGameObject = source.gameObject;
            EditorGUIUtility.PingObject(source);
        }

        [MenuItem("Tools/Capstone/Camera/Kiểm cấu hình", priority = 11)]
        public static void InspectCamera()
        {
            Open();
            GetWindow<CapstoneToolbox>().group = 1;
        }

        void OnEnable()
        {
            Refresh();
            RefreshSceneCamera();
            EditorApplication.hierarchyChanged += RefreshSceneCamera;
        }

        void OnDisable() => EditorApplication.hierarchyChanged -= RefreshSceneCamera;

        void RefreshSceneCamera()
        {
            sceneCamera = FindFirstObjectByType<BasicCameraFollow>();
            nextLookup = EditorApplication.timeSinceStartup + .5;
        }

        void Refresh()
        {
            commands.Clear();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var method in TypeCache.GetMethodsWithAttribute<MenuItem>())
            foreach (var item in method.GetCustomAttributes<MenuItem>())
            {
                if (item.validate || !item.menuItem.StartsWith(AdvancedPrefix, StringComparison.Ordinal)) continue;
                string path = item.menuItem;
                int space = path.LastIndexOf(' ');
                if (space >= 0 && path.Substring(space + 1).IndexOfAny(new[] { '%', '#', '&', '_' }) >= 0)
                    path = path.Substring(0, space);
                if (seen.Add(path)) commands.Add(new Command { path = path, label = path.Substring(AdvancedPrefix.Length) });
            }
            commands.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.label, b.label));
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Capstone Toolbox", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Chọn camera để chỉnh thông số. Lệnh dựng lại asset và kiểm thử nằm trong Nâng cao; chỉ chạy khi bấm lệnh.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Chọn camera")) SelectCamera();
                if (GUILayout.Button("Làm mới danh sách")) { Refresh(); RefreshSceneCamera(); }
            }
            if (sceneCamera == null && EditorApplication.timeSinceStartup >= nextLookup) RefreshSceneCamera();
            DrawCameraStatus();
            advanced = EditorGUILayout.Foldout(advanced, "Nâng cao — setup / kiểm thử (" + commands.Count + ")", true);
            if (!advanced) return;
            group = GUILayout.Toolbar(group, groups);
            search = EditorGUILayout.TextField("Tìm lệnh", search);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var command in commands)
            {
                if (!Matches(command.label)) continue;
                if (GUILayout.Button(new GUIContent(command.label, command.path), EditorStyles.miniButton))
                {
                    if (!EditorApplication.ExecuteMenuItem(command.path))
                        Debug.LogWarning("Lệnh chưa khả dụng ở trạng thái hiện tại: " + command.path);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawCameraStatus()
        {
            if (sceneCamera == null) { EditorGUILayout.HelpBox("Scene chưa có gameplay camera.", MessageType.Info); return; }
            var rig = sceneCamera.freeLookRig;
            EditorGUILayout.LabelField("Scene", sceneCamera.gameObject.scene.name);
            EditorGUILayout.LabelField("Backend", rig != null && rig.IsReady ? "Cinemachine — một solver chung" : "Legacy — cùng solver boom");
            EditorGUILayout.LabelField("Collision", sceneCamera.enableCameraCollision ? "Giữ hướng, thu khoảng cách" : "Đang tắt");
            if (rig != null && !rig.IsReady)
                EditorGUILayout.HelpBox("Rig có tham chiếu thiếu. Kiểm camera/brain/targets trong Inspector trước khi chạy setup.", MessageType.Warning);
            if (rig != null && rig.IsReady && rig.brain.UpdateMethod != Unity.Cinemachine.CinemachineBrain.UpdateMethods.ManualUpdate)
                EditorGUILayout.HelpBox("Brain cần ManualUpdate để không cập nhật camera hai lần.", MessageType.Warning);
        }

        bool Matches(string label)
        {
            if (!string.IsNullOrWhiteSpace(search) && label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) return false;
            string key = label.ToLowerInvariant();
            switch (group)
            {
                case 1: return key.Contains("camera");
                case 2: return key.Contains("pet") || key.Contains("creature") || key.Contains("combat") || key.Contains("capture") || key.Contains("pve");
                case 3: return key.Contains("hud") || key.Contains("ui") || key.Contains("gui") || key.Contains("profile") || key.Contains("quest") || key.Contains("shop");
                case 4: return key.Contains("map");
                case 5: return key.Contains("save") || key.Contains("inventory") || key.Contains("recovery");
                default: return true;
            }
        }
    }
}
