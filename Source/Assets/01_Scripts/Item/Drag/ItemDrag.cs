/// <summary>
/// 아이템 드래그를 가능하게하는 인터페이스
/// </summary>
public interface IItemDragSource
{
    /// <summary>
    /// 드래그를 위한 ItemDrag 클래스 반환(아이템의 소유 위치 및 데이터)
    /// </summary>
    /// <param name="itemDrag"></param>
    /// <returns></returns>
    public bool TryGetDrag(out ItemDrag itemDrag);
}



public class ItemDrag
{
    public const int NoInventoryIndex = -1;

    public enum SourceType
    {
        Inventory,
        Equipment
    }

    public InventoryItemData Item { get; }
    public SourceType Source { get; }
    public InventoryTab InventoryTab { get; }
    public int InventoryIndex { get; }
    public EquipSlot EquipSlot { get; }

    private ItemDrag(InventoryItemData item, SourceType sourceType, InventoryTab inventoryTab, int inventoryIndex, EquipSlot equipSlot)
    {
        Item = item;
        Source = sourceType;
        InventoryTab = inventoryTab;
        InventoryIndex = inventoryIndex;
        EquipSlot = equipSlot;
    }

    /// <summary>
    /// 인벤토리에서 출발
    /// </summary>
    /// <param name="item"></param>
    /// <param name="inventoryTab"></param>
    /// <param name="inventoryIndex"></param>
    /// <returns></returns>
    public static ItemDrag FromInventory(InventoryItemData item, InventoryTab inventoryTab, int inventoryIndex)
    {
        return new ItemDrag(item, SourceType.Inventory, inventoryTab, inventoryIndex, EquipSlot.None);
    }

    public static ItemDrag FromEquipment(InventoryItemData item, EquipSlot equipSlot)
    {
        return new ItemDrag(item, SourceType.Equipment, InventoryTab.None, NoInventoryIndex, equipSlot);
    }


}
