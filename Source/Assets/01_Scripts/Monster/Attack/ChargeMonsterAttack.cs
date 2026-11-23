using UnityEngine;

/// <summary>
/// 실행 시점에 고정된 방향으로 이동하는 몬스터 돌진 공격
/// </summary>
public sealed class ChargeMonsterAttack : MonsterAttackBehaviour
{
    [SerializeField] private MonsterChargeAction _chargeAction;

    private float _directionX;

    protected override bool UsesRecoveryTimer => false;
    public override float CompletionIdleDuration => _chargeAction.RemainingIdleDuration + AttackData.RecoveryDuration;

    protected override bool TryExecuteAttack(Player target)
    {
        float damage = MonsterStats.Atk * AttackData.DamageMultiplier;
        KnockbackData knockbackData = CreateKnockbackData();

        _chargeAction.Begin(_directionX, damage, AttackData.ChargeSpeed, AttackData.ChargeDuration, knockbackData);
        return true;
    }

    protected override void OnInitialize()
    {
        if (_chargeAction == null)
        {
            throw new MissingReferenceException($"{name}: MonsterChargeAction 참조 필요");
        }

        _chargeAction.Initialize(Monster);
    }

    protected override void OnBegin(Player target)
    {
        _directionX = FaceTarget(target);
    }

    protected override bool OnPhysicsUpdate()
    {
        return _chargeAction.PhysicsUpdate();
    }

    protected override void OnComplete()
    {
        _chargeAction.Stop();
    }

    protected override void OnCancel(bool wasExecuted)
    {
        _chargeAction.Stop();
    }
}
