using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 공격 중 피격에 의한 중단 정책
/// </summary>
public enum AttackInterruptPolicy
{
    Always,
    BeforeExecution,
    Never
}

/// <summary>
/// 몬스터 공격의 공통 생명주기와 사용 조건 관리
/// </summary>
public abstract class MonsterAttackBehaviour : MonoBehaviour, IMonsterAttackRuntime
{
    [SerializeField, Min(1)] private int _patternId;
    public int PatternId => _patternId;

    [SerializeField] private MonsterBase _monster;
    protected MonsterBase Monster => _monster;

    [SerializeField]
    private AttackInterruptPolicy _interruptPolicy;

    private MonsterData _monsterData;
    protected MonsterData MonsterStats => _monsterData;

    private MonsterAttackData _attackData;
    protected MonsterAttackData AttackData => _attackData;

    private Player _target;
    protected Player Target => _target;

    /// <summary>
    /// 공격 대기·예고·판정 준비·실행 단계
    /// </summary>
    protected enum AttackStage
    {
        Idle,
        Preparing,
        ReadyToExecute,
        Executed
    }

    protected AttackStage Stage { get; private set; }
    public bool IsPrepared => Stage is AttackStage.ReadyToExecute or AttackStage.Executed;

    private float _preparationEndTime;
    protected float PreparationTimeRemaining => Mathf.Max(0f, _preparationEndTime - Time.time);

    private float _nextAttackTime;
    private float _completionTime;
    public bool IsComplete => Stage == AttackStage.Executed && UsesRecoveryTimer && Time.time >= _completionTime;

    /// <summary>
    /// 공통 후딜 타이머 기반 완료 여부
    /// </summary>
    protected virtual bool UsesRecoveryTimer => true;

    /// <summary>
    /// 공격 완료 후 강제 Idle 시간
    /// </summary>
    public virtual float CompletionIdleDuration => 0f;

    /// <summary>
    /// 공격 데이터 및 런타임 상태 초기화
    /// </summary>
    public void Initialize(MonsterData monsterData, IReadOnlyDictionary<int, MonsterAttackData> attackTable)
    {
        if (!attackTable.TryGetValue(_patternId, out MonsterAttackData attackData))
        {
            throw new InvalidOperationException($"{name}: 공격 패턴 데이터를 찾을 수 없음 PatternId: {_patternId}");
        }

        ValidateReferences(monsterData, attackData);

        _completionTime = 0f;
        _monsterData = monsterData;
        _attackData = attackData;
        _target = null;
        _nextAttackTime = 0f;
        Stage = AttackStage.Idle;

        OnInitialize();
    }

    private void ValidateReferences(MonsterData monsterData, MonsterAttackData attackData)
    {
        if (_monster == null)
        {
            throw new MissingReferenceException($"{name}: MonsterBase 참조 필요");
        }

        if (monsterData == null)
        {
            throw new ArgumentNullException(
                nameof(monsterData),
                $"{name}: MonsterData 필요");
        }

        if (attackData.MonsterID != monsterData.ID)
        {
            throw new InvalidOperationException($"{name}: 몬스터와 공격 패턴 ID 불일치 MonsterID: {monsterData.ID}, PatternMonsterID: {attackData.MonsterID}");
        }

        if (attackData.RangeX < 0f || attackData.RangeY < 0f || attackData.Cooldown < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(attackData), $"{name}: 공격 범위와 쿨타임은 0 이상 필요");
        }
    }

    /// <summary>
    /// 현재 타깃에 대한 공격 시작 가능 여부 확인
    /// </summary>
    public bool CanStart(Player target)
    {
        return Stage == AttackStage.Idle &&
            IsTargetAvailable(target) &&
            Time.time >= _nextAttackTime &&
            IsInRange(target) &&
            CanStartInternal(target);
    }

    /// <summary>
    /// 공격 준비 시작
    /// </summary>
    public void Begin(Player target)
    {
        if (!CanStart(target))
        {
            throw new InvalidOperationException($"{name}: 시작할 수 없는 공격 요청");
        }

        _target = target;
        BeginPreparation(0f);
        try
        {
            OnBegin(target);
        }
        catch
        {
            Cancel();
            throw;
        }
    }

    /// <summary>
    /// 공격 판정 실행
    /// </summary>
    public bool TryExecute()
    {
        if (Stage != AttackStage.ReadyToExecute)
        {
            return false;
        }

        if (!CanExecute(_target))
        {
            Cancel();
            return false;
        }

        if (!TryExecuteAttack(_target))
        {
            Cancel();
            return false;
        }

        if (Stage != AttackStage.ReadyToExecute) { return false; }
        Stage = AttackStage.Executed;
        _completionTime = Time.time + _attackData.RecoveryDuration;
        _nextAttackTime = Time.time + _attackData.Cooldown;
        return true;
    }

    /// <summary>
    /// 정상 공격 완료
    /// </summary>
    public void Complete()
    {
        if (Stage == AttackStage.Idle)
        {
            return;
        }

        Stage = AttackStage.Idle;
        try { OnComplete(); }
        finally { ResetAttack(); }
    }

    /// <summary>
    /// 피격에 의한 공격 중단 가능 여부 확인
    /// </summary>
    public bool CanInterrupt()
    {
        return _interruptPolicy switch
        {
            AttackInterruptPolicy.Always => true,
            AttackInterruptPolicy.BeforeExecution => Stage != AttackStage.Executed,
            AttackInterruptPolicy.Never => false,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// 타깃 공격 범위 포함 여부 확인
    /// </summary>
    public bool IsInRange(Player target)
    {
        if (!IsTargetAvailable(target))
        {
            return false;
        }

        Vector2 offset = target.transform.position - _monster.transform.position;

        return
            Mathf.Abs(offset.x) <= _attackData.RangeX &&
            Mathf.Abs(offset.y) <= _attackData.RangeY;
    }

    /// <summary>
    /// 공격 프레임 갱신
    /// </summary>
    public bool UpdateAttack()
    {
        return Stage != AttackStage.Idle && OnUpdateAttack();
    }

    /// <summary>
    /// 공격 물리 갱신과 완료 요청
    /// </summary>
    public bool PhysicsUpdate()
    {
        return Stage != AttackStage.Idle && OnPhysicsUpdate();
    }

    /// <summary>
    /// 예고 시간에 따른 공격 준비 단계 설정
    /// </summary>
    protected void BeginPreparation(float duration)
    {
        _preparationEndTime = Time.time + duration;
        Stage = duration > 0f ? AttackStage.Preparing : AttackStage.ReadyToExecute;
    }

    /// <summary>
    /// 예고 종료 시 판정 실행 가능 단계로 전환
    /// </summary>
    protected bool TryCompletePreparation()
    {
        if (Stage != AttackStage.Preparing || Time.time < _preparationEndTime)
        {
            return false;
        }

        Stage = AttackStage.ReadyToExecute;
        return true;
    }

    protected virtual bool CanExecute(Player target)
    {
        return Monster.TryGetTrackableTarget(out Player trackedTarget) &&
            trackedTarget == target;
    }

    /// <summary>
    /// 현재 공격 패턴의 넉백 설정 생성
    /// </summary>
    protected KnockbackData CreateKnockbackData()
    {
        return new KnockbackData(AttackData.KnockbackForceX, AttackData.KnockbackForceY, AttackData.KnockbackDuration);
    }

    protected abstract bool TryExecuteAttack(Player target);

    protected virtual bool CanStartInternal(Player target)
    {
        return true;
    }

    private bool IsTargetAvailable(Player target)
    {
        return target != null && target.CanBeMonsterTarget;
    }

    /// <summary>
    /// 현재 타깃 방향 계산과 시각 방향 전환
    /// </summary>
    protected float FaceTarget(Player target)
    {
        float directionX = target.transform.position.x >= Monster.transform.position.x ? 1f : -1f;
        Monster.FaceDirection(directionX);
        return directionX;
    }

    protected virtual void OnInitialize()
    {
    }

    protected virtual void OnBegin(Player target)
    {
    }

    protected virtual bool OnUpdateAttack()
    {
        return true;
    }

    protected virtual bool OnPhysicsUpdate()
    {
        return false;
    }

    protected virtual void OnComplete()
    {
    }

    protected virtual void OnCancel(bool wasExecuted)
    {
    }

    /// <summary>
    /// 진행 중인 공격 취소
    /// </summary>
    public void Cancel()
    {
        if (Stage == AttackStage.Idle)
        {
            return;
        }

        bool wasExecuted = Stage == AttackStage.Executed;
        Stage = AttackStage.Idle;
        try { OnCancel(wasExecuted); }
        finally { ResetAttack(); }
    }

    private void ResetAttack()
    {
        _target = null;
        Stage = AttackStage.Idle;
    }
}
