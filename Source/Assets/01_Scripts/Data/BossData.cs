using System;

/// <summary>
/// 보스 전용 전투 설정 데이터 형식
/// </summary>
[Serializable]
public sealed class BossData
{
    public const int DefaultInitialDeathCount = 5;
    public const float DefaultReviveInvincibleDuration = 2f;

    [SheetColumn("ID")]
    public int BossId;

    public float PhaseChangeHpRatio;
    public float PhaseTransitionDuration;
    public int InitialDeathCount = DefaultInitialDeathCount;
    public float ReviveInvincibleDuration = DefaultReviveInvincibleDuration;
}
