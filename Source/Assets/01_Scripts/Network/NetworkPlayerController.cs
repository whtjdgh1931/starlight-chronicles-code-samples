using Assets.HeroEditor4D.Common.Scripts.Enums;
using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 소유자 이동 입력 전달과 서버 플레이어 물리 제어
/// </summary>
public sealed class NetworkPlayerController : NetworkBehaviour
{
    /// <summary>
    /// 서버 캐릭터의 접속 준비와 씬 이동 단계
    /// </summary>
    private enum ServerGameplayState
    {
        WaitingForOwner,
        Active,
        SceneTransition
    }

    private enum AttackIntent : byte { None, Basic, Skill }

    /// <summary>
    /// 표시되는 장비 슬롯의 서버 확정 상태, ID 0은 빈 슬롯
    /// </summary>
    private struct EquipmentAppearance : INetworkSerializable, IEquatable<EquipmentAppearance>
    {
        public int WeaponId;
        public int HelmetId;
        public int ArmorId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref WeaponId);
            serializer.SerializeValue(ref HelmetId);
            serializer.SerializeValue(ref ArmorId);
        }

        public bool Equals(EquipmentAppearance other)
        {
            return WeaponId == other.WeaponId && HelmetId == other.HelmetId && ArmorId == other.ArmorId;
        }
    }

    private readonly NetworkVariable<EquipmentAppearance> _equipmentAppearance = new(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField, Min(0.02f)] private float _inputResendInterval = 0.1f;
    private const int MissedInputIntervalsBeforeStop = 3;
    private Vector2 _ownerMoveInput;
    private double _nextInputSendTime;
    private double _lastServerInputTime;
    private AttackIntent _pendingAttack;
    private int _pendingSkillSlot;

    [SerializeField] private Player _player;
    [SerializeField] private NetworkPlayerLifeController _life;
    [SerializeField] private PlayerHealthBar _healthBar;
    [SerializeField] private PlayerMove _playerMove;
    [SerializeField] private Rigidbody2D _rigidbody;
    [SerializeField] private NetworkTransform _networkTransform;
    [SerializeField, Range(0.01f, 1f), Tooltip("참가자 본인의 위치 보간 시간(초). 기존 값 이상이면 기본 설정 사용")]
    private float _ownerPositionInterpolationTime = 0.04f;
    [SerializeField] private PlayerVisual _playerVisual;
    [SerializeField] private bool _playBasicAttackWindup = true;
    [SerializeField] private PlayerSkillController _playerSkillController;
    [SerializeField, Min(0f)] private float _minimumVisualSpeed = 0.1f;

    // 서버에서 결정한 시선 방향과 공중 방향 유지
    private readonly NetworkVariable<int> _sightDirection = new(1);

    // 서버에서 결정한 이동 애니메이션 상태
    private readonly NetworkVariable<CharacterState> _movementState = new(CharacterState.Idle);

    // 현재·최대 체력의 일괄 공유, 서버에서만 변경
    private readonly NetworkVariable<Vector2> _health = new(
        Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// 늦게 연결되는 HP 표시 UI의 초기값: X 현재 체력, Y 최대 체력
    /// </summary>
    public Vector2 ReplicatedHealth => _health.Value;

    // 서버 시각 기준 무적 종료 시각, 늦은 수신 시 남은 시간만 점멸
    private readonly NetworkVariable<double> _invincibilityEndsAt = new(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkSessionController _session;
    private Vector2 _moveInput;
    private CharacterState _visualState;
    private bool _jumpRequested;
    private ServerGameplayState _serverGameplayState;
    private bool _isOwnerInputSubscribed;
    private bool _isVisualReady;
    private float? _originalPositionInterpolationTime;

    public Player Character => _player;

    /// <summary>
    /// 서버에서 확인하는 캐릭터의 공동 씬 이동 가능 여부
    /// 접속 준비 완료·생존·기존 씬 이동 종료 조건
    /// </summary>
    public bool CanStartSceneTransition =>
        IsServer && IsSpawned && CanProcessGameplay && !_player.IsDead;

    private bool CanProcessGameplay => _serverGameplayState == ServerGameplayState.Active && _life.CanAct;

    /// <summary>
    /// 현재 소유자의 온라인 소비 아이템 요청 가능 여부
    /// 실제 사용 승인은 서버 상태로 최종 판정
    /// </summary>
    public bool CanRequestConsumableUse => IsSpawned && IsOwner && _isOwnerInputSubscribed && _life.CanAct;

    /// <summary>
    /// 필수 프리팹 참조의 초기화 경계 검증
    /// </summary>
    public void ValidateReferences()
    {
        if (_player == null || _playerMove == null || _rigidbody == null ||
            _playerVisual == null || _playerSkillController == null ||
            _networkTransform == null || _healthBar == null || _life == null || GetComponent<NetworkObject>() == null)
        {
            throw new MissingReferenceException($"{name}: 네트워크 플레이어 필수 참조 필요");
        }
    }

    /// <summary>
    /// 서버 요청 처리에서 네트워크 생성 전에 상태 초기화
    /// 소유자의 준비 확인 전까지 물리 시뮬레이션 중지
    /// </summary>
    public void InitializeServerCharacter(DataManager dataManager, SaveData characterState)
    {
        _player.ResetNetworkCharacter();
        _player.MarkNetworkControlled();
        _player.enabled = false;
        _playerMove.enabled = false;
        _rigidbody.simulated = false;
        _player.InitializeFromState(dataManager, characterState);
        if (_player.IsDead)
        {
            throw new InvalidOperationException("살아 있는 캐릭터만 대기실 입장 가능");
        }
    }

    public override void OnNetworkSpawn()
    {
        _session = NetworkManager.GetComponent<NetworkSessionController>();
        try
        {
            ValidateReferences();
            ResetSpawnState();
            ApplyOwnerPositionInterpolation();
            if (!IsServer)
            {
                _player.ResetNetworkCharacter();
            }
            // 모든 컴퓨터에서 새 네트워크 캐릭터의 연출 풀 연결
            MapManager.Instance.ObjectPool.Bind(gameObject);
            GetComponent<PlayerProjectileSkillExecutor>().SetNetworkPool(_session.ObjectPool);
            _player.MarkNetworkControlled();
            _player.enabled = false;
            _playerMove.enabled = false;
            _rigidbody.simulated = false;
            _visualState = CharacterState.Idle;

            if (IsServer)
            {
                if (!_player.CanCaptureCharacterState)
                {
                    throw new InvalidOperationException("네트워크 생성 전에 서버 캐릭터 초기화 필요");
                }

                _invincibilityEndsAt.Value = 0d;
                _player.InvincibilityStarted += HandleServerInvincibilityStarted;
                _player.DamageImpactOccurred += HandleDamageImpact;
                PublishEquipmentAppearance();
                _playerSkillController.ActionStarted += HandleSkillStarted;
                _playerSkillController.CooldownStarted += HandleSkillCooldownStarted;
                _player.EquipmentChanged += HandleServerEquipmentChanged;
                _player.PlayerStat.HpChanged += HandleServerHpChanged;
                _player.PlayerStat.MpChanged += HandleServerMpChanged;
                HandleServerHpChanged(_player.PlayerStat.CurrentHp, _player.PlayerStat.GetTotalStat(StatType.MaxHp));
            }
            else if (!IsOwner)
            {
                _playerVisual.Initialize();
            }

            _session.Roster.Register(OwnerClientId, _player);

            if (IsOwner)
            {
                if (!_session.CompleteLocalCharacterJoin(this))
                {
                    return;
                }
            }

            if (!IsServer)
            {
                _equipmentAppearance.OnValueChanged += HandleReplicatedEquipmentChanged;
                ApplyEquipmentAppearance(_equipmentAppearance.Value);
                _invincibilityEndsAt.OnValueChanged += HandleReplicatedInvincibilityChanged;
                _health.OnValueChanged += HandleReplicatedHpChanged;
                HandleReplicatedHpChanged(_health.Value, _health.Value);
            }

            _player.BindNetworkLifeController(this);
            _life.Initialize(_player);

            if (IsOwner)
            {
                ConfirmOwnerReadyRpc();
            }

            _healthBar.Initialize(IsClient && !IsOwner, _health.Value);
            _isVisualReady = true;
            HandleReplicatedInvincibilityChanged(0d, _invincibilityEndsAt.Value);
        }
        catch (Exception exception)
        {
            _session.HandlePlayerInitializationFailure(this, exception);
        }
    }

    /// <summary>참가자 본인의 위치 보간 시간만 단축</summary>
    private void ApplyOwnerPositionInterpolation()
    {
        if (!IsClient || !IsOwner || IsServer ||
            _networkTransform.PositionMaxInterpolationTime <= _ownerPositionInterpolationTime) { return; }

        _originalPositionInterpolationTime = _networkTransform.PositionMaxInterpolationTime;
        _networkTransform.PositionMaxInterpolationTime = _ownerPositionInterpolationTime;
    }

    /// <summary>네트워크 객체 반환 시 기존 보간 설정 복원</summary>
    private void RestorePositionInterpolation()
    {
        if (_originalPositionInterpolationTime is not float originalTime) { return; }

        _networkTransform.PositionMaxInterpolationTime = originalTime;
        _originalPositionInterpolationTime = null;
    }

    /// <summary>
    /// 공동 씬 이동 중 서버 캐릭터의 조작·물리 잠금
    /// 접속 준비 완료 후 서버의 씬 이동 담당자에서 호출
    /// </summary>
    public void SetServerSceneTransitionLocked(bool isLocked)
    {
        if (!IsServer || _serverGameplayState == ServerGameplayState.WaitingForOwner)
        {
            throw new InvalidOperationException("접속 준비가 완료된 서버 캐릭터만 씬 이동 잠금 변경 가능");
        }

        ServerGameplayState nextState = isLocked
            ? ServerGameplayState.SceneTransition
            : ServerGameplayState.Active;
        if (_serverGameplayState == nextState)
        {
            return;
        }

        _serverGameplayState = nextState;
        ResetServerInput();

        if (isLocked)
        {
            StopServerActions();
        }

        bool canSimulate = !isLocked && !_player.IsDead;
        _playerMove.enabled = false;
        _rigidbody.simulated = canSimulate;
        _player.SetNetworkCombatReady(canSimulate);
        SetOwnerInputEnabledRpc(canSimulate);
    }

    /// <summary>
    /// 씬 이동 중 서버 캐릭터의 도착 위치 배치
    /// 이동 계산 위치·물리 위치·네트워크 위치 동기화
    /// </summary>
    public void PlaceAtServerMapSpawn(Vector3 position)
    {
        if (!IsServer || !IsSpawned ||
            _serverGameplayState != ServerGameplayState.SceneTransition)
        {
            throw new InvalidOperationException("씬 이동 중인 서버 캐릭터만 도착 위치 배치 가능");
        }

        // 포털의 깊이와 무관하게 캐릭터의 기존 Z 위치 유지
        position.z = transform.position.z;

        _player.PlaceAtMapSpawn(position);
        _networkTransform.Teleport(position, transform.rotation, transform.localScale);
    }

    /// <summary>
    /// 대기실 보스 포털에서 호스트 소유자의 출발 요청
    /// </summary>
    public async System.Threading.Tasks.Task<bool> TryStartBossEncounterFromPortalAsync(string targetScene)
    {
        return IsOwner && IsHost && targetScene == _session.BossScene.ToString() && await _session.TryStartBossEncounterAsync();
    }

    /// <summary>
    /// 서버 배치 완료 후 해당 소유자의 맵 준비 요청
    /// </summary>
    public void PrepareBossMapOwner()
    {
        if (!IsServer || _serverGameplayState != ServerGameplayState.SceneTransition)
        {
            throw new InvalidOperationException("씬 이동 중인 서버 캐릭터만 소유자 준비 요청 가능");
        }
        PrepareBossMapOwnerRpc();
    }

    /// <summary>
    /// 서버 상태 승인과 기존 로컬 입력 해제 후 소유자 입력 연결
    /// </summary>
    public void BeginOwnerInput()
    {
        if (!IsSpawned || !IsOwner || !_player.CanCaptureCharacterState)
        {
            throw new InvalidOperationException("스폰된 소유자만 입력 연결 가능");
        }

        if (_isOwnerInputSubscribed)
        {
            return;
        }

        EventBus.OnMoveInput += HandleMoveInput;
        EventBus.OnJumpInput += HandleJumpInput;
        EventBus.OnBasicAttackInput += HandleBasicAttackInput;
        EventBus.OnSkillSlotInput += HandleSkillSlotInput;
        EventBus.OnConsumableUseRequested += HandleConsumableUseRequested;
        _isOwnerInputSubscribed = true;
    }

    /// <summary>
    /// 서버 사망·부활·전투 종료에 따른 물리와 입력 변경
    /// 씬 전환 준비 RPC와 분리된 생명 상태 경로
    /// </summary>
    public void SetLifeControlEnabled(bool isEnabled)
    {
        if (!IsServer)
        {
            throw new InvalidOperationException("서버만 생명 상태의 조작 변경 가능");
        }
        ResetServerInput();
        _ownerMoveInput = Vector2.zero;
        StopServerActions();
        _rigidbody.simulated = isEnabled;
        _player.SetNetworkCombatReady(isEnabled);
        SetLifeInputRpc(isEnabled);
    }

    /// <summary>
    /// 서버에 남은 이동·점프·공격 요청 초기화
    /// 소유자 입력은 각 전환 경로에서 별도 처리
    /// </summary>
    private void ResetServerInput()
    {
        _moveInput = Vector2.zero;
        _jumpRequested = false;
        _pendingAttack = AttackIntent.None;
        _playerMove.InputMoveVector(Vector2.zero);
    }

    /// <summary>
    /// 서버 캐릭터의 공격·임시 이동·물리 속도 중단
    /// 물리 활성화와 소유자 입력 RPC는 호출 경로에서 처리
    /// </summary>
    private void StopServerActions()
    {
        _player.CancelBasicAttack();
        _playerSkillController.Cancel();
        _playerMove.CancelTransientMovement();
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.angularVelocity = 0f;
        _movementState.Value = CharacterState.Idle;
    }

    /// <summary>
    /// 서버 생명 상태에서 확정한 위치의 NGO 순간 이동
    /// </summary>
    public void TeleportForLifeState(Vector3 position)
    {
        if (!IsServer)
        {
            throw new InvalidOperationException("서버만 생명 상태 위치 변경 가능");
        }
        _networkTransform.Teleport(position, transform.rotation, transform.localScale);
    }

    /// <summary>
    /// 서버 이동 결과 공유와 각 화면의 이동 연출 반영
    /// </summary>
    private void LateUpdate()
    {
        if (!IsSpawned || !_isVisualReady)
        {
            return;
        }

        if (IsServer && CanProcessGameplay)
        {
            _sightDirection.Value = _playerMove.SightDirection;

            // 점프 직후 접지 정보 갱신 전에도 상승 상태 우선
            bool isAirborne = !_playerMove.IsGrounded ||
                _rigidbody.linearVelocityY > Constants.FLOAT_CheckFloat;

            if (isAirborne)
            {
                _movementState.Value = CharacterState.Jump;
            }
            else
            {
                bool isMoving = Mathf.Abs(_rigidbody.linearVelocityX) > _minimumVisualSpeed;
                _movementState.Value = isMoving ? CharacterState.Run : CharacterState.Idle;
            }
        }

        _playerVisual.SetDirection(_sightDirection.Value > 0 ? Vector2.right : Vector2.left);

        CharacterState nextState = _life.IsAlive ? _movementState.Value : CharacterState.Death;
        if (_visualState != nextState)
        {
            _visualState = nextState;
            _playerVisual.SetState(nextState);
        }
    }

    private void Update()
    {
        if (IsOwner && _isOwnerInputSubscribed && _ownerMoveInput != Vector2.zero &&
            Time.timeAsDouble >= _nextInputSendTime)
        {
            SendInput(false, AttackIntent.None);
        }
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer || !CanProcessGameplay)
        {
            return;
        }

        if (_player.CheckIsStun())
        {
            _moveInput = Vector2.zero;
            _jumpRequested = false;
            _pendingAttack = AttackIntent.None;
        }
        if (Time.timeAsDouble - _lastServerInputTime > _inputResendInterval * MissedInputIntervalsBeforeStop)
        {
            _moveInput = Vector2.zero;
        }

        _playerMove.InputMoveVector(_moveInput);
        _playerMove.SimulateMovement();

        if (_jumpRequested)
        {
            _jumpRequested = false;
            _playerMove.Jump();
        }

        // 이동 입력·시선 갱신 이후 서버 공격 의도 소비
        AttackIntent attack = _pendingAttack;
        _pendingAttack = AttackIntent.None;
        if (attack == AttackIntent.Basic)
        {
            ExecuteBasicAttack();
        }
        else if (attack == AttackIntent.Skill)
        {
            ExecuteSkill(_pendingSkillSlot);
        }
    }

    /// <summary>
    /// 재생성된 네트워크 캐릭터의 이전 입력과 표시 동기화 상태 초기화
    /// 서버 캐릭터 데이터와 클라이언트 수신 체력은 유지
    /// </summary>
    private void ResetSpawnState()
    {
        EndOwnerInput();
        _nextInputSendTime = 0d;
        _lastServerInputTime = Time.timeAsDouble;
        _pendingSkillSlot = 0;
        _serverGameplayState = ServerGameplayState.WaitingForOwner;
        _isVisualReady = false;
        if (IsServer)
        {
            _sightDirection.Value = 1;
            _movementState.Value = CharacterState.Idle;
        }
    }

    /// <summary>
    /// 서버 장비 변경에 따른 외형 상태 갱신
    /// </summary>
    private void HandleServerEquipmentChanged(EquipSlot slot, InventoryItemData item)
    {
        PublishEquipmentAppearance();
    }

    /// <summary>
    /// 현재 장착 정보를 최초 생성 상태와 후속 변경에 반영
    /// 서버 초기화 또는 서버 장비 변경 경로에서만 호출
    /// </summary>
    private void PublishEquipmentAppearance()
    {
        _equipmentAppearance.Value = new EquipmentAppearance
        {
            WeaponId = GetEquippedItemId(EquipSlot.Weapon),
            HelmetId = GetEquippedItemId(EquipSlot.Helmet),
            ArmorId = GetEquippedItemId(EquipSlot.Armor)
        };
    }

    private int GetEquippedItemId(EquipSlot slot)
    {
        return _player.TryGetEquippedItem(slot, out InventoryItemData item) ? item.ItemData.ID : 0;
    }

    private void HandleReplicatedEquipmentChanged(EquipmentAppearance previous, EquipmentAppearance current)
    {
        ApplyEquipmentAppearance(current);
    }

    private void ApplyEquipmentAppearance(EquipmentAppearance appearance)
    {
        ApplyEquipmentSlot(EquipSlot.Weapon, appearance.WeaponId);
        ApplyEquipmentSlot(EquipSlot.Helmet, appearance.HelmetId);
        ApplyEquipmentSlot(EquipSlot.Armor, appearance.ArmorId);
    }

    private void ApplyEquipmentSlot(EquipSlot slot, int itemId)
    {
        if (itemId == 0)
        {
            _playerVisual.ApplyEquipmentAppearance(slot, null);
            return;
        }

        if (!GameManager.Instance.DataManager.ItemTable.TryGetValue(itemId, out ItemData item) || item.GetEquipSlot() != slot)
        {
            throw new InvalidOperationException($"{name}: 동기화된 장비 데이터 불일치, 슬롯: {slot}, ID: {itemId}");
        }

        _playerVisual.ApplyEquipmentAppearance(slot, item);
    }

    private void SendInput(bool jump, AttackIntent attack, int slotIndex = 0)
    {
        SubmitInputRpc(_ownerMoveInput, jump, attack, slotIndex);
        _nextInputSendTime = Time.timeAsDouble + _inputResendInterval;
    }

    /// <summary>
    /// 소유자 요청의 서버 기본 공격 실행
    /// 공격이 시작된 경우에만 연출 공유
    /// </summary>
    private void ExecuteBasicAttack()
    {
        if (!CanProcessGameplay || !_player.TryBasicAttack())
        {
            return;
        }

        PlayActionRpc(PlayerActionType.BasicAttack, 0f);
    }

    /// <summary>
    /// 소유자 스킬 요청의 서버 실행과 승인된 공격 애니메이션 공유
    /// MP·쿨다운·중복 실행 검사는 기존 스킬 담당자에서 처리
    /// </summary>
    private void ExecuteSkill(int slotIndex)
    {
        if (!CanProcessGameplay || _player.CheckIsStun())
        {
            return;
        }

        _playerSkillController.TryExecuteSlot(slotIndex, _player);
    }

    /// <summary>
    /// 서버가 승인한 스킬 쿨타임을 해당 소유자에게 전달
    /// </summary>
    private void HandleSkillCooldownStarted(int slotIndex, float duration)
    {
        SyncSkillCooldownRpc(slotIndex, NetworkManager.ServerTime.Time + duration);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void SyncSkillCooldownRpc(int slotIndex, double endsAt)
    {
        if (!IsServer)
        {
            _playerSkillController.ApplyCooldown(slotIndex, (float)Math.Max(0d, endsAt - NetworkManager.ServerTime.Time));
        }
    }

    private void HandleSkillStarted(PlayerSkillExecutionType executionType, float duration)
    {
        PlayActionRpc(PlayerVisual.GetSkillAction(executionType), duration);
    }

    /// <summary>
    /// 소유자 화면 준비 확인 후 서버 이동 허용
    /// 소유권 제한 RPC로 타인의 캐릭터 활성화 차단
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ConfirmOwnerReadyRpc()
    {
        if (_serverGameplayState != ServerGameplayState.WaitingForOwner || !_session.ConfirmServerCharacterJoin(this))
        {
            return;
        }

        _moveInput = Vector2.zero;
        _jumpRequested = false;
        _playerMove.enabled = false; // 서버 물리 갱신에서 명시적 이동 호출
        _rigidbody.simulated = true;
        _serverGameplayState = ServerGameplayState.Active;
        _player.SetNetworkCombatReady(true);
        HandleServerMpChanged(_player.PlayerStat.CurrentMp, _player.PlayerStat.GetTotalStat(StatType.MaxMp));
        _session.NotifyStateChanged();
    }

    /// <summary>
    /// 서버 체력 변경의 모든 참가자 공유
    /// </summary>
    private void HandleServerHpChanged(float currentHp, float maxHp)
    {
        _health.Value = new Vector2(currentHp, maxHp);
        _healthBar.SetHealth(currentHp, maxHp);
    }

    private void HandleReplicatedHpChanged(Vector2 previous, Vector2 current)
    {
        _player.PlayerStat.ApplyReplicatedHp(current.x, current.y);
        _healthBar.SetHealth(current.x, current.y);
    }

    /// <summary>
    /// 소유자 HUD 준비 이후 서버 마나 변경 전달
    /// 최초 준비 이전 값은 ConfirmOwnerReadyRpc에서 일괄 전달
    /// </summary>
    private void HandleServerMpChanged(float currentMp, float maxMp)
    {
        if (_serverGameplayState != ServerGameplayState.WaitingForOwner)
        {
            UpdateOwnerMpRpc(currentMp, maxMp);
        }
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void UpdateOwnerMpRpc(float currentMp, float maxMp)
    {
        // 호스트는 서버 능력치 이벤트로 이미 HUD 갱신
        if (!IsServer)
        {
            _player.PlayerStat.ApplyReplicatedMp(currentMp, maxMp);
        }
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void PrepareBossMapOwnerRpc()
    {
        try
        {
            _session.PrepareLocalBossMap(this);
            ConfirmBossMapReadyRpc();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            _session.FailSceneTransition("소유자 보스맵 준비 실패");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ConfirmBossMapReadyRpc()
    {
        if (_serverGameplayState == ServerGameplayState.SceneTransition)
        {
            _session.ConfirmBossMapReady(this);
        }
    }

    /// <summary>
    /// 서버 승인에 따른 소유자 입력 연결·해제
    /// 각 플레이어의 소유자 화면에서만 실행
    /// </summary>
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void SetOwnerInputEnabledRpc(bool inputEnabled)
    {
        if (inputEnabled)
        {
            try
            {
                _session.CompleteLocalBossMap();
                BeginOwnerInput();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                _session.FailSceneTransition("소유자 입력 재개 실패");
            }
        }
        else
        {
            EndOwnerInput();
        }
    }

    private void HandleMoveInput(Vector2 input)
    {
        _ownerMoveInput = input;
        SendInput(false, AttackIntent.None);
    }

    private void HandleJumpInput() => SendInput(true, AttackIntent.None);

    private void HandleBasicAttackInput()
    {
        if (_playBasicAttackWindup && IsSpawned && IsOwner && !IsServer &&
            _isVisualReady && _isOwnerInputSubscribed && _life.CanAct && !_player.CheckIsStun())
        {
            _playerVisual.PlayBasicAttackWindup();
        }
        SendInput(false, AttackIntent.Basic);
    }

    private void HandleSkillSlotInput(int slotIndex) => SendInput(false, AttackIntent.Skill, slotIndex);

    /// <summary>
    /// 소유자가 선택한 슬롯과 아이템 ID의 서버 사용 요청
    /// 회복량·소비 수량은 서버 데이터에서 결정
    /// </summary>
    private void HandleConsumableUseRequested(int slotIndex, InventoryItemData item)
    {
        if (!CanRequestConsumableUse) { return; }
        // UI 선택 데이터의 빈 슬롯·파괴 상태 경계 확인
        if (item == null || item.IsEmpty || item.IsDestroyed) { return; }
        RequestConsumableUseRpc(slotIndex, item.ItemData.ID);
    }

    /// <summary>
    /// 소유권이 확인된 요청의 서버 소비 서비스 실행
    /// 기존 능력치 이벤트로 HP·MP 동기화
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestConsumableUseRpc(int slotIndex, int itemId)
    {
        if (!CanProcessGameplay) { return; }
        if (!_player.TryUseConsumable(slotIndex, itemId, out int remainingCount)) { return; }
        double endsAt = NetworkManager.ServerTime.Time + _player.GetItemCooldownRemaining(itemId);
        SyncConsumableUseRpc(slotIndex, itemId, remainingCount, endsAt);
    }

    /// <summary>
    /// 서버 승인 수량·종료 시각의 소유자 반영
    /// 호스트는 이미 적용된 서버 상태 유지
    /// </summary>
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void SyncConsumableUseRpc(int slotIndex, int itemId, int remainingCount, double endsAt)
    {
        if (IsServer) { return; }
        float remaining = (float)Math.Max(0d, endsAt - NetworkManager.ServerTime.Time);
        _player.ApplyConsumableUse(slotIndex, itemId, remainingCount, remaining);
    }

    /// <summary>
    /// 위치·피해가 아닌 이동·점프·공격 의도 수신
    /// 실제 실행은 서버 물리 갱신에서 처리, 틱당 첫 공격만 보관
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitInputRpc(Vector2 input, bool jump, AttackIntent attack, int slotIndex)
    {
        if (!CanProcessGameplay || _player.CheckIsStun())
        {
            return;
        }
        if (float.IsNaN(input.x) || float.IsNaN(input.y) ||
            float.IsInfinity(input.x) || float.IsInfinity(input.y))
        {
            return;
        }

        _moveInput = new Vector2(Mathf.Clamp(input.x, -1f, 1f), Mathf.Clamp(input.y, -1f, 1f));
        _lastServerInputTime = Time.timeAsDouble;
        _jumpRequested |= jump;
        if (_pendingAttack == AttackIntent.None && attack is AttackIntent.Basic or AttackIntent.Skill)
        {
            _pendingAttack = attack;
            _pendingSkillSlot = slotIndex;
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayActionRpc(PlayerActionType action, float duration)
    {
        if (_life.CanAct)
        {
            _playerVisual.PlayAction(action, duration);
        }
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void SetLifeInputRpc(bool isEnabled)
    {
        if (isEnabled)
        {
            BeginOwnerInput();
        }
        else
        {
            EndOwnerInput();
        }
    }

    /// <summary>
    /// 소유자 입력 해제와 남은 서버 입력 초기화
    /// </summary>
    public void EndOwnerInput()
    {
        if (_isOwnerInputSubscribed)
        {
            EventBus.OnMoveInput -= HandleMoveInput;
            EventBus.OnJumpInput -= HandleJumpInput;
            EventBus.OnBasicAttackInput -= HandleBasicAttackInput;
            EventBus.OnSkillSlotInput -= HandleSkillSlotInput;
            EventBus.OnConsumableUseRequested -= HandleConsumableUseRequested;
            _isOwnerInputSubscribed = false;
            _playerVisual.StopBasicAttackWindup();
        }

        _moveInput = Vector2.zero;
        _ownerMoveInput = Vector2.zero;
        _jumpRequested = false;
        _pendingAttack = AttackIntent.None;
    }

    private void HandleDamageImpact(Vector3 position, bool fromLeft)
    {
        if (IsSpawned && !NetworkManager.ShutdownInProgress)
        {
            PlayDamageImpactRpc(position, fromLeft);
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayDamageImpactRpc(Vector3 position, bool fromLeft)
    {
        if (IsOwner && !IsServer) { _playerVisual.StopBasicAttackWindup(); }
        MapManager.Instance.DamageImpactPresentation.PlayPlayerImpact(position, fromLeft, IsOwner);
    }

    private void HandleServerInvincibilityStarted(float duration)
    {
        _invincibilityEndsAt.Value = NetworkManager.ServerTime.Time + duration;
    }

    private void HandleReplicatedInvincibilityChanged(double previous, double current)
    {
        if (IsServer || !_isVisualReady) { return; }
        float remaining = (float)Math.Max(0d, current - NetworkManager.ServerTime.Time);
        _playerVisual.PlayInvincibilityBlink(remaining);
    }

    public override void OnNetworkDespawn()
    {
        RestorePositionInterpolation();
        _player.InvincibilityStarted -= HandleServerInvincibilityStarted;
        _player.DamageImpactOccurred -= HandleDamageImpact;
        _invincibilityEndsAt.OnValueChanged -= HandleReplicatedInvincibilityChanged;
        _playerVisual.StopInvincibilityBlink();
        _player.EquipmentChanged -= HandleServerEquipmentChanged;
        _equipmentAppearance.OnValueChanged -= HandleReplicatedEquipmentChanged;
        _playerSkillController.ActionStarted -= HandleSkillStarted;
        _playerSkillController.CooldownStarted -= HandleSkillCooldownStarted;
        _player.PlayerStat.HpChanged -= HandleServerHpChanged;
        _player.PlayerStat.MpChanged -= HandleServerMpChanged;
        _health.OnValueChanged -= HandleReplicatedHpChanged;

        _playerMove.CancelTransientMovement();
        _player.SetNetworkCombatReady(false);
        _player.ClearNetworkLifeController();
        EndOwnerInput();
        _player.CancelBasicAttack();

        // 네트워크 객체 제거 시 다단 공격·돌진 실행 종료
        _playerSkillController.Cancel();

        _serverGameplayState = ServerGameplayState.WaitingForOwner;
        _isVisualReady = false;
        _playerMove.enabled = false;
        _rigidbody.simulated = false;

        // NetworkManager와 함께 파괴되는 종료 경계
        if (_session != null)
        {
            if (_session.Roster.TryGet(OwnerClientId, out Player registeredPlayer) && ReferenceEquals(registeredPlayer, _player))
            {
                _session.Roster.Remove(OwnerClientId, out _);
            }

            if (IsOwner)
            {
                _session.ReleaseLocalCharacterJoin(this);
            }
        }

        _session = null;
        _player.ResetNetworkCharacter();
    }
}
