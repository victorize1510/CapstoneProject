using System;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    public enum PetCoreStat { Hp, Attack, Defense, MagicAttack, MagicDefense, Speed }

    [Serializable]
    public struct PetStatValues {
        public int hp, attack, defense, magicAttack, magicDefense, speed;

        public PetStatValues(int hp, int attack, int defense, int magicAttack, int magicDefense, int speed) {
            this.hp = hp;
            this.attack = attack;
            this.defense = defense;
            this.magicAttack = magicAttack;
            this.magicDefense = magicDefense;
            this.speed = speed;
        }

        public int Total => hp + attack + defense + magicAttack + magicDefense + speed;

        public int Get(PetCoreStat stat) => stat switch {
            PetCoreStat.Hp => hp,
            PetCoreStat.Attack => attack,
            PetCoreStat.Defense => defense,
            PetCoreStat.MagicAttack => magicAttack,
            PetCoreStat.MagicDefense => magicDefense,
            _ => speed
        };

        public void Set(PetCoreStat stat, int value) {
            switch (stat) {
                case PetCoreStat.Hp: hp = value; break;
                case PetCoreStat.Attack: attack = value; break;
                case PetCoreStat.Defense: defense = value; break;
                case PetCoreStat.MagicAttack: magicAttack = value; break;
                case PetCoreStat.MagicDefense: magicDefense = value; break;
                case PetCoreStat.Speed: speed = value; break;
            }
        }

        public PetStatValues Clamped(int minimum, int maximum) => new PetStatValues(
            Mathf.Clamp(hp, minimum, maximum), Mathf.Clamp(attack, minimum, maximum),
            Mathf.Clamp(defense, minimum, maximum), Mathf.Clamp(magicAttack, minimum, maximum),
            Mathf.Clamp(magicDefense, minimum, maximum), Mathf.Clamp(speed, minimum, maximum));
    }

    public static class PetNature {
        // Order is stable because new pets choose a uniformly random index.
        static readonly string[] Names = {
            "Hardy", "Lonely", "Brave", "Adamant", "Naughty",
            "Bold", "Docile", "Relaxed", "Impish", "Lax",
            "Timid", "Hasty", "Serious", "Jolly", "Naive",
            "Modest", "Mild", "Quiet", "Bashful", "Rash",
            "Calm", "Gentle", "Sassy", "Careful", "Quirky"
        };

        // Rows are the increased stat, columns the decreased stat.
        static readonly PetCoreStat[] Axes = {
            PetCoreStat.Attack, PetCoreStat.Defense, PetCoreStat.Speed,
            PetCoreStat.MagicAttack, PetCoreStat.MagicDefense
        };

        public static string RandomName => Names[UnityEngine.Random.Range(0, Names.Length)];

        public static string Normalize(string name) {
            for (int i = 0; i < Names.Length; i++)
                if (string.Equals(name, Names[i], StringComparison.OrdinalIgnoreCase)) return Names[i];
            return "Serious";
        }

        public static float Multiplier(string nature, PetCoreStat stat) {
            if (stat == PetCoreStat.Hp) return 1f;
            int index = Array.FindIndex(Names, name => string.Equals(name, nature, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return 1f;
            int row = index / 5, column = index % 5;
            if (row == column) return 1f;
            if (Axes[row] == stat) return 1.1f;
            if (Axes[column] == stat) return 0.9f;
            return 1f;
        }
    }
}
