using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;

namespace Capstone.Game.Elements
{
    [Serializable]
    public sealed class ElementMatchupRow
    {
        [SupportedElement]
        public PetElement attack;
        public PetElement[] strong = Array.Empty<PetElement>();
        public PetElement[] weak = Array.Empty<PetElement>();
    }

    /// <summary>Bang lookup bat bien; kiem tra va cache mot lan, khong cap phat moi hit.</summary>
    public sealed class ElementMatchupLookup
    {
        public static readonly ElementMatchupLookup Neutral = new ElementMatchupLookup(null);
        readonly float[] factors = new float[ElementCatalog.Count * ElementCatalog.Count];
        public IReadOnlyList<string> Errors { get; }

        public bool IsValid => Errors.Count == 0;
        public ElementMatchupLookup(IReadOnlyList<ElementMatchupRow> rows)
        {
            for (int i = 0; i < factors.Length; i++)
            {
                factors[i] = 1f;
            }

            var errors = new List<string>();
            var attacks = new HashSet<PetElement>();
            var pairs = new HashSet<int>();
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row == null || !ElementCatalog.IsSupported(row.attack))
                    {
                        errors.Add("Hang tan cong khong thuoc chin he: " + i);
                        continue;
                    }

                    if (!attacks.Add(row.attack))
                    {
                        errors.Add("Trung hang tan cong: " + row.attack);
                    }

                    ValidateTargets(row.attack, row.strong, pairs, errors);
                    ValidateTargets(row.attack, row.weak, pairs, errors);
                    if (row.attack == PetElement.Normal && ((row.strong?.Length ?? 0) + (row.weak?.Length ?? 0) > 0))
                    {
                        errors.Add("He Thuong tan cong phai trung tinh.");
                    }
                }
            }

            Errors = errors.AsReadOnly();
            if (!IsValid || rows == null)
            {
                return;
            }

            foreach (var row in rows)
            {
                Write(row.attack, row.strong, 2f);
                Write(row.attack, row.weak, .5f);
            }
        }

        static void ValidateTargets(PetElement attack, PetElement[] targets, HashSet<int> pairs, List<string> errors)
        {
            if (targets == null)
            {
                return;
            }

            foreach (var target in targets)
            {
                if (!ElementCatalog.IsSupported(target))
                {
                    errors.Add("He phong thu ngoai danh muc: " + target);
                    continue;
                }

                int key = ElementCatalog.IndexOf(attack) * ElementCatalog.Count + ElementCatalog.IndexOf(target);
                if (!pairs.Add(key))
                {
                    errors.Add("Trung hoac mau thuan cap he: " + attack + " / " + target);
                }
            }
        }

        void Write(PetElement attack, PetElement[] targets, float factor)
        {
            if (targets == null)
            {
                return;
            }

            int row = ElementCatalog.IndexOf(attack) * ElementCatalog.Count;
            foreach (var target in targets)
            {
                factors[row + ElementCatalog.IndexOf(target)] = factor;
            }
        }

        public float Against(PetElement attack, PetElement target)
        {
            int row = ElementCatalog.IndexOf(attack), column = ElementCatalog.IndexOf(target);
            return row >= 0 && column >= 0 ? factors[row * ElementCatalog.Count + column] : 1f;
        }
    }
}
