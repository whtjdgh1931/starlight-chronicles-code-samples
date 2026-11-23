using System;

/// <summary>
/// 몬스터 공격 패턴 데이터 형식
/// </summary>
[Serializable]
public sealed class MonsterAttackData
{
    [SheetColumn("ID")]
    public int PatternId;

    public int MonsterID;
    public string AttackType;
    public float DamageMultiplier;
    public float RangeX;
    public float RangeY;
    public float PreparationDuration;
    public float RecoveryDuration;
    public float Cooldown;
    public float ChargeSpeed;
    public float ChargeDuration;
    public float KnockbackForceX;
    public float KnockbackForceY;
    public float KnockbackDuration;
    public float ProjectileSpeed;
    public float ProjectileLifetime;
    public string TelegraphTargetMode;
}
