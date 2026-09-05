namespace Capstone.Game.QuestSystem.Save {
    public interface IQuestSaveStore {
        bool Exists(string slotId);
        bool TrySave(string slotId, QuestSaveData data, out string error);
        bool TryLoad(string slotId, out QuestSaveData data, out string error);
        bool TryDelete(string slotId, out string error);
    }
}
