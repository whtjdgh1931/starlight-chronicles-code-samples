using UnityEngine;

public class PlayerPickupReceiver : MonoBehaviour, IPickupReceiver
{
    [SerializeField] private Transform _pickupAnchor;

    private Player _owner;
    private PlayerInventory _inventory;
    private GoldWallet _goldWallet;

    public Transform PickupAnchor => _pickupAnchor != null ? _pickupAnchor : transform;

    /// <summary>
    /// 획득 소유자와 아이템·골드 저장 대상 연결
    /// </summary>
    public void Initialize(Player owner, PlayerInventory inventory, GoldWallet goldWallet)
    {
        _owner = owner != null ? owner : throw new System.ArgumentNullException(nameof(owner));
        _inventory = inventory;
        _goldWallet = goldWallet;
    }

    /// <summary>획득 시점의 소유자 상태와 인벤토리 공간 확인</summary>
    public bool CanReceive(DroppedItem droppedItem)
    {
        if (droppedItem == null || _owner == null || !_owner.isActiveAndEnabled ||
            _owner.IsDead || _owner.IsNetworkJoinLocked)
        {
            return false;
        }

        return droppedItem.IsGold ? _goldWallet.CanAdd(droppedItem.GoldAmount) : _inventory.CanAddItem(droppedItem.ItemData);
    }

    /// <summary>획득 가능 여부를 재확인한 뒤 아이템 또는 골드 지급</summary>
    public bool TryReceive(DroppedItem droppedItem)
    {
        if (!CanReceive(droppedItem))
        {
            return false;
        }

        if (droppedItem.IsGold)
        {
            return _goldWallet.TryAdd(droppedItem.GoldAmount);
        }

        InventoryResult result = _inventory.AddItem(droppedItem.ItemData);

        return result == InventoryResult.Success;
    }
}

public interface IPickupReceiver
{
    bool CanReceive(DroppedItem droppedItem);
    bool TryReceive(DroppedItem droppedItem);

}

