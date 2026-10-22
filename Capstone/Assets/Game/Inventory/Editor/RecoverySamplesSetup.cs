using System;
using System.IO;
using System.Linq;
using Capstone.Game.SaveSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.Inventory.Editor {
    /// <summary>Creates only the approved recovery samples; never resets the player's bag.</summary>
    public static class RecoverySamplesSetup {
        const string Folder="Assets/Game/Inventory";
        const string CatalogPath="Assets/Resources/Inventory/RecoveryItems.asset";
        static bool installing;
        static string deliveryDetails;

        [InitializeOnLoadMethod] static void Initialize() {
            EditorApplication.delayCall += EnsureDefinitions;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }
        [MenuItem("Game Tools/GameToolThang/Inventory/Create Recovery Sample Definitions")]
        public static void EnsureDefinitions() {
            if(installing || EditorApplication.isPlayingOrWillChangePlaymode) return;
            installing=true;
            try {
                var catalog=AssetDatabase.LoadAssetAtPath<InventoryRecoveryCatalog>(CatalogPath);
                if(catalog!=null) return;
                var potion=AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>(Folder+"/Potion.asset");
                if(potion==null) { Debug.LogError("Không tìm thấy Potion.asset để tạo dữ liệu hồi phục."); return; }
                // Keep Potion's GUID/stable ID and its existing reward/save references.
                Configure(potion,"Bình hồi máu",GameItemCategory.Medicine,100,0,
                    "Bình thuốc hồi phục sinh lực cho thú đồng hành.","Hồi tối đa 100 HP.");
                var apple=Create("HealingApple","healing-apple","Táo hồi phục",GameItemCategory.Food,30,0,
                    "Assets/ThangTest/UIThang/GDS/Common/Sprites/Items/apple.png",
                    "Trái táo dùng để hồi một phần sinh lực cho thú đồng hành.","Hồi tối đa 30 HP.");
                var mana=Create("ManaPotion","mana-potion","Bình mana",GameItemCategory.Medicine,0,30,
                    "Assets/ThangTest/UIThang/GDS/Common/Sprites/Items/mana.png",
                    "Bình thuốc hồi năng lượng cho thú có sử dụng MP.","Hồi tối đa 30 MP.");
                if(!AssetDatabase.IsValidFolder("Assets/Resources/Inventory")) AssetDatabase.CreateFolder("Assets/Resources","Inventory");
                catalog=ScriptableObject.CreateInstance<InventoryRecoveryCatalog>();
                catalog.items=new[]{potion,apple,mana,AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>(Folder+"/DuskStone.asset")};
                AssetDatabase.CreateAsset(catalog,CatalogPath);
                AssetDatabase.SaveAssetIfDirty(potion);
                AssetDatabase.SaveAssetIfDirty(apple);
                AssetDatabase.SaveAssetIfDirty(mana);
                AssetDatabase.SaveAssetIfDirty(catalog);
            } finally { installing=false; }
        }
        static MonsterItemDefinition Create(string assetName,string id,string displayName,GameItemCategory category,int hp,int mp,string iconPath,string description,string effect) {
            string path=Folder+"/"+assetName+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>(path);
            if(existing!=null) return existing;
            var item=ScriptableObject.CreateInstance<MonsterItemDefinition>(); item.AssignStableId(id);
            item.Icon=AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            Configure(item,displayName,category,hp,mp,description,effect);
            AssetDatabase.CreateAsset(item,path); return item;
        }
        static void Configure(MonsterItemDefinition item,string name,GameItemCategory category,int hp,int mp,string description,string effect) {
            item.Name=name; item.Stackable=true; item.MaxStackSize=99;
            var so=new SerializedObject(item);
            so.FindProperty("category").enumValueIndex=(int)category;
            so.FindProperty("description").stringValue=description;
            so.FindProperty("effect").stringValue=effect;
            so.FindProperty("source").stringValue="Dữ liệu thử nghiệm";
            so.FindProperty("healAmount").intValue=hp;
            so.FindProperty("energyRestoreAmount").intValue=mp;
            so.FindProperty("usableFromInventory").boolValue=true;
            so.FindProperty("consumable").boolValue=true;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(item);
        }
        [MenuItem("Game Tools/GameToolThang/Inventory/Add Recovery Samples To Current Bag")]
        static void AddSamplesFromMenu() => DeliverSamples();

        public static bool DeliverSamples() {
            if(!Application.isPlaying) return false;
            var save=Object.FindFirstObjectByType<PlayerSaveController>();
            var catalog=InventoryRecoveryCatalog.Load();
            deliveryDetails="Không tìm thấy thành phần Túi đồ, save hoặc catalog.";
            if(save==null || catalog==null) return false;
            // Read an existing slot before delivery. Never bypass the unloaded/corrupt-save guard.
            if(save.HasSave && !save.IsCurrentSlotLoaded && !save.LoadNow()) {
                deliveryDetails="Không thể nạp save hiện có: "+save.SaveBlockedReason;
                return false;
            }
            var inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();
            if(inventory==null || save.IsSaveBlocked) return false;
            var snapshot=inventory.CreateSaveData();
            foreach(var item in catalog.items) {
                if(item==null || item.HealAmount<=0 && item.EnergyRestoreAmount<=0) continue;
                int missing=Mathf.Max(0,5-inventory.GetQuantity(item));
                if(missing>0 && !inventory.TryAddItemAtomic(item,missing)) {
                    inventory.RestoreFromSaveData(snapshot,out _); return false;
                }
            }
            if(!save.SaveNow()) { inventory.RestoreFromSaveData(snapshot,out _); return false; }
            deliveryDetails=string.Join("\n",inventory.GetItems().Select(item=>item.ItemId+" | "+item.Name+" | "+item.Quantity));
            Debug.Log("Đã thêm tối thiểu 5 táo, 5 bình máu và 5 bình mana; giữ nguyên các vật phẩm khác.");
            return true;
        }
        static void Poll() {
            const string request="Temp/AddRecoverySamples.request";
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || !Application.isPlaying || Time.unscaledTime < 2f || !File.Exists(request)) return;
            try { File.Delete(request); }
            catch (IOException) { return; }
            bool result=DeliverSamples();
            Directory.CreateDirectory("../artifacts/panel-consistency");
            File.WriteAllText("../artifacts/panel-consistency/sample-delivery.txt",(result?"PASS recovery samples delivered and saved":"FAIL recovery samples delivery; existing bag preserved")+"\n"+deliveryDetails);
        }
    }
}
