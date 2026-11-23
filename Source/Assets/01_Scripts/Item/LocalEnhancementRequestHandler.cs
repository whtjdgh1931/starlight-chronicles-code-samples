using System;

/// <summary>
/// 로컬 강화 대상 검증과 실행 및 결과 전달
/// </summary>
public sealed class LocalEnhancementRequestHandler : IEnhancementRequestHandler
{
    private readonly EnhancementService _enhancementService;
    private readonly PlayerInventory _inventory;
    private readonly PlayerEquipment _equipment;
    private readonly Func<int, EnhanceData> _getEnhanceData;
    private readonly Func<float> _getRandomValue;

    public event Action<Guid, EnhancementResult, int> Completed;

    /// <summary>
    /// 플레이어 데이터와 강화 실행 의존성 연결
    /// </summary>
    public LocalEnhancementRequestHandler(
        EnhancementService enhancementService,
        PlayerInventory inventory,
        PlayerEquipment equipment,
        Func<int, EnhanceData> getEnhanceData,
        Func<float> getRandomValue)
    {
        _enhancementService = enhancementService ?? throw new ArgumentNullException(nameof(enhancementService));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        _getEnhanceData = getEnhanceData ?? throw new ArgumentNullException(nameof(getEnhanceData));
        _getRandomValue = getRandomValue ?? throw new ArgumentNullException(nameof(getRandomValue));
    }

    /// <summary>
    /// 소유한 장비의 강화 실행과 동일 요청 식별자로 결과 전달
    /// </summary>
    public void RequestEnhancement(Guid requestId, InventoryItemData item)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("강화 요청 식별자 필요", nameof(requestId));
        }

        if (item == null || item.IsEmpty || item.IsDestroyed ||
            item.ItemData.GetItemType() != GameItemType.Equipment || !OwnsItem(item))
        {
            Completed?.Invoke(requestId, EnhancementResult.InvalidItem, 0);
            return;
        }

        EnhanceData enhanceData = _getEnhanceData(item.EnhanceLevel);
        EnhancementResult result = _enhancementService.TryEnhance(item, enhanceData, _getRandomValue());
        int enhanceLevel = item.EnhanceLevel;

        Completed?.Invoke(requestId, result, enhanceLevel);
    }

    /// <summary>
    /// 현재 인벤토리 또는 장착 장비의 동일 인스턴스 확인
    /// </summary>
    private bool OwnsItem(InventoryItemData item)
    {
        InventoryItemData[] items = _inventory.GetListByTab(InventoryTab.Equipment);
        int slotCount = _inventory.GetCurrentSlotCount(InventoryTab.Equipment);

        for (int i = 0; i < slotCount; i++)
        {
            if (ReferenceEquals(items[i], item))
            {
                return true;
            }
        }

        foreach (InventoryItemData equippedItem in _equipment.EquippedItems)
        {
            if (ReferenceEquals(equippedItem, item))
            {
                return true;
            }
        }

        return false;
    }
}
