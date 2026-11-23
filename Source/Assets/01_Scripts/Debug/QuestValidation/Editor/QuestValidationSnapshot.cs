using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 퀘스트·보상 상태 비교용 독립 스냅샷
/// 자연 회복으로 달라지는 현재 HP·MP는 비교에서 제외
/// </summary>
internal sealed class QuestValidationSnapshot
{
    [Serializable]
    private sealed class RewardState
    {
        public PlayerClass Class;
        public int Level;
        public float Experience;
        public InventorySaveData Inventory;
        public List<EquippedItemSaveData> Equipment;
        public List<QuestSaveData> Quests;
    }

    private readonly SaveData _data;
    private readonly Dictionary<int, long> _items = new Dictionary<int, long>();
    private readonly string _signature;

    internal QuestValidationSnapshot(SaveData data)
    {
        // CaptureCharacterState와 파일 역직렬화가 제공한 독립 데이터만 소유
        _data = data;
        data.Quests.Sort((a, b) => a.QuestId.CompareTo(b.QuestId));
        data.EquipItems.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        data.Inventory.Tabs.Sort((a, b) => a.Tab.CompareTo(b.Tab));
        foreach (InventoryTabSaveData tab in data.Inventory.Tabs)
        {
            tab.Slots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            foreach (InventorySlotSaveData slot in tab.Slots)
            {
                if (slot.Item == null) { continue; }
                _items.TryGetValue(slot.Item.ItemId, out long count);
                _items[slot.Item.ItemId] = count + slot.Item.Count;
            }
        }
        _signature = JsonUtility.ToJson(new RewardState
        {
            Class = data.Player.Class,
            Level = data.Player.Level,
            Experience = data.Player.CurrentExp,
            Inventory = data.Inventory,
            Equipment = data.EquipItems,
            Quests = data.Quests
        });
    }

    internal bool Matches(QuestValidationSnapshot other) => _signature == other._signature;

    /// <summary>
    /// 최초 수령의 골드와 수집품 차감·보상 아이템 총수량 확인
    /// 레벨·경험치는 변경 내역으로 별도 표시
    /// </summary>
    internal bool HasExpectedRewardAmounts(QuestValidationSnapshot after, QuestData quest)
    {
        if ((long)after._data.Inventory.Gold - _data.Inventory.Gold != quest.RewardGold)
        {
            return false;
        }
        var ids = new HashSet<int>(_items.Keys);
        ids.UnionWith(after._items.Keys);
        ids.Add(quest.RewardItemID);
        foreach (ItemRequirement required in quest.GetRequiredItems()) { if (required != null) { ids.Add(required.ItemId); } }
        foreach (int id in ids)
        {
            _items.TryGetValue(id, out long expected);
            after._items.TryGetValue(id, out long actual);
            if (id == quest.RewardItemID) { expected += quest.RewardItemCount; }
            foreach (ItemRequirement required in quest.GetRequiredItems())
            {
                if (required != null && required.ItemId == id) { expected -= required.Count; }
            }
            if (expected != actual) { return false; }
        }
        return true;
    }

    /// <summary>
    /// 변경이 없는 항목도 포함한 Inspector 전후 비교 표
    /// </summary>
    internal List<QuestValidationResult.Change> CompareRows(QuestValidationSnapshot after, QuestData quest, IReadOnlyDictionary<int, ItemData> definitions)
    {
        QuestSaveData beforeQuest = _data.Quests.Find(record => record.QuestId == quest.ID);
        QuestSaveData afterQuest = after._data.Quests.Find(record => record.QuestId == quest.ID);
        var rows = new List<QuestValidationResult.Change>
        {
            new QuestValidationResult.Change("전체 퀘스트 기록 수", _data.Quests.Count.ToString(), after._data.Quests.Count.ToString()),
            new QuestValidationResult.Change("수락 여부", beforeQuest == null ? "미수락" : "수락",
                afterQuest == null ? "미수락" : "수락"),
            new QuestValidationResult.Change("보상 수령", beforeQuest?.RewardClaimed == true ? "수령" : "미수령",
                afterQuest?.RewardClaimed == true ? "수령" : "미수령"),
            new QuestValidationResult.Change(quest.ObjectiveType == QuestObjectiveType.CollectItem ? "충족한 수집 목표 수" : "목표 진행",
                ProgressFor(quest).ToString() + "/" + (quest.ObjectiveType == QuestObjectiveType.CollectItem ? quest.GetRequiredItems().Count : quest.TargetCount),
                after.ProgressFor(quest).ToString() + "/" + (quest.ObjectiveType == QuestObjectiveType.CollectItem ? quest.GetRequiredItems().Count : quest.TargetCount)),
            new QuestValidationResult.Change("골드", _data.Inventory.Gold.ToString(), after._data.Inventory.Gold.ToString()),
            new QuestValidationResult.Change("레벨", _data.Player.Level.ToString(), after._data.Player.Level.ToString()),
            new QuestValidationResult.Change("현재 경험치", _data.Player.CurrentExp.ToString(), after._data.Player.CurrentExp.ToString())
        };
        foreach (ItemRequirement required in quest.GetRequiredItems())
        {
            if (required == null) { continue; }
            string name = definitions.TryGetValue(required.ItemId, out ItemData item) ? item.Name : "정의 없음";
            long beforeCount = ItemCount(required.ItemId);
            long afterCount = after.ItemCount(required.ItemId);
            rows.Add(new QuestValidationResult.Change($"수집 {required.ItemId} · {name}",
                $"{beforeCount}/{required.Count} · 부족 {Math.Max(0L, required.Count - beforeCount)}",
                $"{afterCount}/{required.Count} · 부족 {Math.Max(0L, required.Count - afterCount)}"));
        }
        var ids = new SortedSet<int>(_items.Keys);
        ids.UnionWith(after._items.Keys);
        ids.Add(quest.RewardItemID);
        foreach (ItemRequirement required in quest.GetRequiredItems()) { if (required != null) { ids.Add(required.ItemId); } }
        foreach (int id in ids)
        {
            long beforeCount = ItemCount(id);
            long afterCount = after.ItemCount(id);
            if (beforeCount != afterCount || id == quest.RewardItemID || id == quest.TargetID)
            {
                rows.Add(new QuestValidationResult.Change($"아이템 {id}", beforeCount.ToString(), afterCount.ToString()));
            }
        }
        var questIds = new SortedSet<int>();
        foreach (QuestSaveData record in _data.Quests) { questIds.Add(record.QuestId); }
        foreach (QuestSaveData record in after._data.Quests) { questIds.Add(record.QuestId); }
        foreach (int id in questIds)
        {
            string beforeRecord = DescribeQuestRecord(_data.Quests.Find(record => record.QuestId == id));
            string afterRecord = DescribeQuestRecord(after._data.Quests.Find(record => record.QuestId == id));
            if (beforeRecord != afterRecord)
            {
                rows.Add(new QuestValidationResult.Change($"퀘스트 {id}", beforeRecord, afterRecord));
            }
        }
        rows.Add(new QuestValidationResult.Change("보상 관련 전체 데이터", "기준", Matches(after) ? "동일" : "변경됨"));
        return rows;
    }

    private static string DescribeQuestRecord(QuestSaveData record) => record == null ? "미수락"
        : $"수락 · 진행 {record.ProgressCount} · 수령 {record.RewardClaimed}";

    internal long ItemCount(int id) => _items.TryGetValue(id, out long count) ? count : 0;

    internal long ProgressFor(QuestData quest)
    {
        if (quest.ObjectiveType == QuestObjectiveType.CollectItem)
        {
            long complete = 0;
            foreach (ItemRequirement required in quest.GetRequiredItems())
            {
                if (required != null && required.Count > 0 && ItemCount(required.ItemId) >= required.Count) { complete++; }
            }
            return complete;
        }
        return _data.Quests.Find(record => record.QuestId == quest.ID)?.ProgressCount ?? 0;
    }

    internal string Describe(int questId)
    {
        QuestSaveData quest = _data.Quests.Find(record => record.QuestId == questId);
        string state = quest == null ? "미수락"
            : $"진행 {quest.ProgressCount}, 보상 수령 {quest.RewardClaimed}";
        return $"퀘스트 {questId}: {state}\n" +
            $"골드 {_data.Inventory.Gold}, 레벨 {_data.Player.Level}, 경험치 {_data.Player.CurrentExp}";
    }

    internal string Difference(QuestValidationSnapshot after, int questId)
    {
        var text = new StringBuilder();
        text.AppendLine("[이전] " + Describe(questId));
        text.AppendLine("[이후] " + after.Describe(questId));
        text.AppendLine($"골드 변화: {(long)after._data.Inventory.Gold - _data.Inventory.Gold:+#;-#;0}");
        var ids = new SortedSet<int>(_items.Keys);
        ids.UnionWith(after._items.Keys);
        foreach (int id in ids)
        {
            _items.TryGetValue(id, out long beforeCount);
            after._items.TryGetValue(id, out long afterCount);
            if (beforeCount != afterCount)
            {
                text.AppendLine($"아이템 {id}: {beforeCount} → {afterCount}");
            }
        }
        text.AppendLine("퀘스트·인벤토리·장비·성장 전체: " + (Matches(after) ? "동일" : "변경됨"));
        return text.ToString();
    }
}
