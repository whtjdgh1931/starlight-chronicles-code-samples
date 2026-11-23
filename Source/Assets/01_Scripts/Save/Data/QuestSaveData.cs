using System;

/// <summary>
/// 캐릭터별 수락 퀘스트 진행도와 보상 수령 기록
/// 수집 목표는 수령 시 실제 인벤토리 수량 확인 필요
/// </summary>
[Serializable]
public sealed class QuestSaveData
{
    public int QuestId;
    public int ProgressCount;
    public bool RewardClaimed;
}
