using UnityEngine;

/// <summary>
/// 몬스터의 FSM을 관리하는 컴포넌트
/// </summary>
[RequireComponent(typeof(MonsterBase))]
public class MonsterFSM : MonoBehaviour
{
    private MonsterBase _monster;
    public MonsterBase Monster => _monster;

    private MonsterState _currentState;
    private double _nextHitReactionAt;
    public MonsterState CurrentState => _currentState;

    private MonsterIdleState _idleState;
    private MonsterPatrolState _patrolState;
    private MonsterChaseState _chaseState;
    private MonsterAttackState _attackState;
    private MonsterHitState _hitState;
    private MonsterDieState _dieState;

    /// <summary>
    /// 상태 객체 생성과 실행 준비 단계
    /// </summary>
    private enum InitializationStage
    {
        Uninitialized,
        StatesReady,
        Running
    }

    private InitializationStage _initializationStage;

    /// <summary>
    /// 초기화
    /// </summary>
    public void Initialize()
    {
        if (_initializationStage == InitializationStage.Uninitialized)
        {
            InitializeStates();
        }

        _nextHitReactionAt = 0d;
        _initializationStage = InitializationStage.StatesReady;
        ChangeState(_idleState);
        _initializationStage = InitializationStage.Running;
    }

    private void InitializeStates()
    {
        _monster = GetComponent<MonsterBase>();

        _idleState = new MonsterIdleState(_monster, this);
        _patrolState = new MonsterPatrolState(_monster, this);
        _chaseState = new MonsterChaseState(_monster, this);
        _attackState = new MonsterAttackState(_monster, this);
        _hitState = new MonsterHitState(_monster, this);
        _dieState = new MonsterDieState(_monster, this);


        _initializationStage = InitializationStage.StatesReady;
    }

    public void ChangeToIdleState()
    {
        ChangeState(_idleState);
    }

    /// <summary>
    /// 지정 시간 동안 강제 Idle 상태 전환
    /// </summary>
    public void ChangeToIdleState(float duration)
    {
        _idleState.SetForcedDuration(duration);
        ChangeState(_idleState);
    }

    public void ChangeToPatrolState()
    {
        ChangeState(_patrolState);
    }

    public void ChangeToChaseState()
    {
        ChangeState(_chaseState);
    }

    /// <summary>
    /// 피격 상태 전환
    /// </summary>
    public void ChangeToHitState()
    {
        if (_currentState == _attackState && !_monster.CanInterruptAttack())
        {
            return;
        }

        float cooldown = _monster.HitReactionCooldown;
        if (cooldown > 0f)
        {
            double now = Time.timeAsDouble;
            if (_currentState == _hitState || now < _nextHitReactionAt) { return; }
            _nextHitReactionAt = now + cooldown;
        }

        ChangeState(_hitState);
    }

    public void ChangeToAttackState()
    {
        ChangeState(_attackState);
    }

    public void ChangeToDieState()
    {
        ChangeState(_dieState);
    }

    /// <summary>
    /// 애니메이션 종료를 전달
    /// </summary>
    public void NotifyAnimationFinished()
    {
        _currentState.OnAnimationFinished();
    }

    /// <summary>
    /// 공격 애니메이션 실행 시점 전달
    /// </summary>
    public void NotifyAttackExecution()
    {
        if (_currentState != _attackState)
        {
            return;
        }

        _attackState.ExecuteAttack();
    }

    private void ChangeState(MonsterState state)
    {
        _currentState?.Exit();
        _currentState = state;
        _currentState.Enter();
    }

    private void Update()
    {
        if (_initializationStage != InitializationStage.Running)
        {
            return;
        }

        _currentState.Update();
    }

    private void FixedUpdate()
    {
        if (_initializationStage != InitializationStage.Running)
        {
            return;
        }


        _currentState.PhysicsUpdate();
    }
}
