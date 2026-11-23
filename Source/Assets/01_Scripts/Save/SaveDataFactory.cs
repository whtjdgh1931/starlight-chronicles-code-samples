using System;
using System.Collections.Generic;

/// <summary>
/// 플레이어 정보를 런타임 저장 전용 SaveData로 변환
/// </summary>
public sealed class SaveDataFactory
{
    public SaveData Create(
     PlayerStat playerStat,
     PlayerInventory playerInventory,
     PlayerEquipment playerEquipment,
     GoldWallet goldWallet,
     PlayerQuestProgress questProgress,
     PlayerQuickSlots quickSlots)
    {
        return new SaveData
        {
            Player = CreatePlayerData(playerStat),
            Inventory = CreateInventoryData(playerInventory, goldWallet),
            EquipItems = CreateEquipItems(playerEquipment),
            Quests = questProgress.Capture(),
            QuickSlots = quickSlots.Capture()
        };
    }

    private static PlayerSaveData CreatePlayerData(PlayerStat playerStat)
    {
        return new PlayerSaveData
        {
            Class = playerStat.Class,
            Level = playerStat.Level,
            CurrentHp = playerStat.CurrentHp,
            CurrentMp = playerStat.CurrentMp,
            CurrentExp = playerStat.CurrentExp
        };
    }

    private static InventorySaveData CreateInventoryData(PlayerInventory playerInventory, GoldWallet goldWallet)
    {
        InventorySaveData inventoryData = new()
        {
            Gold = goldWallet.CurrentGold
        };

        foreach (InventoryTab tab in Enum.GetValues(typeof(InventoryTab)))
        {
            if (tab == InventoryTab.None)
            {
                continue;
            }

            inventoryData.Tabs.Add(CreateInventoryTabData(playerInventory, tab));
        }

        foreach (InventoryItemData item in playerInventory.PendingBossRewards)
        {
            inventoryData.PendingBossRewards.Add(CreateItemData(item));
        }
        return inventoryData;
    }

    private static InventoryTabSaveData CreateInventoryTabData(PlayerInventory playerInventory, InventoryTab tab)
    {
        int slotCnt = playerInventory.GetCurrentSlotCount(tab);
        InventoryItemData[] items = playerInventory.GetListByTab(tab);
        InventoryTabSaveData tabData = new()
        {
            Tab = tab,
            UnlockedSlotCnt = slotCnt
        };

        for (int i = 0; i < slotCnt; i++)
        {
            InventoryItemData item = items[i];

            if (item == null || item.IsEmpty || item.IsDestroyed)
            {
                continue;
            }

            tabData.Slots.Add(new InventorySlotSaveData
            {
                SlotIndex = i,
                Item = CreateItemData(item)
            });
        }

        return tabData;
    }

    private static List<EquippedItemSaveData> CreateEquipItems(PlayerEquipment playerEquipment)
    {
        List<EquippedItemSaveData> equipItems = new();

        foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
        {
            if (slot == EquipSlot.None || !playerEquipment.TryGetEquippedItem(slot, out InventoryItemData item))
            {
                continue;
            }

            if (item.IsDestroyed)
            {
                continue;
            }

            equipItems.Add(new EquippedItemSaveData
            {
                Slot = slot,
                Item = CreateItemData(item)
            });
        }

        return equipItems;
    }

    private static ItemSaveData CreateItemData(InventoryItemData item)
    {
        return new ItemSaveData
        {
            ItemId = item.ItemData.ID,
            Count = item.Count,
            EnhanceLevel = item.EnhanceLevel
        };
    }
}
