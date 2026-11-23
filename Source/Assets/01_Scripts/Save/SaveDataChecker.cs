using System;
using System.Collections.Generic;

public sealed class SaveDataChecker
{
    /// <summary>
    /// 저장 버전, 플레이어 상태, 인벤토리와 장착 아이템을 검사
    /// </summary>
    public bool TryCheck(
SaveData saveData,
InventorySettings inventorySettings,
DataManager dataManager,
out string errorMessage)
    {
        if (inventorySettings == null)
        {
            throw new ArgumentNullException(nameof(inventorySettings));
        }

        if (dataManager == null)
        {
            throw new ArgumentNullException(nameof(dataManager));
        }

        if (saveData == null)
        {
            return Fail("저장 데이터가 없습니다.", out errorMessage);
        }

        saveData.MigrateLegacyVersion();

        if (saveData.Version != SaveData.CurrentVersion)
        {
            return Fail($"지원하지 않는 저장 버전입니다. Version: {saveData.Version}", out errorMessage);
        }

        if (!TryCheckQuests(saveData.Quests, out errorMessage)) { return false; }

        if (!TryCheckPlayer(saveData.Player, out errorMessage))
        {
            return false;
        }

        if (!TryCheckInventory(saveData.Inventory, inventorySettings, dataManager, out errorMessage))
        {
            return false;
        }

        if (!TryCheckEquipItems(saveData.EquipItems, dataManager, out errorMessage))
        {
            return false;
        }

        NormalizeQuickSlots(saveData, dataManager);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 퀵슬롯 길이 보정과 삭제·타 직업·중복 등록의 해당 칸 초기화
    /// 개인 UI 설정 오류로 캐릭터 전체 복원을 막지 않는 정규화
    /// </summary>
    private static void NormalizeQuickSlots(SaveData saveData, DataManager dataManager)
    {
        Array.Resize(ref saveData.QuickSlots, PlayerQuickSlots.SlotCount);
        string playerClass = saveData.Player.Class.ToString();
        HashSet<(QuickSlotKind Kind, int ContentId)> registered = new();
        for (int i = 0; i < saveData.QuickSlots.Length; i++)
        {
            QuickSlotSaveData slot = saveData.QuickSlots[i];
            bool valid = slot.Kind switch
            {
                QuickSlotKind.Empty => slot.ContentId == 0,
                QuickSlotKind.Skill => dataManager.SkillTable.TryGetValue(slot.ContentId, out SkillData skill) &&
                    string.Equals(skill.PlayerClass, playerClass, StringComparison.OrdinalIgnoreCase),
                QuickSlotKind.Consumable => dataManager.ItemTable.TryGetValue(slot.ContentId, out ItemData item) &&
                    item.GetItemType() == GameItemType.Consumable,
                _ => false
            };
            if (!valid)
            {
                saveData.QuickSlots[i] = default;
                continue;
            }
            if (slot.Kind != QuickSlotKind.Empty && !registered.Add((slot.Kind, slot.ContentId)))
            {
                saveData.QuickSlots[i] = default;
            }
        }
    }

    /// <summary>
    /// 외부 저장·접속 데이터의 누락·중복 ID·음수 진행도 차단
    /// </summary>
    private static bool TryCheckQuests(List<QuestSaveData> quests, out string errorMessage)
    {
        if (quests == null) { return Fail("퀘스트 저장 기록이 없습니다.", out errorMessage); }
        HashSet<int> questIds = new();
        foreach (QuestSaveData quest in quests)
        {
            if (quest == null || quest.QuestId <= 0 || quest.ProgressCount < 0 || !questIds.Add(quest.QuestId))
            {
                return Fail("퀘스트 ID 또는 진행도가 올바르지 않습니다.", out errorMessage);
            }
        }
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryCheckPlayer(PlayerSaveData playerData, out string errorMessage)
    {
        if (playerData == null)
        {
            return Fail("플레이어 저장 데이터가 없습니다.", out errorMessage);
        }

        if (playerData.Level < 1 || playerData.Level > Constants.MaxPlayerLevel)
        {
            return Fail($"플레이어 레벨 범위는 1~{Constants.MaxPlayerLevel}입니다. Level: {playerData.Level}", out errorMessage);
        }

        if (IsInvalidNumber(playerData.CurrentHp) || playerData.CurrentHp < 0f)
        {
            return Fail($"현재 HP가 올바르지 않습니다. CurrentHp: {playerData.CurrentHp}", out errorMessage);
        }

        if (IsInvalidNumber(playerData.CurrentMp) || playerData.CurrentMp < 0f)
        {
            return Fail($"현재 MP가 올바르지 않습니다. CurrentMp: {playerData.CurrentMp}", out errorMessage);
        }

        if (IsInvalidNumber(playerData.CurrentExp) || playerData.CurrentExp < 0f)
        {
            return Fail($"현재 경험치가 올바르지 않습니다. CurrentExp: {playerData.CurrentExp}", out errorMessage);
        }

        if (playerData.Class == PlayerClass.None || !Enum.IsDefined(typeof(PlayerClass), playerData.Class))
        {
            return Fail($"올바르지 않은 직업입니다. Class: {playerData.Class}", out errorMessage);
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryCheckInventory(
    InventorySaveData inventoryData,
    InventorySettings inventorySettings,
    DataManager dataManager,
    out string errorMessage)
    {
        if (inventoryData == null || inventoryData.Tabs == null)
        {
            return Fail("인벤토리 저장 데이터가 없습니다.", out errorMessage);
        }

        if (inventoryData.Gold < 0)
        {
            return Fail($"골드가 올바르지 않습니다. Gold: {inventoryData.Gold}", out errorMessage);
        }

        // 기존 버전 2 저장 파일의 보상 목록 누락 허용
        inventoryData.PendingBossRewards ??= new List<ItemSaveData>();
        foreach (ItemSaveData item in inventoryData.PendingBossRewards)
        {
            if (item == null)
            {
                return Fail("비어 있는 보관 보상 데이터", out errorMessage);
            }
            if (!TryCheckItem(item, dataManager, out _, out errorMessage))
            {
                return false;
            }
        }
        HashSet<InventoryTab> restoredTabs = new();

        foreach (InventoryTabSaveData tabData in inventoryData.Tabs)
        {
            if (tabData == null || tabData.Slots == null)
            {
                return Fail("비어 있는 인벤토리 탭 데이터가 있습니다.", out errorMessage);
            }

            if (tabData.Tab == InventoryTab.None || !Enum.IsDefined(typeof(InventoryTab), tabData.Tab))
            {
                return Fail($"올바르지 않은 인벤토리 탭입니다. Tab: {tabData.Tab}", out errorMessage);
            }

            if (!restoredTabs.Add(tabData.Tab))
            {
                return Fail($"중복된 인벤토리 탭입니다. Tab: {tabData.Tab}", out errorMessage);
            }

            int minimumSlotCnt = inventorySettings.DefaultSlotCnt;

            if (tabData.UnlockedSlotCnt < minimumSlotCnt || tabData.UnlockedSlotCnt > inventorySettings.MaxSlotCnt)
            {
                return Fail($"활성 슬롯 수가 범위를 벗어났습니다. " + $"Tab: {tabData.Tab}, " + $"SlotCnt: {tabData.UnlockedSlotCnt}", out errorMessage);
            }

            if (!TryCheckInventorySlots(tabData, dataManager, out errorMessage))
            {
                return false;
            }
        }

        int expectedTabCnt = Enum.GetValues(typeof(InventoryTab)).Length - 1;

        if (restoredTabs.Count != expectedTabCnt)
        {
            return Fail($"인벤토리 탭 수가 올바르지 않습니다. TabCnt: {restoredTabs.Count}", out errorMessage);
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryCheckInventorySlots(
        InventoryTabSaveData tabData,
        DataManager dataManager,
        out string errorMessage)
    {
        HashSet<int> restoredSlotIndices = new();

        foreach (InventorySlotSaveData slotData in tabData.Slots)
        {
            if (slotData == null || slotData.Item == null)
            {
                return Fail($"비어 있는 슬롯 저장 데이터가 있습니다. Tab: {tabData.Tab}", out errorMessage);
            }

            if (slotData.SlotIndex < 0 || slotData.SlotIndex >= tabData.UnlockedSlotCnt)
            {
                return Fail(
                        $"슬롯 인덱스가 범위를 벗어났습니다. Tab: {tabData.Tab}, SlotIndex: {slotData.SlotIndex}",
                        out errorMessage);
            }

            if (!restoredSlotIndices.Add(slotData.SlotIndex))
            {
                return Fail(
                        $"중복된 슬롯 인덱스입니다. Tab: {tabData.Tab}, SlotIndex: {slotData.SlotIndex}",
                        out errorMessage);
            }

            if (!TryCheckItem(slotData.Item, dataManager, out ItemData itemData, out errorMessage))
            {
                return false;
            }

            if (GetInventoryTab(itemData) != tabData.Tab)
            {
                return Fail(
                        $"아이템 종류와 인벤토리 탭이 일치하지 않습니다. ItemId: {itemData.ID}, Tab: {tabData.Tab}",
                        out errorMessage);
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryCheckEquipItems(
            List<EquippedItemSaveData> equipItems,
            DataManager dataManager,
            out string errorMessage)
    {
        if (equipItems == null)
        {
            return Fail("장착 아이템 저장 목록이 없습니다.", out errorMessage);
        }

        HashSet<EquipSlot> restoredSlots = new();

        foreach (EquippedItemSaveData equipData in equipItems)
        {
            if (equipData == null || equipData.Item == null)
            {
                return Fail("비어 있는 장착 아이템 데이터가 있습니다.", out errorMessage);
            }

            if (equipData.Slot == EquipSlot.None || !Enum.IsDefined(typeof(EquipSlot), equipData.Slot))
            {
                return Fail($"올바르지 않은 장비 슬롯입니다. Slot: {equipData.Slot}", out errorMessage);
            }

            if (!restoredSlots.Add(equipData.Slot))
            {
                return Fail($"중복된 장비 슬롯입니다. Slot: {equipData.Slot}", out errorMessage);
            }

            if (!TryCheckItem(equipData.Item, dataManager, out ItemData itemData, out errorMessage))
            {
                return false;
            }

            if (itemData.GetItemType() != GameItemType.Equipment || itemData.GetEquipSlot() != equipData.Slot)
            {
                return Fail(
                        $"아이템과 장비 슬롯이 일치하지 않습니다. ItemId: {itemData.ID}, Slot: {equipData.Slot}",
                        out errorMessage);
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryCheckItem(
         ItemSaveData itemData,
         DataManager dataManager,
         out ItemData runtimeItemData,
         out string errorMessage)
    {
        runtimeItemData = null;

        if (itemData.Count <= 0 || itemData.EnhanceLevel < 0)
        {
            return Fail($"아이템 수량 또는 강화 단계가 올바르지 않습니다. ItemId: {itemData.ItemId}", out errorMessage);
        }

        if (!dataManager.ItemTable.TryGetValue(itemData.ItemId, out runtimeItemData))
        {
            return Fail($"존재하지 않는 아이템 ID입니다. ItemId: {itemData.ItemId}", out errorMessage);
        }

        GameItemType itemType = runtimeItemData.GetItemType();

        if (itemType == GameItemType.None || itemType == GameItemType.Gold)
        {
            return Fail($"저장할 수 없는 아이템 종류입니다. ItemId: {itemData.ItemId}", out errorMessage);
        }

        if (itemType == GameItemType.Equipment && itemData.Count != 1)
        {
            return Fail($"장비 아이템 수량은 1이어야 합니다. ItemId: {itemData.ItemId}", out errorMessage);
        }

        if (itemType != GameItemType.Equipment && itemData.EnhanceLevel != 0)
        {
            return Fail($"장비가 아닌 아이템에 강화 단계가 있습니다. ItemId: {itemData.ItemId}", out errorMessage);
        }

        errorMessage = string.Empty;
        return true;
    }

    private static InventoryTab GetInventoryTab(ItemData itemData)
    {
        return itemData.GetItemType() switch
        {
            GameItemType.Equipment => InventoryTab.Equipment,
            GameItemType.Consumable => InventoryTab.Consumable,
            _ => InventoryTab.Etc
        };
    }

    private static bool IsInvalidNumber(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value);
    }

    private static bool Fail(string message, out string errorMessage)
    {
        errorMessage = message;
        return false;
    }
}
