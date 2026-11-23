using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 몬스터 기본 클래스
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(MonsterFSM))]
[RequireComponent(typeof(MonsterMove))]
[RequireComponent(typeof(MonsterTargetDetector))]
public class MonsterBase : MonoBehaviour
{
    /// <summary>
    /// 화면에 재생 중인 몬스터 애니메이션 종류
    /// </summary>
    public enum AnimationState : byte
    {
        Idle,
        Move,
        Attack,
        Hit,
        Die
    }

    public AnimationState CurrentAnimation { get; private set; }
    public float FacingDirection { get; private set; } = 1f;

    /// <summary>
    /// 네트워크 표시를 위한 실제 애니메이션 재생 알림
    /// </summary>
    public event Action<AnimationState> AnimationPlayed;
    public event Action<float> FacingDirectionChanged;

    [Header("Components")]
    [SerializeField] private Rigidbody2D _rb;
    public Rigidbody2D Rb => _rb;

    [SerializeField] private MonsterFSM _monsterFSM;

    [SerializeField] private MonsterData _data;
    public MonsterData Data => _data;

    [SerializeField] private MonsterMove _monsterMove;

    [SerializeField] private MonsterAnimationController _monsterAnimator;

    [SerializeField] private MonsterTargetDetector _targetDetector;

    [SerializeField] private StatusEffectHandler _statusEffectHandler;
    public StatusEffectHandler StatusEffects => _statusEffectHandler;

    [SerializeField] private float _currentHp;
    private float _maxHp;
    public float CurrentHp => _currentHp;
    public float MaxHp => _maxHp;
    public bool CanDealDamage => _hasSimulationAuthority && _currentHp > 0f;
    internal virtual float HitReactionCooldown => 0f;
    protected virtual bool UsesWorldHealthBar => true;
    protected virtual bool ConsumesHealth => true;
    protected virtual bool RetaliatesOnHit => true;
    protected virtual bool UsesStandardMotionPresentation => true;


    private Player _target;
    [SerializeField] private MonsterContactAttack _contactAttack;
    [SerializeField] private MonsterAttackBehaviour _attackBehaviour;

    protected virtual IMonsterAttackRuntime AttackRuntime => _attackBehaviour;

    [Header("UI")]
    [SerializeField] private MonsterHealthBar _healthBar;


    [Header("FSM 세팅")]
    [Tooltip("선공 여부")]
    [SerializeField] private bool _isAggressive = false;

    /// <summary>
    /// 주변 플레이어에 대한 선공 탐지 사용 여부
    /// </summary>
    public bool IsAggressive => _isAggressive;

    private bool _componentsInitialized;
    private bool _isDeathCompleted;
    private bool _hasSimulationAuthority;
    private MonsterMotionPresentation _motionPresentation;
    private float _lastHitDirection;


    [Header("Colliders")]
    [SerializeField] private Collider2D _bodyCollider;
    public Collider2D BodyCollider => _bodyCollider;


    public event Action<MonsterBase> OnReturnedToPool;
    public event Action<MonsterBase> OnKilledEvent;

    /// <summary>
    /// 일반 몬스터와 보스의 현재·최대 체력 변경 알림
    /// </summary>
    public event Action<float, float> HealthChanged;
    public event Action<Vector3, bool, Player> DamageImpactOccurred;

    /// <summary>
    /// 사망 연출 또는 강제 정리 완료
    /// 생성 담당자 한 곳에서 구독하여 객체 반환
    /// </summary>
    public event Action<MonsterBase> DeathCompleted;


    /// <summary>
    /// 오프라인 또는 서버 몬스터의 게임 판정 초기화
    /// </summary>
    public virtual void Initialize(MonsterData data, IReadOnlyDictionary<int, MonsterAttackData> attackTable)
    {
        ValidateReferences();

        _hasSimulationAuthority = true;
        _monsterFSM.enabled = true;
        _statusEffectHandler.enabled = true;
        _rb.simulated = true;
        _statusEffectHandler.Clear();

        if (data.TurnDelay < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(data), $"{name}: 방향 전환 대기 시간은 0 이상 필요");
        }

        _data = data;
        _maxHp = data.Hp;
        _currentHp = _maxHp;
        _target = null;
        _isDeathCompleted = false;

        _targetDetector.Initialize(data);
        _contactAttack.Initialize(data.Atk, data.ContactDamageEnabled);
        _monsterMove.Initialize(this);
        AttackRuntime?.Initialize(data, attackTable);

        SetCombatCollidersEnabled(true);
        _monsterAnimator.Initialize(_monsterFSM.NotifyAnimationFinished, _monsterFSM.NotifyAttackExecution);
        InitializeMotionPresentation();
        ResetPresentation();
        _healthBar.Initialize(data.Hp);
        _monsterFSM.Initialize();
    }

    /// <summary>
    /// 클라이언트의 몬스터 표시 초기화
    /// AI·물리·접촉 피해 실행 제외
    /// </summary>
    public void InitializeReplica(MonsterData data)
    {
        ValidateReferences();

        _hasSimulationAuthority = false;
        _monsterFSM.enabled = false;
        _statusEffectHandler.enabled = false;
        _statusEffectHandler.Clear();
        _rb.simulated = false;

        _data = data;
        _maxHp = 0f;
        _currentHp = 0f;
        _target = null;
        _isDeathCompleted = false;

        _contactAttack.Initialize(data.Atk, false);
        SetCombatCollidersEnabled(false);

        _monsterAnimator.Initialize(IgnoreAnimationEvent, IgnoreAnimationEvent);
        ResetPresentation();
        _healthBar.Initialize(data.Hp);
        _monsterAnimator.PlayIdle();
    }

    /// <summary>
    /// 필수 Inspector 참조 검증
    /// </summary>
    private void ValidateReferences()
    {
        if (_componentsInitialized)
        {
            return;
        }

        if (_rb == null)
        {
            throw new MissingReferenceException(
                $"{name}: Rigidbody2D 참조 필요");
        }

        if (_monsterFSM == null)
        {
            throw new MissingReferenceException(
                $"{name}: MonsterFSM 참조 필요");
        }

        if (_monsterMove == null)
        {
            throw new MissingReferenceException(
                $"{name}: MonsterMove 참조 필요");
        }

        if (_monsterAnimator == null)
        {
            throw new MissingReferenceException(
                $"{name}: MonsterAnimator 참조 필요");
        }

        if (_contactAttack == null)
        {
            throw new MissingReferenceException(
                $"{name}: MonsterContactAttack 참조 필요");
        }

        if (_targetDetector == null)
        {
            throw new MissingReferenceException(
                $"{name}: MonsterTargetDetector 참조 필요");
        }

        if (_healthBar == null)
        {
            throw new MissingReferenceException(
                $"{name}: MonsterHealthBar 참조 필요");
        }

        if (_bodyCollider == null)
        {
            throw new MissingReferenceException(
                $"{name}: 몸체 Collider2D 참조 필요");
        }

        if (_statusEffectHandler == null)
        {
            throw new MissingReferenceException(
                $"{name}: StatusEffectHandler 참조 필요");
        }

        _componentsInitialized = true;
    }

    /// <summary>
    /// 서버에서 수신한 현재·최대 체력을 클라이언트 상태와 화면에 반영
    /// 피해 계산과 사망 판정은 서버에서 실행
    /// </summary>
    public void ApplyReplicatedHealth(float currentHp, float maxHp)
    {
        if (_hasSimulationAuthority)
        {
            return;
        }

        bool maxHpChanged = _maxHp != maxHp;
        _maxHp = maxHp;
        _currentHp = Mathf.Clamp(currentHp, 0f, _maxHp);
        OnHealthChanged(_currentHp, _maxHp);

        if (!UsesWorldHealthBar)
        {
            return;
        }

        if (maxHpChanged) { _healthBar.Initialize(_maxHp); }
        if (_currentHp < _maxHp)
        {
            _healthBar.Show(_currentHp);
        }
        else
        {
            _healthBar.Hide();
        }
    }

    /// <summary>
    /// 피해와 피격 반응 적용 및 수용한 피해량 반환
    /// 무한 체력 대상은 체력 변경 없이 계산된 피해량 반환
    /// </summary>
    public float TakeDamage(
        float damage, Vector2 attackPosition, Player attacker, Vector3? impactPosition = null)
    {
        if (!_hasSimulationAuthority || _currentHp <= 0f)
        {
            return 0f;
        }

        if (RetaliatesOnHit) { SetTarget(attacker); }

        float modifiedDamage = _statusEffectHandler.ModifyIncomingDamage(damage);
        float previousHp = _currentHp;
        if (ConsumesHealth)
        {
            _currentHp = Mathf.Max(0f, _currentHp - modifiedDamage);
            OnHealthChanged(_currentHp, _maxHp);
        }
        float appliedDamage = ConsumesHealth ? previousHp - _currentHp : Mathf.Max(0f, modifiedDamage);
        
        //보스 몬스터라면 hp바 표시x
        if (UsesWorldHealthBar)
        {
            _healthBar.Show(_currentHp);
        }

        if (appliedDamage > Constants.FLOAT_CheckFloat)
        {
            Vector3 numberPosition = new Vector3(_bodyCollider.bounds.center.x, _bodyCollider.bounds.max.y + 0.5f, transform.position.z);
            EventBus.PublishDamageNumber(new DamageNumberRequest(Mathf.CeilToInt(appliedDamage), numberPosition, DamageNumberStyle.Dealt, GetEntityId()));

            Bounds body = _bodyCollider.bounds;
            bool fromLeft = attackPosition.x <= body.center.x;
            _lastHitDirection = fromLeft ? 1f : -1f;
            _motionPresentation?.PlayDamageFlash(_currentHp <= 0f);
            Vector3 position = impactPosition ??
                DamageImpactPresentation.GetImpactPosition(body, fromLeft, transform.position.z);
            if (DamageImpactOccurred != null)
            {
                DamageImpactOccurred.Invoke(position, fromLeft, attacker);
            }
            else if (MapManager.Instance != null)
            {
                MapManager.Instance.DamageImpactPresentation.PlayMonsterImpact(position, fromLeft, isLocalSound: true);
            }
        }

        if (_currentHp <= 0f)
        {
            _monsterFSM.ChangeToDieState();
            return appliedDamage;
        }

        // 행동 정지 중 피격 애니메이션 전환 제외
        if (_monsterFSM.enabled)
        {
            _monsterFSM.ChangeToHitState();
        }
        return appliedDamage;
    }

    /// <summary>
    /// 이동 처리 위임
    /// </summary>
    /// <param Name="dierction"></param>
    public virtual void Move(Vector2 direction)
    {
        _monsterMove.Move(direction);

        if (Mathf.Abs(direction.x) > Constants.FLOAT_CheckFloat)
        {
            FaceDirection(direction.x);
        }
    }

    /// <summary>
    /// 지정 속도 기반 이동
    /// </summary>
    public void MoveAtSpeed(Vector2 direction, float speed)
    {
        _monsterMove.Move(direction, speed);

        if (Mathf.Abs(direction.x) > Constants.FLOAT_CheckFloat)
        {
            FaceDirection(direction.x);
        }
    }

    /// <summary>
    /// 지정된 수평 방향으로 시각 방향 전환
    /// </summary>
    public void FaceDirection(float directionX)
    {
        if (Mathf.Abs(directionX) <= Constants.FLOAT_CheckFloat)
        {
            return;
        }

        float direction = directionX > 0f ? 1f : -1f;
        _monsterAnimator.SetFacingDirection(direction);
        if (FacingDirection == direction) { return; }

        FacingDirection = direction;
        FacingDirectionChanged?.Invoke(direction);
    }

    /// <summary>
    /// 접촉 공격 활성 상태 변경
    /// </summary>
    public void SetContactDamageEnabled(bool enabled)
    {
        _contactAttack.SetEnabled(enabled);
    }

    /// <summary>
    /// 진행 중인 공격 물리 갱신과 완료 요청
    /// </summary>
    public bool UpdateAttackPhysics()
    {
        return AttackRuntime.PhysicsUpdate();
    }

    public bool WallCheck(float directionX)
    {
        return _monsterMove.WallCheck(directionX);
    }

    /// <summary>
    /// 현재 타깃 사용 가능 여부 확인
    /// </summary>
    public bool TryGetTarget(out Player target)
    {
        target = _target;

        return target != null && target.CanBeMonsterTarget;
    }

    /// <summary>
    /// 현재 타깃이 활성 상태이며 추적 범위 안에 있는지 확인
    /// </summary>
    public bool TryGetTrackableTarget(out Player target)
    {
        if (!TryGetTarget(out target) || !_targetDetector.IsTrackable(target))
        {
            ClearTarget();
            target = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 추격하거나 공격할 대상을 설정
    /// </summary>
    public void SetTarget(Player target)
    {
        _target = target;
    }

    /// <summary>
    /// 현재 타깃에 대한 상태 공격 시작 가능 여부 확인
    /// </summary>
    public bool CanStartAttack(Player target)
    {
        return AttackRuntime != null && AttackRuntime.CanStart(target);
    }

    /// <summary>
    /// 현재 공격 실행 준비 완료 여부
    /// </summary>
    public bool IsAttackPrepared()
    {
        return AttackRuntime.IsPrepared;
    }

    /// <summary>
    /// 현재 공격 프레임 갱신
    /// </summary>
    public bool UpdateAttack()
    {
        return AttackRuntime.UpdateAttack();
    }

    /// <summary>
    /// 상태 공격 준비 시작
    /// </summary>
    public void BeginAttack(Player target)
    {
        AttackRuntime.Begin(target);
    }

    /// <summary>
    /// 상태 공격 판정 실행
    /// </summary>
    public bool TryExecuteAttack()
    {
        if (!_hasSimulationAuthority || !AttackRuntime.TryExecute())
        {
            return false;
        }

        _motionPresentation?.PlayAttackRelease(FacingDirection, AttackRuntime is ProjectileMonsterAttack);
        return true;
    }

    /// <summary>
    /// 상태 공격 정상 완료
    /// </summary>
    public void CompleteAttack()
    {
        AttackRuntime.Complete();
    }

    public bool IsAttackComplete()
    {
        return AttackRuntime.IsComplete;
    }

    /// <summary>
    /// 현재 공격 완료 후 강제 Idle 시간
    /// </summary>
    public float GetAttackCompletionIdleDuration()
    {
        return AttackRuntime.CompletionIdleDuration;
    }

    /// <summary>
    /// 피격에 의한 상태 공격 중단 가능 여부 확인
    /// </summary>
    public bool CanInterruptAttack()
    {
        return AttackRuntime.CanInterrupt();
    }

    /// <summary>
    /// 선공 몬스터가 감지 범위 안의 플레이어를 현재 타깃으로 설정
    /// Idle과 Patrol 상태에서만 호출
    /// </summary>
    public bool TryAcquireAggressiveTarget()
    {
        if (!_isAggressive || !_targetDetector.TryAcquireTarget(out Player target))
        {
            return false;
        }

        SetTarget(target);
        return true;
    }

    public void PlayIdleAnimation()
    {
        _monsterAnimator.PlayIdle();
        NotifyAnimationPlayed(AnimationState.Idle);
    }

    public void PlayMoveAnimation()
    {
        _monsterAnimator.PlayMove();
        if (CurrentAnimation == AnimationState.Idle)
        {
            _motionPresentation?.PlayStep();
        }
        NotifyAnimationPlayed(AnimationState.Move);
    }

    /// <summary>
    /// 공격 애니메이션 재생
    /// </summary>
    public void PlayAttackAnimation()
    {
        _monsterAnimator.PlayAttack();
        _motionPresentation?.PlayWindup(FacingDirection);
        NotifyAnimationPlayed(AnimationState.Attack);
    }

    /// <summary>
    /// 피격 애니메이션 재생
    /// </summary>
    public void PlayHitAnimation()
    {
        _monsterAnimator.PlayHit();
        _motionPresentation?.PlayHit(_lastHitDirection);
        NotifyAnimationPlayed(AnimationState.Hit);
    }

    /// <summary>
    /// 사망 애니메이션 재생
    /// </summary>
    public void PlayDieAnimation()
    {
        _monsterAnimator.PlayDie();
        _motionPresentation?.PlayDeath();
        NotifyAnimationPlayed(AnimationState.Die);
    }

    private void InitializeMotionPresentation()
    {
        if (!UsesStandardMotionPresentation || _motionPresentation != null) { return; }

        Transform visualRoot = _monsterAnimator.transform;
        bool containsFirePoint = AttackRuntime is ProjectileMonsterAttack projectile && projectile.FirePoint.IsChildOf(visualRoot);
        bool canOffsetVisual = visualRoot != transform && visualRoot.IsChildOf(transform) && !containsFirePoint &&
            visualRoot.GetComponentInChildren<Collider2D>(true) == null &&
            visualRoot.GetComponentInChildren<Rigidbody2D>(true) == null;
        _motionPresentation = new MonsterMotionPresentation(visualRoot, canOffsetVisual);
    }

    /// <summary>
    /// 풀 재사용과 복제 초기화 시 기본 표시 상태 복구
    /// </summary>
    private void ResetPresentation()
    {
        _motionPresentation?.Clear();
        _lastHitDirection = 0f;
        CurrentAnimation = AnimationState.Idle;
        FacingDirection = 1f;
        _monsterAnimator.SetFacingDirection(FacingDirection);
    }

    /// <summary>
    /// 반복 대기·이동 알림 생략과 연속 공격·피격 재생 구분
    /// </summary>
    private void NotifyAnimationPlayed(AnimationState animation)
    {
        bool isRepeatedLocomotion = CurrentAnimation == animation &&
            (animation is AnimationState.Idle or AnimationState.Move);
        CurrentAnimation = animation;
        if (!isRepeatedLocomotion)
        {
            AnimationPlayed?.Invoke(animation);
        }
    }

    /// <summary>
    /// 사망 연출 또는 강제 정리 완료 후 생성 담당자에게 반환 요청
    /// 생성 담당자의 DeathCompleted 구독 필수
    /// </summary>
    public void CompleteDeath()
    {
        if (_isDeathCompleted)
        {
            return;
        }

        _isDeathCompleted = true;
        _healthBar.Hide();

        // 실제 반환 전 관련 객체의 연결 정리
        OnReturnedToPool?.Invoke(this);
        OnReturnedToPool = null;
        OnKilledEvent = null;

        // 구독 해제 후 생성 담당자에게 반환 요청
        Action<MonsterBase> deathCompleted = DeathCompleted;
        DeathCompleted = null;
        deathCompleted(this);
    }

    /// <summary>
    /// 표시 전용 애니메이션에서 게임 판정 콜백 제외
    /// </summary>
    private static void IgnoreAnimationEvent()
    {
    }

    private void SetCombatCollidersEnabled(bool enabled)
    {
        _bodyCollider.enabled = enabled;
    }

    /// <summary>
    /// 피해 계산 후 체력 변경 알림
    /// </summary>
    protected virtual void OnHealthChanged(float currentHp, float maxHp)
    {
        HealthChanged?.Invoke(currentHp, maxHp);
    }

    /// <summary>
    /// 사망 시작 처리 
    /// </summary>
    public virtual void OnDie()
    {
        OnKilledEvent?.Invoke(this);

        _bodyCollider.enabled = false;
        _contactAttack.SetEnabled(false);
        _monsterMove.OnDie();
    }

    /// <summary>
    /// 피해 수신과 물리를 유지한 행동 일시 정지·재개
    /// </summary>
    public void SetBehaviourPaused(bool paused)
    {
        _monsterFSM.ChangeToIdleState();

        if (paused)
        {
            AttackRuntime.Cancel();
            _monsterMove.Stop();
            _motionPresentation?.Clear();
        }

        _monsterFSM.enabled = !paused;
    }

    /// <summary>
    /// 전투 종료 시 공격·AI·접촉 피해와 피해 수신 중단
    /// 재생성 시 기존 Initialize에서 권한 복구
    /// </summary>
    public void StopCombat()
    {
        _motionPresentation?.Clear();
        _hasSimulationAuthority = false;
        AttackRuntime?.Cancel();
        _monsterFSM.enabled = false;
        _statusEffectHandler.Clear();
        _statusEffectHandler.enabled = false;
        SetContactDamageEnabled(false);
        SetCombatCollidersEnabled(false);
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.simulated = false;
    }

    /// <summary>
    /// 수평 이동 정지
    /// </summary>
    public void StopMovement()
    {
        _monsterMove.Stop();
    }

    /// <summary>
    /// 현재 대상을 해제
    /// </summary>
    public void ClearTarget()
    {
        _target = null;
    }

    /// <summary>
    /// 진행 중인 상태 공격 취소
    /// </summary>
    public void CancelAttack()
    {
        AttackRuntime.Cancel();
        _motionPresentation?.CancelWindup();
    }

    private void OnDisable() => _motionPresentation?.Clear();

}
