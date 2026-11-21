using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Viền sáng khi Player tới gần vật tương tác (cửa, vật phẩm, rương, NPC...).
// Script khác chỉ cần gọi SetHighlighted(true / false).
[DisallowMultipleComponent]
public class InteractionHighlight : MonoBehaviour
{
    const string ShaderName = "Capstone/InteractionOutline";
    const string OutlineChildName = "__InteractionOutline";

    static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static Material fallbackMaterial;

    [Tooltip("Material dùng shader Capstone/InteractionOutline. Nên gán để chắc chắn shader có trong bản Build.")]
    [SerializeField] Material outlineMaterial;
    [Tooltip("Để trống = tự lấy mọi MeshRenderer / SkinnedMeshRenderer con")]
    [SerializeField] Renderer[] targets;

    [Header("Hiển thị")]
    [SerializeField] Color color = Color.white;
    [SerializeField, Range(0f, 0.1f)] float width = 0.015f;
    [SerializeField, Range(0f, 4f)] float intensity = 1.5f;
    [Tooltip("Tốc độ hiện / tắt viền")]
    [SerializeField, Min(0.1f)] float fadeSpeed = 8f;

    [Header("Nhấp nháy nhẹ")]
    [SerializeField] bool pulse = true;
    [SerializeField, Min(0f)] float pulseSpeed = 3f;
    [SerializeField, Range(0f, 1f)] float pulseAmount = 0.3f;

    // Renderer vẽ viền + vị trí material viền trong renderer đó.
    readonly List<(Renderer renderer, int materialIndex)> outlineSlots = new List<(Renderer, int)>();
    readonly List<GameObject> createdChildren = new List<GameObject>();
    readonly List<(Renderer renderer, Material[] original)> appendedRenderers = new List<(Renderer, Material[])>();
    MaterialPropertyBlock block;
    bool built;
    bool highlighted;
    float alpha;

    public bool IsHighlighted => highlighted;

    public void SetHighlighted(bool value)
    {
        if (highlighted == value) return;
        highlighted = value;
        if (value) { Build(); SetOutlineVisible(true); }
    }

    void Update()
    {
        if (!built) return;
        float target = highlighted ? 1f : 0f;
        if (Mathf.Approximately(alpha, target) && !highlighted) return;

        alpha = Mathf.MoveTowards(alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        float glow = intensity;
        if (pulse) glow *= 1f - pulseAmount * 0.5f + pulseAmount * 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
        ApplyProperties(alpha, glow);

        if (!highlighted && alpha <= 0f) SetOutlineVisible(false);
    }

    void OnDisable()
    {
        highlighted = false;
        alpha = 0f;
        if (built) SetOutlineVisible(false);
    }

    void OnDestroy()
    {
        foreach (var child in createdChildren) if (child != null) Destroy(child);
        foreach (var (renderer, original) in appendedRenderers) if (renderer != null) renderer.sharedMaterials = original;
    }

    void Build()
    {
        if (built) return;
        built = true;
        block = new MaterialPropertyBlock();

        Material material = ResolveMaterial();
        if (material == null)
        {
            Debug.LogWarning($"{name}: không tìm thấy shader '{ShaderName}'. Hãy gán Outline Material.", this);
            return;
        }

        Renderer[] sources = targets != null && targets.Length > 0 ? targets : GetComponentsInChildren<Renderer>(true);
        HashSet<Renderer> lowerLods = CollectLowerLodRenderers();
        foreach (Renderer source in sources)
        {
            if (source == null || source.name == OutlineChildName) continue;
            if (lowerLods.Contains(source)) continue;   // Chỉ vẽ viền cho LOD0, tránh 3 lớp viền chồng nhau.

            if (source is MeshRenderer meshRenderer) AddMeshOutline(meshRenderer, material);
            else if (source is SkinnedMeshRenderer skinned) AddSkinnedOutline(skinned, material);
        }

        if (outlineSlots.Count == 0)
            Debug.LogWarning($"{name}: không có MeshRenderer nào để vẽ viền.", this);
    }

    HashSet<Renderer> CollectLowerLodRenderers()
    {
        var result = new HashSet<Renderer>();
        LODGroup group = GetComponentInChildren<LODGroup>(true);
        if (group == null) group = GetComponentInParent<LODGroup>();
        if (group == null) return result;

        LOD[] lods = group.GetLODs();
        if (lods.Length == 0) return result;
        var lod0 = new HashSet<Renderer>(lods[0].renderers);
        for (int i = 1; i < lods.Length; i++)
            foreach (Renderer renderer in lods[i].renderers)
                if (renderer != null && !lod0.Contains(renderer)) result.Add(renderer);
        return result;
    }

    // MeshRenderer: tạo object con vẽ lại cùng mesh bằng material viền (đủ cho mọi sub-mesh).
    void AddMeshOutline(MeshRenderer source, Material material)
    {
        var filter = source.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;
        if (source.isPartOfStaticBatch)
        {
            Debug.LogWarning($"{source.name}: object Static (đã gộp batch) không vẽ viền được. Bỏ tick Static.", source);
            return;
        }

        var child = new GameObject(OutlineChildName);
        child.layer = source.gameObject.layer;
        child.transform.SetParent(source.transform, false);
        child.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

        var renderer = child.AddComponent<MeshRenderer>();
        var materials = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.enabled = false;

        createdChildren.Add(child);
        for (int i = 0; i < materials.Length; i++) outlineSlots.Add((renderer, i));
    }

    // SkinnedMeshRenderer (pet, NPC): thêm material viền vào cuối danh sách material.
    void AddSkinnedOutline(SkinnedMeshRenderer source, Material material)
    {
        Material[] original = source.sharedMaterials;
        appendedRenderers.Add((source, original));
        outlineSlots.Add((source, -1));   // -1: chỉ gắn khi đang hiện.
    }

    void SetOutlineVisible(bool visible)
    {
        Material material = ResolveMaterial();
        foreach (var (renderer, original) in appendedRenderers)
        {
            if (renderer == null) continue;
            if (visible && material != null)
            {
                var list = new List<Material>(original) { material };
                renderer.sharedMaterials = list.ToArray();
            }
            else renderer.sharedMaterials = original;
        }

        foreach (var child in createdChildren)
            if (child != null) child.GetComponent<MeshRenderer>().enabled = visible;

        if (visible) ApplyProperties(alpha, intensity);
    }

    void ApplyProperties(float currentAlpha, float currentIntensity)
    {
        if (block == null) return;
        foreach (var (renderer, slot) in outlineSlots)
        {
            if (renderer == null) continue;
            int index = slot >= 0 ? slot : renderer.sharedMaterials.Length - 1;
            renderer.GetPropertyBlock(block, index);
            block.SetColor(ColorId, color);
            block.SetFloat(WidthId, width);
            block.SetFloat(IntensityId, currentIntensity);
            block.SetFloat(AlphaId, currentAlpha);
            renderer.SetPropertyBlock(block, index);
        }
    }

    Material ResolveMaterial()
    {
        if (outlineMaterial != null) return outlineMaterial;
        if (fallbackMaterial != null) return fallbackMaterial;
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) return null;
        fallbackMaterial = new Material(shader) { name = "InteractionOutline (Runtime)" };
        return fallbackMaterial;
    }
}