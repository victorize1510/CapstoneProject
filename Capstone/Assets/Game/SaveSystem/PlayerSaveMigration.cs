using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Capstone.Game.SaveSystem {
    public static class PlayerSaveMigration {
        static readonly string[] Sections = { "quest", "inventory", "currency", "pets", "profile", "achievements" };

        public static PlayerSaveData Read(string json, out int sourceVersion) {
            JObject root = Parse(json);
            sourceVersion = ReadVersion(root, 1);
            if (sourceVersion > PlayerSaveData.CurrentVersion) throw new NotSupportedException("Player save version " + sourceVersion + " is newer than supported.");
            if (sourceVersion < 1) throw new InvalidDataException("Invalid player save version.");
            bool hasSection = false;
            foreach (string section in Sections) {
                JToken value = root[section];
                if (value == null || value.Type == JTokenType.Null) continue;
                if (!(value is JObject)) throw new InvalidDataException("Invalid save section: " + section);
                hasSection = true;
            }
            if (!hasSection) throw new InvalidDataException("No recognized player data in save.");

            // A missing section is not an instruction to reset an existing subsystem.
            var data = new PlayerSaveData { quest = null, inventory = null, currency = null, pets = null, profile = null, achievements = null };
            JsonUtility.FromJsonOverwrite(root.ToString(Formatting.None), data);
            if (Absent(root, "quest")) data.quest = null;
            if (Absent(root, "inventory")) data.inventory = null;
            if (Absent(root, "currency")) data.currency = null;
            if (Absent(root, "pets")) data.pets = null;
            if (Absent(root, "profile")) data.profile = null;
            if (Absent(root, "achievements")) data.achievements = null;
            if (data.pets?.petStates != null) {
                JArray states = root["pets"]?["petStates"] as JArray;
                for (int i = 0; i < data.pets.petStates.Count; i++) {
                    var pet = data.pets.petStates[i];
                    if (pet == null) continue;
                    JObject source = states != null && i < states.Count ? states[i] as JObject : null;
                    if (source?["customization"] == null || source["customization"].Type == JTokenType.Null) pet.customization = null;
                    if (source?["runtimeStats"] == null || source["runtimeStats"].Type == JTokenType.Null) pet.runtimeStats = null;
                    else if (sourceVersion < 6 && pet.runtimeStats != null)
                        pet.runtimeStats.hasSkillLoadout = source["runtimeStats"]?["equippedSkillIds"] is JArray;
                }
            }
            data.version = PlayerSaveData.CurrentVersion;
            Validate(data);
            return data;
        }

        public static string Write(PlayerSaveData data) {
            Validate(data);
            var root = JObject.Parse(JsonUtility.ToJson(data));
            // Unity's inline serialization turns null classes into default objects.
            // Preserve absence explicitly so a partial save cannot reset a subsystem.
            if (data.quest == null) root["quest"] = JValue.CreateNull();
            if (data.inventory == null) root["inventory"] = JValue.CreateNull();
            if (data.currency == null) root["currency"] = JValue.CreateNull();
            if (data.pets == null) root["pets"] = JValue.CreateNull();
            if (data.profile == null) root["profile"] = JValue.CreateNull();
            if (data.achievements == null) root["achievements"] = JValue.CreateNull();
            if (data.pets?.petStates != null && root["pets"]?["petStates"] is JArray states) {
                for (int i = 0; i < data.pets.petStates.Count; i++) {
                    var pet = data.pets.petStates[i];
                    if (pet == null) { states[i] = JValue.CreateNull(); continue; }
                    if (pet.customization == null) states[i]["customization"] = JValue.CreateNull();
                    if (pet.runtimeStats == null) states[i]["runtimeStats"] = JValue.CreateNull();
                }
            }
            return root.ToString(Formatting.Indented);
        }

        static bool Absent(JObject root, string key) => root[key] == null || root[key].Type == JTokenType.Null;

        internal static JObject Parse(string json) {
            using (var reader = new JsonTextReader(new StringReader(json))) {
                reader.DateParseHandling = DateParseHandling.None;
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidDataException("Trailing content after save JSON.");
                return root;
            }
        }

        internal static int ReadVersion(JObject root, int fallback) {
            JToken token = root["version"];
            if (token == null) return fallback;
            if (token.Type != JTokenType.Integer) throw new InvalidDataException("Save version must be an integer.");
            return token.Value<int>();
        }

        public static void Validate(PlayerSaveData data) {
            if (data == null) throw new InvalidDataException("Player save data is null.");
            if (data.version != PlayerSaveData.CurrentVersion) throw new InvalidDataException("Migrate the save before writing it.");
            if (data.inventory?.captured == true) {
                if (data.inventory.capacity < 1 || data.inventory.items == null) throw new InvalidDataException("Invalid inventory capacity/items.");
                var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in data.inventory.items) {
                    if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.quantity < 0 || item.maxStackSize < 1 || !itemIds.Add(item.itemId))
                        throw new InvalidDataException("Invalid or duplicate inventory item ID.");
                }
            }
            if (data.pets?.captured != true) return;
            var roster = data.pets;
            if (roster.petStates == null || roster.partyPetIds == null || roster.boxPetIds == null || roster.partyPetIds.Count > 6 || roster.boxCapacity < 1)
                throw new InvalidDataException("Invalid pet roster.");
            var petIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pet in roster.petStates) {
                if (pet == null || string.IsNullOrWhiteSpace(pet.petId) || !petIds.Add(pet.petId)) throw new InvalidDataException("Invalid or duplicate pet instance ID.");
                var stats = pet.runtimeStats;
                if (stats?.captured == true && (stats.equippedSkillSlotCount < 2 || stats.equippedSkillSlotCount > 4
                    || (stats.hasSkillLoadout && (stats.equippedSkillIds == null || stats.equippedSkillIds.Count > stats.equippedSkillSlotCount))))
                    throw new InvalidDataException("Invalid skill loadout for pet " + pet.petId);
            }
            var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in roster.partyPetIds) ValidateAssignment(id, petIds, assigned);
            foreach (string id in roster.boxPetIds) ValidateAssignment(id, petIds, assigned);
            if (roster.boxPetIds.Count > roster.boxCapacity) throw new InvalidDataException("Box exceeds its saved capacity.");
            if (!string.IsNullOrWhiteSpace(roster.activePetId) && !roster.partyPetIds.Exists(id => string.Equals(id, roster.activePetId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Active pet is not in the saved party.");
            if (roster.releasedPetIds != null) foreach (string id in roster.releasedPetIds) {
                if (petIds.Contains(id)) throw new InvalidDataException("Pet is both released and owned: " + id);
            }
        }

        static void ValidateAssignment(string id, HashSet<string> pets, HashSet<string> assigned) {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (!pets.Contains(id) || !assigned.Add(id)) throw new InvalidDataException("Missing or multiply assigned pet: " + id);
        }
    }
}
