using System;

/// <summary>
/// 단일 상점 거래의 수량·공간·골드 검사와 구매·판매
/// 플레이어 상태·상품 목록·화면 표시·파일 저장 제외
/// </summary>
public sealed class ShopTradeService
{
    private readonly PlayerInventory _inventory;
    private readonly GoldWallet _wallet;
    private readonly decimal _sellRate;
    private bool _isTrading;

    public ShopTradeService(PlayerInventory inventory, GoldWallet wallet, float sellRate)
    {
        if (float.IsNaN(sellRate) || float.IsInfinity(sellRate) || sellRate < 0f || sellRate > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(sellRate), "판매 비율은 0~1 범위 필요");
        }
        _inventory = inventory;
        _wallet = wallet;
        _sellRate = (decimal)sellRate;
    }

    /// <summary>
    /// 구매 단가에 판매 비율을 적용한 정수 단가, 소수점 버림
    /// </summary>
    public int GetSellPrice(ItemData item) => item.Price <= 0 ? 0 : (int)decimal.Floor(item.Price * _sellRate);

    /// <summary>
    /// 실제 상품 데이터로 구매, 지급 실패 반환 시 차감 비용 환불
    /// 장비는 독립 인스턴스 한 개씩 구매
    /// </summary>
    public bool TryBuy(ItemData item, int count, out string message)
    {
        message = string.Empty;
        if (_isTrading) { message = "이전 거래 처리 중입니다."; return false; }
        if (item == null || item.Price <= 0 || count <= 0 ||
            item.GetItemType() == GameItemType.Equipment && count != 1)
        {
            message = "구매 수량을 확인해 주세요. 장비는 한 개씩 구매할 수 있어요.";
            return false;
        }
        long total = (long)item.Price * count;
        if (total > int.MaxValue)
        {
            message = "골드가 부족합니다.";
            return false;
        }
        _isTrading = true;
        try
        {
            if (!_wallet.TrySpend((int)total)) { message = "골드가 부족합니다."; return false; }
            ItemExchangeResult result = _inventory.ExchangeItems(null, 0, item, count);
            if (!result.Succeeded)
            {
                _wallet.Add((int)total);
                message = "인벤토리 공간 또는 중첩 가능한 수량이 부족합니다.";
                return false;
            }
            message = $"{item.Name} {count:N0}개 구매 / {total:N0}골드 사용";
            return true;
        }
        finally { _isTrading = false; }
    }

    /// <summary>
    /// 선택 슬롯의 실제 인스턴스·현재 수량 확인 후 판매
    /// 잠긴 슬롯·교체된 아이템·0골드·골드 상한 초과 거래 제외
    /// </summary>
    public bool TrySell(InventoryTab tab, int slotIndex, InventoryItemData expected, int count, out string message)
    {
        message = string.Empty;
        if (_isTrading) { message = "이전 거래 처리 중입니다."; return false; }
        if (expected == null || expected.IsEmpty || expected.IsDestroyed || count <= 0 || count > expected.Count ||
            expected.ItemData.GetItemType() == GameItemType.Equipment && count != 1)
        {
            message = "판매할 아이템과 수량을 다시 확인해 주세요.";
            return false;
        }
        int unitPrice = GetSellPrice(expected.ItemData);
        if (unitPrice == 0) { message = "판매 가격이 0골드인 아이템은 판매할 수 없습니다."; return false; }
        long total = (long)unitPrice * count;
        if (total > int.MaxValue || !_wallet.CanAdd((int)total))
        {
            message = "보유할 수 있는 골드 한도를 초과합니다.";
            return false;
        }
        string itemName = expected.ItemData.Name;
        _isTrading = true;
        try
        {
            if (!_inventory.TryRemoveItem(tab, slotIndex, expected, count))
            {
                message = "인벤토리가 변경되었습니다. 아이템을 다시 선택해 주세요.";
                return false;
            }
            _wallet.Add((int)total);
            message = $"{itemName} {count:N0}개 판매 / {total:N0}골드 획득";
            return true;
        }
        finally { _isTrading = false; }
    }
}
