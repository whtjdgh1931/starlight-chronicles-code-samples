using UnityEngine;

/// <summary>
/// 순찰 이동과 선공 타깃 탐지 처리
/// </summary>
public class MonsterPatrolState : MonsterState
{

    private float _patrolTimer;
    private Vector2 _moveDirection;

    public MonsterPatrolState(MonsterBase monster, MonsterFSM monsterFSM) : base(monster, monsterFSM)
    {
    }

    public override void Enter()
{
    _patrolTimer = 0f;

    float randomDirectionX = Random.value > 0.5f ? 1f : -1f;
    _moveDirection = new Vector2(randomDirectionX, 0f);

    Monster.PlayMoveAnimation();
}

    public override void Update()
    {
        if (Monster.TryAcquireAggressiveTarget())
        {
            MonsterFSM.ChangeToChaseState();
            return;
        }


        _patrolTimer += Time.deltaTime;

        if (_patrolTimer < Monster.Data.PatrolDuration)
        {
            return;
        }

        MonsterFSM.ChangeToIdleState();

    }

    public override void PhysicsUpdate()
    {
        if (Monster.WallCheck(_moveDirection.x))
        {
            _moveDirection.x *= -1f;
        }

        Monster.Move(_moveDirection);
    }

    public override void Exit()
    {
        Monster.Move(Vector2.zero);
    }
}
