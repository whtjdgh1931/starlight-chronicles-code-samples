using UnityEngine;

public class MonsterAttackState : MonsterState
{

    private bool _attackAnimationStarted;


    public MonsterAttackState(MonsterBase monster, MonsterFSM monsterFSM): base(monster, monsterFSM)
    {
    }

    public override void Enter()
    {
        if (!Monster.TryGetTrackableTarget(out Player target))
        {
            MonsterFSM.ChangeToIdleState();
            return;
        }

        _attackAnimationStarted = false;
        Monster.BeginAttack(target);
        TryStartAttackAnimation();
    }

    /// <summary>
    /// 공격 애니메이션 실행 시점 처리
    /// </summary>
    public void ExecuteAttack()
    {
        Monster.TryExecuteAttack();
    }

    public override void PhysicsUpdate()
    {
        if (Monster.UpdateAttackPhysics())
        {
            FinishAttack();
        }
    }

    public override void Update()
    {
        if (!Monster.UpdateAttack())
        {
            FinishAttack();
            return;
        }

        TryStartAttackAnimation();

        if (Monster.IsAttackComplete())
        {
            FinishAttack();
        }
    }

    public override void Exit()
    {
        Monster.CancelAttack();
    }

    /// <summary>
    /// 준비 완료 후 공격 애니메이션 시작
    /// </summary>
    private void TryStartAttackAnimation()
    {
        if (_attackAnimationStarted || !Monster.IsAttackPrepared())
        {
            return;
        }

        _attackAnimationStarted = true;
        Monster.PlayAttackAnimation();
    }

    /// <summary>
    /// 공격 완료와 다음 상태 결정
    /// </summary>
    private void FinishAttack()
    {
        float completionIdleDuration = Monster.GetAttackCompletionIdleDuration();
        Monster.CompleteAttack();

        if (completionIdleDuration > Constants.FLOAT_CheckFloat)
        {
            MonsterFSM.ChangeToIdleState(completionIdleDuration);
            return;
        }

        if (Monster.TryGetTrackableTarget(out _))
        {
            MonsterFSM.ChangeToChaseState();
            return;
        }

        MonsterFSM.ChangeToIdleState();
    }

    public override void OnAnimationFinished()
    {
        FinishAttack();
    }

}
