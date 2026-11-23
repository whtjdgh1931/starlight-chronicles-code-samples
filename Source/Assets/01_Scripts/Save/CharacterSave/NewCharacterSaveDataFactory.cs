using System;
using System.Collections.Generic;

public sealed class NewCharacterSaveDataFactory
{
    private const int TestBeginnerWeaponId = 3001;
    private const int TestLevelFiveWeaponId = 3002;
    private const int TestBeginnerHelmetId = 4001;
    private const int TestBeginnerArmorId = 5001;

    public SaveData Create(
       PlayerClass playerClass,
       PlayerStatData statData,
       InventorySettings inventorySettings)
    {
        if (playerClass == PlayerClass.None || !Enum.IsDefined(typeof(PlayerClass), playerClass))
        {
            throw new ArgumentOutOfRangeException(nameof(playerClass));
        }

        if (statData == null)
        {
            throw new ArgumentNullException(nameof(statData));
        }

        if (inventorySettings == null)
        {
            throw new ArgumentNullException(nameof(inventorySettings));
        }

        return new SaveData
        {
            Player = new PlayerSaveData
            {
                Class = playerClass,
                Level = 1,
                CurrentHp = statData.BaseHp,
                CurrentMp = statData.BaseMp,
                CurrentExp = 0f
            },
            Inventory = CreateInventory(inventorySettings),
            EquipItems = CreateTestEquipItems()
        };
    }

    private static InventorySaveData CreateInventory(InventorySettings settings)
    {
        InventorySaveData inventoryData = new()
        {
            Gold = settings.InitialGold
        };

        foreach (InventoryTab tab in Enum.GetValues(typeof(InventoryTab)))
        {
            if (tab == InventoryTab.None)
            {
                continue;
            }

            InventoryTabSaveData tabData = new()
            {
                Tab = tab,
                UnlockedSlotCnt = settings.DefaultSlotCnt
            };

            if (tab == InventoryTab.Equipment)
            {
                tabData.Slots.Add(new InventorySlotSaveData
                {
                    SlotIndex = 0,
                    Item = CreateItem(TestBeginnerHelmetId)
                });
                tabData.Slots.Add(new InventorySlotSaveData
                {
                    SlotIndex = 1,
                    Item = CreateItem(TestBeginnerArmorId)
                });
                tabData.Slots.Add(new InventorySlotSaveData
                {
                    SlotIndex = 2,
                    Item = CreateItem(TestLevelFiveWeaponId)
                });
            }

            inventoryData.Tabs.Add(tabData);
        }

        return inventoryData;
    }

    private static List<EquippedItemSaveData> CreateTestEquipItems()
    {
        return new List<EquippedItemSaveData>
        {
            CreateEquipItem(EquipSlot.Weapon, TestBeginnerWeaponId)
        };
    }

    private static EquippedItemSaveData CreateEquipItem(EquipSlot slot, int itemId)
    {
        return new EquippedItemSaveData
        {
            Slot = slot,
            Item = CreateItem(itemId)
        };
    }

    private static ItemSaveData CreateItem(int itemId)
    {
        return new ItemSaveData
        {
            ItemId = itemId,
            Count = 1,
            EnhanceLevel = 0
        };
    }
}
