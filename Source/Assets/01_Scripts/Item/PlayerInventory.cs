using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


public enum InventoryTab
{
    None,
    Equipment,
    Consumable,
    Etc
}

public enum InventoryResult
{
    Success,
    InventoryFull,
    InvalidItem
}



public class PlayerInventory : MonoBehaviour
{
    [Header("인벤토리 크기 설정")]
    [SerializeField] private InventorySettings _settings;
    public InventorySettings Settings => _settings;
    private readonly List<InventoryItemData> _pendingBossRewards = new();
    public IReadOnlyList<InventoryItemData> PendingBossRewards => _pendingBossRewards;
    public int MaxSlot => _settings.MaxSlotCnt;
    public int DefaultSlot => _settings.DefaultSlotCnt;

    /// <summary>
    /// 탭별 활성화된 슬롯 수
    /// </summary>
    private Dictionary<InventoryTab, int> _currentSlotCounts;

    /// <summary>
    /// 탭별 아이템 저장용 딕셔너리
    /// </summary>
    private Dictionary<InventoryTab, InventoryItemData[]> _inventoryLists;

    /// <summary>
    /// 인벤토리 탭 데이터 변경 시 발행
    /// </summary>
    public event Action<InventoryTab, InventoryItemData[], int> InventoryChanged;


    /// <summary>
    /// 인벤토리 초기화
    /// </summary>
    public void Initialize()
    {
        _pendingBossRewards.Clear();
        _currentSlotCounts = new Dictionary<InventoryTab, int>();
        _inventoryLists = new Dictionary<InventoryTab, InventoryItemData[]>();

        //인벤토리 탭 생성
        foreach (InventoryTab tab in Enum.GetValues(typeof(InventoryTab)))
        {
            if (tab == InventoryTab.None)
            {
                continue;
            }

            _currentSlotCounts.Add(tab, _settings.DefaultSlotCnt);
            _inventoryLists.Add(tab, new InventoryItemData[_settings.MaxSlotCnt]);
        }

    }

		public void ExpandInventory(InventoryTab tab, int addSlot)
		{
				_currentSlotCounts[tab] = Mathf.Min(_currentSlotCounts[tab] + addSlot, MaxSlot);
		}

		/// <summary>
		/// 인벤토리 탭의 현재 슬롯 수 반환
		/// </summary>
		/// <param name="tab"></param>
		/// <returns></returns>
		public int GetCurrentSlotCount(InventoryTab tab)
		{
				return _currentSlotCounts[tab];
		}

		/// <summary>
		/// 인벤토리 탭의 아이템 리스트 반환
		/// </summary>
		/// <param Name="tab"></param>
		/// <returns></returns>
		public InventoryItemData[] GetListByTab(InventoryTab tab)
		{
				return _inventoryLists[tab];
		}

		/// <summary>
		/// 저장된 인벤토리 적용 준비
		/// </summary>
		public void BeginRestore()
    {
        _pendingBossRewards.Clear();
        foreach (InventoryTab tab in Enum.GetValues(typeof(InventoryTab)))
        {
            if (tab == InventoryTab.None)
            {
                continue;
            }

            InventoryItemData[] items = _inventoryLists[tab];
            Array.Clear(items, 0, items.Length);
        }
    }

    public void RestoreTab(InventoryTab tab, int unlockedSlotCnt)
    {
        _currentSlotCounts[tab] = unlockedSlotCnt;
    }

    public void RestoreItem(InventoryTab tab, int slotIndex, InventoryItemData item)
    {
        _inventoryLists[tab][slotIndex] = item;
    }

    public void CompleteRestore()
    {
        foreach (InventoryTab tab in Enum.GetValues(typeof(InventoryTab)))
        {
            if (tab == InventoryTab.None)
            {
                continue;
            }

            NotifyInventoryChanged(tab);
        }
    }

    /// <summary>
    /// 외부 요청의 소비 슬롯 범위·아이템 ID·수량 확인
    /// 실제 차감은 기존 TryRemoveItem에서 처리
    /// </summary>
    public bool TryGetConsumable(int slotIndex, int itemId, out InventoryItemData item)
    {
        item = null;
        if (slotIndex < 0 || slotIndex >= GetCurrentSlotCount(InventoryTab.Consumable))
        {
            return false;
        }
        item = _inventoryLists[InventoryTab.Consumable][slotIndex];
        return item != null && !item.IsEmpty && !item.IsDestroyed &&
            item.ItemData.ID == itemId && item.ItemData.GetItemType() == GameItemType.Consumable;
    }

    /// <summary>
    /// 서버가 확정한 소비 슬롯의 최종 수량과 변경 알림 반영
    /// 온라인 슬롯 이동이 제한된 소유자 인벤토리 전용
    /// </summary>
    public void ApplyConsumableCount(int slotIndex, int count)
    {
        InventoryItemData[] items = _inventoryLists[InventoryTab.Consumable];
        if (count == 0)
        {
            items[slotIndex] = null;
        }
        else
        {
            items[slotIndex].Count = count;
        }
        NotifyInventoryChanged(InventoryTab.Consumable);
    }

    /// <summary>
    /// 선택한 열린 슬롯의 실제 인스턴스와 수량 확인 후 제거
    /// 장비는 한 개만 제거, 실패 시 목록·수량·변경 알림 유지
    /// </summary>
    public bool TryRemoveItem(InventoryTab tab, int slotIndex, InventoryItemData expected, int count)
    {
        if (!_inventoryLists.TryGetValue(tab, out InventoryItemData[] items) ||
            slotIndex < 0 || slotIndex >= GetCurrentSlotCount(tab) ||
            expected == null || expected.IsEmpty || expected.IsDestroyed || count <= 0 || count > expected.Count ||
            !ReferenceEquals(items[slotIndex], expected) ||
            expected.ItemData.GetItemType() == GameItemType.Equipment && count != 1)
        {
            return false;
        }
        expected.Count -= count;
        if (expected.Count == 0) { items[slotIndex] = null; }
        NotifyInventoryChanged(tab);
        return true;
    }

    public InventoryResult AddItem(ItemData item, int count = 1)
    {
        if (item == null)
        {
            Debug.LogWarning("아이템 정보 null");
            return InventoryResult.InvalidItem;
        }

        return AddItem(new InventoryItemData(item, count));
    }

    public bool CanAddItem(ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        InventoryTab tab = GetTabForItem(item);
        InventoryItemData[] targetArray = GetListByTab(tab);
        int currentCount = GetCurrentSlotCount(tab);

        if (currentCount <= 0)
        {
            return false;
        }

        for (int i = 0; i < currentCount; i++)
        {
            InventoryItemData slotItem = targetArray[i];

            if (slotItem == null || slotItem.IsEmpty)
            {
                return true;
            }

            if (item.GetItemType() != GameItemType.Equipment &&
                slotItem.ItemData.ID == item.ID)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 서버 개인 보상 또는 저장된 미수령 아이템 보관
    /// </summary>
    public void StoreBossReward(InventoryItemData item)
    {
        _pendingBossRewards.Add(item);
    }

    /// <summary>
    /// 공간이 허용하는 보상만 인벤토리로 이동
    /// 실제 저장은 플레이어의 로컬 저장 경로 담당
    /// </summary>
    public int ClaimBossRewards()
    {
        int claimed = 0;
        for (int i = _pendingBossRewards.Count - 1; i >= 0; i--)
        {
            if (AddItem(_pendingBossRewards[i]) == InventoryResult.Success)
            {
                _pendingBossRewards.RemoveAt(i);
                claimed++;
            }
        }
        return claimed;
    }

    public InventoryResult AddItem(InventoryItemData itemData)
    {
        if (itemData == null || itemData.IsEmpty)
        {
            return InventoryResult.InvalidItem;
        }
        var tab = GetTabForItem(itemData.ItemData);
        var targetArray = GetListByTab(tab);
        int currentCount = GetCurrentSlotCount(tab);

        if (currentCount == 0)
        {
            return InventoryResult.InventoryFull;
        }

        //장비 아이템
        if (itemData.ItemData.GetItemType() == GameItemType.Equipment)
        {
            for (int i = 0; i < currentCount; i++)
            {
                if (targetArray[i] == null || targetArray[i].IsEmpty)
                {
                    targetArray[i] = itemData;

                    NotifyInventoryChanged(tab);
                    return InventoryResult.Success;
                }
            }
        }
        else
        {
            // 소비/기타 아이템은 수량 합산 처리
            for (int i = 0; i < currentCount; i++)
            {
                if (targetArray[i] != null && !targetArray[i].IsEmpty && targetArray[i].ItemData.ID == itemData.ItemData.ID)
                {
                    targetArray[i].Count += itemData.Count;

                    NotifyInventoryChanged(tab);
                    return InventoryResult.Success;
                }
            }
            for (int i = 0; i < currentCount; i++)
            {
                if (targetArray[i] == null || targetArray[i].IsEmpty)
                {
                    targetArray[i] = itemData;

                    NotifyInventoryChanged(tab);
                    return InventoryResult.Success;
                }
            }
        }


        return InventoryResult.InventoryFull;
    }

    /// <summary>
    /// 인벤토리 슬롯 교환
    /// </summary>
    public void SwapItems(InventoryTab tab, int fromIndex, int toIndex)
    {
        InventoryItemData[] targetArray = GetListByTab(tab);
        int currentCount = GetCurrentSlotCount(tab);

        if (fromIndex < 0 || fromIndex >= currentCount || toIndex < 0 || toIndex >= currentCount)
        {
            return;
        }

        InventoryItemData temp = targetArray[fromIndex];
        targetArray[fromIndex] = targetArray[toIndex];
        targetArray[toIndex] = temp;

        NotifyInventoryChanged(tab);
    }

    /// <summary>
    /// 아이템 존재 확인
    /// </summary>
    /// <param Name="item"></param>
    /// <returns></returns>
    public int HasItem(ItemData item) => (int)Math.Min(int.MaxValue, CountItems(item));

    /// <summary>
    /// 여러 슬롯의 보유 수량을 정수 오버플로 없이 합산
    /// </summary>
    public long CountItems(ItemData item)
    {
        if (item == null) { return 0; }
        InventoryTab tab = GetTabForItem(item);
        InventoryItemData[] items = GetListByTab(tab);
        int slotCount = GetCurrentSlotCount(tab);
        long total = 0;
        for (int i = 0; i < slotCount; i++)
        {
            InventoryItemData slot = items[i];
            if (slot != null && !slot.IsEmpty && slot.ItemData.ID == item.ID) { total += slot.Count; }
        }
        return total;
    }

    /// <summary>
    /// 수집품 전달과 보상 아이템 추가
    /// 공간·수량 검사 완료 전 인벤토리 변경 제외
    /// 전달 대상이 없는 처치 퀘스트는 requiredItem에 null 전달
    /// </summary>
    public bool TryExchangeItems(
        ItemData requiredItem, int requiredCount,
        ItemData rewardItem, int rewardCount, out string errorMessage)
    {
        ItemExchangeResult result = ExchangeItems(requiredItem, requiredCount, rewardItem, rewardCount);
        errorMessage = result.Succeeded ? string.Empty : result.Message;
        return result.Succeeded;
    }

    /// <summary>
    /// 수집품 소모 후 공간을 고려한 실제 교환과 구조화된 결과 반환
    /// </summary>
    public ItemExchangeResult ExchangeItems(ItemData requiredItem, int requiredCount, ItemData rewardItem, int rewardCount)
    {
        var requirements = new List<ItemRequirement>();
        var definitions = new Dictionary<int, ItemData>();
        if (requiredItem != null)
        {
            requirements.Add(new ItemRequirement { ItemId = requiredItem.ID, Count = requiredCount });
            definitions.Add(requiredItem.ID, requiredItem);
        }
        return ExchangeItems(requirements, definitions, rewardItem, rewardCount);
    }

    /// <summary>
    /// 모든 수집품·소모 후 보상 공간 검사 이후 일괄 교환
    /// 실패 시 소모·보상·변경 알림 없음, 중복 목표 ID는 정의 오류
    /// </summary>
    public ItemExchangeResult ExchangeItems(IReadOnlyList<ItemRequirement> requirements,
        IReadOnlyDictionary<int, ItemData> definitions, ItemData rewardItem, int rewardCount)
    {
        bool isEquipmentReward = rewardItem.GetItemType() == GameItemType.Equipment;
        if (rewardCount <= 0 || isEquipmentReward && rewardCount != 1)
        {
            return new ItemExchangeResult(ItemExchangeResult.Code.InvalidRewardQuantity,
                $"보상 {rewardItem.ID} · {rewardItem.Name}: 요청 {rewardCount}개. 양수 수량이며 장비는 1개여야 합니다.",
                rewardCount, 1, rewardItem.ID);
        }
        var requiredCounts = new Dictionary<int, int>();
        var progress = new List<ItemRequirementProgress>();
        var shortage = new StringBuilder();
        foreach (ItemRequirement requirement in requirements)
        {
            if (requirement == null || requirement.ItemId <= 0 || requirement.Count <= 0 ||
                !definitions.TryGetValue(requirement.ItemId, out ItemData item) ||
                item.GetItemType() == GameItemType.Equipment || requiredCounts.ContainsKey(requirement.ItemId))
            {
                return new ItemExchangeResult(ItemExchangeResult.Code.InvalidRequiredItem,
                    requirement == null ? "비어 있는 수집 목표가 있습니다."
                        : $"수집 목표 ID {requirement.ItemId}, 필요 {requirement.Count}개: 정의 존재·양수 ID/수량·비장비 여부·중복 ID를 확인하세요.",
                    itemProgress: progress);
            }
            requiredCounts.Add(requirement.ItemId, requirement.Count);
            var row = new ItemRequirementProgress(item.ID, item.Name, CountItems(item), requirement.Count);
            progress.Add(row);
            if (!row.Satisfied)
            {
                shortage.AppendLine($"{row.ItemId} · {row.Name}: 보유 {row.Current} / 필요 {row.Required} · {row.Missing}개 부족");
            }
        }
        if (shortage.Length > 0)
        {
            ItemRequirementProgress first = progress.Find(row => !row.Satisfied);
            return new ItemExchangeResult(ItemExchangeResult.Code.InsufficientItems,
                shortage.ToString(), first.Current, first.Required, first.ItemId, progress);
        }
        InventoryTab rewardTab = GetTabForItem(rewardItem);
        InventoryItemData[] rewardItems = GetListByTab(rewardTab);
        int slotCount = GetCurrentSlotCount(rewardTab);
        var remaining = new Dictionary<int, int>(requiredCounts);
        int emptySlot = -1;
        int rewardSlot = -1;
        int rewardSlotCount = 0;
        for (int i = 0; i < slotCount; i++)
        {
            InventoryItemData slot = rewardItems[i];
            int count = GetRemainingCount(slot, remaining);
            if (count == 0)
            {
                if (emptySlot < 0) { emptySlot = i; }
                continue;
            }
            if (!isEquipmentReward && slot.ItemData.ID == rewardItem.ID && count <= int.MaxValue - rewardCount)
            {
                rewardSlot = i;
                rewardSlotCount = count;
                break;
            }
        }
        if (rewardSlot < 0) { rewardSlot = emptySlot; }
        if (rewardSlot < 0)
        {
            return new ItemExchangeResult(ItemExchangeResult.Code.NoCapacity,
                $"보상 {rewardItem.ID} · {rewardItem.Name} × {rewardCount}개\n대상 탭: {rewardTab} / 열린 슬롯: {slotCount}칸\n" +
                "모든 수집품 소모 이후에도 빈 슬롯 또는 전체 수량을 중첩할 슬롯이 없습니다.",
                0, rewardCount, rewardItem.ID, progress);
        }

        // 모든 실패 조건 확인 이후 알림 없이 전체 수집품 소모
        var changedTabs = new SortedSet<InventoryTab> { rewardTab };
        foreach (ItemRequirement requirement in requirements)
        {
            ItemData item = definitions[requirement.ItemId];
            changedTabs.Add(GetTabForItem(item));
            ConsumeItemsWithoutNotification(item, requirement.Count);
        }
        if (rewardSlotCount > 0) { rewardItems[rewardSlot].Count = rewardSlotCount + rewardCount; }
        else { rewardItems[rewardSlot] = new InventoryItemData(rewardItem, rewardCount); }
        foreach (InventoryTab tab in changedTabs) { NotifyInventoryChanged(tab); }
        return new ItemExchangeResult(ItemExchangeResult.Code.Success,
            $"{rewardItem.Name} × {rewardCount}개 교환 완료", rewardCount, rewardCount, rewardItem.ID, progress);
    }

    public bool TryReplaceItemInstance(InventoryItemData sourceItem, InventoryItemData replacementItem)
    {
        if (sourceItem == null || sourceItem.IsEmpty)
        {
            return false;
        }

        InventoryTab tab = GetTabForItem(sourceItem.ItemData);

        // 교체 아이템은 null이거나 같은 인벤토리 탭
        if (replacementItem != null && GetTabForItem(replacementItem.ItemData) != tab)
        {
            return false;
        }

        InventoryItemData[] targetArray = GetListByTab(tab);
        int currentCount = GetCurrentSlotCount(tab);

        for (int i = 0; i < currentCount; i++)
        {
            if (targetArray[i] != sourceItem)
            {
                continue;
            }

            targetArray[i] = replacementItem;

            NotifyInventoryChanged(tab);

            return true;
        }

        return false;
    }

    /// <summary>
    /// 아이템 배치 가능 여부 확인
    /// </summary>
    /// <param name="tab"></param>
    /// <param name="index"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool CanPlaceItemAt(InventoryTab tab, int index, InventoryItemData item)
    {
        if (item == null || item.IsEmpty || GetTabForItem(item.ItemData) != tab)
        {
            return false;
        }

        InventoryItemData[] targetArray = GetListByTab(tab);

        int currentCount = GetCurrentSlotCount(tab);

        return index >= 0 && index < currentCount && (targetArray[index] == null || targetArray[index].IsEmpty);
    }

    /// <summary>
    /// 아이템 배치
    /// </summary>
    /// <param name="tab"></param>
    /// <param name="index"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool TryPlaceItemAt(InventoryTab tab, int index, InventoryItemData item)
    {
        if (!CanPlaceItemAt(tab, index, item))
        {
            return false;
        }

        InventoryItemData[] targetArray = GetListByTab(tab);
        targetArray[index] = item;

        NotifyInventoryChanged(tab);

        return true;
    }

    /// <summary>
    /// 현재 인벤토리 탭 데이터 발행
    /// </summary>
    public void RequestTabData(InventoryTab tab)
    {
        NotifyInventoryChanged(tab);
    }

    /// <summary>
    /// 아이템 타입을 기반으로 인벤토리 탭 파싱
    /// </summary>
    /// <param Name="item"></param>
    /// <returns></returns>
    internal static InventoryTab GetTabForItem(ItemData item)
    {
        if (Enum.TryParse<InventoryTab>(item.GetItemType().ToString(), out var tab))
        {
            return tab;
        }
        return InventoryTab.Etc;
    }

    /// <summary>
    /// 전달 수량을 반영한 슬롯의 예상 잔여 수량 계산
    /// 실제 슬롯 변경 제외
    /// </summary>
    private static int GetRemainingCount(InventoryItemData slot, IDictionary<int, int> remaining)
    {
        if (slot == null || slot.IsEmpty) { return 0; }
        if (!remaining.TryGetValue(slot.ItemData.ID, out int required) || required == 0) { return slot.Count; }
        int consumed = Math.Min(slot.Count, required);
        remaining[slot.ItemData.ID] = required - consumed;
        return slot.Count - consumed;
    }

    /// <summary>
    /// 사전 확인된 전달 수량 소모
    /// 보상 추가 완료까지 변경 알림 보류
    /// </summary>
    private void ConsumeItemsWithoutNotification(ItemData item, int count)
    {
        InventoryTab tab = GetTabForItem(item);
        InventoryItemData[] items = GetListByTab(tab);
        int slotCount = GetCurrentSlotCount(tab);
        for (int i = 0; i < slotCount && count > 0; i++)
        {
            InventoryItemData slot = items[i];
            if (slot == null || slot.IsEmpty || slot.ItemData.ID != item.ID) { continue; }

            int consumed = Math.Min(slot.Count, count);
            slot.Count -= consumed;
            count -= consumed;
            if (slot.Count == 0) { items[i] = null; }
        }
    }

    private void NotifyInventoryChanged(InventoryTab tab)
    {
        InventoryChanged?.Invoke(tab, GetListByTab(tab), GetCurrentSlotCount(tab));
    }

}

