using System;
using System.Collections.Generic;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.MapSystem;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace Capstone.Game.CreatureSystem
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class CreatureEncounterZone : MonoBehaviour
    {
        [Header("Dinh danh va ranh gioi")]
        public string zoneId = "";
        public string zoneName = "Wild Zone";
        public BoxCollider zoneBounds;
        [Tooltip("Tat chi dung sinh moi; actor cu van duoc owner xu ly chet/capture.")]
        public bool spawningEnabled = true;

        [Header("Cap va so luong")]
        [Min(1)] public int baseLevel = 5;
        [Min(0)] public int levelVariance = 2;
        [Tooltip("0: giu BaseLevel +/- Variance cua Zone cu.")]
        [Min(0)] public int minimumLevel, maximumLevel;
        [Min(0)] public int regionMinimumLevel, regionMaximumLevel;
        [Tooltip("Zone moi: xet nguong Form theo cap ca the da roll. False giu cach xet BaseLevel cu.")]
        public bool formEligibilityUsesIndividualLevel;
        [Tooltip("Bo qua Form BLOCKED va bao ly do; khong gia lap du lieu con thieu. False giu validation nghiem cua Zone cu.")]
        public bool allowPartialConfiguration;
        public bool requireSupportedTypes;
        [Min(.1f)] public float respawnDelay = 5;
        [Tooltip("So Pet hoang toi thieu/toi da cung luc (so slot). De Maximum = 0 de dung cong thuc cu cua CreatureSpawnRules. Min = Max la so co dinh.")]
        [Min(0)] public int minimumPopulation, maximumPopulation;
        [Min(0)] public float minimumSpawnSeparation = 2.5f;
        [Min(0)] public float minimumPlayerSpawnDistance = 5;
        public bool allowNearPlayerFallback = true;

        [Header("Danh sach dung chung hoac rieng Zone")]
        public CreatureSpawnTable spawnTable;
        [Tooltip("Chi dung khi Spawn Table trong.")]
        public PetPrefabCatalog ownedPetCatalog;
        public List<CreatureSpawnSpecies> species = new List<CreatureSpawnSpecies>();
        public List<CreatureSpawnLevelOverride> formLevelOverrides = new List<CreatureSpawnLevelOverride>();

        [Header("Vi tri va hieu ung")]
        public Transform player;
        public DayNightCycle dayNightClock;
        [Tooltip("Zone ven nuoc: gan trigger BoxCollider phu mat nuoc, dat Zone tren bo.")]
        public bool requireWaterNearby;
        public BoxCollider waterBoundary;
        [Min(.1f)] public float maximumDistanceFromWater = 5f;
        [Range(1, 128)] public int positionAttempts = 24;
        [Min(.1f)] public float navMeshSampleDistance = 2;
        public LayerMask obstacleMask = Physics.DefaultRaycastLayers;
        [Tooltip("Dung sai tiep xuc rieng o chan capsule (met). Mat san phai do tai diem dat va khong cham phan than. 0 = khong bo qua tiep xuc.")]
        [Min(0)] public float groundIgnoreHeight = .2f;
        public GameObject smokePrefab, downedStarsPrefab;
        public bool drawGizmos = true;

        // Gioi han cung de tranh tao qua nhieu slot do nhap sai.
        const int MaxPopulationCap = 32;

        sealed class EligibleSpecies
        {
            public CreatureSpawnSpecies source;
            public readonly List<CreatureSpawnForm> forms = new List<CreatureSpawnForm>();
            public readonly List<float> weights = new List<float>();
            public readonly List<int> formMinimumLevels = new List<int>();
            public int minimumLevel, maximumLevel;
        }

        readonly List<EligibleSpecies> eligible = new List<EligibleSpecies>();
        readonly List<EncounterSpawnPoint> slots = new List<EncounterSpawnPoint>();
        readonly List<float> speciesWeights = new List<float>();
        readonly List<bool> represented = new List<bool>();
        readonly List<float> formWeights = new List<float>();
        readonly List<string> configurationIssues = new List<string>();
        readonly Collider[] overlaps = new Collider[32];
        readonly Collider[] aboveFootOverlaps = new Collider[32];
        readonly RaycastHit[] surfaceHits = new RaycastHit[32];
        Random random;
        GameObject lifecycleRoot, staging;
        float initializeAt, findPlayerAt;
        float findClockAt;
        bool initialized;
        public int TargetPopulation { get; private set; }
        public int EligibleSpeciesCount => eligible.Count;
        public int TotalSpawned { get; private set; }
        public string LastStatus { get; private set; } = "Chua khoi tao";
        public IReadOnlyList<string> ConfigurationIssues => configurationIssues;
        public string LastPlacementReport { get; private set; } = "Chua thu vi tri";
        public int OccupiedSlots
        {
            get
            {
                int count = 0;
                foreach (var slot in slots) if (slot != null && slot.CurrentActor != null) count++;
                return count;
            }
        }

        void Reset()
        {
            zoneId = Guid.NewGuid().ToString("N");
            zoneBounds = GetComponent<BoxCollider>();
            zoneBounds.isTrigger = true;
            zoneBounds.center = Vector3.up * 1.5f;
            zoneBounds.size = new Vector3(18, 6, 18);
        }

        void OnValidate()
        {
            baseLevel = Mathf.Clamp(baseLevel, 1, PetStatCalculator.MaxLevel);
            levelVariance = Mathf.Clamp(levelVariance, 0, PetStatCalculator.MaxLevel);
            respawnDelay = Mathf.Max(.1f, respawnDelay);
            maximumPopulation = Mathf.Clamp(maximumPopulation, 0, MaxPopulationCap);
            if (maximumPopulation > 0) minimumPopulation = Mathf.Clamp(minimumPopulation, 1, maximumPopulation);
            minimumSpawnSeparation = Mathf.Max(0, minimumSpawnSeparation);
            minimumPlayerSpawnDistance = Mathf.Max(0, minimumPlayerSpawnDistance);
            navMeshSampleDistance = Mathf.Max(.1f, navMeshSampleDistance);
            positionAttempts = Mathf.Clamp(positionAttempts, 1, 128);
        }

        void Update()
        {
            if (!spawningEnabled || UISystem.GameplayPause.IsPaused) return;
            if (player == null && Time.time >= findPlayerAt)
            {
                findPlayerAt = Time.time + 1;
                var movement = FindFirstObjectByType<BasicPlayerMovement>();
                if (movement != null) player = movement.transform;
            }
            if (initialized || Time.time < initializeAt) return;
            initializeAt = Time.time + 1;
            if (!ValidateConfiguration(out var reason)) { LastStatus = reason; return; }
            random = new Random(Guid.NewGuid().GetHashCode());
            TargetPopulation = ResolvePopulation();
            lifecycleRoot = new GameObject(zoneName + " - zone lifecycle");
            SceneManager.MoveGameObjectToScene(lifecycleRoot, gameObject.scene);
            staging = new GameObject("Inactive spawn staging");
            staging.transform.SetParent(lifecycleRoot.transform, false);
            staging.SetActive(false);
            // Owner khong nam duoi Zone: tat collider/Zone van ket thuc duoc chet va capture.
            for (int i = 0; i < TargetPopulation; i++)
            {
                var host = new GameObject("Slot " + (i + 1));
                host.SetActive(false);
                host.transform.SetParent(lifecycleRoot.transform, false);
                var owner = host.AddComponent<EncounterSpawnPoint>();
                owner.ZoneOwner = this;
                owner.respawnDelay = respawnDelay;
                owner.smokePrefab = smokePrefab;
                owner.downedStarsPrefab = downedStarsPrefab;
                slots.Add(owner);
                host.SetActive(true);
            }
            initialized = true;
            LastStatus = "San sang: " + TargetPopulation + " slot";
        }

        // Maximum = 0: giu nguyen cong thuc cu (Zone hien co khong doi hanh vi).
        // Maximum > 0: roll ngau nhien trong [Minimum, Maximum] moi lan Zone khoi tao.
        int ResolvePopulation()
        {
            if (maximumPopulation <= 0) return CreatureSpawnRules.Population(eligible.Count, random);
            int max = Mathf.Clamp(maximumPopulation, 1, MaxPopulationCap);
            int min = Mathf.Clamp(minimumPopulation, 1, max);
            return random.Next(min, max + 1);
        }

        public bool ValidateConfiguration(out string reason)
        {
            eligible.Clear();
            configurationIssues.Clear();
            reason = "Form trung ID hoac weight khong hop le.";
            if (zoneBounds == null) zoneBounds = GetComponent<BoxCollider>();
            if (zoneBounds == null || !zoneBounds.isTrigger || !zoneBounds.enabled)
            {
                reason = "Zone can BoxCollider bat va IsTrigger."; return false;
            }
            Vector3 up = zoneBounds.transform.up;
            Vector3 boundsScale = zoneBounds.transform.lossyScale;
            if (Vector3.Dot(up, Vector3.up) < .999f || zoneBounds.size.x <= 0 || zoneBounds.size.y <= 0 || zoneBounds.size.z <= 0)
            {
                reason = "Box phai dung theo truc Y, kich thuoc duong; co the xoay yaw."; return false;
            }
            if (Mathf.Abs(boundsScale.x * boundsScale.y * boundsScale.z) < .000001f)
            {
                reason = "Scale Zone khong duoc bang 0."; return false;
            }
            var entries = spawnTable != null ? spawnTable.species : species;
            var catalog = spawnTable != null ? spawnTable.ownedPetCatalog : ownedPetCatalog;
            if (entries == null || catalog == null) { reason = "Thieu danh sach Species hoac catalog OwnedPet de load sau capture."; return false; }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (entry == null || !entry.enabled || entry.weight == 0) continue;
                string id = entry.definitionId?.Trim();
                if (string.IsNullOrEmpty(id) || !ids.Add(id) || !CreatureSpawnRules.ValidWeight(entry.weight))
                {
                    reason = "DefinitionId trung/trong hoac weight khong hop le."; eligible.Clear(); return false;
                }
                if (spawnTable != null && spawnTable.excludedDefinitionIds != null && spawnTable.excludedDefinitionIds.Exists(excluded => Same(excluded, id)))
                {
                    reason = id + ": Family bi loai khoi table nay.";
                    configurationIssues.Add(reason);
                    if (!allowPartialConfiguration) { eligible.Clear(); return false; }
                    continue;
                }
                var group = new EligibleSpecies { source = entry };
                if (!CreatureSpawnRules.TryLevelRange(baseLevel, levelVariance, PetStatCalculator.MaxLevel,
                    minimumLevel, maximumLevel, regionMinimumLevel, regionMaximumLevel, entry.minimumLevel, entry.maximumLevel,
                    out group.minimumLevel, out group.maximumLevel))
                {
                    reason = id + ": giao khoang cap Zone/khu vuc/Species rong.";
                    configurationIssues.Add(reason);
                    if (!allowPartialConfiguration) { eligible.Clear(); return false; }
                    continue;
                }
                var formIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (entry.forms != null) foreach (var form in entry.forms)
                    {
                        if (form == null || !form.enabled || form.weight == 0) continue;
                        reason = id + "/" + form.formId + ": Form ID trung hoac weight sai.";
                        bool valid = formIds.Add(form.formId?.Trim() ?? "") && CreatureSpawnRules.ValidWeight(form.weight)
                            && ValidatePrefab(id, form, catalog, out reason);
                        if (valid && requireSupportedTypes)
                        {
                            var typing = form.prefab.GetComponent<PetCollectionMetadata>();
                            valid = Elements.ElementCatalog.IsValidCreatureTypes(typing.Element, typing.SecondaryElement);
                            if (!valid) reason = id + "/" + form.formId + ": typing chua hop le theo 9 he hien co.";
                            else valid = ValidateRuntimeData(form.prefab, out reason);
                        }
                        if (!valid)
                        {
                            configurationIssues.Add(reason);
                            if (!allowPartialConfiguration) { eligible.Clear(); return false; }
                            continue;
                        }
                        var metadata = form.prefab.GetComponent<PetCollectionMetadata>();
                        int minimum = form.minimumZoneLevel > 0 ? form.minimumZoneLevel : CreatureSpawnRules.MinimumFormLevel(metadata.EvolutionStage);
                        foreach (var rule in formLevelOverrides)
                            if (rule != null && Same(rule.definitionId, id) && Same(rule.formId, form.formId)) minimum = Mathf.Max(1, rule.minimumZoneLevel);
                        int availableLevel = CreatureSpawnRules.FormReferenceLevel(formEligibilityUsesIndividualLevel, baseLevel, group.maximumLevel);
                        if (availableLevel < minimum) continue;
                        group.forms.Add(form);
                        group.weights.Add(form.weight);
                        group.formMinimumLevels.Add(minimum);
                    }
                if (group.forms.Count > 0)
                {
                    if (formEligibilityUsesIndividualLevel)
                    {
                        int lowestFormLevel = int.MaxValue;
                        foreach (int value in group.formMinimumLevels) lowestFormLevel = Math.Min(lowestFormLevel, value);
                        group.minimumLevel = Math.Max(group.minimumLevel, lowestFormLevel);
                    }
                    eligible.Add(group);
                }
            }
            reason = eligible.Count > 0 ? "Cau hinh: " + eligible.Count + " loai san sang" : "BLOCKED: khong co loai/form san sang";
            reason += configurationIssues.Count > 0 ? "; " + configurationIssues.Count + " muc BLOCKED. " + configurationIssues[0] : ".";
            return eligible.Count > 0;
        }

        static bool ValidateRuntimeData(GameObject prefab, out string reason)
        {
            reason = prefab.name + ": thieu Combat/Presentation/Catalog/loadout hoac capture rate.";
            var metadata = prefab.GetComponent<PetCollectionMetadata>();
            if (!PetStatCalculator.TryGetProfile(metadata, out var profile) || profile.BaseCatchRate < 1 || profile.BaseCatchRate > 255) return false;
            var set = prefab.GetComponent<PetHudRuntimeStats>().SpeciesSkillSet;
            if (set.catalog == null || set.learnset == null || set.defaultLoadout == null || set.defaultLoadout.Count == 0 || set.defaultLoadout.Count > 4) return false;
            foreach (var learned in set.learnset)
            {
                if (learned?.skill == null || string.IsNullOrWhiteSpace(learned.skill.Id)
                    || !set.catalog.TryGet(learned.skill.Id, out var registered) || registered != learned.skill
                    || learned.skill.combat == null || learned.Presentation == null) return false;
            }
            var loadoutIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var skill in set.defaultLoadout)
                if (skill == null || !loadoutIds.Add(skill.Id) || !set.TryGet(skill.Id, out _)) return false;
            return true;
        }

        // Chi do coverage; khong bake, khong doi Surface hay tao actor.
        public string ProbeNavMeshCoverage()
        {
            if (zoneBounds == null) zoneBounds = GetComponent<BoxCollider>();
            var box = zoneBounds;
            if (box == null || !box.enabled || !box.gameObject.activeInHierarchy) return "Khong do duoc: Box/Zone dang tat hoac chua gan.";
            var filters = new HashSet<(int agentType, int areaMask)>();
            var entries = spawnTable != null ? spawnTable.species : species;
            if (entries != null) foreach (var entry in entries) if (entry?.forms != null)
                        foreach (var form in entry.forms)
                        {
                            var agent = form?.prefab != null ? form.prefab.GetComponent<NavMeshAgent>() : null;
                            if (agent != null) filters.Add((agent.agentTypeID, agent.areaMask));
                        }
            bool generic = filters.Count == 0;
            if (generic && NavMesh.GetSettingsCount() > 0) filters.Add((NavMesh.GetSettingsByIndex(0).agentTypeID, NavMesh.AllAreas));
            if (filters.Count == 0) return "Khong co Agent Type/NavMesh settings.";
            var reports = new List<string>();
            foreach (var (agentType, areaMask) in filters)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = areaMask };
                int covered = 0, height = 0, outside = 0;
                for (int x = 0; x < 3; x++) for (int z = 0; z < 3; z++)
                    {
                        Vector3 local = box.center + new Vector3((x - 1) * box.size.x / 3, 0, (z - 1) * box.size.z / 3);
                        Vector3 point = box.transform.TransformPoint(local);
                        if (TrySampleNavMesh(point, filter, out var hit))
                        {
                            if (Contains(hit.position)) covered++; else outside++;
                        }
                        else if (NavMesh.SamplePosition(point, out var wide, Mathf.Max(navMeshSampleDistance, box.bounds.size.y + 4), filter)
                            && HorizontalSquared(wide.position - point) <= navMeshSampleDistance * navMeshSampleDistance
                            && Mathf.Abs(wide.position.y - point.y) > navMeshSampleDistance) height++;
                    }
                reports.Add("AgentType " + agentType + ", AreaMask " + areaMask + ": " + covered + "/9 mau trong Box (co chieu theo mat san); sai do cao=" + height + "; ngoai Box=" + outside);
            }
            return string.Join("\n", reports) + (generic ? "\nChua co prefab san sang: chi do hinh hoc, chua kiem actor." : "")
                + "\nDay la mau coverage, khong dam bao moi vi tri deu du cho collider. 0/9: kiem Surface bat, Agent Type, vi tri va chieu cao Box; khong tu bake.";
        }

        static bool Same(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        static bool ValidatePrefab(string id, CreatureSpawnForm form, PetPrefabCatalog catalog, out string reason)
        {
            string formKey = id + "/" + form.formId;
            if (form.prefab == null)
            {
                reason = formKey + ": Spawn Table khong co tham chieu prefab Wild hop le.";
                if (!string.IsNullOrWhiteSpace(form.configurationBlocker)) reason += " " + form.configurationBlocker;
                return false;
            }
            if (string.IsNullOrWhiteSpace(form.formId))
            {
                reason = formKey + ": FormId trong.";
                return false;
            }
            var prefab = form.prefab;
            if (prefab.scene.IsValid()) { reason = formKey + ": Can prefab asset, khong dung actor dang o trong scene."; return false; }
            var pet = prefab.GetComponent<PetController>();
            var enemy = prefab.GetComponent<DummyEnemy>();
            var metadata = prefab.GetComponent<PetCollectionMetadata>();
            var stats = prefab.GetComponent<PetHudRuntimeStats>();
            var capture = prefab.GetComponent<WildPetCaptureTarget>();
            var role = prefab.GetComponent<CreatureRolePresentation>();
            var marker = prefab.GetComponent<MapMarker>();
            var animator = prefab.GetComponentInChildren<Animator>(true);
            Vector3 scale = prefab.transform.localScale;
            string issue;
            if (pet == null || !pet.IsWildEncounter) issue = "PetController thieu hoac IsWildEncounter=false";
            else if (enemy == null || enemy.IsBoss || enemy.IsTrainingDummy || marker != null && marker.MarkerType == MapMarkerType.Boss)
                issue = "DummyEnemy thieu hoac danh dau Boss/Training Dummy";
            else if (metadata == null || !Same(metadata.DefinitionId, id) || !Same(metadata.CurrentFormId, form.formId))
                issue = "Metadata Wild thieu hoac sai DefinitionId/FormId";
            else if (stats == null || stats.SpeciesSkillSet == null) issue = "Wild thieu PetHudRuntimeStats/SpeciesSkillSet";
            else if (capture == null) issue = "Thieu WildPetCaptureTarget";
            else if (!capture.Allowed) issue = "WildPetCaptureTarget.Allowed=false";
            else if (role == null || role.role != CreatureRole.Wild) issue = "CreatureRolePresentation thieu hoac Role khong phai Wild";
            else if (prefab.GetComponent<CreatureCombatant>() == null || prefab.GetComponent<CreatureAnimationDriver>() == null)
                issue = "Thieu CreatureCombatant/CreatureAnimationDriver";
            else if (prefab.GetComponent<NavMeshAgent>() == null) issue = "Thieu NavMeshAgent";
            else if (prefab.GetComponent<CapsuleCollider>() is not CapsuleCollider capsule || !capsule.enabled || capsule.isTrigger || capsule.direction != 1)
                issue = "Can CapsuleCollider dang bat, truc Y, IsTrigger=false";
            else if (animator == null || animator.runtimeAnimatorController == null) issue = "Thieu Animator/controller";
            else if (animator.GetComponent<CreatureVisualBinding>() is not CreatureVisualBinding binding || binding.animationProfile == null)
                issue = "Thieu CreatureVisualBinding/AnimationProfile";
            else if (binding.skillSet != stats.SpeciesSkillSet) issue = "SkillSet cua Visual va Stats khong khop";
            else if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0) issue = "Scale cua prefab Wild phai duong";
            else if (prefab.GetComponentInChildren<EncounterSpawnPoint>(true) != null) issue = "Prefab Wild chua EncounterSpawnPoint";
            else if (!PetStatCalculator.TryGetProfile(metadata, out var profile)) issue = "Khong tim thay StatProfile cua Wild";
            else if (!profile.IsCatchable) issue = "StatProfile.IsCatchable=false";
            else if (!catalog.TryGetPrefab(id, form.formId, out var owned)) issue = "Catalog khong resolve duoc OwnedPet";
            else
            {
                var ownedMetadata = owned.GetComponent<PetCollectionMetadata>();
                var ownedStats = owned.GetComponent<PetHudRuntimeStats>();
                if (owned.IsWildEncounter || ownedMetadata == null || !Same(ownedMetadata.DefinitionId, id)
                    || !Same(ownedMetadata.CurrentFormId, form.formId) || ownedMetadata.EvolutionStage != metadata.EvolutionStage)
                    issue = "OwnedPet sai role, DefinitionId, FormId hoac EvolutionStage";
                else if (ownedStats == null || ownedStats.SpeciesSkillSet != stats.SpeciesSkillSet)
                    issue = "OwnedPet thieu Stats hoac khac SkillSet voi Wild";
                else { reason = ""; return true; }
            }
            reason = formKey + ": " + issue + " (" + prefab.name + ").";
            return false;
        }

        internal bool TryCreateActor(EncounterSpawnPoint owner, out DummyEnemy actor)
        {
            actor = null;
            if (!initialized || !isActiveAndEnabled || !spawningEnabled || zoneBounds == null || !zoneBounds.enabled || !zoneBounds.isTrigger
                || !zoneBounds.gameObject.activeInHierarchy || !slots.Contains(owner) || OccupiedSlots >= TargetPopulation) return false;
            speciesWeights.Clear(); represented.Clear();
            foreach (var group in eligible)
            {
                speciesWeights.Add(CanSpawnNow(group.source) ? group.source.weight : 0f);
                bool present = false;
                foreach (var slot in slots)
                {
                    var current = slot != null ? slot.CurrentActor : null;
                    var metadata = current != null ? current.GetComponent<PetCollectionMetadata>() : null;
                    if (metadata != null && Same(metadata.DefinitionId, group.source.definitionId)) { present = true; break; }
                }
                represented.Add(present);
            }
            int speciesIndex = CreatureSpawnRules.Pick(speciesWeights, represented, random);
            if (speciesIndex < 0) { LastStatus = "Đang chờ khung giờ xuất hiện hoặc thiếu DayNightCycle."; return false; }
            var selected = eligible[speciesIndex];
            int rolled = 0;
            if (formEligibilityUsesIndividualLevel) rolled = random.Next(selected.minimumLevel, selected.maximumLevel + 1);
            formWeights.Clear();
            for (int i = 0; i < selected.forms.Count; i++)
            {
                int reference = CreatureSpawnRules.FormReferenceLevel(formEligibilityUsesIndividualLevel, baseLevel, rolled);
                formWeights.Add(reference >= selected.formMinimumLevels[i] ? selected.weights[i] : 0);
            }
            int formIndex = CreatureSpawnRules.Pick(formWeights, null, random);
            if (formIndex < 0) return false;
            var form = selected.forms[formIndex];
            if (form.prefab == null) { LastStatus = "Prefab da mat; dung Play va kiem tra Spawn Table."; return false; }
            if (!FindPosition(form.prefab, out var position, out var rotation, out bool nearPlayer)) return false;
            GameObject instance = null;
            try
            {
                // Inactive parent cho phep gan ID/cap truoc Awake va truoc cooldown dau tien.
                instance = Instantiate(form.prefab, position, rotation, staging.transform);
                var metadata = instance.GetComponent<PetCollectionMetadata>();
                metadata.AssignPersistentId("wild-" + Guid.NewGuid().ToString("N"));
                metadata.InitializeWildIndividual();
                var brain = instance.GetComponent<BasicCreatureEnemy>() ?? instance.AddComponent<BasicCreatureEnemy>();
                brain.enabled = true;
                brain.territory = zoneBounds;
                var level = instance.GetComponent<EncounterLevel>() ?? instance.AddComponent<EncounterLevel>();
                // Giu thu tu RNG cu (vi tri/yaw truoc cap) cho Zone legacy.
                if (!formEligibilityUsesIndividualLevel) rolled = random.Next(selected.minimumLevel, selected.maximumLevel + 1);
                if (!level.InitializeAtLevel(rolled)) throw new InvalidOperationException("Khong khoi tao duoc cap Wild.");
                actor = instance.GetComponent<DummyEnemy>();
                actor.autoRevive = false;
                actor.DeathManagedByEncounter = true;
                var stars = instance.GetComponent<DownedStatusVisual>() ?? instance.AddComponent<DownedStatusVisual>();
                stars.effectPrefab = downedStarsPrefab;
                owner.transform.position = position;
                owner.respawnDelay = respawnDelay;
                owner.settings.Read(brain);
                // Actor khong lam con cua Zone/owner: capture khong bi huy khi Zone bi xoa.
                instance.transform.SetParent(null, true);
                instance.SetActive(true);
                TotalSpawned++;
                LastStatus = selected.source.definitionId + "/" + form.formId + " Lv" + rolled + (nearPlayer ? " (vi tri gan player duoc phep du phong)" : "");
                return true;
            }
            catch (Exception error)
            {
                if (instance != null) Destroy(instance);
                actor = null;
                LastStatus = "Spawn bi huy: " + error.Message;
                Debug.LogError("[Creature Zone] " + zoneName + ": " + LastStatus, this);
                return false;
            }
        }

        bool CanSpawnNow(CreatureSpawnSpecies entry)
        {
            if (!entry.HasTimeWindow) return true;
            if (dayNightClock == null && Time.time >= findClockAt)
            {
                findClockAt = Time.time + 1f;
                foreach (var clock in FindObjectsByType<DayNightCycle>(FindObjectsSortMode.None))
                    if (clock.gameObject.scene == gameObject.scene) { dayNightClock = clock; break; }
            }
            return dayNightClock != null && entry.MatchesHour(Mathf.Repeat(dayNightClock.timeOfDay, 1f) * 24f);
        }

        internal bool CanRetireForSchedule(DummyEnemy actor)
        {
            if (!isActiveAndEnabled || !spawningEnabled || dayNightClock == null || player == null || actor == null
                || !actor.IsAlive || actor.IsDowned || actor.CaptureLocked
                || (actor.transform.position - player.position).sqrMagnitude < 144f) return false;
            var metadata = actor.GetComponent<PetCollectionMetadata>();
            if (metadata == null) return false;
            var group = eligible.Find(entry => Same(entry.source.definitionId, metadata.DefinitionId));
            if (group == null || CanSpawnNow(group.source)) return false;
            var brain = actor.GetComponent<BasicCreatureEnemy>();
            var commands = player.GetComponent<PetCommandInput>();
            if (brain == null || brain.Target != null || (commands != null &&
                (commands.SelectedEnemy == actor || commands.activePet != null && commands.activePet.CombatTarget == actor))) return false;
            var view = commands != null && commands.commandCamera != null ? commands.commandCamera : Camera.main;
            if (view == null) return false;
            Vector3 point = view.WorldToViewportPoint(actor.TargetPosition);
            return point.z <= 0 || point.x < -.12f || point.x > 1.12f || point.y < -.12f || point.y > 1.12f;
        }

        bool TrySampleNavMesh(Vector3 candidate, NavMeshQueryFilter filter, out NavMeshHit nav)
        {
            if (NavMesh.SamplePosition(candidate, out nav, navMeshSampleDistance, filter) && Contains(nav.position)) return true;

            // Search at the local floor height for this X/Z instead of increasing the
            // spherical radius, which can snap to a distant tile or another storey.
            var bounds = zoneBounds.bounds;
            Vector3 origin = new Vector3(candidate.x, bounds.max.y + .05f, candidate.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, surfaceHits, bounds.size.y + .10f,
                obstacleMask, QueryTriggerInteraction.Ignore);
            if (count == surfaceHits.Length) return false;
            float closestHeight = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var floor = surfaceHits[i];
                if (floor.normal.y < .5f || !Contains(floor.point) || IsActorCollider(floor.collider)) continue;
                float height = Mathf.Abs(floor.point.y - candidate.y);
                if (height >= closestHeight || !NavMesh.SamplePosition(floor.point, out var sample, navMeshSampleDistance, filter)
                    || !Contains(sample.position) || Mathf.Abs(sample.position.y - floor.point.y) > Mathf.Max(.10f, groundIgnoreHeight)) continue;
                closestHeight = height;
                nav = sample;
                found = true;
            }
            return found;
        }

        bool FindPosition(GameObject prefab, out Vector3 position, out Quaternion rotation, out bool nearPlayer)
        {
            position = default; rotation = Quaternion.identity; nearPlayer = false;
            if (requireWaterNearby && (waterBoundary == null || !waterBoundary.enabled || !waterBoundary.gameObject.activeInHierarchy)) {
                LastStatus = "Zone ven nước cần gắn Water Boundary đang hoạt động.";
                return false;
            }
            var capsule = prefab.GetComponent<CapsuleCollider>();
            var agent = prefab.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Vector3 size = zoneBounds.size, center = zoneBounds.center, scale = prefab.transform.localScale;
            float radius = Mathf.Max(.05f, capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            float height = Mathf.Max(radius * 2, capsule.height * Mathf.Abs(scale.y));
            int noNav = 0, wrongHeight = 0, outside = 0, edges = 0, players = 0, separation = 0, obstacles = 0, awayFromWater = 0;
            string blocker = "";
            for (int attempt = 0; attempt < positionAttempts; attempt++)
            {
                var local = center + new Vector3(((float)random.NextDouble() - .5f) * size.x, 0, ((float)random.NextDouble() - .5f) * size.z);
                var candidate = zoneBounds.transform.TransformPoint(local);
                if (!TrySampleNavMesh(candidate, filter, out var nav)) { noNav++; continue; }
                if (Mathf.Abs(nav.position.y - zoneBounds.bounds.center.y) > zoneBounds.bounds.extents.y) { wrongHeight++; continue; }
                if (!Contains(nav.position)) { outside++; continue; }
                if (requireWaterNearby) {
                    var shore = waterBoundary.ClosestPoint(nav.position);
                    float distance = HorizontalSquared(shore - nav.position);
                    if (distance < .0025f || distance > maximumDistanceFromWater * maximumDistanceFromWater
                        || Mathf.Abs(shore.y - nav.position.y) > 2.5f) { awayFromWater++; continue; }
                }
                bool close = player != null && HorizontalSquared(nav.position - player.position) < minimumPlayerSpawnDistance * minimumPlayerSpawnDistance;
                if (close && (!allowNearPlayerFallback || attempt < positionAttempts * 2 / 3)) { players++; continue; }
                bool separated = true;
                foreach (var other in DummyEnemy.ActiveEnemies)
                    if (other != null && other.gameObject.activeInHierarchy && HorizontalSquared(nav.position - other.transform.position) < minimumSpawnSeparation * minimumSpawnSeparation) { separated = false; break; }
                if (separated) foreach (var other in CreatureCombatant.Active)
                        if (other != null && !other.IsEnemy && other.CanBeAttacked && HorizontalSquared(nav.position - other.transform.position) < minimumSpawnSeparation * minimumSpawnSeparation) { separated = false; break; }
                if (!separated) { separation++; continue; }
                var yaw = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
                Vector3 bodyOffset = yaw * Vector3.Scale(capsule.center, scale);
                float clearance = radius + Mathf.Sqrt(HorizontalSquared(bodyOffset));
                if (!NavMesh.FindClosestEdge(nav.position, out var edge, filter) || edge.distance < clearance) { edges++; continue; }
                Vector3 bodyCenter = nav.position + bodyOffset;
                Vector3 halfSegment = Vector3.up * (height * .5f - radius);
                Vector3 lower = bodyCenter - halfSegment, upper = bodyCenter + halfSegment;
                if (!ContainsBody(lower, upper, radius)) { outside++; continue; }
                if (BodyBlocked(nav.position, lower, upper, radius, out blocker)) { obstacles++; continue; }
                LastPlacementReport = "Vi tri hop le sau " + (attempt + 1) + " lan thu";
                position = nav.position; rotation = yaw; nearPlayer = close; return true;
            }
            LastPlacementReport = prefab.name + ": NavMesh khong tim thay=" + noNav + ", sai do cao=" + wrongHeight
                + ", ngoai Box/body khong vua=" + outside + ", sat mep NavMesh=" + edges + ", gan player=" + players
                + ", sat actor=" + separation + ", sai khoang cach bo nuoc=" + awayFromWater
                + ", vat can=" + obstacles + (blocker.Length > 0 ? " (" + blocker + ")" : "");
            LastStatus = LastPlacementReport + ". Thu lai sau 1 giay; dung nut coverage de phan biet NavMesh thieu/sai do cao.";
            return false;
        }

        static float HorizontalSquared(Vector3 value) => value.x * value.x + value.z * value.z;
        static bool IsActorCollider(Collider collider) => collider != null &&
            (collider.GetComponentInParent<CreatureCombatant>() != null || collider.GetComponentInParent<BasicPlayerMovement>() != null);

        bool BodyBlocked(Vector3 foot, Vector3 lower, Vector3 upper, float radius, out string blocker)
        {
            blocker = "";
            int mask = obstacleMask.value;
            var playerBody = player != null ? player.GetComponent<CharacterController>() : null;
            if (playerBody != null) mask |= 1 << playerBody.gameObject.layer;
            // A small sole clearance avoids rejecting exact ground contact due to float error.
            Vector3 lift = Vector3.up * .03f;
            lower += lift;
            upper += lift;
            int count = Physics.OverlapCapsuleNonAlloc(lower, upper, radius, overlaps, mask, QueryTriggerInteraction.Collide);
            if (count == overlaps.Length) { blocker = "overlap buffer"; return true; }
            float tolerance = Mathf.Min(groundIgnoreHeight, radius * .5f);
            int aboveCount = -1;
            for (int i = 0; i < count; i++)
            {
                var other = overlaps[i];
                if (other == null) continue;
                bool actor = IsActorCollider(other);
                if (!actor && (other.isTrigger || (obstacleMask.value & (1 << other.gameObject.layer)) == 0)) continue;
                if (!actor && tolerance > 0f && IsLocalSupport(other, foot, lower.y - radius, tolerance))
                {
                    if (aboveCount < 0)
                    {
                        Vector3 raised = lower + Vector3.up * tolerance;
                        var top = Vector3.Max(raised, upper);
                        aboveCount = Physics.OverlapCapsuleNonAlloc(raised, top, radius, aboveFootOverlaps, mask, QueryTriggerInteraction.Collide);
                    }
                    if (aboveCount < aboveFootOverlaps.Length && Array.IndexOf(aboveFootOverlaps, other, 0, aboveCount) < 0) continue;
                }
                blocker = other.name;
                return true;
            }
            return false;
        }

        static bool IsLocalSupport(Collider collider, Vector3 foot, float soleY, float tolerance)
        {
            // Measure height at the feet, not the top of the terrain's world AABB.
            float top = Mathf.Max(foot.y, soleY) + tolerance + .03f;
            float bottom = Mathf.Min(foot.y, soleY) - tolerance;
            return collider.Raycast(new Ray(new Vector3(foot.x, top, foot.z), Vector3.down), out var hit, top - bottom)
                && hit.normal.y >= .5f && Mathf.Abs(hit.point.y - foot.y) <= tolerance + .03f
                && hit.point.y <= soleY + tolerance;
        }

        bool ContainsBody(Vector3 lower, Vector3 upper, float radius)
        {
            var inverse = zoneBounds.transform.worldToLocalMatrix;
            Vector3 a = inverse.MultiplyPoint3x4(lower) - zoneBounds.center;
            Vector3 b = inverse.MultiplyPoint3x4(upper) - zoneBounds.center;
            Vector3 half = zoneBounds.size * .5f;
            for (int axis = 0; axis < 3; axis++)
            {
                var row = inverse.GetRow(axis);
                float inset = radius * new Vector3(row.x, row.y, row.z).magnitude;
                if (Mathf.Max(Mathf.Abs(a[axis]), Mathf.Abs(b[axis])) + inset > half[axis]) return false;
            }
            return true;
        }
        bool Contains(Vector3 point)
        {
            Vector3 local = zoneBounds.transform.InverseTransformPoint(point) - zoneBounds.center;
            Vector3 half = zoneBounds.size * .5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        void OnDestroy()
        {
            foreach (var slot in slots)
            {
                var actor = slot != null ? slot.CurrentActor : null;
                var capture = actor != null ? actor.GetComponent<WildPetCaptureTarget>() : null;
                if (capture == null || !capture.IsWild) continue;
                actor.DeathManagedByEncounter = false;
                if (!actor.CaptureLocked && !capture.StagedForSave) Destroy(actor.gameObject);
            }
            if (lifecycleRoot != null) Destroy(lifecycleRoot);
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            var box = zoneBounds != null ? zoneBounds : GetComponent<BoxCollider>();
            if (box == null) return;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.color = new Color(.3f, .8f, .65f, .15f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(.3f, .8f, .65f, 1);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = previous;
            foreach (var slot in slots) if (slot != null && slot.CurrentActor != null)
                {
                    Gizmos.DrawWireSphere(slot.CurrentActor.transform.position, .35f);
                    Gizmos.DrawLine(slot.transform.position, slot.CurrentActor.transform.position);
                }
        }
    }
}
