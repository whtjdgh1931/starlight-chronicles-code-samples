using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크 보스 초기화·체력·애니메이션 동기화·페이즈 전환·서버 제거
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkBossController : NetworkBehaviour
{
    [SerializeField] private BossMonster _boss;
    [SerializeField] private MonsterChargeAction _chargeAction;
    private readonly NetworkVariable<int> _chargeDirection = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [SerializeField, Min(1)] private int _bossId;

    private readonly NetworkVariable<Vector2> _health = new(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>
    /// 재생 순번으로 중복·역순 수신을 구분하는 보스 표시 상태
    /// </summary>
    private struct PresentationSnapshot : INetworkSerializable, IEquatable<PresentationSnapshot>
    {
        public MonsterBase.AnimationState Animation;
        public float FacingDirection;
        public ulong Revision;
        public ulong AnimationRevision;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Animation);
            serializer.SerializeValue(ref FacingDirection);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref AnimationRevision);
        }

        public bool Equals(PresentationSnapshot other)
        {
            return Animation == other.Animation && FacingDirection == other.FacingDirection &&
                Revision == other.Revision && AnimationRevision == other.AnimationRevision;
        }
    }

    private readonly NetworkVariable<PresentationSnapshot> _presentation = new(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private ulong _lastPresentationRevision;
    private ulong _lastAnimationRevision;
    private BossMonster.Phase _phase;

    private IReadOnlyList<BossPhaseAttackData> _secondPhaseAttacks;
    private Coroutine _phaseTransition;
    private bool _isInitialized;

    public BossMonster Boss => _boss;

    /// <summary>
    /// 현재 화면의 네트워크 보스 제거 알림
    /// HUD 및 보스 추적 연결 해제용
    /// </summary>
    public event Action<NetworkBossController> Removed;

    /// <summary>
    /// 생성 전 필수 보스 참조와 초기 전투 데이터 확인
    /// </summary>
    public void ValidateConfiguration(DataManager dataManager)
    {
        if (_boss == null || _chargeAction == null)
        {
            throw new MissingReferenceException($"{name}: BossMonster·MonsterChargeAction 참조 필요");
        }

        if (!dataManager.IsDataLoaded ||
            !dataManager.MonsterTable.TryGetValue(_bossId, out MonsterData monsterData) ||
            !dataManager.BossTable.TryGetValue(_bossId, out BossData bossData) ||
            monsterData.ID != bossData.BossId ||
            dataManager.GetBossPhaseAttacks(_bossId, (int)BossMonster.Phase.First).Count == 0)
        {
            throw new InvalidOperationException($"{name}: 보스 데이터와 초기 패턴 준비 필요 ID: {_bossId}");
        }

        if (dataManager.GetBossPhaseAttacks(_bossId, (int)BossMonster.Phase.Second).Count == 0)
        {
            throw new InvalidOperationException($"{name}: 2페이즈 공격 데이터 필요 ID: {_bossId}");
        }
    }

    public override void OnNetworkSpawn()
    {
        _isInitialized = false;
        _lastPresentationRevision = 0;
        _lastAnimationRevision = 0;
        DataManager dataManager = GameManager.Instance.DataManager;
        ValidateConfiguration(dataManager);

        NetworkBossEncounterController encounter =
            FindAnyObjectByType<NetworkBossEncounterController>(FindObjectsInactive.Exclude);

        if (encounter == null)
        {
            throw new MissingReferenceException($"{name}: NetworkBossEncounterController 참조 필요");
        }

        MonsterData monsterData = dataManager.MonsterTable[_bossId];
        BossData bossData = dataManager.BossTable[_bossId];

        // NGO에서 직접 생성한 보스의 투사체 발사기 연결
        MapManager.Instance.ObjectPool.Bind(gameObject);
        GetComponentInChildren<BossFanProjectileEmitter>(true).SetNetworkPool(
            NetworkManager.GetComponent<NetworkSessionController>().ObjectPool);

        if (IsServer)
        {
            InitializeServer(dataManager, monsterData, bossData);
        }
        else
        {
            InitializeClient(monsterData, bossData);
        }

        _isInitialized = true;
        if (!IsServer)
        {
            ApplyPresentation(_presentation.Value);
            _chargeAction.ApplyReplicaPresentation(_chargeDirection.Value, emitLaunchDust: false);
        }
        encounter.BindBoss(this);
    }

    /// <summary>
    /// 서버 보스의 전투 데이터와 초기 패턴 구성
    /// </summary>
    private void InitializeServer(
        DataManager dataManager,
        MonsterData monsterData,
        BossData bossData)
    {
        var initialAttacks = dataManager.GetBossPhaseAttacks(_bossId, (int)BossMonster.Phase.First);
        _secondPhaseAttacks = dataManager.GetBossPhaseAttacks(_bossId, (int)BossMonster.Phase.Second);

        _boss.Initialize(monsterData, bossData, dataManager.MonsterAttackTable, initialAttacks);

        // 전투 시작 전 2페이즈 패턴 연결 확인 후 초기 패턴 복원
        _boss.ApplyPhaseAttacks(_secondPhaseAttacks);
        _boss.ApplyPhaseAttacks(initialAttacks);

        _chargeDirection.Value = 0;
        _chargeAction.ChargeStarted += HandleChargeStarted;
        _chargeAction.ChargeStopped += HandleChargeStopped;
        _phase = BossMonster.Phase.First;
        _health.Value = new Vector2(_boss.CurrentHp, _boss.MaxHp);
        _boss.HealthChanged += HandleHealthChanged;
        _boss.DamageImpactOccurred += HandleDamageImpact;
        _boss.DeathCompleted += HandleDeathCompleted;
        _boss.AnimationPlayed += HandleServerAnimationPlayed;
        _boss.FacingDirectionChanged += HandleServerFacingDirectionChanged;
        PublishPresentation(_boss.CurrentAnimation, _boss.FacingDirection, true);
    }

    /// <summary>
    /// 클라이언트 보스 표시와 서버 체력 적용
    /// </summary>
    private void InitializeClient(MonsterData monsterData, BossData bossData)
    {
        _boss.InitializeReplica(monsterData, bossData);
        _chargeAction.InitializeReplicaPresentation();
        _chargeDirection.OnValueChanged += HandleReplicatedChargeChanged;

        _health.OnValueChanged += HandleReplicatedHealthChanged;
        _presentation.OnValueChanged += HandleReplicatedPresentationChanged;
        HandleReplicatedHealthChanged(_health.Value, _health.Value);
    }

    private void HandleDamageImpact(Vector3 position, bool fromLeft, Player attacker)
    {
        if (!IsSpawned || NetworkManager.ShutdownInProgress)
        {
            return;
        }

        ulong attackerClientId = attacker.GetComponent<NetworkObject>().OwnerClientId;
        PlayDamageImpactRpc(position, fromLeft, attackerClientId);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayDamageImpactRpc(Vector3 position, bool fromLeft, ulong attackerClientId)
    {
        bool isLocalSound = attackerClientId == NetworkManager.LocalClientId;
        MapManager.Instance.DamageImpactPresentation.PlayMonsterImpact(position, fromLeft, isLocalSound);
    }

    private void HandleServerAnimationPlayed(MonsterBase.AnimationState animation)
    {
        if (!IsServer || !IsSpawned) { return; }
        PublishPresentation(animation, _boss.FacingDirection, true);
    }

    private void HandleServerFacingDirectionChanged(float direction)
    {
        if (!IsServer || !IsSpawned) { return; }
        PublishPresentation(_boss.CurrentAnimation, direction, false);
    }

    /// <summary>
    /// 현재 표시 상태 보관과 연속 애니메이션 재생 신호 전달
    /// </summary>
    private void PublishPresentation(MonsterBase.AnimationState animation, float direction, bool animationChanged)
    {
        PresentationSnapshot snapshot = _presentation.Value;
        snapshot.Revision++;
        snapshot.Animation = animation;
        snapshot.FacingDirection = direction;
        if (animationChanged) { snapshot.AnimationRevision = snapshot.Revision; }
        _presentation.Value = snapshot;

        // 변수 갱신 사이에 발생한 같은 종류의 연속 재생도 전달
        if (_isInitialized && animationChanged) { PlayAnimationRpc(snapshot); }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayAnimationRpc(PresentationSnapshot snapshot)
    {
        ApplyPresentation(snapshot);
    }

    private void HandleReplicatedPresentationChanged(PresentationSnapshot previous, PresentationSnapshot current)
    {
        ApplyPresentation(current);
    }

    /// <summary>
    /// 호스트 중복 재생과 오래된 신호 제외, 클라이언트 표시만 갱신
    /// </summary>
    private void ApplyPresentation(PresentationSnapshot snapshot)
    {
        if (IsServer || !_isInitialized || snapshot.Revision <= _lastPresentationRevision)
        {
            return;
        }

        _lastPresentationRevision = snapshot.Revision;
        _boss.FaceDirection(snapshot.FacingDirection);
        if (snapshot.AnimationRevision == _lastAnimationRevision) { return; }
        _lastAnimationRevision = snapshot.AnimationRevision;

        switch (snapshot.Animation)
        {
            case MonsterBase.AnimationState.Idle:
                _boss.PlayIdleAnimation();
                break;
            case MonsterBase.AnimationState.Move:
                _boss.PlayMoveAnimation();
                break;
            case MonsterBase.AnimationState.Attack:
                _boss.PlayAttackAnimation();
                break;
            case MonsterBase.AnimationState.Hit:
                _boss.PlayHitAnimation();
                break;
            case MonsterBase.AnimationState.Die:
                _boss.PlayDieAnimation();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(snapshot), snapshot.Animation, "지원하지 않는 보스 애니메이션");
        }
    }

    /// <summary>
    /// 체력 동기화와 서버 페이즈 전환 시작
    /// </summary>
    private void HandleHealthChanged(float currentHp, float maxHp)
    {
        _health.Value = new Vector2(currentHp, maxHp);

        if (currentHp <= 0f)
        {
            CancelPhaseTransition();
            return;
        }

        if (_phase != BossMonster.Phase.First || _phaseTransition != null)
        {
            return;
        }

        float transitionHp = maxHp * _boss.BossData.PhaseChangeHpRatio;
        if (currentHp <= transitionHp)
        {
            _phaseTransition = StartCoroutine(ChangeToSecondPhase());
        }
    }

    /// <summary>
    /// 전환 시간 대기 후 2페이즈 공격 적용
    /// </summary>
    private IEnumerator ChangeToSecondPhase()
    {
        _boss.SetBehaviourPaused(true);

        yield return new WaitForSecondsRealtime(_boss.BossData.PhaseTransitionDuration);

        _phaseTransition = null;

        // 사망·전투 종료·네트워크 제거 이후 행동 재개 차단
        if (!IsSpawned || !_boss.CanDealDamage)
        {
            yield break;
        }

        _boss.ApplyPhaseAttacks(_secondPhaseAttacks);
        _phase = BossMonster.Phase.Second;
        _boss.SetBehaviourPaused(false);
    }

    /// <summary>
    /// 진행 중인 페이즈 전환 대기 취소
    /// </summary>
    private void CancelPhaseTransition()
    {
        if (_phaseTransition == null)
        {
            return;
        }

        StopCoroutine(_phaseTransition);
        _phaseTransition = null;
    }

    private void HandleReplicatedHealthChanged(Vector2 previousHealth, Vector2 health)
    {
        if (health.y <= 0f) { return; }
        _boss.ApplyReplicatedHealth(health.x, health.y);
    }

    /// <summary>
    /// 서버 사망 연출 완료 후 네트워크 보스 제거
    /// </summary>
    private void HandleDeathCompleted(MonsterBase monster)
    {
        NetworkObject.Despawn(true);
    }

    private void HandleChargeStarted(float direction)
    {
        _chargeDirection.Value = direction < 0f ? -1 : 1;
    }

    private void HandleChargeStopped()
    {
        _chargeDirection.Value = 0;
    }

    private void HandleReplicatedChargeChanged(int previous, int current)
    {
        if (IsServer || !_isInitialized) { return; }
        _chargeAction.ApplyReplicaPresentation(current);
    }

    public override void OnNetworkDespawn()
    {
        _chargeAction.ChargeStarted -= HandleChargeStarted;
        _chargeAction.ChargeStopped -= HandleChargeStopped;
        _chargeDirection.OnValueChanged -= HandleReplicatedChargeChanged;
        CancelPhaseTransition();
        _boss.AnimationPlayed -= HandleServerAnimationPlayed;
        _boss.FacingDirectionChanged -= HandleServerFacingDirectionChanged;
        _presentation.OnValueChanged -= HandleReplicatedPresentationChanged;

        if (!_isInitialized)
        {
            return;
        }

        _isInitialized = false;

        // 보스 참조가 유효한 제거 시점에 화면 연결 정리
        Removed?.Invoke(this);

        _boss.HealthChanged -= HandleHealthChanged;
        _boss.DamageImpactOccurred -= HandleDamageImpact;
        _boss.DeathCompleted -= HandleDeathCompleted;
        _health.OnValueChanged -= HandleReplicatedHealthChanged;
        _chargeAction.ApplyReplicaPresentation(0);
        _boss.StopCombat();
        Removed = null;
    }
}
