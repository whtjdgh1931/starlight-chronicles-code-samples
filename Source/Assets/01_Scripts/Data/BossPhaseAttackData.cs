using System;

/// <summary>
/// 보스 페이즈별 공격 선택 데이터 형식
/// </summary>
[Serializable]
public sealed class BossPhaseAttackData
{
    [SheetColumn("ID")]
    public int PhaseAttackId;

    public int BossID;
    public int Phase;
    public int PatternID;
    public float Weight;
}
