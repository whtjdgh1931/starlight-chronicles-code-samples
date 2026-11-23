using System;

/// <summary>
/// 인벤토리 슬롯의 저장을 위한 클래스
/// </summary>
[Serializable]
public sealed class InventorySlotSaveData
{
    public int SlotIndex;

    public ItemSaveData Item;
}
