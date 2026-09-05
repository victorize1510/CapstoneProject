using System;
using System.Collections.Generic;
using Capstone.Game.SaveSystem;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    public sealed class PetRosterReleaseToken {
        internal PetController Pet;
        internal int PartyIndex = -1;
        internal int BoxIndex = -1;
        internal bool WasActive;
        internal bool WasSummoned;

        public bool IsValid => Pet != null && (PartyIndex >= 0 || BoxIndex >= 0);
    }

    [DisallowMultipleComponent]
    public sealed class PetBoxRuntimeProvider : MonoBehaviour {
        const int PartySize = 6;

        [Header("References")]
        [SerializeField] PetCommandInput petCommandInput = null;
        [SerializeField] PlayerCurrencyWallet currencyWallet = null;
        [SerializeField] PlayerSaveController saveController = null;
        [SerializeField] bool autoFindPetCommandInput = true;

        [Header("Storage")]
        [SerializeField, Min(1)] int capacity = 60;
        [SerializeField, Min(1)] int expansionSize = 10;
        [SerializeField, Min(0)] int expansionGoldCost = 5000;
        [SerializeField] List<PetController> initialStoredPets = new List<PetController>();

        readonly List<PetController> storedPets = new List<PetController>();
        PetController pendingCapturedPet;
        string releaseCountDateUtc = string.Empty;
        int releasedToday;
        bool initialized;

        public int Capacity => Mathf.Max(1, capacity);
        public int ExpansionSize => Mathf.Max(1, expansionSize);
        public int ExpansionGoldCost => Mathf.Max(0, expansionGoldCost);
        public int StoredCount => storedPets.Count;
        public int PartyCount => CountPartyPets();
        public int OwnedCount => PartyCount + StoredCount;
        public int ReleasedToday {
            get {
                EnsureCurrentReleaseDay();
                return releasedToday;
            }
        }
        public string ReleaseCountDateUtc {
            get {
                EnsureCurrentReleaseDay();
                return releaseCountDateUtc;
            }
        }
        public bool IsFull => StoredCount >= Capacity;
        public IReadOnlyList<PetController> StoredPets => storedPets;
        public PetController PendingCapturedPet => pendingCapturedPet;
        public PetCommandInput PartyInput => ResolveInput();

        public event Action Changed;
        public event Action<int, int, int> CapacityPurchaseRequested;
        public event Action<PetController> StorageFull;

        void Awake() {
            Initialize();
        }

        void OnValidate() {
            capacity = Mathf.Max(1, capacity);
            expansionSize = Mathf.Max(1, expansionSize);
            expansionGoldCost = Mathf.Max(0, expansionGoldCost);
        }

        public void Bind(PetCommandInput input) {
            petCommandInput = input;
            Initialize();
            RemovePartyDuplicates();
            Changed?.Invoke();
        }

        public PetController GetPartyPet(int slotIndex) {
            PetCommandInput input = ResolveInput();
            if (input == null || input.petSlots == null) return null;
            return slotIndex >= 0 && slotIndex < input.petSlots.Length ? input.petSlots[slotIndex] : null;
        }

        public PetController GetStoredPet(int boxIndex) {
            Initialize();
            return boxIndex >= 0 && boxIndex < storedPets.Count ? storedPets[boxIndex] : null;
        }

        public bool TryStoreCapturedPet(PetController pet, out string error) {
            Initialize();
            if (pet == null) return Fail("Pet không hợp lệ.", out error);
            if (ContainsPet(pet)) return Fail("Pet đã nằm trong Party hoặc Box.", out error);
            if (IsFull) {
                pendingCapturedPet = pet;
                StorageFull?.Invoke(pet);
                return Fail("BOX đã đầy.", out error);
            }

            pet.Withdraw();
            storedPets.Add(pet);
            if (pendingCapturedPet == pet) pendingCapturedPet = null;
            Changed?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool TryRemoveCapturedPetForRollback(PetController pet, bool wasSummoned, out string error) {
            Initialize();
            if (pet == null) return Fail("Pet không hợp lệ.", out error);

            bool removed = storedPets.Remove(pet);
            if (pendingCapturedPet == pet) {
                pendingCapturedPet = null;
                removed = true;
            }
            if (!removed) return Fail("Pet không còn nằm trong Box.", out error);

            if (wasSummoned) pet.Summon();
            else pet.Withdraw();
            Changed?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool TrySwapPartySlots(int sourceIndex, int targetIndex, out string error) {
            PetCommandInput input = ResolveInput();
            if (!TryGetPartyArray(input, out PetController[] party, out error)) return false;
            if (!IsPartyIndex(sourceIndex) || !IsPartyIndex(targetIndex)) return Fail("Slot Party không hợp lệ.", out error);
            if (sourceIndex == targetIndex) return Fail("Pet đã ở slot này.", out error);
            if (party[sourceIndex] == null && party[targetIndex] == null) return Fail("Hai slot đều trống.", out error);

            PetController source = party[sourceIndex];
            party[sourceIndex] = party[targetIndex];
            party[targetIndex] = source;
            EnsureActivePet(input);
            Changed?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool TrySwapStoredPets(int sourceIndex, int targetIndex, out string error) {
            Initialize();
            if (sourceIndex < 0 || sourceIndex >= storedPets.Count || targetIndex < 0 || targetIndex >= storedPets.Count) {
                return Fail("Vị trí pet trong Box không hợp lệ.", out error);
            }
            if (sourceIndex == targetIndex) return Fail("Pet đã ở vị trí này.", out error);

            PetController source = storedPets[sourceIndex];
            storedPets[sourceIndex] = storedPets[targetIndex];
            storedPets[targetIndex] = source;
            Changed?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool TryMovePartyToBox(int partyIndex, out string error) {
            Initialize();
            PetCommandInput input = ResolveInput();
            if (!TryGetPartyArray(input, out PetController[] party, out error)) return false;
            if (!IsPartyIndex(partyIndex)) return Fail("Slot Party không hợp lệ.", out error);
            PetController pet = party[partyIndex];
            if (pet == null) return Fail("Slot Party đang trống.", out error);
            if (IsFull) {
                StorageFull?.Invoke(pet);
                return Fail("BOX đã đầy.", out error);
            }

            pet.Withdraw();
            party[partyIndex] = null;
            storedPets.Add(pet);
            EnsureActivePet(input);
            Changed?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool TryMoveBoxToNextEmptyPartySlot(int boxIndex, out int targetIndex, out string error) {
            targetIndex = FindNextEmptyPartySlot();
            if (targetIndex < 0) return Fail("Party đã đủ 6 pet.", out error);
            return TryMoveBoxToParty(boxIndex, targetIndex, out error);
        }

        public bool TryMoveBoxToParty(int boxIndex, int partyIndex, out string error) {
            Initialize();
            PetCommandInput input = ResolveInput();
            if (!TryGetPartyArray(input, out PetController[] party, out error)) return false;
            if (!IsPartyIndex(partyIndex)) return Fail("Slot Party không hợp lệ.", out error);
            if (boxIndex < 0 || boxIndex >= storedPets.Count) return Fail("Pet trong Box không hợp lệ.", out error);

            PetController incoming = storedPets[boxIndex];
            PetController outgoing = party[partyIndex];
            if (incoming == null) return Fail("Pet trong Box đã mất reference.", out error);

            if (outgoing != null) {
                outgoing.Withdraw();
                storedPets[boxIndex] = outgoing;
            }
            else {
                storedPets.RemoveAt(boxIndex);
            }

            party[partyIndex] = incoming;
            if (incoming.owner == null && input != null) incoming.AssignOwner(input.transform);
            if (input.activePet == outgoing || input.activePet == null) input.SetActivePet(incoming);
            Changed?.Invoke();
            error = string.Empty;
            return true;
        }

        public int FindNextEmptyPartySlot() {
            PetCommandInput input = ResolveInput();
            if (input == null || input.petSlots == null) return -1;
            for (int i = 0; i < Mathf.Min(PartySize, input.petSlots.Length); i++) {
                if (input.petSlots[i] == null) return i;
            }
            return -1;
        }

        public void RequestCapacityExpansion() {
            CapacityPurchaseRequested?.Invoke(Capacity, ExpansionSize, ExpansionGoldCost);
        }

        public bool TryPurchaseCapacity(out string error) {
            Initialize();
            ResolveServices();
            if (currencyWallet == null) return Fail("Không tìm thấy ví Gold.", out error);
            if (saveController == null) return Fail("Không tìm thấy PlayerSaveController.", out error);
            if (!currencyWallet.TrySpendGold(ExpansionGoldCost)) {
                return Fail($"Không đủ {ExpansionGoldCost:N0} Gold.", out error);
            }

            int previousCapacity = capacity;
            capacity += ExpansionSize;
            Changed?.Invoke();

            if (!saveController.SaveNow()) {
                capacity = previousCapacity;
                currencyWallet.AddGold(ExpansionGoldCost);
                Changed?.Invoke();
                return Fail("Không thể lưu giao dịch mua slot. Gold đã được hoàn lại.", out error);
            }

            error = string.Empty;
            return true;
        }

        public void ApplyPurchasedCapacity(int addedSlots) {
            if (addedSlots <= 0) return;
            capacity += addedSlots;
            Changed?.Invoke();
        }

        public void RestoreState(
            IEnumerable<PetController> pets,
            int restoredCapacity,
            string restoredReleaseDateUtc = "",
            int restoredReleasedToday = 0) {
            Initialize();
            capacity = Mathf.Max(1, restoredCapacity);
            releaseCountDateUtc = restoredReleaseDateUtc ?? string.Empty;
            releasedToday = Mathf.Max(0, restoredReleasedToday);
            EnsureCurrentReleaseDay();
            pendingCapturedPet = null;
            storedPets.Clear();

            if (pets != null) {
                foreach (PetController pet in pets) {
                    if (pet == null || storedPets.Contains(pet) || IsPartyPet(pet)) continue;

                    pet.Withdraw();
                    storedPets.Add(pet);
                    if (storedPets.Count >= Capacity) break;
                }
            }

            Changed?.Invoke();
        }

        public bool TryConsumeDailyRelease(int dailyLimit, out string error) {
            EnsureCurrentReleaseDay();
            dailyLimit = Mathf.Max(1, dailyLimit);
            if (releasedToday >= dailyLimit) {
                return Fail($"Đã đạt giới hạn thả {dailyLimit} pet hôm nay.", out error);
            }

            releasedToday++;
            error = string.Empty;
            return true;
        }

        public void UndoDailyRelease() {
            EnsureCurrentReleaseDay();
            releasedToday = Mathf.Max(0, releasedToday - 1);
        }

        public void ClearPendingCapturedPet() {
            pendingCapturedPet = null;
        }

        internal void RestorePendingCapturedPet(PetController pet) {
            if (pet == null || ContainsPet(pet)) return;
            pendingCapturedPet = pet;
            Changed?.Invoke();
        }

        public bool Contains(PetController pet) {
            Initialize();
            return ContainsPet(pet);
        }

        public bool TryDetachPetForRelease(
            PetController pet,
            out PetRosterReleaseToken token,
            out string error) {
            Initialize();
            token = null;
            if (pet == null) return Fail("Pet không hợp lệ.", out error);

            PetCommandInput input = ResolveInput();
            int partyIndex = FindPartyIndex(pet, input);
            int boxIndex = storedPets.IndexOf(pet);
            if (partyIndex < 0 && boxIndex < 0) {
                return Fail("Pet không còn nằm trong Party hoặc Box.", out error);
            }

            token = new PetRosterReleaseToken {
                Pet = pet,
                PartyIndex = partyIndex,
                BoxIndex = boxIndex,
                WasActive = input != null && input.activePet == pet,
                WasSummoned = pet.IsSummoned
            };

            pet.Withdraw();
            if (partyIndex >= 0) input.petSlots[partyIndex] = null;
            else storedPets.RemoveAt(boxIndex);

            EnsureActivePet(input);
            Changed?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool RestoreDetachedPet(PetRosterReleaseToken token, out string error) {
            Initialize();
            if (token == null || !token.IsValid) return Fail("Không có trạng thái pet để khôi phục.", out error);

            PetCommandInput input = ResolveInput();
            if (token.PartyIndex >= 0) {
                if (!TryGetPartyArray(input, out PetController[] party, out error)) return false;
                if (party[token.PartyIndex] != null) return Fail("Slot Party cũ không còn trống.", out error);
                party[token.PartyIndex] = token.Pet;
            }
            else {
                int insertIndex = Mathf.Clamp(token.BoxIndex, 0, storedPets.Count);
                storedPets.Insert(insertIndex, token.Pet);
            }

            if (token.WasActive && input != null) input.SetActivePet(token.Pet);
            if (token.WasSummoned) token.Pet.Summon();
            else token.Pet.Withdraw();

            Changed?.Invoke();
            error = string.Empty;
            return true;
        }

        void Initialize() {
            if (initialized) return;
            initialized = true;
            ResolveInput();
            EnsureCurrentReleaseDay();

            foreach (PetController pet in initialStoredPets) {
                if (pet == null || storedPets.Contains(pet) || IsPartyPet(pet)) continue;
                pet.Withdraw();
                storedPets.Add(pet);
                if (storedPets.Count >= Capacity) break;
            }
        }

        void EnsureCurrentReleaseDay() {
            string todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (string.Equals(releaseCountDateUtc, todayUtc, StringComparison.Ordinal)) return;
            releaseCountDateUtc = todayUtc;
            releasedToday = 0;
        }

        PetCommandInput ResolveInput() {
            if (petCommandInput == null && autoFindPetCommandInput) {
                petCommandInput = FindFirstObjectByType<PetCommandInput>();
            }
            return petCommandInput;
        }

        void ResolveServices() {
            if (currencyWallet == null) currencyWallet = FindFirstObjectByType<PlayerCurrencyWallet>(FindObjectsInactive.Include);
            if (saveController == null) saveController = FindFirstObjectByType<PlayerSaveController>(FindObjectsInactive.Include);
        }

        void RemovePartyDuplicates() {
            for (int i = storedPets.Count - 1; i >= 0; i--) {
                if (storedPets[i] == null || IsPartyPet(storedPets[i])) storedPets.RemoveAt(i);
            }
        }

        bool ContainsPet(PetController pet) {
            return pet != null && (storedPets.Contains(pet) || IsPartyPet(pet));
        }

        bool IsPartyPet(PetController pet) {
            PetCommandInput input = ResolveInput();
            if (pet == null || input == null || input.petSlots == null) return false;
            for (int i = 0; i < input.petSlots.Length; i++) {
                if (input.petSlots[i] == pet) return true;
            }
            return false;
        }

        static int FindPartyIndex(PetController pet, PetCommandInput input) {
            if (pet == null || input == null || input.petSlots == null) return -1;
            for (int i = 0; i < input.petSlots.Length; i++) {
                if (input.petSlots[i] == pet) return i;
            }
            return -1;
        }

        int CountPartyPets() {
            PetCommandInput input = ResolveInput();
            if (input == null || input.petSlots == null) return 0;
            int count = 0;
            for (int i = 0; i < Mathf.Min(PartySize, input.petSlots.Length); i++) {
                if (input.petSlots[i] != null) count++;
            }
            return count;
        }

        void EnsureActivePet(PetCommandInput input) {
            if (input == null || input.petSlots == null) return;
            for (int i = 0; i < input.petSlots.Length; i++) {
                if (input.petSlots[i] == input.activePet) return;
            }

            PetController replacement = null;
            for (int i = 0; i < input.petSlots.Length; i++) {
                if (input.petSlots[i] == null) continue;
                replacement = input.petSlots[i];
                break;
            }
            input.SetActivePet(replacement);
        }

        static bool TryGetPartyArray(PetCommandInput input, out PetController[] party, out string error) {
            party = input != null ? input.petSlots : null;
            if (party == null || party.Length < PartySize) {
                error = "Không tìm thấy Party 6 slot.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        static bool IsPartyIndex(int index) {
            return index >= 0 && index < PartySize;
        }

        static bool Fail(string message, out string error) {
            error = message;
            return false;
        }
    }
}
