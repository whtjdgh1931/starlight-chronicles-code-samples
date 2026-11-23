/// <summary>
/// 아이템 유실 방지, 아이템 이동 조정
/// </summary>
public class EquipmentTransferService
{
    private readonly PlayerInventory _inventory;
    private readonly PlayerEquipment _playerEquipment;

    public EquipmentTransferService(PlayerInventory inventory, PlayerEquipment playerEquipment)
    {
        _inventory = inventory;
        _playerEquipment = playerEquipment;
    }

    /// <summary>
    /// 플레이어 레벨과 장착 조건을 검사하고 기존 장비와 교체
    /// </summary>
    public bool TryEquip(InventoryItemData item, int playerLevel)
    {
        if (!IsValidEquipment(item))
        {
            return false;
        }

        // 인벤토리를 변경하기 전에 요구 레벨 검사
        if (playerLevel < item.ItemData.ReqLevel)
        {
            return false;
        }

        EquipSlot targetSlot = item.ItemData.GetEquipSlot();

        _playerEquipment.TryGetEquippedItem(targetSlot, out InventoryItemData previousItem);

        if (!_inventory.TryReplaceItemInstance(item, previousItem))
        {
            return false;
        }

        _playerEquipment.ChangeItem(item);
        return true;
    }

    /// <summary>
    /// 장착 아이템 해제
    /// 첫 번째 빈 슬롯으로, 빈 슬롯이 없으면 장착 상태 유지
    /// </summary>
    /// <param name="slot"></param>
    /// <returns></returns>
    public bool TryUnequip(EquipSlot slot)
    {
        if (!_playerEquipment.TryGetEquippedItem(slot, out InventoryItemData equippedItem))
        {
            return false;
        }

        if (!_inventory.CanAddItem(equippedItem.ItemData))
        {
            return false;
        }

        InventoryItemData unequippedItem = _playerEquipment.UnequipItem(slot);

        InventoryResult result = _inventory.AddItem(unequippedItem);

        if (result == InventoryResult.Success)
        {
            return true;
        }

        _playerEquipment.ChangeItem(unequippedItem);
        return false;
    }

    /// <summary>
    /// 장비창에서 인벤토리 창으로 이동
    /// </summary>
    /// <param name="sourceSlot"></param>
    /// <param name="targetTab"></param>
    /// <param name="targetIndex"></param>
    /// <returns></returns>
    public bool TryUnequipToInventorySlot(EquipSlot sourceSlot, InventoryTab targetTab, int targetIndex)
    {
        if (!_playerEquipment.TryGetEquippedItem(sourceSlot, out InventoryItemData equippedItem))
        {
            return false;
        }

        if (!_inventory.CanPlaceItemAt(targetTab, targetIndex, equippedItem))
        {
            return false;
        }

        InventoryItemData unequippedItem = _playerEquipment.UnequipItem(sourceSlot);

        if (_inventory.TryPlaceItemAt(targetTab, targetIndex, unequippedItem))
        {
            return true;
        }

        //오류 방지
        _playerEquipment.ChangeItem(unequippedItem);
        return false;
    }

    /// <summary>
    /// 아이템이 장착 가능한 상태인지(슬롯, 파괴, null 검사 등)
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    private static bool IsValidEquipment(InventoryItemData item)
    {
        return item != null &&
                !item.IsEmpty &&
                !item.IsDestroyed &&
                item.ItemData.GetItemType() ==
                        GameItemType.Equipment &&
                item.ItemData.GetEquipSlot() !=
                        EquipSlot.None;
    }
}
