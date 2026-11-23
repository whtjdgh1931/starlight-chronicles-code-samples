using System;
using System.Collections.Generic;

/// <summary>
/// 플레이어의 장착 아이템과 장비 슬롯 상태를 관리
/// 최종 능력치 계산은 PlayerStat이 담당
/// </summary>
public sealed class PlayerEquipment 
{
    private readonly Dictionary<EquipSlot, InventoryItemData> _equipItems = new Dictionary<EquipSlot, InventoryItemData>();
    public IEnumerable<InventoryItemData> EquippedItems => _equipItems.Values;


    /// <summary>
    /// 장비 구성 또는 강화 상태가 변경됐을 때 발생
    /// </summary>
    public event Action EquipmentStatsChanged;

    /// <summary>
    /// 해당 캐릭터의 장착 슬롯 변경 알림
    /// </summary>
    public event Action<EquipSlot, InventoryItemData> EquippedItemChanged;

    /// <summary>
    /// 아이템을 해당 장비 슬롯에 장착하고 이전 장비를 반환
    /// 장비 아이템이 아니면 상태를 변경하지 않음
    /// </summary>
    public InventoryItemData ChangeItem(InventoryItemData inventoryItem)
    {

        if (inventoryItem == null ||
            inventoryItem.IsEmpty ||
            inventoryItem.IsDestroyed ||
            inventoryItem.ItemData.GetItemType() != GameItemType.Equipment)
        {
            return null;
        }

        EquipSlot slot = inventoryItem.ItemData.GetEquipSlot();

        if (slot == EquipSlot.None)
        {
            return null;
        }

        _equipItems.TryGetValue(slot, out InventoryItemData previousItem);

        _equipItems[slot] = inventoryItem;

        EquippedItemChanged?.Invoke(slot, inventoryItem);

        EquipmentStatsChanged?.Invoke();

        return previousItem;
    }

    /// <summary>
    /// 해당 슬롯의 장비를 해제하고 해제된 아이템을 반환
    /// </summary>
    public InventoryItemData UnequipItem(EquipSlot slot)
    {
        if (!_equipItems.Remove(slot, out InventoryItemData unequippedItem))
        {
            return null;
        }

        EquippedItemChanged?.Invoke(slot, null);
        EquipmentStatsChanged?.Invoke();

        return unequippedItem;
    }

    /// <summary>
    /// 장착하고 있는 장비 반환
    /// </summary>
    /// <param name="slot"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool TryGetEquippedItem(EquipSlot slot, out InventoryItemData item)
    {
        return _equipItems.TryGetValue(slot, out item);
    }

    /// <summary>
    /// 저장 장비 복원을 시작하기 전에 현재 장비를 제거
    /// </summary>
    public void BeginRestore()
    {
        _equipItems.Clear();
    }

    /// <summary>
    /// 검증된 저장 장비를 알림 없이 슬롯에 복원
    /// </summary>
    public void RestoreItem(EquipSlot slot, InventoryItemData item)
    {
        _equipItems[slot] = item;
    }

    /// <summary>
    /// 장비 복원을 완료하고 장비 및 스탯 변경 발행
    /// </summary>
    public void CompleteRestore()
    {

        foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
        {
            if (slot == EquipSlot.None)
            {
                continue;
            }

            _equipItems.TryGetValue(slot, out InventoryItemData item);

            EquippedItemChanged?.Invoke(slot, item);
        }

        EquipmentStatsChanged?.Invoke();
    }

    /// <summary>
    /// 강화 결과가 장착 아이템에 적용된 경우 장비 상태 갱신
    /// </summary>
    public void ApplyEnhancedItem(InventoryItemData item)
    {
        EquipSlot slot = item.ItemData.GetEquipSlot();

        if (!_equipItems.TryGetValue(slot, out InventoryItemData equippedItem) || equippedItem != item)
        {
            return;
        }

        if (item.IsDestroyed)
        {
            _equipItems.Remove(slot);
            EquippedItemChanged?.Invoke(slot, null);
        }
        else
        {
            EquippedItemChanged?.Invoke(slot, item);
        }

        EquipmentStatsChanged?.Invoke();
    }

}
