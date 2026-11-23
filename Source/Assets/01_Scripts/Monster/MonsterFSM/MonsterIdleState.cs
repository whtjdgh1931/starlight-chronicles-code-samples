using UnityEngine;

/// <summary>
/// 정지 상태에서 선공 타깃 탐지와 Patrol 전환 처리
/// </summary>
public class MonsterIdleState : MonsterState
{
    private float _idleTimer;
    private float _forcedDuration;
    private float _activeForcedDuration;

    public MonsterIdleState(MonsterBase monster, MonsterFSM monsterFSM) : base(monster, monsterFSM)
    {
    }

    public override void Enter()
    {
        _idleTimer = 0f;
        _activeForcedDuration = _forcedDuration;
        _forcedDuration = 0f;

        Monster.Move(Vector2.zero);
        Monster.PlayIdleAnimation();
    }

    /// <summary>
    /// 다음 진입에서 적용할 강제 Idle 시간 설정
    /// </summary>
    public void SetForcedDuration(float duration)
    {
        _forcedDuration = Mathf.Max(0f, duration);
    }

    public override void Exit()
    {

    }

    public override void Update()
    {
        _idleTimer += Time.deltaTime;

        if (_activeForcedDuration > 0f)
        {
            if (_idleTimer < _activeForcedDuration)
            {
                return;
            }

            _activeForcedDuration = 0f;

            if (Monster.TryGetTrackableTarget(out _))
            {
                MonsterFSM.ChangeToChaseState();
                return;
            }
        }

        if (Monster.TryAcquireAggressiveTarget())
        {
            MonsterFSM.ChangeToChaseState();
            return;
        }

        if (_idleTimer >= Monster.Data.IdleDuration)
        {
            MonsterFSM.ChangeToPatrolState();
        }
    }

}
