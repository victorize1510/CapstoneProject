using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    [Serializable]
    public sealed class CreatureSpawnForm
    {
        public bool enabled = true;
        public string formId = "01";
        [Tooltip("Prefab Wild day du combat/capture; khong dung Visual hoac OwnedPet.")]
        public GameObject prefab;
        [Min(0)] public float weight = 1;
        [Tooltip("0: dung mac dinh theo stage 1/18/36. Day la cap Zone, khong phai cap ca the.")]
        [Min(0)] public int minimumZoneLevel;
        [Tooltip("Ly do chua the gan prefab. Khong dung model-only de lap cho trong.")]
        [TextArea] public string configurationBlocker = "";
    }

    [Serializable]
    public sealed class CreatureSpawnSpecies
    {
        public bool enabled = true;
        public string definitionId = "";
        [Min(0)] public float weight = 1;
        [Tooltip("0: khong gioi han them; lay giao voi khoang cap Zone va khu vuc.")]
        [Min(0)] public int minimumLevel;
        [Min(0)] public int maximumLevel;
        public List<CreatureSpawnForm> forms = new List<CreatureSpawnForm>();
    }

    [Serializable]
    public sealed class CreatureSpawnLevelOverride
    {
        public string definitionId = "";
        public string formId = "01";
        [Min(1)] public int minimumZoneLevel = 1;
    }

    [CreateAssetMenu(menuName = "Game/Creatures/Spawn Table", fileName = "CreatureSpawnTable")]
    public sealed class CreatureSpawnTable : ScriptableObject
    {
        [Tooltip("Catalog OwnedPet hien co, chi kiem tra kha nang load lai sau capture; khong spawn tu catalog nay.")]
        public PetPrefabCatalog ownedPetCatalog;
        public List<string> excludedDefinitionIds = new List<string>();
        public List<CreatureSpawnSpecies> species = new List<CreatureSpawnSpecies>();
    }
}
