/// <summary>
/// SaveData를 런타임 상태로 복원
/// </summary>
public sealed class SaveDataRestorer
{

    /// <summary>
    /// 사전에 검증된 저장 데이터를 런타임 상태에 적용
    /// </summary>
    public void Restore(
       SaveData saveData,
       PlayerStat playerStat,
       PlayerInventory playerInventory,
       PlayerEquipment playerEquipment,
       GoldWallet goldWallet,
       PlayerQuestProgress questProgress,
       PlayerQuickSlots quickSlots,
       DataManager dataManager)
    {
        playerStat.RestoreProgress(saveData.Player);
        goldWallet.Restore(saveData.Inventory.Gold);
        questProgress.Restore(saveData.Quests);

        RestoreInventory(saveData.Inventory, playerInventory, dataManager);
        RestoreEquipItems(saveData, playerEquipment, dataManager);
        quickSlots.Restore(saveData.QuickSlots);

        goldWallet.PublishCurrentBalance();
        playerStat.CompleteRestore();
    }

    private static void RestoreInventory(
         InventorySaveData inventoryData,
         PlayerInventory playerInventory,
         DataManager dataManager)
    {
        playerInventory.BeginRestore();

        foreach (InventoryTabSaveData tabData in inventoryData.Tabs)
        {
            playerInventory.RestoreTab(tabData.Tab, tabData.UnlockedSlotCnt);

            foreach (InventorySlotSaveData slotData in tabData.Slots)
            {
                InventoryItemData item = CreateInventoryItem(slotData.Item, dataManager);
                playerInventory.RestoreItem(tabData.Tab, slotData.SlotIndex, item);
            }
        }

        foreach (ItemSaveData item in inventoryData.PendingBossRewards)
        {
            playerInventory.StoreBossReward(CreateInventoryItem(item, dataManager));
        }
        playerInventory.CompleteRestore();
    }

    private static void RestoreEquipItems(
    SaveData saveData,
    PlayerEquipment playerEquipment,
    DataManager dataManager)
    {
        playerEquipment.BeginRestore();

        foreach (EquippedItemSaveData equipData in saveData.EquipItems)
        {
            InventoryItemData item = CreateInventoryItem(equipData.Item, dataManager);
            playerEquipment.RestoreItem(equipData.Slot, item);
        }

        playerEquipment.CompleteRestore();
    }

    private static InventoryItemData CreateInventoryItem(ItemSaveData itemData, DataManager dataManager)
    {
        ItemData runtimeItemData = dataManager.ItemTable[itemData.ItemId];

        return new InventoryItemData(runtimeItemData, itemData.Count)
        {
            EnhanceLevel = itemData.EnhanceLevel
        };
    }

}
