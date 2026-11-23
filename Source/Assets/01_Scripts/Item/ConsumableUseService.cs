using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 소비 아이템 차감·자원 회복과 종류별 재사용 대기 관리
/// 인벤토리와 퀵슬롯의 동일 사용 규칙 공유
/// </summary>
public sealed class ConsumableUseService
{
    private readonly PlayerInventory _inventory;
    private readonly PlayerStat _stat;
    private readonly Dictionary<int, double> _nextUseTimes = new();

    /// <summary>
    /// 같은 아이템을 등록한 모든 슬롯과 인벤토리의 재사용 대기 공유
    /// </summary>
    public float GetCooldownRemaining(int itemId)
    {
        return _nextUseTimes.TryGetValue(itemId, out double nextUseAt) ? (float)System.Math.Max(0d, nextUseAt - Time.timeAsDouble) : 0f;
    }

    /// <summary>
    /// 서버가 승인한 남은 쿨타임의 소유자 표시 반영
    /// 아이템 차감·자원 회복 제외
    /// </summary>
    public void ApplyCooldown(int itemId, float remaining)
    {
        _nextUseTimes[itemId] = Time.timeAsDouble + remaining;
    }

    public ConsumableUseService(PlayerInventory inventory, PlayerStat stat)
    {
        _inventory = inventory;
        _stat = stat;
    }

    /// <summary>
    /// 선택한 소비 아이템 한 개 사용
    /// 회복 가능한 자원이 없거나 슬롯이 변경되면 차감 제외
    /// </summary>
    public bool TryUse(int slotIndex, InventoryItemData expected, out string message)
    {
        ItemData item = expected.ItemData;
        if (GetCooldownRemaining(item.ID) > 0f)
        {
            message = "아이템 재사용 대기 중";
            return false;
        }

        float maxHp = _stat.GetTotalStat(StatType.MaxHp);
        float maxMp = _stat.GetTotalStat(StatType.MaxMp);
        float restoreHp = Mathf.Clamp(item.RestoreHp + maxHp * item.RestoreHpRatio, 0f, Mathf.Max(0f, maxHp - _stat.CurrentHp));
        float restoreMp = Mathf.Clamp(item.RestoreMp + maxMp * item.RestoreMpRatio, 0f, Mathf.Max(0f, maxMp - _stat.CurrentMp));
        if (restoreHp <= 0f && restoreMp <= 0f)
        {
            message = "회복할 체력이나 마나 없음";
            return false;
        }

        // 인벤토리 변경 알림 중 재진입에도 동일 쿨타임 적용
        _nextUseTimes.TryGetValue(item.ID, out double previousUseAt);
        _nextUseTimes[item.ID] = Time.timeAsDouble + item.Cooldown;
        if (!_inventory.TryRemoveItem(InventoryTab.Consumable, slotIndex, expected, 1))
        {
            _nextUseTimes[item.ID] = previousUseAt;
            message = "사용할 아이템 재선택 필요";
            return false;
        }

        if (restoreHp > 0f) { _stat.ChangeHp(restoreHp); }
        if (restoreMp > 0f) { _stat.ChangeMp(restoreMp); }
        message = $"{item.Name} 사용";
        return true;
    }
}
