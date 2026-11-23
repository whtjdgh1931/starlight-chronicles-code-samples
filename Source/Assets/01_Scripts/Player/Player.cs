using Assets.HeroEditor4D.Common.Scripts.Enums;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 기본 플레이어 부모 클래스, 하위 컴포넌트 제어
/// </summary>
public class Player : MonoBehaviour
{
    #region Field
    /// <summary>
    /// 플레이어 이동 관련 컴포넌트
    /// </summary>
    [SerializeField] private PlayerMove _playerMove;

    /// <summary>
    /// 플레이어 강제 이동 기능
    /// </summary>
    public IPlayerForcedMovement ForcedMovement => _playerMove;

    /// <summary>
    /// 플레이어 텔레포트나 강제이동을 위한 컴포넌트
    /// </summary>
    [SerializeField] private PlayerTeleport _playerTeleport;
    public PlayerTeleport PlayerTeleport => _playerTeleport;

    /// <summary>
    /// 플레이어의 공격을 관리하는 컴포넌트
    /// </summary>
    [SerializeField] private PlayerAttack _playerAttack;
    [SerializeField] private PlayerSkillController _playerSkillController;
    /// <summary>
    /// 플레이어 비쥬얼 관련 컴포넌트
    /// </summary>
    [SerializeField] private PlayerVisual _playerVisual;

    /// <summary>
    /// 플레이어 스탯 관련 컴포넌트
    /// </summary>
    [SerializeField] private PlayerStat _playerStat;
    public PlayerStat PlayerStat => _playerStat;

    [SerializeField] private PlayerInventory _playerInventory;
    public PlayerInventory PlayerInventory => _playerInventory;

    private readonly PlayerQuickSlots _quickSlots = new();
    public PlayerQuickSlots QuickSlots => _quickSlots;

    /// <summary>
    /// 저장된 스킬 ID에 해당하는 현재 스킬 슬롯 검색
    /// </summary>
    public bool TryFindSkillSlot(int skillId, out int skillIndex)
    {
        for (int i = 0; i < _playerSkillController.SkillSlotCount; i++)
        {
            if (_playerSkillController.TryGetSlotSkill(i, out SkillData skill) && skill.ID == skillId)
            {
                skillIndex = i;
                return true;
            }
        }
        skillIndex = -1;
        return false;
    }

    /// <summary>
    /// 보유 수량과 관계없는 소비 아이템 원본 데이터 조회
    /// </summary>
    public bool TryGetConsumableData(int itemId, out ItemData item)
    {
        return _dataManager.ItemTable.TryGetValue(itemId, out item) && item.GetItemType() == GameItemType.Consumable;
    }

    /// <summary>
    /// 현재 소유자의 소비 요청 가능 여부, 온라인 준비 전 입력 제외
    /// </summary>
    public bool CanUseItems => IsNetworkControlled
        ? _networkLifeController != null && _networkLifeController.CanRequestConsumableUse
        : CanUseLocalItems();

    /// <summary>
    /// 현재 캐릭터의 스킬 슬롯 데이터 조회
    /// </summary>
    public bool TryGetSlotSkill(int skillIndex, out SkillData skill)
    {
        return _playerSkillController.TryGetSlotSkill(skillIndex, out skill);
    }

    /// <summary>
    /// 스킬 재사용 대기 시간 조회
    /// </summary>
    public float GetSkillCooldownRemaining(int skillIndex)
    {
        return _playerSkillController.GetCooldownRemaining(skillIndex);
    }

    /// <summary>
    /// 소비 아이템 재사용 대기 시간 조회
    /// </summary>
    public float GetItemCooldownRemaining(int itemId)
    {
        return _consumableUseService.GetCooldownRemaining(itemId);
    }

    private readonly PlayerQuestProgress _questProgress = new();
    public PlayerQuestProgress QuestProgress => _questProgress;
    private QuestRewardService _questRewardService;

    // 보상 변경 이벤트에서 들어오는 중복 요청 차단
    private bool _isQuestRequestRunning;

    private GoldWallet _goldWallet;
    public GoldWallet GoldWallet => _goldWallet;

    [SerializeField] private PlayerPickupReceiver _pickupReceiver;

    [SerializeField] private PlayerSaveController _playerSaveController;

    private ConsumableUseService _consumableUseService;

    private EquipmentTransferService _equipmentTransferService;
    private IEnhancementRequestHandler _enhancementRequestHandler;
    private PlayerEquipment _playerEquipment;

    /// <summary>
    /// 해당 캐릭터의 확정된 장비 변경 알림
    /// </summary>
    public event System.Action<EquipSlot, InventoryItemData> EquipmentChanged;

    /// <summary>
    /// 초기화된 캐릭터의 현재 장착 아이템 조회
    /// 빈 슬롯은 false 반환
    /// </summary>
    public bool TryGetEquippedItem(EquipSlot slot, out InventoryItemData item)
    {
        return _playerEquipment.TryGetEquippedItem(slot, out item);
    }

    /// <summary>
    /// 공격 진행 여부
    /// </summary>
    [SerializeField] private bool _isAttacking;
    public bool IsAttacking { get { return _isAttacking; } set { _isAttacking = value; } }

    /// <summary>
    /// 무적 종료 시각
    /// </summary>
    private float _invincibleUntil;

    /// <summary>
    /// 서버에서 결정한 무적 지속시간의 표시 동기화 알림
    /// </summary>
    public event System.Action<float> InvincibilityStarted;
    public event System.Action<Vector3, bool> DamageImpactOccurred;

    /// <summary>
    /// 플레이어 사망 여부
    /// </summary>
    public bool IsDead => _playerStat.CurrentHp <= 0f;

    private bool _isNetworkJoinLocked;
    private bool _isDialogueOpen;
    private bool _inputOwnerBeforeJoin;
    private bool _isDeathHandled;

    public bool IsNetworkControlled { get; private set; }
    public bool IsNetworkJoinLocked => _isNetworkJoinLocked;
    /// <summary>
    /// 준비된 로컬 캐릭터의 지상 대화 가능 여부
    /// </summary>
    public bool CanTalk => _isLocalInputOwner && !IsNetworkControlled && CanCaptureCharacterState && !CheckIsStun() && _playerMove.IsGrounded;

    /// <summary>
    /// 입력 소유권을 유지하며 대화 전용 이동·전투 제한 설정
    /// </summary>
    public void SetDialogueOpen(bool isOpen)
    {
        _isDialogueOpen = isOpen;
        if (isOpen)
        {
            _playerMove.InputMoveVector(Vector2.zero);
            _playerMove.CancelTransientMovement();
            _currentState = CharacterState.Idle;
            _playerVisual.SetState(_currentState);
        }
    }

    public bool CanPrepareNetworkJoin => !IsNetworkControlled && CanCaptureCharacterState && !CheckIsStun() && _playerMove.IsGrounded;

    public event System.Action<Player> Died;

    /// <summary>
    /// 넉백 관련 코루틴
    /// </summary>
    private Coroutine _knockbackCoroutine;

    /// <summary>
    /// 현재 캐릭터 상태
    /// </summary>
    private CharacterState _currentState;

    [SerializeField] private MagnetPickupScanner _magnetPickupScanner;
    float testRadius = 30f;

    private DataManager _dataManager;

    /// <summary>
    /// 초기화 진행과 원격 표시·전체 캐릭터 준비 단계
    /// </summary>
    private enum InitializationStage
    {
        Uninitialized,
        Initializing,
        ReplicaReady,
        CharacterReady,
        Failed
    }

    private InitializationStage _initializationStage;
    private bool IsInitialized => _initializationStage is InitializationStage.ReplicaReady or InitializationStage.CharacterReady;

    /// <summary>
    /// 성장·인벤토리·장비 상태 추출 가능 여부
    /// </summary>
    public bool CanCaptureCharacterState => _initializationStage == InitializationStage.CharacterReady;

    private bool _isNetworkCombatReady;
    private NetworkPlayerController _networkLifeController;

    /// <summary>
    /// 초기화·생존·접속 상태를 반영한 몬스터 타깃 가능 여부
    /// 온라인에서는 준비가 완료된 서버 캐릭터만 허용
    /// </summary>
    public bool CanBeMonsterTarget =>
        CanCaptureCharacterState &&
        gameObject.activeInHierarchy &&
        !IsDead &&
        !IsNetworkJoinLocked &&
        (IsNetworkControlled ? _isNetworkCombatReady : enabled);

    /// <summary>
    /// 온라인 생명 상태 요청을 처리할 컨트롤러 연결
    /// </summary>
    public void BindNetworkLifeController(NetworkPlayerController controller)
    {
        _networkLifeController = controller;
    }

    /// <summary>
    /// 서버 사망·부활에 따른 온라인 입력과 물리 활성 상태 변경
    /// </summary>
    public void SetNetworkLifeControl(bool isEnabled)
    {
        _networkLifeController.SetLifeControlEnabled(isEnabled);
    }

    /// <summary>
    /// 서버 생명 상태 위치의 이동 컴포넌트·NGO 위치 동기화
    /// </summary>
    public void PlaceForNetworkLife(Vector3 position)
    {
        PlaceAtMapSpawn(position);
        _networkLifeController.TeleportForLifeState(position);
    }

    /// <summary>
    /// 플레이어 콜라이더 발 높이를 반영한 지면 배치 위치 탐색
    /// 지면이 없으면 호출자가 지정한 안전 위치 사용
    /// </summary>
    public Vector3 FindGroundedPosition(LayerMask groundMask, float searchDistance, float skin, Vector3 fallback)
    {
        Collider2D collider = _playerMove.PlayerCollider;
        Vector2 origin = new(collider.bounds.center.x, collider.bounds.max.y + skin);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, searchDistance, groundMask);
        if (hit.collider == null || hit.normal.y <= 0f)
        {
            return fallback;
        }
        float footOffset = transform.position.y - collider.bounds.min.y;
        return new Vector3(hit.point.x, hit.point.y + footOffset + skin, transform.position.z);
    }

    /// <summary>
    /// 네트워크 루트를 유지한 캐릭터 표시만 변경
    /// </summary>
    public void SetCharacterVisible(bool isVisible) => _playerVisual.SetVisible(isVisible);

    /// <summary>
    /// 복제된 사망 상태의 캐릭터 애니메이션 재생
    /// </summary>
    public void PlayDeathAnimation() => _playerVisual.TriggerDie();

    /// <summary>
    /// 서버 캐릭터의 전투 참여 준비 상태 설정
    /// 생성 준비 완료와 네트워크 제거 시 호출
    /// </summary>
    public void SetNetworkCombatReady(bool ready)
    {
        _isNetworkCombatReady = ready;
    }

    /// <summary>
    /// 네트워크 객체 제거 시 생명 상태 요청 연결 해제
    /// </summary>
    public void ClearNetworkLifeController()
    {
        _networkLifeController = null;
    }
    private bool _isInputSubscribed;

    private bool _areCharacterEventsConnected;
    private bool _areLocalGameplayEventsConnected;
    private bool _areLocalPresentationEventsConnected;
    private bool _isLocalInputOwner;

    #endregion

    #region Init
    /// <summary>
    /// 선택한 로컬 캐릭터 복원과 UI 및 직접 입력 연결
    /// </summary>
    public void InitializeLocal()
    {
        if (!CanBeginInitialization())
        {
            return;
        }

        _dataManager ??= GameManager.Instance.DataManager;

        if (!_dataManager.IsDataLoaded)
        {
            return;
        }


        CharacterSession session = GameManager.Instance.CharacterSession;

        if (!session.HasSelectedCharacter)
        {
            Debug.LogError("선택된 캐릭터 없이 플레이 씬에 진입했습니다.");

            return;
        }

        _initializationStage = InitializationStage.Initializing;
        try
        {
            CheckComponent();


            _playerEquipment ??= new PlayerEquipment();
            _goldWallet ??= new GoldWallet();


            InitializeLocalSaveController(session.SelectedSlotId);

            CharacterSaveReadResult readResult = _playerSaveController.Read(out SaveData saveData, out string errorMessage);
            if (readResult == CharacterSaveReadResult.NotFound || readResult == CharacterSaveReadResult.Failed)
            {
                Debug.LogError($"선택한 캐릭터를 불러오지 못했습니다. {errorMessage}");
                _initializationStage = InitializationStage.Uninitialized;
                return;
            }

            InitializeCharacterRuntime(_dataManager, saveData.Player.Class);

            // 저장 복원 대상인 인벤토리와 아이템 수령 기능 준비
            InitializeCharacterInventory();

            // 저장 복원 알림 수신을 위한 로컬 이벤트와 UI 연결
            ConnectLocalSession();

            _playerSaveController.MarkRuntimeReady();
            _playerSaveController.Restore(saveData);
            if (readResult == CharacterSaveReadResult.BackupSuccess)
            {
                Debug.LogWarning("본 저장 파일을 복원하지 못해 백업 데이터를 적용했습니다.");
            }

            // 저장 복원 완료 후 플레이 세션 등록
            GameManager.Instance.PlaySessionController.RegisterPlayer(this);

            // 직접 씬 실행에서도 입력 활성화 전에 연출 풀 연결
            MapManager.Instance.ObjectPool.Bind(gameObject);

            _initializationStage = InitializationStage.CharacterReady;
            // 로컬 플레이 경로의 직접 입력 활성화
            SetLocalInputOwner(true);
        }
        catch
        {
            FailInitialization();
            throw;
        }
    }

    /// <summary>
    /// 선택한 로컬 저장 슬롯과 플레이어 저장 시스템 연결
    /// </summary>
    private void InitializeLocalSaveController(int slotId)
    {
        _playerSaveController.Initialize(
            slotId,
            _playerStat,
            _playerInventory,
            _playerEquipment,
            _goldWallet,
            _questProgress,
            _quickSlots,
            _dataManager,
            GameManager.Instance.CharacterSaveLocator);
    }

    /// <summary>
    /// 각 플레이어의 이동, 전투, 외형과 캐릭터 내부 이벤트 준비
    /// </summary>
    private void InitializeCharacterRuntime(DataManager dataManager, PlayerClass playerClass)
    {
        InitializeCharacterComponents(dataManager, playerClass);
        ConnectCharacterEvents();
    }

    /// <summary>
    /// 로컬 게임 요청과 화면 연결
    /// </summary>
    private void ConnectLocalSession()
    {
        if (_enhancementRequestHandler == null)
        {
            InitializeLocalRequestHandlers();
        }

        ConnectLocalGameplayEvents();
        ConnectLocalPresentationEvents();
        BindLocalUI();
    }

    /// <summary>
    /// 초기화된 캐릭터를 현재 컴퓨터의 소유자로 연결
    /// 현재 캐릭터 데이터를 UI에 다시 표시
    /// </summary>
    public void ConnectLocalOwner()
    {
        if (!CanCaptureCharacterState)
        {
            throw new System.InvalidOperationException("로컬 소유권 연결 전에 캐릭터 상태 초기화 필요");
        }

        EnsureLocalSaveController();
        ConnectLocalSession();
        PublishCurrentLocalPresentation();
        SetLocalInputOwner(!IsDead);
    }

    /// <summary>
    /// 초기화된 온라인 소유자의 상태 알림과 HUD 연결
    /// 기존 로컬 소유자 연결 해제 후 호출
    /// </summary>
    public void ConnectNetworkOwnerPresentation()
    {
        if (!CanCaptureCharacterState)
        {
            throw new System.InvalidOperationException("온라인 화면 연결 전에 캐릭터 상태 초기화 필요");
        }

        // 이동·공격과 게임 상태 변경은 네트워크 요청 경로에서 처리
        SetLocalInputOwner(false);
        DisconnectLocalGameplayEvents();

        BindPlayerHUD();
        BindInventoryUI();
        ConnectLocalPresentationEvents();
        PublishCurrentLocalPresentation();
    }

    /// <summary>
    /// 전달받은 캐릭터 상태 검증과 런타임 초기화
    /// 로컬 저장 파일 읽기와 UI 및 입력 연결 제외
    /// </summary>
    public void InitializeFromState(DataManager dataManager, SaveData characterState)
    {
        if (!CanBeginInitialization())
        {
            throw new System.InvalidOperationException("이미 초기화된 플레이어의 상태 초기화 요청");
        }

        if (dataManager == null)
        {
            throw new System.ArgumentNullException(nameof(dataManager));
        }

        if (!dataManager.IsDataLoaded)
        {
            throw new System.InvalidOperationException("캐릭터 초기화 전에 게임 데이터 로드 필요");
        }

        _initializationStage = InitializationStage.Initializing;
        try
        {
            CheckComponent();

            SaveDataChecker checker = new();

            if (!checker.TryCheck(characterState, _playerInventory.Settings, dataManager, out string errorMessage))
            {
                throw new System.ArgumentException(errorMessage, nameof(characterState));
            }

            _dataManager = dataManager;
            _playerEquipment = new PlayerEquipment();
            _goldWallet = new GoldWallet();

            InitializeCharacterRuntime(_dataManager, characterState.Player.Class);
            InitializeCharacterInventory();

            SaveDataRestorer restorer = new();

            restorer.Restore(
                characterState,
                _playerStat,
                _playerInventory,
                _playerEquipment,
                _goldWallet,
                _questProgress,
                _quickSlots,
                _dataManager);

            _initializationStage = InitializationStage.CharacterReady;
        }
        catch
        {
            FailInitialization();
            throw;
        }
    }

    /// <summary>
    /// 원격 캐릭터의 이동·전투·외형 초기화
    /// 로컬 저장·UI·입력 연결 제외
    /// </summary>
    public void InitializeRemote(PlayerClass playerClass)
    {
        if (!CanBeginInitialization())
        {
            return;
        }

        DataManager dataManager = GameManager.Instance.DataManager;
        if (!dataManager.IsDataLoaded)
        {
            throw new System.InvalidOperationException("원격 플레이어 초기화 전에 게임 데이터 로드 필요");
        }

        _initializationStage = InitializationStage.Initializing;
        try
        {
            CheckComponent();
            _playerEquipment ??= new PlayerEquipment();

            InitializeCharacterRuntime(dataManager, playerClass);
            _initializationStage = InitializationStage.ReplicaReady;
        }
        catch
        {
            FailInitialization();
            throw;
        }
    }

    /// <summary>
    /// 준비 완료·재진입 차단과 실패 후 명시적 재설정 요구
    /// </summary>
    private bool CanBeginInitialization()
    {
        if (_initializationStage == InitializationStage.Failed)
        {
            throw new System.InvalidOperationException("초기화 실패한 플레이어는 재설정 후 초기화 필요");
        }
        return _initializationStage == InitializationStage.Uninitialized;
    }

    /// <summary>
    /// 부분 초기화 실패 시 준비 상태와 이벤트 연결 해제
    /// </summary>
    private void FailInitialization()
    {
        _questProgress.Shutdown();
        _initializationStage = InitializationStage.Failed;
        _isLocalInputOwner = false;
        UnsubscribeInput();
        DisconnectLocalGameplayEvents();
        DisconnectLocalPresentationEvents();
        DisconnectCharacterEvents();
    }

    /// <summary>
    /// 이동, 외형, 능력치와 전투 컴포넌트 준비
    /// PlayerEquipment 생성 이후 호출
    /// </summary>
    private void InitializeCharacterComponents(DataManager dataManager, PlayerClass playerClass)
    {
        _playerMove.Initialize(dataManager.CachedPlayerMoveData);
        _playerTeleport.Initialize();
        _playerVisual.Initialize();

        _playerStat.Initialize(dataManager.CachedPlayerStatData, playerClass, _playerEquipment, dataManager);

        _playerAttack.Initialize(this, _playerStat);
        _playerSkillController.Initialize(dataManager.SkillTable, _playerStat);
    }

    /// <summary>
    /// 캐릭터 인벤토리와 장비 교환 및 아이템 수령 기능 준비
    /// PlayerEquipment와 GoldWallet 생성 이후 호출
    /// </summary>
    private void InitializeCharacterInventory()
    {
        _playerInventory.Initialize();
        _questProgress.Initialize(_dataManager, _playerInventory);

        _equipmentTransferService = new EquipmentTransferService(_playerInventory, _playerEquipment);
        _consumableUseService = new ConsumableUseService(_playerInventory, _playerStat);
        _pickupReceiver.Initialize(this, _playerInventory, _goldWallet);
        _questRewardService = new QuestRewardService(_questProgress, _playerInventory, _goldWallet, _playerStat);
        _isQuestRequestRunning = false;

    }

    /// <summary>
    /// 로컬 플레이의 강화 요청 담당자 준비
    /// </summary>
    private void InitializeLocalRequestHandlers()
    {
        EnhancementService enhancementService = new(_playerInventory, _playerEquipment, _goldWallet);

        _enhancementRequestHandler = new LocalEnhancementRequestHandler(
            enhancementService, _playerInventory, _playerEquipment,
            _dataManager.GetEnhanceProb, () => UnityEngine.Random.value);
    }

    /// <summary>
    /// 현재 화면의 플레이어 상태 HUD 활성화
    /// </summary>
    private void BindPlayerHUD()
    {
        GameManager.Instance.UIManager.PlayerUI.Initialize();
    }

    /// <summary>
    /// 준비된 개인 데이터와 현재 화면의 UI 연결
    /// 인벤토리 슬롯 생성에 사용하는 맵 오브젝트 풀 준비 필요
    /// </summary>
    private void BindLocalUI()
    {
        BindPlayerHUD();
        GameManager.Instance.UIManager.EnhanceUI.BindEnhancementRequestHandler(_enhancementRequestHandler, _playerStat.MainStat);
        BindInventoryUI();
    }

    /// <summary>
    /// 로컬·온라인 소유자의 인벤토리 표시 연결
    /// 창 준비와 실제 데이터 연결을 분리한 입력 구독 복구
    /// </summary>
    private void BindInventoryUI()
    {
        UIManager ui = GameManager.Instance.UIManager;
        ui.StatUI.BindEquipmentPreview(this, _playerVisual);
        ui.BindItemTooltip(_playerStat, _playerEquipment);
        ui.InventoryUI.Initialize(_playerInventory);
        ui.QuickSlotBar.Bind(this);
        ui.BindQuestTracker(_questProgress);
        RefreshItemInteraction();
    }

    /// <summary>
    /// 현재 소유자의 온라인·사망·접속 대기에 따른 아이템 조작 제한
    /// 인벤토리 열기와 종류별 탐색은 유지
    /// </summary>
    private void RefreshItemInteraction()
    {
        UIManager ui = GameManager.Instance.UIManager;
        bool locked = IsNetworkControlled || IsDead || _isNetworkJoinLocked;
        ui.InventoryUI.SetInteractionLocked(locked);
        ui.EquipUI.SetInteractionLocked(locked);
        ui.EnhanceUI.SetInteractionLocked(locked);
    }

    /// <summary>
    /// 컴포넌트 체크 메서드
    /// </summary>
    public void CheckComponent()
    {
        if (_playerMove == null || _playerTeleport == null || _playerAttack == null ||
            _playerSkillController == null || _playerVisual == null || _playerStat == null ||
            _playerInventory == null || _pickupReceiver == null || _playerSaveController == null)
        {
            throw new MissingReferenceException(
                $"{name}: Player의 이동·전투·상태·인벤토리·저장 컴포넌트 참조 필요");
        }
    }

    /// <summary>
    /// 맵 진입 시 이 플레이어의 이동 위치와 물리 위치를 동기화합니다.
    /// </summary>
    public void PlaceAtMapSpawn(Vector2 position)
    {
        _playerMove.SyncPosition(position);
    }

    /// <summary>
    /// 현재 선택 슬롯과 준비된 캐릭터 상태의 저장 연결
    /// </summary>
    private void EnsureLocalSaveController()
    {
        if (_playerSaveController.IsInitialized)
        {
            return;
        }

        int slotId = GameManager.Instance.CharacterSession.SelectedSlotId;
        InitializeLocalSaveController(slotId);
        _playerSaveController.MarkRuntimeReady();
    }

    /// <summary>
    /// 현재 능력치와 장비 및 골드 화면 갱신
    /// </summary>
    private void PublishCurrentLocalPresentation()
    {
        _playerStat.PublishAllStats();

        foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
        {
            if (slot == EquipSlot.None)
            {
                continue;
            }

            _playerEquipment.TryGetEquippedItem(slot, out InventoryItemData item);
            EventBus.PublishEquipChanged(slot, item);
        }

        _goldWallet.PublishCurrentBalance();
    }

    /// <summary>
    /// 로컬 입력과 UI 및 게임 요청 연결 해제
    /// 캐릭터 상태와 저장 연결은 유지
    /// </summary>
    public void DisconnectLocalOwner()
    {
        SetLocalInputOwner(false);

        UIManager ui = GameManager.Instance.UIManager;
        ui.StatUI.UnbindEquipmentPreview(this);
        ui.UnbindItemTooltip();
        ui.InventoryUI.UnbindInventory();
        ui.QuickSlotBar.Unbind();
        ui.UnbindQuestTracker();
        ui.EquipUI.SetInteractionLocked(true);
        ui.EnhanceUI.UnbindEnhancementRequestHandler();

        DisconnectLocalGameplayEvents();
        DisconnectLocalPresentationEvents();
    }

    /// <summary>
    /// 온라인 소유자의 로컬 상태 알림 연결 해제
    /// </summary>
    public void DisconnectNetworkOwnerPresentation()
    {
        UIManager ui = GameManager.Instance.UIManager;
        ui.StatUI.UnbindEquipmentPreview(this);
        ui.UnbindItemTooltip();
        ui.InventoryUI.UnbindInventory();
        ui.QuickSlotBar.Unbind();
        ui.UnbindQuestTracker();
        ui.EquipUI.SetInteractionLocked(true);
        DisconnectLocalPresentationEvents();
    }
    #endregion

    private void Update()
    {
        if (!IsInitialized)
        {
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 행동 제한과 관계없이 실제 무적 종료 시각 확인
        if (_invincibleUntil > 0f && Time.time >= _invincibleUntil)
        {
            float endedAt = _invincibleUntil;
            _invincibleUntil = 0f;
            Debug.Log($"[무적 종료] {name} | 종료 시각: {endedAt:F3}초 | 확인 시각: {Time.time:F3}초", this);
        }
#endif

        //행동불가 체크
        if (CheckIsStun())
        {
            return;
        }

        CharacterState targetState = CharacterState.Idle;

        //지상이 아니면 점프 상태
        if (!_playerMove.IsGrounded)
        {
            targetState = CharacterState.Jump;
        }
        else
        {
            //이동 상태 체크
            if (Mathf.Abs(_playerMove.MoveVector.x) > Constants.FLOAT_CheckFloat)
            {
                targetState = CharacterState.Run;
            }
            else
            {
                targetState = CharacterState.Idle;
            }
        }

        if (_currentState != targetState)
        {
            _currentState = targetState;
            _playerVisual.SetState(_currentState);
        }

    }

    #region Input
    /// <summary>
    /// 이동 입력 시 PlayerMove의 이동 메서드 실행
    /// </summary>
    /// <param Name="inputVector"></param>
    public void OnMoveInput(Vector2 inputVector)
    {
        if (CheckIsStun())
        {
            _playerMove.InputMoveVector(Vector2.zero);
            return;
        }
        _playerMove.InputMoveVector(inputVector);
    }

    /// <summary>
    /// 점프 입력 시 PlayerMove의 점프 메서드 실행
    /// </summary>
    public void OnJumpInput()
    {
        if (CheckIsStun())
        {
            return;
        }
        _playerMove.Jump();
    }

    public void OnPickupInput()
    {
        if (IsDead || _isNetworkJoinLocked || _isDialogueOpen)
        {
            return;
        }

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 1.5f);

        List<DroppedItem> candidates = new();

        foreach (Collider2D candidateCollider in colliders)
        {
            if (candidateCollider.TryGetComponent(
                    out DroppedItem droppedItem) &&
                !droppedItem.IsFlying)
            {
                candidates.Add(droppedItem);
            }
        }

        //아이템 후보군 정렬
        candidates.Sort((left, right) =>
        {
            float leftDistance =
                (left.transform.position - transform.position).sqrMagnitude;

            float rightDistance =
                (right.transform.position - transform.position).sqrMagnitude;

            return leftDistance.CompareTo(rightDistance);
        });

        foreach (DroppedItem candidate in candidates)
        {
            if (candidate.TryPickup(_pickupReceiver))
            {
                return;
            }
        }


        _magnetPickupScanner.Scan(
    transform.position,
    testRadius,
    _pickupReceiver.PickupAnchor,
    _pickupReceiver);
    }

    /// <summary>
    /// 지정 슬롯 스킬 실행 요청
    /// </summary>
    public void OnSkillSlotInput(int slotIndex)
    {
        if (CheckIsStun())
        {
            return;
        }

        _playerSkillController.TryExecuteSlot(slotIndex, this);
    }
    #endregion


    /// <summary>
    /// 플레이어가 바라보는 방향
    /// </summary>
    /// <returns></returns>
    public int GetPlayerSightDir()
    {
        return ForcedMovement.SightDirection;
    }

    public float GetStat(StatType statType)
    {
        return _playerStat.GetTotalStat(statType);
    }

    /// <summary>
    /// 플레이어 피격 처리
    /// </summary>
    /// <param Name="damage">받은 데미지</param>
    /// <param Name="attackPos">공격 받은 위치</param>
    public void TakeDamage(float damage, Vector2 attackPos, KnockbackData knockbackData = default)
    {
        if (Time.time < _invincibleUntil || IsDead ||
            (IsNetworkControlled ? !_isNetworkCombatReady : _soloBossResultDamageBlocked))
        {
            return;
        }

        _playerSkillController.Cancel();


        //피격 애니메이션
        _playerVisual.TriggerHit();

        // 최종 방어력을 반영한 실제 피해 계산
        float receivedDamage = _playerStat.CalculateReceivedDamage(damage);
        float previousHp = _playerStat.CurrentHp;
        _playerStat.ChangeHp(-receivedDamage);
        float appliedDamage = previousHp - _playerStat.CurrentHp;

        if (appliedDamage > Constants.FLOAT_CheckFloat)
        {
            EventBus.PublishDamageNumber(new DamageNumberRequest(Mathf.CeilToInt(appliedDamage), transform.position + Vector3.up * 1.5f, DamageNumberStyle.Received, GetEntityId()));

            Bounds body = _playerMove.PlayerCollider.bounds;
            bool fromLeft = attackPos.x <= body.center.x;
            Vector3 position = DamageImpactPresentation.GetImpactPosition(body, fromLeft, transform.position.z);
            if (IsNetworkControlled)
            {
                DamageImpactOccurred?.Invoke(position, fromLeft);
            }
            else if (MapManager.Instance != null)
            {
                MapManager.Instance.DamageImpactPresentation.PlayPlayerImpact(position, fromLeft, isLocalSound: true);
            }
        }

        if (IsDead)
        {
            OnDie();
            return;
        }

        //넉백 프로세스
        if (_knockbackCoroutine == null)
        {
            if (knockbackData.ForceX == 0f && knockbackData.ForceY == 0f && knockbackData.Duration == 0f)
            {
                knockbackData = KnockbackData.Default;
            }
            float knockbackDirX = (transform.position.x > attackPos.x) ? 1f : -1f;
            Vector2 knockbackForce = new Vector2(knockbackDirX * knockbackData.ForceX, knockbackData.ForceY);
            _knockbackCoroutine = StartCoroutine(CoKnockback(knockbackForce, knockbackData.Duration));

        }

        SetInvincibilityDuration(Constants.FLOAT_InvincibleDuration);

     
    }

    /// <summary>
    /// 부활 시 체력·마나 전량 회복과 사망 상태·무적 복구, 쿨다운 유지
    /// </summary>
    public void Revive(float invincibleDuration)
    {
        _invincibleUntil = 0f;
        CancelKnockback();
        _playerMove.CancelTransientMovement();
        _playerMove.ResetVelocity();
        _playerStat.ChangeHp(_playerStat.GetTotalStat(StatType.MaxHp) - _playerStat.CurrentHp);
        _playerStat.ChangeMp(_playerStat.GetTotalStat(StatType.MaxMp) - _playerStat.CurrentMp);
        RestoreAlivePresentation();
        SetInvincibilityDuration(invincibleDuration);
    }

    private bool _soloBossResultDamageBlocked;

    /// <summary>로컬 보스전 승리 이후 잔류 공격 피해 제한</summary>
    public void SetSoloBossResultDamageBlocked(bool blocked) => _soloBossResultDamageBlocked = blocked;

    /// <summary>
    /// 본인 UI 잠금과 반복 사망을 위한 기존 플래그 복구
    /// 네트워크 입력 연결은 컨트롤러에서 수행
    /// </summary>
    public void RestoreAlivePresentation()
    {
        _isDeathHandled = false;
        _playerVisual.SetVisible(true);
        _playerVisual.SetState(CharacterState.Idle);
        if (_areLocalPresentationEventsConnected)
        {
            RefreshItemInteraction();
        }
    }

    /// <summary>
    /// 행동 불가 체크 (후에 바인드 등 추가 가능)
    /// </summary>
    /// <returns></returns>
    public bool CheckIsStun() => IsDead || _isNetworkJoinLocked || _isDialogueOpen || _playerMove.IsKnockback || _isAttacking || _playerSkillController.IsExecuting;

    /// <summary>
    /// 서버 생성 및 원격 표시 캐릭터의 로컬 저장·씬 이동 경로 차단
    /// 네트워크 캐릭터 초기화 전에 지정
    /// </summary>
    public void MarkNetworkControlled()
    {
        IsNetworkControlled = true;
    }

    /// <summary>
    /// 캐릭터와 UI 연결을 유지한 접속 대기 조작 잠금
    /// </summary>
    public void SetNetworkJoinLocked(bool locked)
    {
        if (_isNetworkJoinLocked == locked)
        {
            return;
        }

        _isNetworkJoinLocked = locked;
        if (locked)
        {
            _inputOwnerBeforeJoin = _isLocalInputOwner;
            SetLocalInputOwner(false);
        }
        else
        {
            SetLocalInputOwner(_inputOwnerBeforeJoin && !IsDead);
        }

        RefreshItemInteraction();
    }

    /// <summary>
    /// 오프라인 보관 보상의 인벤토리 수령
    /// 파일 저장 없이 수령 건수 반환, 미수령 보상은 보관 목록에 유지
    /// </summary>
    public bool TryClaimBossRewards(out int claimed, out string errorMessage)
    {
        claimed = 0;
        if (IsNetworkControlled || IsNetworkJoinLocked)
        {
            errorMessage = "네트워크 종료 후 보상 수령 가능";
            return false;
        }
        if (!CanCaptureCharacterState)
        {
            errorMessage = "플레이어가 아직 준비되지 않았습니다.";
            return false;
        }
        claimed = _playerInventory.ClaimBossRewards();
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 로컬 캐릭터의 퀘스트 수락 기록 반영
    /// 파일 저장 제외, 이미 수락한 기록은 변경 없이 성공 반환
    /// </summary>
    public bool TryAcceptQuest(int questId, out string errorMessage)
    {
        QuestRequestResult result = AcceptQuest(questId);
        errorMessage = result.Succeeded ? string.Empty : result.Message;
        return result.Succeeded;
    }

    /// <summary>
    /// 수락 요청과 구조화된 처리 결과 반환, 자동 저장 제외
    /// </summary>
    public QuestRequestResult AcceptQuest(int questId)
    {
        if (!TryBeginQuestRequest(questId, out QuestData quest, out QuestRequestResult rejection)) { return rejection; }
        try
        {
            return _questProgress.Accept(quest);
        }
        finally
        {
            _isQuestRequestRunning = false;
        }
    }

    /// <summary>
    /// 기존 호출부용 보상 요청과 실패 설명 반환
    /// </summary>
    public bool TryClaimQuestReward(int questId, out string errorMessage)
    {
        QuestRequestResult result = ClaimQuestReward(questId);
        errorMessage = result.Succeeded ? string.Empty : result.Message;
        return result.Succeeded;
    }

    /// <summary>
    /// 보상 요청의 사유·수량 반환, 중복 지급 방지와 자동 저장 제외
    /// </summary>
    public QuestRequestResult ClaimQuestReward(int questId)
    {
        if (!TryBeginQuestRequest(questId, out QuestData quest, out QuestRequestResult rejection)) { return rejection; }
        try
        {
            return _questRewardService.Claim(quest, _dataManager.ItemTable);
        }
        finally
        {
            _isQuestRequestRunning = false;
        }
    }

    /// <summary>
    /// 요청 진입 단계의 준비·네트워크·사망·중복 실행 제한 사유 반환
    /// </summary>
    private bool TryBeginQuestRequest(int questId, out QuestData quest, out QuestRequestResult rejection)
    {
        quest = null;
        rejection = null;
        if (!CanCaptureCharacterState)
        {
            rejection = new QuestRequestResult(QuestRequestResult.Code.NotReady, "캐릭터와 인벤토리 초기화가 완료되지 않았습니다.");
        }
        else if (IsNetworkControlled)
        {
            rejection = new QuestRequestResult(QuestRequestResult.Code.Online, "온라인 제어 캐릭터입니다. 서버 퀘스트 처리 경로가 필요합니다.");
        }
        else if (IsNetworkJoinLocked)
        {
            rejection = new QuestRequestResult(QuestRequestResult.Code.Joining, "멀티플레이 접속 준비로 캐릭터 조작이 잠겨 있습니다.");
        }
        else if (IsDead)
        {
            rejection = new QuestRequestResult(QuestRequestResult.Code.Dead, "캐릭터가 사망한 상태입니다.");
        }
        else if (_isQuestRequestRunning)
        {
            rejection = new QuestRequestResult(QuestRequestResult.Code.Busy, "이전 퀘스트 요청 처리 중입니다. 요청 완료 후 다시 실행하세요.");
        }
        else if (!_dataManager.QuestTable.TryGetValue(questId, out quest))
        {
            rejection = new QuestRequestResult(QuestRequestResult.Code.QuestMissing, $"퀘스트 ID {questId}의 정의가 없습니다.", relatedId: questId);
        }
        else
        {
            _isQuestRequestRunning = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 현재 성장·인벤토리·장비 상태의 독립 데이터 생성
    /// </summary>
    public SaveData CaptureCharacterState()
    {
        if (!CanCaptureCharacterState)
        {
            throw new System.InvalidOperationException("캐릭터 상태 추출 전에 런타임과 인벤토리 초기화 필요");
        }

        SaveDataFactory factory = new();

        return factory.Create(_playerStat, _playerInventory, _playerEquipment, _goldWallet, _questProgress, _quickSlots);
    }

    /// <summary>
    /// 현재 플레이어 상태를 선택된 캐릭터 슬롯에 저장
    /// </summary>
    public bool TrySave(out string errorMessage)
    {
        if (IsNetworkControlled)
        {
            errorMessage = "온라인 캐릭터는 서버 결과 저장 경로 필요";
            return false;
        }

        if (!IsInitialized)
        {
            errorMessage = "플레이어가 아직 초기화되지 않았습니다.";

            return false;
        }

        if (IsDead)
        {
            errorMessage = "사망 상태에서는 저장할 수 없어요. 부활한 뒤 다시 시도해 주세요.";
            return false;
        }

        return _playerSaveController.TrySave(out errorMessage);
    }

    /// <summary>
    /// 접속 요청과 동일한 캐릭터 상태를 선택된 슬롯에 저장
    /// CaptureCharacterState로 추출한 상태 전달
    /// </summary>
    public bool TrySaveState(SaveData characterState, out string errorMessage)
    {
        if (IsNetworkControlled)
        {
            errorMessage = "온라인 캐릭터는 서버 결과 저장 경로 필요";
            return false;
        }

        if (!CanCaptureCharacterState)
        {
            errorMessage = "캐릭터 상태 저장 전에 런타임과 인벤토리 초기화 필요";
            return false;
        }

        return _playerSaveController.TrySaveState(characterState, out errorMessage);
    }

    /// <summary>온라인 결과가 저장된 뒤 로비에서 사용할 로컬 캐릭터 상태 복원</summary>
    internal void RestoreSavedBattleResult(SaveData state)
    {
        if (IsNetworkControlled || !CanCaptureCharacterState)
        {
            throw new System.InvalidOperationException("전투 결과를 복원할 로컬 캐릭터가 준비되지 않았습니다.");
        }

        _playerSaveController.Restore(state);
    }

    /// <summary>
    /// 지정한 지속시간으로 무적 종료 시각 재설정
    /// </summary>
    private void SetInvincibilityDuration(float duration)
    {
        duration = Mathf.Max(0f, duration);
        _invincibleUntil = Time.time + duration;
        _playerVisual.PlayInvincibilityBlink(duration);
        InvincibilityStarted?.Invoke(duration);
    }

    /// <summary>
    /// 넉백 타이머 중단과 이동 제어 해제
    /// </summary>
    private void CancelKnockback()
    {
        if (_knockbackCoroutine != null)
        {
            StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = null;
        }
        _playerMove.EndKnockback();
    }

    private IEnumerator CoKnockback(Vector2 force, float duration)
    {
        _playerMove.StartKnockback(force);
        try
        {
            yield return new WaitForSeconds(duration);
        }
        finally
        {
            _playerMove.EndKnockback();
            _knockbackCoroutine = null;
        }
    }

    /// <summary>
    /// 사망 시 입력·공격 중단과 사망 연출 및 상태 알림
    /// 재시도·귀환 결정 전 캐릭터와 저장 상태 유지
    /// </summary>
    public void OnDie()
    {
        if (!IsDead || _isDeathHandled)
        {
            return;
        }

        _isDeathHandled = true;
        SetLocalInputOwner(false);
        _playerAttack.Cancel();
        _playerSkillController.Cancel();
        StopAllCoroutines();
        _knockbackCoroutine = null;
        _invincibleUntil = 0f;
        _playerMove.CancelTransientMovement();
        _playerVisual.TriggerDie();

        // 원격 캐릭터 사망으로 현재 사용자의 UI를 잠그지 않는 소유자 경계
        if (_areLocalPresentationEventsConnected)
        {
            RefreshItemInteraction();
        }

        Died?.Invoke(this);
        if (_areLocalPresentationEventsConnected)
        {
            GameManager.Instance.UIManager.ShowSinglePlayerDeath(this);
        }
    }

    private void HandleEquipRequest(InventoryItemData item)
    {
        if (IsDead || _isNetworkJoinLocked)
        {
            return;
        }

        bool succeeded = _equipmentTransferService.TryEquip(item, _playerStat.Level);

        if (!succeeded)
        {
            Debug.LogWarning($"장비 장착 실패: {item?.ItemData?.Name}");
        }
    }

    /// <summary>
    /// 로컬 플레이의 인벤토리 슬롯 교환 처리
    /// </summary>
    private void HandleInventorySwapRequest(InventoryTab tab, int fromIndex, int toIndex)
    {
        if (IsDead || _isNetworkJoinLocked)
        {
            return;
        }

        _playerInventory.SwapItems(tab, fromIndex, toIndex);
    }

    /// <summary>
    /// 로컬 플레이의 지정 슬롯 장비 해제 처리
    /// </summary>
    private void HandleUnequipToInventoryRequest(EquipSlot sourceSlot, InventoryTab targetTab, int targetIndex)
    {
        if (IsDead || _isNetworkJoinLocked)
        {
            return;
        }

        _equipmentTransferService.TryUnequipToInventorySlot(sourceSlot, targetTab, targetIndex);
    }

    private void HandleUnequipRequest(EquipSlot slot)
    {
        if (IsDead || _isNetworkJoinLocked)
        {
            return;
        }

        _equipmentTransferService.TryUnequip(slot);
    }

   


    #region 이벤트 연결
    private void SubscribeInput()
    {
        if (_isInputSubscribed)
        {
            return;
        }

        EventBus.OnMoveInput += OnMoveInput;
        EventBus.OnJumpInput += OnJumpInput;
        EventBus.OnBasicAttackInput += OnBasicAttackInput;
        EventBus.OnPickupInput += OnPickupInput;
        EventBus.OnSkillSlotInput += OnSkillSlotInput;
        _isInputSubscribed = true;
    }

    /// <summary>
    /// 장비 외형 적용과 해당 캐릭터의 변경 알림
    /// </summary>
    private void HandleEquipmentChanged(EquipSlot slot, InventoryItemData item)
    {
        _playerVisual.ApplyEquipmentChange(slot, item);
        EquipmentChanged?.Invoke(slot, item);
    }

    /// <summary>
    /// 해당 캐릭터의 이동과 전투 결과를 외형 및 위치에 연결
    /// </summary>
    private void ConnectCharacterEvents()
    {
        if (_areCharacterEventsConnected)
        {
            return;
        }

        _playerMove.OnMoved += _playerVisual.SetDirection;
        _playerAttack.OnAttackTrigger += HandleBasicAttackStarted;
        _playerSkillController.ActionStarted += HandleSkillStarted;
        _playerTeleport.OnTeleported += _playerMove.SyncPosition;

        _playerEquipment.EquippedItemChanged += HandleEquipmentChanged;

        _areCharacterEventsConnected = true;
    }

    /// <summary>
    /// 로컬 소유자의 생존·대화·접속 상태에 따른 소비 가능 여부
    /// </summary>
    private bool CanUseLocalItems()
    {
        return CanCaptureCharacterState && _isLocalInputOwner && !IsDead && !IsNetworkControlled && !_isNetworkJoinLocked && !_isDialogueOpen;
    }

    /// <summary>
    /// 서버에서 확인한 소비 아이템의 사용과 남은 수량 반환
    /// 서버의 생존·전투 상태 검사는 호출 컨트롤러에서 수행
    /// </summary>
    public bool TryUseConsumable(int slotIndex, int itemId, out int remainingCount)
    {
        remainingCount = 0;
        if (!_playerInventory.TryGetConsumable(slotIndex, itemId, out InventoryItemData item))
        {
            return false;
        }
        if (!_consumableUseService.TryUse(slotIndex, item, out _))
        {
            return false;
        }
        remainingCount = item.Count;
        return true;
    }

    /// <summary>
    /// 서버 승인 쿨타임과 최종 수량의 소유자 화면 반영
    /// 인벤토리 변경 알림 전에 쿨타임 갱신
    /// </summary>
    public void ApplyConsumableUse(int slotIndex, int itemId, int remainingCount, float remainingCooldown)
    {
        _consumableUseService.ApplyCooldown(itemId, remainingCooldown);
        _playerInventory.ApplyConsumableCount(slotIndex, remainingCount);
    }

    /// <summary>
    /// 로컬 UI의 소비 아이템 요청을 현재 캐릭터에 전달
    /// </summary>
    private void HandleConsumableUseRequest(int slotIndex, InventoryItemData item)
    {
        if (!CanUseLocalItems()) { return; }
        // UI에서 전달된 선택 데이터 경계 확인, 슬롯 교체 여부는 인벤토리에서 확인
        if (item == null || item.IsEmpty || item.IsDestroyed || item.ItemData.GetItemType() != GameItemType.Consumable) { return; }
        if (!_consumableUseService.TryUse(slotIndex, item, out string message)) { Debug.Log(message, this); }
    }

    /// <summary>
    /// 로컬 플레이의 장비·소비 요청과 경험치 지급 연결
    /// </summary>
    private void ConnectLocalGameplayEvents()
    {
        if (_areLocalGameplayEventsConnected)
        {
            return;
        }

        EventBus.OnConsumableUseRequested += HandleConsumableUseRequest;
        EventBus.OnEquipRequested += HandleEquipRequest;
        EventBus.OnUnequipRequested += HandleUnequipRequest;
        EventBus.OnInventorySwapRequested += HandleInventorySwapRequest;
        EventBus.OnUnequipToInventoryRequested += HandleUnequipToInventoryRequest;
        EventBus.OnExpEarned += _playerStat.AddExp;

        _areLocalGameplayEventsConnected = true;
    }

    /// <summary>
    /// 내 캐릭터의 상태 변경을 현재 컴퓨터의 UI 알림으로 전달
    /// </summary>
    private void ConnectLocalPresentationEvents()
    {
        if (_areLocalPresentationEventsConnected)
        {
            return;
        }

        _playerEquipment.EquippedItemChanged += EventBus.PublishEquipChanged;
        _playerStat.HpChanged += EventBus.PublishPlayerHpChanged;
        _playerStat.MpChanged += EventBus.PublishPlayerMpChanged;
        _playerStat.ExpChanged += EventBus.PublishPlayerExpChanged;
        _playerStat.LevelChanged += EventBus.PublishPlayerLevelChanged;
        _playerStat.StatsChanged += EventBus.PublishPlayerStatsChanged;
        _goldWallet.GoldChanged += EventBus.PublishGoldChanged;

        _areLocalPresentationEventsConnected = true;
    }

    private void OnEnable()
    {
        if (IsInitialized && _isLocalInputOwner)
        {
            SubscribeInput();
        }
    }

    /// <summary>
    /// 로컬 입력 이벤트의 직접 실행 여부 설정
    /// 입력 해제 시 남은 이동 입력 초기화
    /// </summary>
    public void SetLocalInputOwner(bool isLocalInputOwner)
    {
        _isLocalInputOwner = isLocalInputOwner;

        if (_isLocalInputOwner && isActiveAndEnabled)
        {
            SubscribeInput();
            return;
        }

        _isDialogueOpen = false;
        UnsubscribeInput();
        _playerMove.InputMoveVector(Vector2.zero);
    }

    private void UnsubscribeInput()
    {
        if (!_isInputSubscribed)
        {
            return;
        }

        EventBus.OnMoveInput -= OnMoveInput;
        EventBus.OnJumpInput -= OnJumpInput;
        EventBus.OnBasicAttackInput -= OnBasicAttackInput;
        EventBus.OnPickupInput -= OnPickupInput;
        EventBus.OnSkillSlotInput -= OnSkillSlotInput;

        _isInputSubscribed = false;
    }

    private void DisconnectCharacterEvents()
    {
        if (!_areCharacterEventsConnected)
        {
            return;
        }

        _playerMove.OnMoved -= _playerVisual.SetDirection;
        _playerAttack.OnAttackTrigger -= HandleBasicAttackStarted;
        _playerSkillController.ActionStarted -= HandleSkillStarted;
        _playerTeleport.OnTeleported -= _playerMove.SyncPosition;

        _playerEquipment.EquippedItemChanged -= HandleEquipmentChanged;

        _areCharacterEventsConnected = false;
    }

    /// <summary>
    /// 로컬 플레이의 장비 변경 요청과 경험치 지급 연결 해제
    /// </summary>
    private void DisconnectLocalGameplayEvents()
    {
        if (!_areLocalGameplayEventsConnected)
        {
            return;
        }

        EventBus.OnConsumableUseRequested -= HandleConsumableUseRequest;
        EventBus.OnEquipRequested -= HandleEquipRequest;
        EventBus.OnUnequipRequested -= HandleUnequipRequest;
        EventBus.OnInventorySwapRequested -= HandleInventorySwapRequest;
        EventBus.OnUnequipToInventoryRequested -= HandleUnequipToInventoryRequest;
        EventBus.OnExpEarned -= _playerStat.AddExp;

        _areLocalGameplayEventsConnected = false;
    }

    /// <summary>
    /// 내 캐릭터의 상태 변경과 UI 알림 연결 해제
    /// </summary>
    private void DisconnectLocalPresentationEvents()
    {
        if (!_areLocalPresentationEventsConnected)
        {
            return;
        }

        _playerEquipment.EquippedItemChanged -= EventBus.PublishEquipChanged;
        _playerStat.HpChanged -= EventBus.PublishPlayerHpChanged;
        _playerStat.MpChanged -= EventBus.PublishPlayerMpChanged;
        _playerStat.ExpChanged -= EventBus.PublishPlayerExpChanged;
        _playerStat.LevelChanged -= EventBus.PublishPlayerLevelChanged;
        _playerStat.StatsChanged -= EventBus.PublishPlayerStatsChanged;
        _goldWallet.GoldChanged -= EventBus.PublishGoldChanged;

        _areLocalPresentationEventsConnected = false;
    }

    private void OnDisable()
    {
        UnsubscribeInput();

        _playerMove.InputMoveVector(Vector2.zero);

        _invincibleUntil = 0f;
        CancelKnockback();
        _playerSkillController.Cancel();
    }

    /// <summary>
    /// 풀에서 재사용할 온라인 캐릭터의 이전 연결과 일시 상태 정리
    /// 새 장비·골드 객체 생성 전에 기존 이벤트 연결 해제
    /// </summary>
    public void ResetNetworkCharacter()
    {
        SetLocalInputOwner(false);
        DisconnectLocalGameplayEvents();
        DisconnectLocalPresentationEvents();
        DisconnectCharacterEvents();
        StopAllCoroutines();
        _playerAttack.Cancel();
        _playerSkillController.Cancel();
        _playerMove.ResetNetworkMovement();
        _playerVisual.StopInvincibilityBlink();
        _questProgress.Clear();
        _questProgress.Shutdown();
        _quickSlots.Clear();
        _playerVisual.SetVisible(true);

        _initializationStage = InitializationStage.Uninitialized;
        _isNetworkCombatReady = false;
        _isNetworkJoinLocked = false;
        _inputOwnerBeforeJoin = false;
        _isDeathHandled = false;
        _isAttacking = false;
        _invincibleUntil = 0f;
        _currentState = CharacterState.Idle;
        _knockbackCoroutine = null;
        _networkLifeController = null;
    }

    private void OnDestroy()
    {
        _questProgress.Shutdown();
        UnsubscribeInput();
        DisconnectLocalGameplayEvents();
        DisconnectLocalPresentationEvents();
        DisconnectCharacterEvents();
    }
    #endregion

    #region test
    /// <summary>
    /// 오프라인 입력의 기본 공격 요청
    /// </summary>
    public void OnBasicAttackInput()
    {
        TryBasicAttack();
    }

    /// <summary>
    /// 기본 공격 실행 요청과 시작 여부 반환
    /// 온라인에서는 서버 요청 처리에서 호출
    /// </summary>
    public bool TryBasicAttack()
    {
        return _playerAttack.TryBasicAttack();
    }

    /// <summary>
    /// 오프라인 기본 공격 연출
    /// 온라인 연출은 서버 승인 RPC에서 재생
    /// </summary>
    private void HandleBasicAttackStarted()
    {
        if (!IsNetworkControlled)
        {
            _playerVisual.TriggerAttack();
        }
    }

    /// <summary>
    /// 오프라인 스킬 승인 시 종류에 맞는 행동 연출
    /// 온라인 모션은 서버 승인 RPC에서 재생
    /// </summary>
    private void HandleSkillStarted(PlayerSkillExecutionType executionType, float duration)
    {
        if (!IsNetworkControlled)
        {
            _playerVisual.PlayAction(PlayerVisual.GetSkillAction(executionType), duration);
        }
    }

    /// <summary>
    /// 네트워크 연결 종료 시 남은 기본 공격 취소
    /// </summary>
    public void CancelBasicAttack()
    {
        _playerAttack.Cancel();
    }
    #endregion

}
