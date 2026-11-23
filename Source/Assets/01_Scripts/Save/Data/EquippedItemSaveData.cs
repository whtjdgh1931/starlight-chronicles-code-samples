using System;

/// <summary>
/// 장비 아이템 슬롯 저장용 클래스
/// </summary>
[Serializable]
public sealed class EquippedItemSaveData
{
    public EquipSlot Slot;

    public ItemSaveData Item;
}
