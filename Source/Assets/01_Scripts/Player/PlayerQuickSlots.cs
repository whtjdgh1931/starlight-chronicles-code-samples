using System;

/// <summary>
/// 캐릭터별 퀵슬롯 등록과 독립된 저장 데이터 생성
/// UI 표시와 실제 스킬·아이템 사용 제외
/// </summary>
public sealed class PlayerQuickSlots
{
    public const int SlotCount = 6;

    private readonly QuickSlotSaveData[] _slots = new QuickSlotSaveData[SlotCount];

    public event Action<int> Changed;

    public QuickSlotSaveData GetSlot(int slotIndex)
    {
        return _slots[slotIndex];
    }

    /// <summary>
    /// 중복 등록 제거와 목적지 등록 갱신
    /// 전체 변경 완료 후 슬롯별 알림
    /// </summary>
    public void SetSlot(int slotIndex, QuickSlotKind kind, int contentId)
    {
        ValidateSlotIndex(slotIndex);
        bool[] changed = new bool[SlotCount];
        if (kind != QuickSlotKind.Empty)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (i != slotIndex && _slots[i].Kind == kind && _slots[i].ContentId == contentId)
                {
                    _slots[i] = default;
                    changed[i] = true;
                }
            }
        }
        _slots[slotIndex] = kind == QuickSlotKind.Empty
            ? default : new QuickSlotSaveData { Kind = kind, ContentId = contentId };
        changed[slotIndex] = true;
        for (int i = 0; i < SlotCount; i++)
        {
            if (changed[i]) { Changed?.Invoke(i); }
        }
    }

    /// <summary>
    /// 슬롯 등록 교환과 빈 슬롯으로 이동
    /// 실제 아이템 수량과 쿨다운 유지
    /// </summary>
    public void SwapSlots(int sourceIndex, int targetIndex)
    {
        ValidateSlotIndex(sourceIndex);
        ValidateSlotIndex(targetIndex);
        if (sourceIndex == targetIndex) { return; }
        (_slots[sourceIndex], _slots[targetIndex]) = (_slots[targetIndex], _slots[sourceIndex]);
        Changed?.Invoke(sourceIndex);
        Changed?.Invoke(targetIndex);
    }

    private static void ValidateSlotIndex(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }
    }

    /// <summary>
    /// 이후 조작과 분리된 저장용 배열 복사본 생성
    /// </summary>
    public QuickSlotSaveData[] Capture()
    {
        return (QuickSlotSaveData[])_slots.Clone();
    }

    /// <summary>
    /// 저장 경계에서 정규화한 6칸 정보 복원
    /// </summary>
    public void Restore(QuickSlotSaveData[] slots)
    {
        Array.Copy(slots, _slots, SlotCount);
        PublishAllChanged();
    }

    /// <summary>
    /// 풀 재사용 전 이전 캐릭터 등록 정보 정리
    /// </summary>
    public void Clear()
    {
        Array.Clear(_slots, 0, _slots.Length);
        PublishAllChanged();
    }

    private void PublishAllChanged()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            Changed?.Invoke(i);
        }
    }
}
