using System;

public enum EnhancementResult
{
    Success,
    Failed,
    LevelDown,
    Destroyed,
    NotEnoughGold,
    MaxLevel,
    InvalidItem
}

/// <summary>
/// 강화 비용 차감 및 강화 결과 적용
/// </summary>
public sealed class EnhancementService
{
    private readonly PlayerInventory _inventory;
    private readonly PlayerEquipment _equipment;
    private readonly GoldWallet _goldWallet;

    /// <summary>
    /// 강화에 필요한 플레이어 데이터 연결
    /// </summary>
    public EnhancementService(PlayerInventory inventory, PlayerEquipment equipment, GoldWallet goldWallet)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        _goldWallet = goldWallet ?? throw new ArgumentNullException(nameof(goldWallet));
    }

    /// <summary>
    /// 요청 핸들러에서 소유권과 유효성을 확인한 장비의 강화 결과 반환
    /// </summary>
    public EnhancementResult TryEnhance(InventoryItemData item, EnhanceData enhanceData, float randomValue)
    {
        if (enhanceData == null)
        {
            return EnhancementResult.MaxLevel;
        }

        if (!_goldWallet.TrySpend(enhanceData.Cost))
        {
            return EnhancementResult.NotEnoughGold;
        }

        EnhancementResult result = ApplyResult(item, enhanceData, randomValue);

        _equipment.ApplyEnhancedItem(item);

        if (result == EnhancementResult.Destroyed)
        {
            _inventory.TryReplaceItemInstance(item, null);
        }

        return result;
    }

    private static EnhancementResult ApplyResult(InventoryItemData item, EnhanceData enhanceData, float randomValue)
    {
        if (randomValue < enhanceData.SuccessRate)
        {
            item.EnhanceLevel++;
            return EnhancementResult.Success;
        }

        if (randomValue < enhanceData.SuccessRate + enhanceData.DropRate)
        {
            item.EnhanceLevel--;
            return EnhancementResult.LevelDown;
        }

        if (randomValue < enhanceData.SuccessRate + enhanceData.DropRate + enhanceData.DestroyRate)
        {
            item.IsDestroyed = true;
            return EnhancementResult.Destroyed;
        }

        return EnhancementResult.Failed;
    }
}