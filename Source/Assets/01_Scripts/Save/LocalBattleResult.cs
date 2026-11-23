using System;
using System.Collections.Generic;

/// <summary>
/// 네트워크 종료와 독립적인 개인 결과의 메모리 보관·로컬 저장
/// 프로그램 종료 전까지 동일한 JSON으로 재시도
/// </summary>
public sealed class LocalBattleResult
{
    internal enum RewardStatus { Pending, Claimed, Forfeited }

    internal sealed class Reward
    {
        internal ItemSaveData Item { get; }
        internal int Count { get; }
        internal RewardStatus Status { get; set; }

        internal Reward(ItemSaveData item)
        {
            Item = item;
            Count = item.Count;
        }
    }

    private ISaveWriter _writer;
    private string _json;
    private string _savedId = string.Empty;
    private readonly List<Reward> _rewards = new();

    internal IReadOnlyList<Reward> Rewards => _rewards;
    internal int UnclaimedRewardCount { get; private set; }

    public event Action Changed;
    public NetworkBattleResultSession.Result Current { get; private set; }
    public int SlotId { get; private set; } = -1;
    public string Error { get; private set; } = string.Empty;
    public bool IsSaved => Current != null && _savedId == Current.Id;
    public bool HasPending => Current != null && !IsSaved;

    /// <summary>
    /// 검증된 개인 결과와 저장 대상 고정
    /// 재수신으로 저장 상태나 퀵슬롯 스냅샷 교체 방지
    /// </summary>
    public void Accept(NetworkBattleResultSession.Result result, int slotId, ISaveWriter writer)
    {
        if (Current != null && Current.Id == result.Id) { return; }
        if (HasPending) { throw new InvalidOperationException("이전 보스전 결과 저장 또는 포기 필요"); }
        List<ItemSaveData> stored = result.State.Inventory.PendingBossRewards;
        if (result.ItemCount < 0 || result.ItemCount > stored.Count)
        {
            throw new InvalidOperationException("이번 전투의 개인 보상 개수가 올바르지 않습니다.");
        }

        string json = new SaveDataSerializer().Serialize(result.State, false);
        _rewards.Clear();
        // 이번 전투에서 보관 목록 끝에 추가된 보상만 결과창 대상으로 등록
        for (int i = stored.Count - result.ItemCount; i < stored.Count; i++)
        {
            _rewards.Add(new Reward(stored[i]));
        }

        UnclaimedRewardCount = result.ItemCount;
        Current = result;
        SlotId = slotId;
        _writer = writer;
        _json = json;
        _savedId = string.Empty;
        Error = string.Empty;
        Changed?.Invoke();
    }

    /// <summary>
    /// 고정된 최종 상태의 저장과 실패 사유 전달
    /// 서버 통신이나 보상 재계산 제외
    /// </summary>
    public bool TrySave()
    {
        if (Current == null) { return false; }
        if (IsSaved) { return true; }
        if (!_writer.TryWrite(_json, out string error))
        {
            Error = error;
            Changed?.Invoke();
            return false;
        }
        _savedId = Current.Id;
        Error = string.Empty;
        Changed?.Invoke();
        return true;
    }

    internal InventoryResult ClaimReward(Reward reward, InventoryTab tab)
    {
        if (!_rewards.Contains(reward) || reward.Status != RewardStatus.Pending)
        {
            return InventoryResult.InvalidItem;
        }

        InventoryTabSaveData target = Current.State.Inventory.Tabs.Find(entry => entry.Tab == tab);
        if (target == null)
        {
            return InventoryResult.InventoryFull;
        }

        ItemSaveData item = reward.Item;
        InventorySlotSaveData stack = tab == InventoryTab.Equipment ? null :
            target.Slots.Find(slot => slot.Item.ItemId == item.ItemId);
        if (stack != null)
        {
            if (item.Count > int.MaxValue - stack.Item.Count)
            {
                return InventoryResult.InventoryFull;
            }

            stack.Item.Count += item.Count;
        }
        else
        {
            int slotIndex = 0;
            while (slotIndex < target.UnlockedSlotCnt && target.Slots.Exists(slot => slot.SlotIndex == slotIndex))
            {
                slotIndex++;
            }

            if (slotIndex >= target.UnlockedSlotCnt)
            {
                return InventoryResult.InventoryFull;
            }

            target.Slots.Add(new InventorySlotSaveData { SlotIndex = slotIndex, Item = item });
        }

        ResolveReward(reward, RewardStatus.Claimed);
        SaveRewardChanges();
        return InventoryResult.Success;
    }

    internal void ForfeitRemainingRewards()
    {
        if (UnclaimedRewardCount == 0) { return; }
        foreach (Reward reward in _rewards)
        {
            if (reward.Status == RewardStatus.Pending)
            {
                ResolveReward(reward, RewardStatus.Forfeited);
            }
        }

        SaveRewardChanges();
    }

    private void ResolveReward(Reward reward, RewardStatus status)
    {
        Current.State.Inventory.PendingBossRewards.Remove(reward.Item);
        reward.Status = status;
        UnclaimedRewardCount--;
    }

    private void SaveRewardChanges()
    {
        _json = new SaveDataSerializer().Serialize(Current.State, false);
        _savedId = string.Empty;
        TrySave();
    }

    /// <summary>
    /// 사용자 확인 후 미저장 결과 포기 또는 다음 세션 준비
    /// 기존 캐릭터 저장 파일 변경 제외
    /// </summary>
    public void Discard()
    {
        Current = null;
        _rewards.Clear();
        UnclaimedRewardCount = 0;
        SlotId = -1;
        _writer = null;
        _json = null;
        _savedId = string.Empty;
        Error = string.Empty;
        Changed?.Invoke();
    }
}
