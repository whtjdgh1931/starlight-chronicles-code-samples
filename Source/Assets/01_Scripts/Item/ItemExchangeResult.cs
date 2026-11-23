using System;
using System.Collections.Generic;

/// <summary>
/// 실제 아이템 교환 판정의 사유와 수량
/// 퀘스트와 UI 표시 책임 제외
/// </summary>
public sealed class ItemExchangeResult
{
    public enum Code
    {
        Success,
        InvalidRewardQuantity,
        InvalidRequiredItem,
        InsufficientItems,
        NoCapacity
    }

    public Code Reason { get; }
    public string Message { get; }
    public IReadOnlyList<ItemRequirementProgress> ItemProgress { get; }
    public long Current { get; }
    public long Required { get; }
    public int ItemId { get; }
    public bool Succeeded => Reason == Code.Success;

    /// <summary>
    /// 교환 처리 결과와 관련 아이템 수치 생성
    /// </summary>
    public ItemExchangeResult(Code reason, string message, long current = 0, long required = 0, int itemId = 0, IReadOnlyList<ItemRequirementProgress> itemProgress = null)
    {
        Reason = reason;
        Message = message;
        ItemProgress = itemProgress == null ? Array.Empty<ItemRequirementProgress>()
            : new List<ItemRequirementProgress>(itemProgress).AsReadOnly();
        Current = current;
        Required = required;
        ItemId = itemId;
    }
}
