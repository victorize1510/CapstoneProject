using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CHỈ DÙNG ĐỂ TEST. Gắn trực tiếp lên GameObject của pet đang xài (cùng cấp với PetController).
/// Nhấn phím testKey (hoặc chuột phải vào component > Force Evolve trong Inspector) để tiến hóa
/// NGAY LẬP TỨC, bỏ qua hết điều kiện Level/Item, KHÔNG qua PetEvolutionService nên cũng KHÔNG
/// cộng stat, KHÔNG lưu save, KHÔNG trừ item — chỉ để xem thử hiệu ứng xoay/trắng xóa + đổi model
/// chạy đúng hay chưa. Nhớ GỠ script này ra khỏi pet trước khi build bản thật.
/// </summary>
public class PetEvolutionTestButton : MonoBehaviour
{
    [Tooltip("Nhấn phím này lúc Play để ép tiến hóa ngay, bỏ qua mọi điều kiện.")]
    public Key testKey = Key.T;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[testKey].wasPressedThisFrame)
            ForceEvolve();
    }

    [ContextMenu("Force Evolve (bỏ qua điều kiện)")]
    public void ForceEvolve()
    {
        PetController pet = GetComponent<PetController>();
        PetCollectionMetadata metadata = GetComponentInChildren<PetCollectionMetadata>(true);
        PetEvolutionVisualController visual = GetComponentInChildren<PetEvolutionVisualController>(true);
        PetHudRuntimeStats stats = GetComponentInChildren<PetHudRuntimeStats>(true);

        if (pet == null || metadata == null || visual == null)
        {
            Debug.LogWarning($"{name}: thiếu PetController/PetCollectionMetadata/PetEvolutionVisualController, không test được.");
            return;
        }

        if (!metadata.TryGetNextEvolutionRule(out PetEvolutionRule rule))
        {
            Debug.LogWarning($"{name}: {metadata.DefinitionId} đã ở dạng tiến hóa cuối, không còn rule nào.");
            return;
        }

        if (rule.TargetVisualPrefab == null)
        {
            Debug.LogWarning($"{name}: rule '{rule.EvolutionId}' chưa gán Target Visual Prefab, không có model để đổi.");
            return;
        }

        string currentName = metadata.ResolveDisplayName(stats != null ? stats.DisplayName : metadata.Species);
        string targetName = string.IsNullOrWhiteSpace(rule.TargetDisplayName) ? rule.TargetFormId : rule.TargetDisplayName;

        bool ApplyNow()
        {
            bool applied = metadata.ApplyEvolution(rule);
            if (applied) visual.TryApply(rule.TargetVisualPrefab, out string error);
            return applied;
        }

        if (PetEvolutionPresentationController.Instance != null)
        {
            Debug.Log($"{name}: bắt đầu test tiến hóa (có animation) -> {targetName}");
            PetEvolutionPresentationController.Instance.Play(pet, currentName, targetName,
                applyEvolution: ApplyNow,
                onComplete: success => Debug.Log($"{name}: test tiến hóa xong, success={success}"));
        }
        else
        {
            // Chưa có PetEvolutionPresentationController trong scene -> đổi model tức thì, không animation.
            Debug.LogWarning($"{name}: không tìm thấy PetEvolutionPresentationController trong scene, đổi model tức thì không có hiệu ứng.");
            ApplyNow();
        }
    }
}