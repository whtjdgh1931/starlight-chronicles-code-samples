using System;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 요청의 처리 유형과 실행 당시 조건
/// 파일 저장과 검증 도구 표시 책임 제외
/// </summary>
public sealed class QuestRequestResult
{
    public enum Code
    {
        Success,
        AlreadyAccepted,
        AlreadyClaimed,
        NotReady,
        Online,
        Joining,
        Dead,
        Busy,
        QuestMissing,
        NotAccepted,
        Prerequisite,
        KillTarget,
        ItemTarget,
        RewardCapacity,
        InvalidData
    }

    public Code Reason { get; }
    public string Message { get; }
    public IReadOnlyList<ItemRequirementProgress> ItemProgress { get; }
    public long Current { get; }
    public long Required { get; }
    public int RelatedId { get; }
    public bool Succeeded => Reason == Code.Success || Reason == Code.AlreadyAccepted || Reason == Code.AlreadyClaimed;

    /// <summary>
    /// 처리 유형·설명·관련 대상과 현재/필요 수치의 불변 결과 생성
    /// </summary>
    public QuestRequestResult(Code reason, string message, long current = 0, long required = 0, int relatedId = 0, IReadOnlyList<ItemRequirementProgress> itemProgress = null)
    {
        Reason = reason;
        Message = message;
        ItemProgress = itemProgress == null ? Array.Empty<ItemRequirementProgress>()
            : new List<ItemRequirementProgress>(itemProgress).AsReadOnly();
        Current = current;
        Required = required;
        RelatedId = relatedId;
    }
}
