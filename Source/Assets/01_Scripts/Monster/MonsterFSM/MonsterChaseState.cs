using UnityEngine;

/// <summary>
/// 지정된 타깃만 제한 시간 동안 추적
/// </summary>
public class MonsterChaseState : MonsterState
{
    private Vector2 _moveDirection;
    private float _pendingDirectionX;
    private float _turnReadyTime;
    private float _remainingTime;

    private bool _isMovementBlocked;
    public MonsterChaseState(MonsterBase monster, MonsterFSM monsterFSM) : base(monster, monsterFSM)
    {
    }

    public override void Enter()
    {
        _isMovementBlocked = false;
        _moveDirection = Vector2.zero;
        _pendingDirectionX = 0f;
        _turnReadyTime = 0f;
        _remainingTime = Monster.Data.ChaseDuration;

        Monster.PlayMoveAnimation();
    }

    public override void Update()
    {
        if (!Monster.TryGetTrackableTarget(out Player target))
        {
            FinishChase();
            return;
        }

        _remainingTime -= Time.deltaTime;

        if (_remainingTime <= 0f)
        {
            FinishChase();
            return;
        }

        if (Monster.CanStartAttack(target))
        {
            MonsterFSM.ChangeToAttackState();
            return;
        }

        float targetDistanceX =
            target.transform.position.x - Monster.transform.position.x;

        UpdateMoveDirection(targetDistanceX);
    }

    public override void PhysicsUpdate()
    {
        bool isTurning = Mathf.Abs(_pendingDirectionX) > Constants.FLOAT_CheckFloat;

        if (!isTurning && Mathf.Abs(_moveDirection.x) > Constants.FLOAT_CheckFloat)
        {
            Monster.FaceDirection(_moveDirection.x);
        }

        bool isBlocked = !isTurning && Monster.WallCheck(_moveDirection.x);
        Monster.Move(isTurning || isBlocked ? Vector2.zero : _moveDirection);
        UpdateMovementAnimation(isTurning || isBlocked);
    }

    public override void Exit()
    {
        _pendingDirectionX = 0f;
        Monster.Move(Vector2.zero);
    }

    private void UpdateMovementAnimation(bool isBlocked)
    {
        if (_isMovementBlocked == isBlocked)
        {
            return;
        }

        _isMovementBlocked = isBlocked;

        if (_isMovementBlocked)
        {
            Monster.PlayIdleAnimation();
            return;
        }

        Monster.PlayMoveAnimation();
    }

    private void UpdateMoveDirection(float targetDistanceX)
    {
        if (_moveDirection.x == 0f)
        {
            _moveDirection.x = targetDistanceX >= 0f ? 1f : -1f;
            return;
        }

        bool shouldTurn = _moveDirection.x > 0f
            ? targetDistanceX < -Constants.FLOAT_CharacterCheck
            : targetDistanceX > Constants.FLOAT_CharacterCheck;

        if (!shouldTurn)
        {
            _pendingDirectionX = 0f;
            return;
        }

        float targetDirectionX = -_moveDirection.x;

        if (_pendingDirectionX != targetDirectionX)
        {
            _pendingDirectionX = targetDirectionX;
            _turnReadyTime = Time.time + Monster.Data.TurnDelay;
        }

        if (Time.time < _turnReadyTime)
        {
            return;
        }

        _moveDirection.x = _pendingDirectionX;
        _pendingDirectionX = 0f;
    }

    private void FinishChase()
    {
        Monster.ClearTarget();
        MonsterFSM.ChangeToIdleState();
    }
}
