using System;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 공통 정의, 캐릭터별 진행 상태 제외
/// Google Sheets 연동 시 컬럼명과 동일한 public 필드 사용
/// </summary>
[Serializable]
public sealed class QuestData
{
    public int ID;
    public string Name;
    public string Description;
    public QuestObjectiveType ObjectiveType;
    public int TargetID;
    public int TargetCount;

    /// <summary>
    /// 수집 목표 목록, 비어 있으면 기존 TargetID·TargetCount 사용
    /// </summary>
    public List<ItemRequirement> RequiredItems = new();
    public int RequiredQuestID;
    public int RewardGold;
    public int RewardExp;
    public int RewardItemID;
    public int RewardItemCount;
    /// <summary>
    /// 기존 단일 수집 목표와 새 목록의 공통 조회
    /// 처치 목표에는 빈 목록 반환
    /// </summary>
    public IReadOnlyList<ItemRequirement> GetRequiredItems()
    {
        if (ObjectiveType != QuestObjectiveType.CollectItem) { return Array.Empty<ItemRequirement>(); }
        if (RequiredItems != null && RequiredItems.Count > 0) { return RequiredItems; }
        return new[] { new ItemRequirement { ItemId = TargetID, Count = TargetCount } };
    }
}
