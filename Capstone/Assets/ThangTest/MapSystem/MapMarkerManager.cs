using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AaMapIcon = AAMAP.MapIcon;

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public class MapMarkerManager : MonoBehaviour {
        [Header("References")]
        [SerializeField] AAMapRuntimeBinder mapBinder = null;
        [SerializeField] GameObject mapIconPrefab = null;
        [SerializeField] Transform iconContainer = null;

        [Header("Default Icons")]
        [SerializeField] Texture playerIcon = null;
        [SerializeField] Texture petIcon = null;
        [SerializeField] Texture enemyIcon = null;
        [SerializeField] Texture bossIcon = null;
        [SerializeField] Texture npcIcon = null;
        [SerializeField] Texture questIcon = null;
        [SerializeField] Texture itemIcon = null;
        [SerializeField] Texture shopIcon = null;
        [SerializeField] Texture fastTravelIcon = null;
        [SerializeField] Texture coopPlayerIcon = null;

        [Header("Default Colors")]
        [SerializeField] Color playerColor = new Color(0.2f, 0.85f, 1f, 1f);
        [SerializeField] Color petColor = new Color(0.3f, 1f, 0.55f, 1f);
        [SerializeField] Color enemyColor = new Color(1f, 0.25f, 0.18f, 1f);
        [SerializeField] Color bossColor = new Color(1f, 0.1f, 0.55f, 1f);
        [SerializeField] Color npcColor = new Color(1f, 0.86f, 0.35f, 1f);
        [SerializeField] Color questColor = new Color(0.55f, 0.95f, 1f, 1f);
        [SerializeField] Color itemColor = new Color(0.95f, 0.8f, 0.35f, 1f);
        [SerializeField] Color shopColor = new Color(0.9f, 0.65f, 1f, 1f);
        [SerializeField] Color fastTravelColor = new Color(0.45f, 0.95f, 1f, 1f);
        [SerializeField] Color coopPlayerColor = new Color(0.55f, 0.75f, 1f, 1f);

        [Header("Auto Markers")]
        [SerializeField] bool scanExistingMarkers = true;
        [SerializeField] bool autoCreatePlayerMarker = true;
        [SerializeField] bool autoCreatePetMarkers = true;
        [SerializeField] bool autoCreateEnemyMarkers = true;
        [SerializeField, Min(0.1f)] float markerScanInterval = 0.75f;

        [Header("Icon Presentation")]
        [SerializeField] bool useMarkerTextureOverrides = false;
        [SerializeField] Vector3 defaultIconScale = new Vector3(0.28f, 1f, 0.28f);
        [SerializeField] Vector3 playerIconScale = new Vector3(0.32f, 1f, 0.32f);
        [SerializeField] Vector3 bossIconScale = new Vector3(0.42f, 1f, 0.42f);

        [Header("Filters")]
        [SerializeField] List<MapMarkerType> hiddenMarkerTypes = new List<MapMarkerType>();

        readonly Dictionary<MapMarker, AaMapIcon> iconsByMarker = new Dictionary<MapMarker, AaMapIcon>();
        float nextScanTime;

        void Awake() {
            ResolveReferences();
            ScanMarkers();
        }

        void OnEnable() {
            MapMarker.MarkerEnabled += RegisterMarker;
            MapMarker.MarkerDisabled += UnregisterMarker;
            MapMarker.MarkerChanged += RefreshMarkerIcon;
            ResolveReferences();
        }

        void OnDisable() {
            MapMarker.MarkerEnabled -= RegisterMarker;
            MapMarker.MarkerDisabled -= UnregisterMarker;
            MapMarker.MarkerChanged -= RefreshMarkerIcon;
        }

        void Start() {
            ResolveReferences();
            ScanMarkers();
        }

        void LateUpdate() {
            if (Time.unscaledTime >= nextScanTime) {
                nextScanTime = Time.unscaledTime + markerScanInterval;
                ScanMarkers();
            }

            RefreshVisibility();
        }

        public void ScanMarkers() {
            ResolveReferences();
            AutoCreateMarkers();

            if (!scanExistingMarkers) return;

            foreach (MapMarker marker in FindObjectsByType<MapMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                RegisterMarker(marker);
            }
        }

        public void RegisterMarker(MapMarker marker) {
            if (marker == null || iconsByMarker.ContainsKey(marker)) return;

            AaMapIcon icon = CreateIcon(marker);
            if (icon == null) return;

            iconsByMarker.Add(marker, icon);
            ConfigureIcon(marker, icon);
        }

        public void UnregisterMarker(MapMarker marker) {
            if (marker == null || !iconsByMarker.TryGetValue(marker, out AaMapIcon icon)) return;

            iconsByMarker.Remove(marker);
            if (icon != null) Destroy(icon.gameObject);
        }

        public MapMarker EnsureRuntimeMarker(GameObject source, MapMarkerType markerType, string id, string title) {
            if (source == null) return null;

            MapMarker marker = source.GetComponent<MapMarker>();
            if (marker == null) marker = source.AddComponent<MapMarker>();

            marker.ConfigureRuntime(
                markerType,
                id,
                title,
                GetDefaultTexture(markerType),
                GetDefaultColor(markerType),
                true,
                true);

            marker.ConfigureRotation(markerType != MapMarkerType.Player, true, 0f);
            RegisterMarker(marker);
            RefreshMarkerIcon(marker);
            return marker;
        }

        public void SetMarkerTypeVisible(MapMarkerType markerType, bool visible) {
            if (visible) hiddenMarkerTypes.Remove(markerType);
            else if (!hiddenMarkerTypes.Contains(markerType)) hiddenMarkerTypes.Add(markerType);
            RefreshVisibility();
        }

        public bool IsMarkerTypeVisible(MapMarkerType markerType) {
            return !hiddenMarkerTypes.Contains(markerType);
        }

        public void ShowAllMarkerTypes() {
            hiddenMarkerTypes.Clear();
            RefreshVisibility();
        }

        void RefreshMarkerIcon(MapMarker marker) {
            if (marker == null) return;
            if (!iconsByMarker.TryGetValue(marker, out AaMapIcon icon)) return;
            ConfigureIcon(marker, icon);
        }

        void RefreshVisibility() {
            if (iconsByMarker.Count == 0) return;

            Transform viewer = mapBinder != null ? mapBinder.PlayerTarget : null;
            bool mapOpen = mapBinder != null && mapBinder.MapManager != null && mapBinder.MapManager.IsMapEnabled();

            foreach (var pair in iconsByMarker) {
                MapMarker marker = pair.Key;
                AaMapIcon icon = pair.Value;
                if (marker == null || icon == null) continue;

                if (!IsMarkerTypeVisible(marker.MarkerType)) {
                    icon.gameObject.SetActive(false);
                    continue;
                }

                bool showForMinimap = marker.ShouldRender(viewer, true);
                bool showForMap = mapOpen && marker.ShouldRender(viewer, false);
                icon.gameObject.SetActive(showForMinimap || showForMap);
            }
        }

        void ResolveReferences() {
            if (mapBinder == null) mapBinder = FindFirst<AAMapRuntimeBinder>();
            if (mapBinder != null) {
                mapBinder.ResolveReferences();
                mapBinder.ApplyBindings();
            }

            if (iconContainer == null) iconContainer = transform;
            if (mapIconPrefab == null) {
                AaMapIcon sceneIcon = FindFirst<AaMapIcon>();
                if (sceneIcon != null) mapIconPrefab = sceneIcon.gameObject;
            }
        }

        void AutoCreateMarkers() {
            if (autoCreatePlayerMarker) {
                Transform player = ResolvePlayerMarkerTarget(mapBinder != null ? mapBinder.PlayerTarget : null);
                if (player != null) EnsureRuntimeMarker(player.gameObject, MapMarkerType.Player, "player", "Player");
            }

            if (autoCreatePetMarkers) EnsureMarkersForBehaviour("PetController", MapMarkerType.Pet);
            if (autoCreateEnemyMarkers) EnsureMarkersForBehaviour("DummyEnemy", MapMarkerType.Enemy);
        }

        static Transform ResolvePlayerMarkerTarget(Transform candidate) {
            if (candidate == null) candidate = AAMapRuntimeBinder.FindPlayerTarget();
            if (candidate == null) return null;

            foreach (var behaviour in candidate.GetComponentsInParent<MonoBehaviour>(true)) {
                if (behaviour != null && behaviour.GetType().Name == "BasicPlayerMovement") return behaviour.transform;
            }

            foreach (var behaviour in candidate.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (behaviour != null && behaviour.GetType().Name == "BasicPlayerMovement") return behaviour.transform;
            }

            return candidate;
        }

        void EnsureMarkersForBehaviour(string behaviourTypeName, MapMarkerType markerType) {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (behaviour == null || behaviour.GetType().Name != behaviourTypeName) continue;
                EnsureRuntimeMarker(behaviour.gameObject, markerType, behaviour.gameObject.name, behaviour.gameObject.name);
            }
        }

        AaMapIcon CreateIcon(MapMarker marker) {
            GameObject iconObject = null;

            if (mapIconPrefab != null) {
                iconObject = Instantiate(mapIconPrefab, marker.transform);
            } else {
                iconObject = CreateFallbackIconObject(marker.transform);
            }

            if (iconObject == null) return null;

            iconObject.name = marker.DisplayName + " Map Icon";
            AaMapIcon icon = iconObject.GetComponent<AaMapIcon>();
            if (icon == null) icon = iconObject.GetComponentInChildren<AaMapIcon>();
            if (icon == null) icon = iconObject.AddComponent<AaMapIcon>();
            iconObject.SetActive(true);
            return icon;
        }

        GameObject CreateFallbackIconObject(Transform parent) {
            GameObject root = new GameObject("Map Icon");
            root.transform.SetParent(parent, false);

            GameObject visuals = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visuals.name = "Visuals";
            visuals.transform.SetParent(root.transform, false);
            visuals.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Collider collider = visuals.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            MeshRenderer renderer = visuals.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateIconMaterial(null, Color.white);

            root.AddComponent<AaMapIcon>();
            return root;
        }

        void ConfigureIcon(MapMarker marker, AaMapIcon icon) {
            if (marker == null || icon == null) return;

            Texture defaultTexture = GetDefaultTexture(marker.MarkerType);
            Texture texture = ShouldUseMarkerTexture(marker) ? marker.IconTexture : defaultTexture;
            if (texture == null) texture = marker.IconTexture;
            Color color = marker.IconColor == default(Color) ? GetDefaultColor(marker.MarkerType) : marker.IconColor;
            Vector3 scale = ResolveIconScale(marker);

            icon.iconTexture = texture;
            icon.iconColor = color;
            icon.iconOffset = marker.IconOffset;
            icon.iconScale = scale;
            icon.iconRotation = marker.IconRotation;
            icon.rotateWithCamera = marker.RotateWithMinimapCamera;
            icon.haveCustomRotation = marker.UseMapCameraRotation;
            icon.customRotation = marker.MapCameraRotation;

            if (mapBinder != null) {
                icon.minimapCamera = mapBinder.MinimapCamera;
                icon.mapCamera = mapBinder.MapCamera;
            }

            EnsureIconVisuals(icon, texture, color);

            icon.transform.localPosition = marker.IconOffset;
            icon.transform.localScale = scale;
        }

        bool ShouldUseMarkerTexture(MapMarker marker) {
            if (marker == null || marker.IconTexture == null) return false;
            return useMarkerTextureOverrides || marker.MarkerType == MapMarkerType.Custom;
        }

        Vector3 ResolveIconScale(MapMarker marker) {
            if (marker == null) return defaultIconScale;

            Vector3 markerScale = marker.IconScale;
            bool legacyLargeScale = markerScale.x >= 2.5f || markerScale.z >= 2.5f;
            if (!legacyLargeScale) return markerScale;

            switch (marker.MarkerType) {
                case MapMarkerType.Player:
                    return playerIconScale;
                case MapMarkerType.Boss:
                    return bossIconScale;
                default:
                    return defaultIconScale;
            }
        }

        void EnsureIconVisuals(AaMapIcon icon, Texture texture, Color color) {
            if (icon == null) return;

            Transform visuals = icon.transform.Find("Visuals");
            if (visuals == null) {
                GameObject visualsObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visualsObject.name = "Visuals";
                visualsObject.transform.SetParent(icon.transform, false);
                visualsObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                Collider collider = visualsObject.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                visuals = visualsObject.transform;
            }

            MeshRenderer renderer = visuals.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = visuals.gameObject.AddComponent<MeshRenderer>();

            Material source = renderer.sharedMaterial;
            if (source == null && icon.iconMaterial != null) source = icon.iconMaterial;

            Material material = CreateIconMaterial(texture, color, source);
            renderer.sharedMaterial = material;
            icon.iconMaterial = material;
        }

        Texture GetDefaultTexture(MapMarkerType markerType) {
            switch (markerType) {
                case MapMarkerType.Player: return playerIcon;
                case MapMarkerType.Pet: return petIcon;
                case MapMarkerType.Enemy: return enemyIcon;
                case MapMarkerType.Boss: return bossIcon != null ? bossIcon : enemyIcon;
                case MapMarkerType.NPC: return npcIcon;
                case MapMarkerType.QuestAvailable:
                case MapMarkerType.QuestTarget: return questIcon;
                case MapMarkerType.Item: return itemIcon;
                case MapMarkerType.Shop: return shopIcon != null ? shopIcon : npcIcon;
                case MapMarkerType.FastTravel: return fastTravelIcon != null ? fastTravelIcon : questIcon;
                case MapMarkerType.CoopPlayer: return coopPlayerIcon != null ? coopPlayerIcon : playerIcon;
                default: return null;
            }
        }

        Color GetDefaultColor(MapMarkerType markerType) {
            switch (markerType) {
                case MapMarkerType.Player: return playerColor;
                case MapMarkerType.Pet: return petColor;
                case MapMarkerType.Enemy: return enemyColor;
                case MapMarkerType.Boss: return bossColor;
                case MapMarkerType.NPC: return npcColor;
                case MapMarkerType.QuestAvailable:
                case MapMarkerType.QuestTarget: return questColor;
                case MapMarkerType.Item: return itemColor;
                case MapMarkerType.Shop: return shopColor;
                case MapMarkerType.FastTravel: return fastTravelColor;
                case MapMarkerType.CoopPlayer: return coopPlayerColor;
                default: return Color.white;
            }
        }

        static Material CreateIconMaterial(Texture texture, Color color, Material source = null) {
            Material material = source != null ? new Material(source) : new Material(FindIconShader());
            ConfigureTransparentIconMaterial(material);
            SetMaterialTexture(material, texture);
            SetMaterialColor(material, color);
            return material;
        }

        static Shader FindIconShader() {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            return shader != null ? shader : Shader.Find("Standard");
        }

        static void ConfigureTransparentIconMaterial(Material material) {
            if (material == null) return;

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        static void SetMaterialTexture(Material material, Texture texture) {
            if (material == null || texture == null) return;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", texture);
        }

        static void SetMaterialColor(Material material, Color color) {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color);
        }

        static T FindFirst<T>() where T : Object {
            T[] items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }
    }
}


