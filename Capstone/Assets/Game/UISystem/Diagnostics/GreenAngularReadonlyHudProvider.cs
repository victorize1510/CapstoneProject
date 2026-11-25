#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Transient Editor-only visual QA fixture; deliberately has no gameplay/save commands.</summary>
    public sealed class GreenAngularReadonlyHudProvider : MonoBehaviour,IPetHudProvider {
        public IReadOnlyList<PetSlotHudData> Slots;
        public IReadOnlyList<SkillHudData> Skills;
        public PetStatusHudData Status;
        public event Action HudDataChanged {add{}remove{}}
        public PetStatusHudData GetSelectedPetStatus()=>Status;
        public IReadOnlyList<PetSlotHudData> GetPetSlots()=>Slots;
        public IReadOnlyList<SkillHudData> GetSkills()=>Skills;
        public void SelectPetSlot(int index){}
        public void RequestSkill(int index){}
    }
}

#endif

