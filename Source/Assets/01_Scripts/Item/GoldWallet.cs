using System;

/// <summary>
/// 플레이어가 보유한 골드와 증감 규칙을 관리
/// </summary>
public sealed class GoldWallet
{
    private int _currenrtGold;
    public int CurrentGold => _currenrtGold;
    public int RemainingCapacity => int.MaxValue - _currenrtGold;

    /// <summary>
    /// 이 지갑의 잔액이 변경되거나 저장 잔액 표시가 요청될 때 알림
    /// </summary>
    public event Action<int> GoldChanged;

    /// <summary>
    /// 획득, 판매와 환불로 발생한 골드를 잔액에 추가
    /// </summary>
    public void Add(int amount)
    {
        if (!TryAdd(amount))
        {
            throw new InvalidOperationException("보유 골드 상한 초과");
        }
    }

    /// <summary>요청 금액 전액을 받을 수 있는지 확인</summary>
    public bool CanAdd(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "추가할 골드는 0 이상이어야 합니다.");
        }

        return amount <= RemainingCapacity;
    }

    /// <summary>상한 이내의 금액 전액 지급, 거절 시 잔액 유지</summary>
    public bool TryAdd(int amount)
    {
        if (!CanAdd(amount)) { return false; }
        _currenrtGold += amount;
        GoldChanged?.Invoke(_currenrtGold);
        return true;
    }

    /// <summary>
    /// 잔액이 충분할 때만 구매 또는 강화 비용을 차감
    /// </summary>
    public bool TrySpend(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "사용할 골드는 0 이상이어야 합니다.");
        }

        if (_currenrtGold < amount)
        {
            return false;
        }

        _currenrtGold -= amount;
        GoldChanged?.Invoke(_currenrtGold);

        return true;
    }

    /// <summary>
    /// 저장된 잔액을 적용
    /// </summary>
    public void Restore(int currentGold)
    {
        if (currentGold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentGold), currentGold, "복원할 골드는 0 이상이어야 합니다.");
        }

        _currenrtGold = currentGold;
    }

    /// <summary>
    /// 저장 복원이 완료된 뒤 현재 잔액을 UI에 전달
    /// </summary>
    public void PublishCurrentBalance()
    {
        GoldChanged?.Invoke(_currenrtGold);
    }
}
