using UnityEngine;

/// <summary>
/// 몬스터 피격 상태
/// </summary>
public class MonsterHitState : MonsterState
{


    public MonsterHitState(MonsterBase monster, MonsterFSM monsterFSM) : base(monster, monsterFSM)
    {
    }

    public override void Enter()
    {
        Monster.PlayHitAnimation();
        Monster.Move(Vector2.zero);
    }

    public override void Exit()
    {

    }

    public override void PhysicsUpdate()
    {

    }

    public override void Update()
    {
    }

    public override void OnAnimationFinished()
    {
        if (Monster.TryGetTrackableTarget(out _))
        {
            MonsterFSM.ChangeToChaseState();
            return;
        }

        MonsterFSM.ChangeToIdleState();
    }
}
