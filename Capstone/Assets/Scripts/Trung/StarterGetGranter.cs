using Capstone.Game.SaveSystem;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    // Đưa thú khởi đầu vào Party (Party đầy thì vào Box).
    // StarterBallTable tự thêm và tự gọi component này, không cần nối gì trong Inspector.
    // Cách làm: tạo con thú từ Prefab -> gửi qua PetCaptureCoordinator.TryCommitCapture
    // (giống lúc bắt được thú, nên Party/Box, lưu game, PartyHUD đều cập nhật như bình thường).
    [DisallowMultipleComponent]
    public sealed class StarterPetGranter : MonoBehaviour {
        [Tooltip("Để trống = tự tìm trong scene")]
        [SerializeField] PetCaptureCoordinator captureCoordinator = null;
        [Tooltip("Để trống = tự tìm trong scene")]
        [SerializeField] PetBoxRuntimeProvider roster = null;
        [Tooltip("Bật = đi qua luồng bắt thú (PetCaptureCoordinator: tính nhiệm vụ/thành tựu bắt thú, bắt buộc lưu game được). "
            + "Tắt = đặt thẳng vào Party rồi thử lưu game")]
        [SerializeField] bool useCaptureFlow = false;
        [Tooltip("Để trống = tự tìm trong scene")]
        [SerializeField] PlayerSaveController saveController = null;
        [Tooltip("Bật = thả thú ra đi theo Player ngay sau khi nhận. Tắt = thú nằm trong Party, tự gọi ra sau")]
        [SerializeField] bool summonAfterGrant = false;
        [Tooltip("Khoảng cách sinh thú sau lưng Player")]
        [SerializeField, Min(0f)] float spawnBehindPlayer = 1.5f;

        public bool TryGrant(GameObject petPrefab, out string error) {
            if (petPrefab == null) return Fail("Quả bóng này chưa gắn Pet Prefab.", out error);
            if (petPrefab.GetComponentInChildren<PetController>(true) == null)
                return Fail($"Prefab '{petPrefab.name}' không có PetController.", out error);

            ResolveReferences();
            if (roster != null && !roster.HasCaptureRoom) return Fail("Party và Box đều đã đầy.", out error);

            PetCommandInput input = roster != null ? roster.PartyInput : FindFirstObjectByType<PetCommandInput>();
            Transform player = input != null ? input.transform : null;
            Vector3 position = player != null ? player.position - player.forward * spawnBehindPlayer : transform.position;
            Quaternion rotation = player != null ? player.rotation : Quaternion.identity;

            GameObject instance = Instantiate(petPrefab, position, rotation);
            instance.name = petPrefab.name;
            PetController pet = instance.GetComponentInChildren<PetController>(true);
            if (pet.owner == null && player != null) pet.AssignOwner(player);

            bool ok;
            if (useCaptureFlow && captureCoordinator != null) {
                PetCollectionMetadata metadata = instance.GetComponentInChildren<PetCollectionMetadata>(true);
                string instanceId = metadata != null ? metadata.PersistentId : string.Empty;
                string speciesId = metadata != null ? metadata.DefinitionId : string.Empty;
                ok = captureCoordinator.TryCommitCapture(pet, instanceId, speciesId, out error);
            }
            else ok = TryPlaceDirectly(pet, input, out error);

            if (!ok) {
                Destroy(instance);
                return false;
            }

            if (summonAfterGrant && input != null) {
                input.SetActivePet(pet);
                pet.Summon();
            }
            else pet.Withdraw();

            Debug.Log($"[StarterPet] Đã thêm {petPrefab.name} vào Party/Box.", this);
            error = string.Empty;
            return true;
        }

        // Dự phòng khi scene không có PetCaptureCoordinator: đặt thẳng vào slot Party trống.
        bool TryPlaceDirectly(PetController pet, PetCommandInput input, out string error) {
            if (input == null || input.petSlots == null) return Fail("Không tìm thấy PetCommandInput (Party).", out error);
            for (int i = 0; i < Mathf.Min(6, input.petSlots.Length); i++) {
                if (input.petSlots[i] != null) continue;
                input.petSlots[i] = pet;
                if (input.activePet == null) input.SetActivePet(pet);
                if (roster != null) roster.Bind(input);   // Bind gọi Changed -> UI Party cập nhật.
                TrySave();
                error = string.Empty;
                return true;
            }
            if (roster == null) return Fail("Party đã đủ 6 pet.", out error);
            if (!roster.TryStoreCapturedPet(pet, out error)) return false;
            TrySave();
            return true;
        }

        // Lưu game nếu được. Lưu lỗi (vd. scene test chưa có save) thì vẫn giữ thú trong Party.
        void TrySave() {
            if (saveController == null) saveController = FindFirstObjectByType<PlayerSaveController>(FindObjectsInactive.Include);
            if (saveController == null) return;
            bool saved = false;
            try { saved = saveController.SaveNow(); }
            catch (System.Exception exception) { Debug.LogWarning($"[StarterPet] Lưu game lỗi: {exception.Message}", this); }
            if (!saved) Debug.LogWarning("[StarterPet] Chưa lưu được game, thú vẫn có trong Party ở lần chơi này.", this);
        }

        void ResolveReferences() {
            if (captureCoordinator == null) captureCoordinator = FindFirstObjectByType<PetCaptureCoordinator>(FindObjectsInactive.Include);
            if (roster == null) roster = FindFirstObjectByType<PetBoxRuntimeProvider>(FindObjectsInactive.Include);
        }

        static bool Fail(string message, out string error) {
            error = message;
            return false;
        }
    }
}