using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tạo ra các renderer "bản sao" đè lên model gốc, dùng vật liệu trắng trong suốt.
/// Bằng cách tăng alpha của các renderer bản sao này lên 1, ta che hoàn toàn model gốc
/// -> tạo cảm giác "phát sáng trắng, không thấy model". Nếu material có bật Emission,
/// script còn tự điều khiển luôn cường độ Emission (HDR) đồng bộ theo alpha, để tạo
/// hiệu ứng chói/bloom thật sự thay vì chỉ là 1 khối trắng phẳng.
///
/// Vì các renderer bản sao dùng CHUNG mesh (và chung bones nếu là SkinnedMeshRenderer)
/// với renderer gốc, nên chúng sẽ tự động khớp hình dáng + khớp animation của model,
/// mà không cần biết trước model đó dùng material gì.
/// </summary>
public class WhiteoutOverlay
{
    private readonly List<Renderer> overlayRenderers = new List<Renderer>();
    private readonly List<MaterialPropertyBlock> propertyBlocks = new List<MaterialPropertyBlock>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // Nếu > 0, sẽ đồng thời set _EmissionColor = trắng * emissionIntensity * alpha,
    // tạo hiệu ứng chói/bloom (cần material bật sẵn Emission + Bloom bật trong URP Volume).
    // Nếu = 0, chỉ dùng alpha (khối trắng phẳng, không chói bloom).
    private readonly float emissionIntensity;

    /// <summary>
    /// Quét toàn bộ Renderer (MeshRenderer + SkinnedMeshRenderer) bên trong targetRoot,
    /// tạo overlay tương ứng cho từng cái.
    /// </summary>
    public WhiteoutOverlay(Transform targetRoot, Material whiteOverlayMaterial, float emissionIntensity = 0f)
    {
        this.emissionIntensity = emissionIntensity;

        // Lấy tất cả renderer, kể cả renderer nằm trong các con (ví dụ tay, cánh, phụ kiện...)
        foreach (var srcRenderer in targetRoot.GetComponentsInChildren<Renderer>(true))
        {
            GameObject overlayGO = new GameObject("WhiteoutOverlay");
            overlayGO.transform.SetParent(srcRenderer.transform, worldPositionStays: false);
            overlayGO.transform.localPosition = Vector3.zero;
            overlayGO.transform.localRotation = Quaternion.identity;
            overlayGO.transform.localScale = Vector3.one;

            Renderer overlayRenderer;

            if (srcRenderer is SkinnedMeshRenderer srcSkinned)
            {
                // Với model có animation (skinned mesh), overlay cũng phải là SkinnedMeshRenderer
                // và dùng CHUNG mảng "bones" + rootBone với bản gốc thì mới bám đúng animation.
                var overlaySkinned = overlayGO.AddComponent<SkinnedMeshRenderer>();
                overlaySkinned.sharedMesh = srcSkinned.sharedMesh;
                overlaySkinned.bones = srcSkinned.bones;
                overlaySkinned.rootBone = srcSkinned.rootBone;
                overlayRenderer = overlaySkinned;
            }
            else if (srcRenderer is MeshRenderer)
            {
                var srcFilter = srcRenderer.GetComponent<MeshFilter>();
                if (srcFilter == null || srcFilter.sharedMesh == null)
                    continue;

                overlayGO.AddComponent<MeshFilter>().sharedMesh = srcFilter.sharedMesh;
                overlayRenderer = overlayGO.AddComponent<MeshRenderer>();
            }
            else
            {
                Object.Destroy(overlayGO);
                continue;
            }

            // Dùng CHUNG 1 material trắng cho mọi submesh, để tránh phải quan tâm
            // model gốc có bao nhiêu submesh / material slot.
            var mats = new Material[srcRenderer.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = whiteOverlayMaterial;
            overlayRenderer.sharedMaterials = mats;

            // Đẩy renderQueue lên cao hơn 1 chút để overlay luôn vẽ đè lên model gốc,
            // tránh z-fighting (2 mesh trùng khít nhau).
            overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, new Color(1f, 1f, 1f, 0f));
            if (emissionIntensity > 0f)
                block.SetColor(EmissionColorId, Color.black);
            overlayRenderer.SetPropertyBlock(block);

            overlayRenderers.Add(overlayRenderer);
            propertyBlocks.Add(block);
        }
    }

    /// <summary>
    /// Đặt độ "trắng xóa" từ 0 (không thấy overlay, thấy model bình thường) đến 1
    /// (che hoàn toàn model). Nếu emissionIntensity > 0, cường độ chói/bloom cũng
    /// tăng giảm theo cùng giá trị này.
    /// </summary>
    public void SetIntensity(float alpha01)
    {
        for (int i = 0; i < overlayRenderers.Count; i++)
        {
            if (overlayRenderers[i] == null) continue;

            propertyBlocks[i].SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha01));

            if (emissionIntensity > 0f)
            {
                float e = alpha01 * emissionIntensity;
                propertyBlocks[i].SetColor(EmissionColorId, new Color(e, e, e, 1f));
            }

            overlayRenderers[i].SetPropertyBlock(propertyBlocks[i]);
        }
    }

    /// <summary>Dọn dẹp các GameObject overlay đã tạo (gọi khi model bị destroy hoặc hết dùng).</summary>
    public void Cleanup()
    {
        foreach (var r in overlayRenderers)
        {
            if (r != null) Object.Destroy(r.gameObject);
        }
        overlayRenderers.Clear();
        propertyBlocks.Clear();
    }
}