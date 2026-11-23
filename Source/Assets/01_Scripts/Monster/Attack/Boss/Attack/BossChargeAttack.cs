using System;
using UnityEngine;

/// <summary>
/// 예고 완료 후 플레이어 방향으로 돌진하는 보스 공격
/// </summary>
public sealed class BossChargeAttack : BossTelegraphedAttackBehaviour
{
    [SerializeField] private MonsterChargeAction _chargeAction;

    private float _directionX;

    private BossMonster _boss;

    [SerializeField, Min(1f)]
    private float _vulnerabilityDamageMultiplier = 2f;

    [SerializeField, Min(0.01f)]
    private float _vulnerabilityDuration = 5f;

    protected override bool UsesDefaultTelegraph => false;
    protected override bool UsesRecoveryTimer => false;
    public override float CompletionIdleDuration => _chargeAction.RemainingIdleDuration + AttackData.RecoveryDuration;

    protected override bool TryExecuteAttack(Player target)
    {
        float damage = MonsterStats.Atk * AttackData.DamageMultiplier;
        KnockbackData knockbackData = CreateKnockbackData();

        _chargeAction.Begin(_directionX, damage, AttackData.ChargeSpeed, AttackData.ChargeDuration, knockbackData);
        return true;
    }

    protected override void OnInitializeBossAttack()
    {
        if (_chargeAction == null)
        {
            throw new MissingReferenceException($"{name}: MonsterChargeAction 참조 필요");
        }

        if (Monster is not BossMonster boss)
        {
            throw new InvalidOperationException($"{name}: BossMonster 연결 필요");
        }

        _boss = boss;
        _chargeAction.Initialize(Monster);
    }

    protected override void OnBeginBossAttack(Player target)
    {
        _directionX = FaceTarget(target);

        float distance = AttackData.ChargeSpeed * AttackData.ChargeDuration;
        Vector2 center = _chargeAction.AttackCenter + Vector2.right * (_directionX * distance * 0.5f);
        Telegraph.Show(center, new Vector2(distance + _chargeAction.AttackWidth, _chargeAction.AttackHeight), AttackData.PreparationDuration);
    }

    protected override bool OnPhysicsUpdate()
    {
        return _chargeAction.PhysicsUpdate();
    }

    protected override void OnCompleteBossAttack()
    {
        _chargeAction.Stop();

        _boss.StatusEffects.ApplyVulnerability(_vulnerabilityDamageMultiplier, _vulnerabilityDuration);
    }

    protected override void OnCancelBossAttack(bool wasExecuted)
    {
        _chargeAction.Stop();
    }
}
