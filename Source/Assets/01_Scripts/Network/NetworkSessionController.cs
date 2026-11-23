using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// 대기실 접속, 캐릭터 요청 승인과 로컬 소유자 전환 관리
/// </summary>
[RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
public sealed class NetworkSessionController : MonoBehaviour
{
    internal const ushort ProtocolVersion = 2;

    /// <summary>
    /// 로컬 캐릭터의 접속 진행 단계
    /// 종료 처리 중 여부와 별개로 복귀에 필요한 준비 상태 유지
    /// </summary>
    private enum LocalJoinState
    {
        None,
        Prepared,
        RequestSent,
        Joined
    }

    private enum InternetRequest { None, Refresh, Create, Join, Depart }

    /// <summary>
    /// NGO 종료 요청과 로컬 세션 정리 단계
    /// </summary>
    private enum SessionShutdownStage { Ready, Stopping, CleaningUp, Failed }

    /// <summary>
    /// NGO 종료와 독립적으로 진행되는 클라우드 퇴장 단계
    /// </summary>
    private enum InternetCleanupStage { Idle, Leaving, Failed }

    [Header("인터넷 방")]
    [SerializeField, Range(1, 100)] private int _internetRoomLimit = 20;
    [SerializeField, Min(1f)] private float _internetRefreshInterval = 2f;
    [SerializeField, Min(5f)] private float _internetRequestTimeout = 30f;
    [SerializeField, Range(5f, 25f)] private float _internetHeartbeatInterval = 15f;

    private readonly InternetRoomService _internetRooms = new();
    private InternetRequest _internetRequest;
    private CancellationTokenSource _internetRequestCancellation;
    private CancellationTokenSource _internetLifetimeCancellation;
    private Task _internetCleanupTask = Task.CompletedTask;
    private Task _internetHeartbeatTask = Task.CompletedTask;
    private InternetCleanupStage _internetCleanupStage;
    private double _nextInternetRefreshAt;

    public IReadOnlyList<InternetRoomInfo> InternetRooms { get; private set; } = Array.Empty<InternetRoomInfo>();
    public string InternetStatus { get; private set; } = "새로고침으로 인터넷 방 검색";
    public string InternetRoomName => _internetRooms.RoomName;
    public bool IsInternetBusy => _internetRequest != InternetRequest.None || _internetCleanupStage == InternetCleanupStage.Leaving;
    private bool IsInternetConnecting => _internetRequest is InternetRequest.Create or InternetRequest.Join;

    /// <summary>
    /// 참가 가능한 인터넷 방 목록 갱신, 연속 조회 간격 제한
    /// </summary>
    public Task<bool> RefreshInternetRoomsAsync()
    {
        if (Time.unscaledTimeAsDouble < _nextInternetRefreshAt)
        {
            InternetStatus = "잠시 후 다시 새로고침해 주세요";
            NotifyStateChanged();
            return Task.FromResult(false);
        }
        return RunInternetRequestAsync(InternetRequest.Refresh, async cancellation =>
        {
            _nextInternetRefreshAt = Time.unscaledTimeAsDouble + _internetRefreshInterval;
            InternetRooms = await _internetRooms.QueryAsync(_internetRoomLimit, cancellation);
            InternetStatus = InternetRooms.Count == 0 ? "참가 가능한 방이 없습니다" : $"인터넷 방 {InternetRooms.Count}개 / 목록에서 선택하여 참가";
            return true;
        });
    }

    /// <summary>
    /// 방 이름으로 인터넷 호스트 생성
    /// </summary>
    public Task<bool> CreateInternetRoomAsync(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName) || roomName.Trim().Length > InternetRoomService.MaximumRoomNameLength)
        {
            LastStartError = $"방 이름은 1~{InternetRoomService.MaximumRoomNameLength}자로 입력해 주세요";
            NotifyStateChanged();
            return Task.FromResult(false);
        }
        return ConnectInternetRoomAsync(true, roomName.Trim());
    }

    /// <summary>
    /// 조회한 방 ID로 인터넷 참가 요청
    /// </summary>
    public Task<bool> JoinInternetRoomAsync(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) { return Task.FromResult(false); }
        return ConnectInternetRoomAsync(false, roomId);
    }

    private Task<bool> ConnectInternetRoomAsync(bool asHost, string room)
    {
        return RunInternetRequestAsync(asHost ? InternetRequest.Create : InternetRequest.Join, async cancellation =>
        {
            var relay = asHost
                ? await _internetRooms.CreateAsync(room, _maxPlayers, cancellation)
                : await _internetRooms.JoinAsync(room, cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (this == null || !CanStart())
            {
                throw new InvalidOperationException("대기실과 캐릭터 준비 상태 확인 필요");
            }
            _transport.SetRelayServerData(relay);
            if (!TryStartSession(asHost, _serverAddress, true)) { return false; }
            StartInternetHeartbeat();
            if (asHost) { await _internetRooms.SetLockedAsync(false, cancellation); }
            cancellation.ThrowIfCancellationRequested();
            InternetStatus = "인터넷 연결 및 캐릭터 준비 중";
            return true;
        });
    }

    private async Task<bool> RunInternetRequestAsync(InternetRequest request, Func<CancellationToken, Task<bool>> operation)
    {
        bool allowed = request == InternetRequest.Depart ? CanStartBossEncounter() : CanConnect;
        if (!allowed) { return false; }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(_internetRequestTimeout));
        _internetRequestCancellation = cancellation;
        _internetRequest = request;
        LastStartError = string.Empty;
        InternetStatus = request == InternetRequest.Refresh ? "인터넷 방 검색 중" : "인터넷 요청 처리 중";
        NotifyStateChanged();
        bool succeeded = false;
        try
        {
            await WaitInternetTaskAsync(_internetCleanupTask, cancellation.Token);
            if (request != InternetRequest.Depart) { await WaitInternetTaskAsync(_internetHeartbeatTask, cancellation.Token); }
            cancellation.Token.ThrowIfCancellationRequested();
            Task<bool> pending = operation(cancellation.Token);
            await WaitInternetTaskAsync(pending, cancellation.Token);
            succeeded = await pending;
            return succeeded;
        }
        catch (OperationCanceledException)
        {
            if (this != null) { InternetStatus = "인터넷 요청 취소 또는 제한 시간 초과"; }
            return false;
        }
        catch (Exception exception)
        {
            if (this != null)
            {
                LastStartError = $"인터넷 요청 실패: {exception.Message}";
                Debug.LogWarning(LastStartError, this);
            }
            return false;
        }
        finally
        {
            if (!succeeded && (request is InternetRequest.Create or InternetRequest.Join))
            {
                if (this != null && isActiveAndEnabled) { StopSession(); }
                else { BeginInternetCleanup(); }
                // 별도 제한 시간을 가진 퇴장 작업의 종료 대기 제외
            }
            _internetRequestCancellation = null;
            _internetRequest = InternetRequest.None;
            if (this != null) { NotifyStateChanged(); }
        }
    }

    /// <summary>
    /// 취소 API가 없는 SDK 작업의 화면 대기만 제한
    /// 원래 작업의 예외 관찰과 서비스 내부 종료 책임 유지
    /// </summary>
    private static async Task WaitInternetTaskAsync(Task task, CancellationToken cancellation)
    {
        var cancelled = new TaskCompletionSource<bool>();
        using var registration = cancellation.Register(() => cancelled.TrySetResult(true));
        if (await Task.WhenAny(task, cancelled.Task) != task)
        {
            _ = ObserveLateInternetTaskAsync(task);
            cancellation.ThrowIfCancellationRequested();
        }
        await task;
        cancellation.ThrowIfCancellationRequested();
    }

    private static async Task ObserveLateInternetTaskAsync(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Debug.LogWarning($"종료된 인터넷 요청의 지연 응답: {exception.Message}"); }
    }

    private async Task<bool> DepartInternetRoomAsync()
    {
        return await RunInternetRequestAsync(InternetRequest.Depart, async cancellation =>
        {
            bool departed = false;
            try
            {
                await _internetRooms.SetLockedAsync(true, cancellation);
                if (this == null) { return false; }
                if (!CanStartBossEncounterCore())
                {
                    LastStartError = "참가자의 캐릭터 준비 완료 후 출발 가능";
                    return false;
                }
                departed = _bossSceneFlow.TryStart();
                return departed;
            }
            finally
            {
                if (!cancellation.IsCancellationRequested && !departed && _internetRooms.HasRoom && this != null && !IsStopping && _internetCleanupStage == InternetCleanupStage.Idle)
                {
                    await _internetRooms.SetLockedAsync(false, cancellation);
                }
            }
        });
    }

    private void StartInternetHeartbeat()
    {
        var cancellation = new CancellationTokenSource();
        _internetLifetimeCancellation = cancellation;
        _internetHeartbeatTask = MaintainInternetRoomAsync(cancellation);
    }

    private async Task MaintainInternetRoomAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await _internetRooms.KeepAliveAsync(TimeSpan.FromSeconds(_internetHeartbeatInterval), cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!cancellation.IsCancellationRequested && this != null)
            {
                LastStartError = $"인터넷 방 유지 실패: {exception.Message}";
                Debug.LogWarning(LastStartError, this);
                StopSession();
            }
        }
        finally
        {
            if (_internetLifetimeCancellation == cancellation) { _internetLifetimeCancellation = null; }
            cancellation.Dispose();
        }
    }

    private void BeginInternetCleanup()
    {
        _internetRequestCancellation?.Cancel();
        _internetLifetimeCancellation?.Cancel();
        if (_internetCleanupStage == InternetCleanupStage.Leaving)
        {
            return;
        }

        _internetCleanupStage = InternetCleanupStage.Leaving;
        _internetCleanupTask = CleanupInternetRoomAsync();
    }

    private async Task CleanupInternetRoomAsync()
    {
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(_internetRequestTimeout));
            await WaitInternetTaskAsync(_internetRooms.LeaveAsync(), cancellation.Token);
            _internetCleanupStage = InternetCleanupStage.Idle;
        }
        catch (Exception exception)
        {
            _internetCleanupStage = InternetCleanupStage.Failed;
            if (this != null)
            {
                LastStartError = $"인터넷 방 퇴장 실패 / 나가기로 재시도: {exception.Message}";
                Debug.LogWarning(LastStartError, this);
            }
        }
        finally
        {
            if (this != null) { NotifyStateChanged(); }
        }
    }


    [SerializeField] private NetworkManager _networkManager;
    [SerializeField] private UnityTransport _transport;
    [SerializeField] private NetworkPlayerController _serverPlayerPrefab;
    [SerializeField] private string _serverAddress = "127.0.0.1";
    [SerializeField] private string _listenAddress = "0.0.0.0";
    [SerializeField, Range(1, 65535)] private int _port = 7777;

    [Header("캐릭터 접속")]
    // 호스트를 포함한 최대 참가 인원
    [SerializeField, Min(1)] private int _maxPlayers = 2;
    [SerializeField, Min(1)] private int _maxJoinRequestBytes = 32 * 1024;
    [SerializeField, Min(1f)] private float _joinTimeoutSeconds = 30f;
    // 현재 HostScene 타일 범위를 포함한 캐릭터 중심 좌표 영역
    [SerializeField] private Rect _joinPositionBounds = new(-14f, -6f, 28f, 20f);

    [Header("공동 보스맵 이동")]
    [SerializeField] private GameSceneId _bossScene = GameSceneId.BossScene;
    [SerializeField] private int _bossArrivalPortalId = 6;
    [SerializeField, Min(1f)] private float _sceneTransitionTimeoutSeconds = 60f;

    [SerializeField, Min(1024)] private int _maxBattleResultBytes = 256 * 1024;
    [SerializeField, Min(1f)] private float _battleResultTimeoutSeconds = 30f;
    public NetworkBattleResultSession BattleResults { get; private set; }

    private NetworkBossSceneFlow _bossSceneFlow;
    private readonly CharacterJoinRequestSerializer _joinRequestSerializer = new();
    private readonly Dictionary<ulong, double> _joinDeadlines = new();
    private readonly HashSet<ulong> _processedJoinClients = new();
    private readonly List<ulong> _expiredJoinClients = new();

    private Player _pendingLocalPlayer;
    private string _pendingJoinRequestJson;
    private NetworkPlayerController _ownedNetworkPlayer;
    private CustomMessagingManager _joinMessagingManager;
    private Coroutine _joinWatchCoroutine;
    private double _localJoinDeadline;
    private LocalJoinState _localJoinState;
    private SessionShutdownStage _shutdownStage;
    private bool IsStopping => _shutdownStage != SessionShutdownStage.Ready;
    private bool _callbacksRegistered;

    public NetworkObjectPool ObjectPool { get; private set; }

    public RemotePlayerRoster Roster { get; } = new();
    public NetworkSceneTransitionController SceneTransition { get; } = new();
    public event Action Changed;

    public string DefaultServerAddress => _serverAddress;
    public bool CanConnect => !IsInternetBusy && !_internetRooms.HasRoom && CanStart();
    public bool HasSession => HasPreparedJoin || _networkManager.IsListening || _networkManager.IsClient || IsStopping || _internetRooms.HasRoom || IsInternetConnecting;
    public bool IsHost => _networkManager.IsHost;
    public bool IsJoined => _localJoinState == LocalJoinState.Joined;

    /// <summary>
    /// 서버 캐릭터 준비 등 세션 표시 상태 변경 알림
    /// </summary>
    public void NotifyStateChanged() => Changed?.Invoke();

    public string LastStartError { get; private set; } = string.Empty;
    public string PendingJoinRequestJson => _pendingJoinRequestJson;
    public GameSceneId BossScene => _bossScene;
    public int BossArrivalPortalId => _bossArrivalPortalId;
    public float SceneTransitionTimeoutSeconds => _sceneTransitionTimeoutSeconds;

    /// <summary>
    /// 정원 미만에서도 호스트가 준비된 참가자 전체와 출발
    /// </summary>
    public async Task<bool> TryStartBossEncounterAsync()
    {
        if (!CanStartBossEncounter()) { return false; }
        if (_internetRooms.HasRoom)
        {
            // 방 잠금과 출발 판정의 실제 완료 결과 전달
            return await DepartInternetRoomAsync();
        }
        bool started = _bossSceneFlow.TryStart();
        NotifyStateChanged();
        return started;
    }

    /// <summary>
    /// 소유자 화면의 도착 맵·풀·카메라 준비
    /// 서버의 맵 초기화는 공동 이동 담당자에서 선행
    /// </summary>
    public void PrepareLocalBossMap(NetworkPlayerController player)
    {
        if (!player.IsOwner || !SceneTransition.IsTransitioning)
        {
            throw new InvalidOperationException("공동 씬 이동 중인 소유자만 도착 맵 준비 가능");
        }

        MapManager map = MapManager.Instance;
        if (map == null || map.gameObject.scene.name != _bossScene.ToString())
        {
            throw new InvalidOperationException("도착한 보스맵의 MapManager 필요");
        }
        if (!player.IsServer)
        {
            SceneTransition.TryBeginPreparing();
            map.Initialize();
        }
        map.FollowLocalPlayer(player.Character);
    }

    /// <summary>
    /// 서버가 수신한 소유자의 보스맵 준비 응답 전달
    /// </summary>
    public void ConfirmBossMapReady(NetworkPlayerController player)
    {
        if (IsStopping || !player.IsServer || !player.IsSpawned)
        {
            return;
        }
        _bossSceneFlow.ConfirmOwnerReady(player.OwnerClientId);
    }

    /// <summary>
    /// 서버의 입력 재개 통지에 따른 클라이언트 맵 준비 종료
    /// </summary>
    public void CompleteLocalBossMap()
    {
        if (!_networkManager.IsServer && SceneTransition.TryBeginPlaying())
        {
            MapManager.Instance.StartGameplay();
        }
    }

    /// <summary>
    /// 이동 실패 사유 기록과 세션 종료
    /// </summary>
    public void FailSceneTransition(string reason)
    {
        LastStartError = reason;
        StopSession();
    }

    // 접속 완료 후에도 종료 시 원래 캐릭터로 복귀할 준비 정보 유지
    private bool HasPreparedJoin => _localJoinState != LocalJoinState.None;

    private void OnEnable()
    {
        ValidateReferences();
        _networkManager.ConnectionApprovalCallback = HandleConnectionApproval;
        _networkManager.OnServerStarted += RegisterJoinHandler;
        _networkManager.OnClientConnectedCallback += HandleClientConnected;
        _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        _networkManager.OnClientStopped += HandleClientStopped;
        _networkManager.OnServerStopped += HandleServerStopped;
        _callbacksRegistered = true;
    }

    private void RegisterJoinHandler()
    {
        if (_joinMessagingManager != null)
        {
            return;
        }

        CustomMessagingManager messagingManager = _networkManager.CustomMessagingManager;
        messagingManager.RegisterNamedMessageHandler(
            CharacterJoinMessage.Name, HandleCharacterJoinRequest);

        // 등록 성공 후에만 해제에 사용할 참조 보관
        _joinMessagingManager = messagingManager;
        BattleResults.Register();
    }

    private void ValidateReferences()
    {
        if (_networkManager == null || _transport == null || _serverPlayerPrefab == null)
        {
            throw new MissingReferenceException($"{name}: NetworkManager, UnityTransport와 serverPlayerPrefab 참조 필요");
        }

        if (_networkManager.gameObject != gameObject ||
            _networkManager.NetworkConfig.NetworkTransport != _transport)
        {
            throw new InvalidOperationException($"{name}: 같은 객체의 NetworkManager와 Transport 연결 필요");
        }

        if (_maxPlayers <= 0 || _maxJoinRequestBytes <= 0 || _joinTimeoutSeconds <= 0f ||
            _joinPositionBounds.width <= 0f || _joinPositionBounds.height <= 0f)
        {
            throw new InvalidOperationException($"{name}: 정원·접속 크기·제한 시간·생성 영역 설정 필요");
        }

        if (_bossScene != GameSceneId.BossScene || _bossArrivalPortalId < 0 || _sceneTransitionTimeoutSeconds <= 0f)
        {
            throw new InvalidOperationException($"{name}: 보스 씬·도착 포털·준비 제한 시간 설정 필요");
        }
        _serverPlayerPrefab.ValidateReferences();
    }

    /// <summary>
    /// HostScene에서 모든 접속 캐릭터의 보스전 출발 조건 확인
    /// 준비 중이거나 사망한 캐릭터가 있으면 출발 보류
    /// </summary>
    public bool CanStartBossEncounter() => !IsInternetBusy && CanStartBossEncounterCore();

    private bool CanStartBossEncounterCore()
    {
        if (!_networkManager.IsHost || IsStopping ||
            _networkManager.ShutdownInProgress || !IsHostScene() ||
            _localJoinState != LocalJoinState.Joined || _joinDeadlines.Count > 0 ||
            !SceneTransition.CanAcceptParticipants)
        {
            return false;
        }

        foreach (NetworkClient client in _networkManager.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;

            // 접속 종료 또는 캐릭터 생성·제거 경계
            if (playerObject == null)
            {
                return false;
            }

            NetworkPlayerController player = playerObject.GetComponent<NetworkPlayerController>();
            if (!player.CanStartSceneTransition)
            {
                return false;
            }
        }

        return true;
    }

    public bool TryStartHost() => TryStartSession(true, _serverAddress);

    public bool TryStartClient(string serverAddress) => TryStartSession(false, serverAddress);

    /// <summary>
    /// 소유자가 승인된 캐릭터의 화면 준비를 마친 뒤 서버 시뮬레이션 허용
    /// </summary>
    public bool ConfirmServerCharacterJoin(NetworkPlayerController player)
    {
        if (IsStopping || !Roster.TryGet(player.OwnerClientId, out Player character) ||
            character != player.Character)
        {
            return false;
        }

        return _joinDeadlines.Remove(player.OwnerClientId);
    }

    /// <summary>
    /// 서버가 생성한 자신의 캐릭터로 HUD와 입력 전환
    /// 서버와 소유자가 동일한 요청 및 게임 데이터로 초기화하는 방식
    /// </summary>
    public bool CompleteLocalCharacterJoin(NetworkPlayerController networkPlayer)
    {
        if (IsStopping || !networkPlayer.IsOwner ||
            _localJoinState is LocalJoinState.None or LocalJoinState.Joined)
        {
            return false;
        }

        Player replacement = networkPlayer.Character;
        PlaySessionController session = GameManager.Instance.PlaySessionController;
        try
        {
            if (!networkPlayer.IsServer)
            {
                if (!_joinRequestSerializer.TryDeserialize(
                        _pendingJoinRequestJson, out CharacterJoinRequest request, out string errorMessage))
                {
                    throw new InvalidOperationException(errorMessage);
                }

                replacement.InitializeFromState(GameManager.Instance.DataManager, request.CharacterState);
            }

            _ownedNetworkPlayer = networkPlayer;
            _pendingLocalPlayer.DisconnectLocalOwner();
            session.ReplacePlayer(_pendingLocalPlayer, replacement);
            replacement.ConnectNetworkOwnerPresentation();
            MapManager.Instance.FollowLocalPlayer(replacement);
            _pendingLocalPlayer.gameObject.SetActive(false);
            _localJoinState = LocalJoinState.Joined;
            networkPlayer.BeginOwnerInput();
            NotifyStateChanged();
            return true;
        }
        catch (Exception exception)
        {
            LastStartError = "소유자 캐릭터 전환 실패";
            Debug.LogException(exception, this);
            StopSession();
            return false;
        }
    }

    /// <summary>
    /// 보스 결과 저장에 반영할 현재 소유자의 개인 퀵슬롯 설정 추출
    /// </summary>
    private QuickSlotSaveData[] CaptureLocalQuickSlots()
    {
        return _ownedNetworkPlayer.Character.QuickSlots.Capture();
    }

    /// <summary>
    /// 같은 상태의 1회 저장과 JSON 준비 후 네트워크 시작
    /// </summary>
    private bool TryStartSession(bool asHost, string serverAddress, bool useRelay = false)
    {
        LastStartError = string.Empty;
        if (GameManager.Instance.LocalBattleResult.HasPending)
        {
            LastStartError = "선택 화면에서 이전 보스전 결과 저장 또는 포기 필요";
            NotifyStateChanged();
            return false;
        }
        if (!CanStart() || (!useRelay && (string.IsNullOrWhiteSpace(serverAddress) || IsInternetBusy || _internetRooms.HasRoom)))
        {
            LastStartError = "HostScene과 캐릭터 준비 상태 또는 서버 주소 확인 필요";
            NotifyStateChanged();
            return false;
        }

        if (!TryPrepareLocalCharacter(out string errorMessage))
        {
            LastStartError = errorMessage;
            NotifyStateChanged();
            return false;
        }

        try
        {
            GameManager.Instance.LocalBattleResult.Discard();
            BattleResults = new NetworkBattleResultSession(_networkManager,
                GameManager.Instance.CharacterSession.SelectedSlotId, GameManager.Instance.CharacterSaveLocator,
                GameManager.Instance.DataManager, _pendingLocalPlayer.PlayerInventory.Settings, _maxBattleResultBytes, StopSession, CaptureLocalQuickSlots,
                GameManager.Instance.LocalBattleResult, _battleResultTimeoutSeconds);
            ObjectPool ??= new NetworkObjectPool(_networkManager.transform);
            ObjectPool.Register(_networkManager);
            _networkManager.NetworkConfig.ProtocolVersion = ProtocolVersion;
            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.NetworkConfig.EnableSceneManagement = true;
            if (!useRelay)
            {
                _transport.SetConnectionData(serverAddress, (ushort)_port, _listenAddress);
            }
            bool started = asHost ? _networkManager.StartHost() : _networkManager.StartClient();
            if (!started || !HasPreparedJoin || IsStopping || _networkManager.ShutdownInProgress)
            {
                if (string.IsNullOrEmpty(LastStartError))
                {
                    LastStartError = "네트워크 시작 실패";
                }
                StopSession();
                return false;
            }

            _bossSceneFlow = new NetworkBossSceneFlow(_networkManager, this);
            _joinWatchCoroutine = StartCoroutine(WatchJoinProgress());
            NotifyStateChanged();
            return true;
        }
        catch
        {
            StopSession();
            throw;
        }
    }

    private bool TryPrepareLocalCharacter(out string errorMessage)
    {
        Player localPlayer = GameManager.Instance.PlaySessionController.ActivePlayer;
        try
        {
            localPlayer.SetNetworkJoinLocked(true);
            var request = new CharacterJoinRequest
            {
                CharacterState = localPlayer.CaptureCharacterState(),
                Position = localPlayer.transform.position
            };

            string json = _joinRequestSerializer.Serialize(request);
            if (!CharacterJoinMessage.IsWithinLimit(json, _maxJoinRequestBytes))
            {
                localPlayer.SetNetworkJoinLocked(false);
                errorMessage = "캐릭터 접속 요청 크기 제한 초과";
                return false;
            }

            if (!localPlayer.TrySaveState(request.CharacterState, out errorMessage))
            {
                localPlayer.SetNetworkJoinLocked(false);
                return false;
            }

            _pendingLocalPlayer = localPlayer;
            _pendingJoinRequestJson = json;
            _localJoinDeadline = Time.realtimeSinceStartupAsDouble + _joinTimeoutSeconds;
            _localJoinState = LocalJoinState.Prepared;
            _shutdownStage = SessionShutdownStage.Ready;
            return true;
        }
        catch
        {
            localPlayer.SetNetworkJoinLocked(false);
            throw;
        }
    }

    /// <summary>
    /// 현재 요청자를 제외한 연결 인원과 승인 대기 인원 계산
    /// 연결과 캐릭터 준비 목록에 함께 포함된 인원의 중복 제외
    /// </summary>
    private int CountOccupiedSlots(ulong requestingClientId)
    {
        int count = 0;

        foreach (NetworkClient client in _networkManager.ConnectedClientsList)
        {
            if (client.ClientId != requestingClientId)
            {
                count++;
            }
        }

        foreach (ulong clientId in _joinDeadlines.Keys)
        {
            if (clientId != requestingClientId &&
                !_networkManager.ConnectedClients.ContainsKey(clientId))
            {
                count++;
            }
        }

        return count;
    }

    private void RejectCharacterJoin(ulong clientId, string reason)
    {
        _joinDeadlines.Remove(clientId);
        if (clientId == _networkManager.LocalClientId)
        {
            LastStartError = reason;
            StopSession();
            return;
        }

        _networkManager.DisconnectClient(clientId, reason);
    }

    /// <summary>
    /// 연결 및 초기화 확인 누락으로 인한 무기한 대기 방지
    /// </summary>
    private IEnumerator WatchJoinProgress()
    {
        while (!IsStopping && HasPreparedJoin)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            _bossSceneFlow.Tick(now);
            if (IsStopping || !HasPreparedJoin)
            {
                _joinWatchCoroutine = null;
                yield break;
            }
            BattleResults.Tick(now);
            if (_localJoinState != LocalJoinState.Joined && now >= _localJoinDeadline)
            {
                _joinWatchCoroutine = null;
                LastStartError = "캐릭터 접속 준비 시간 초과";
                StopSession();
                yield break;
            }

            _expiredJoinClients.Clear();
            foreach (KeyValuePair<ulong, double> pending in _joinDeadlines)
            {
                if (now >= pending.Value)
                {
                    _expiredJoinClients.Add(pending.Key);
                }
            }

            for (int i = 0; i < _expiredJoinClients.Count; i++)
            {
                RejectCharacterJoin(_expiredJoinClients[i], "캐릭터 초기화 확인 시간 초과");
                if (IsStopping || !HasPreparedJoin)
                {
                    _joinWatchCoroutine = null;
                    yield break;
                }
            }

            yield return null;
        }

        _joinWatchCoroutine = null;
    }

    private void RestorePreparedLocalCharacter()
    {
        if (HasPreparedJoin && _pendingLocalPlayer != null)
        {
            _pendingLocalPlayer.SetNetworkJoinLocked(false);
        }

        _pendingLocalPlayer = null;
        _pendingJoinRequestJson = null;
        _localJoinState = LocalJoinState.None;
    }

    private bool CanStart()
    {
        if (!isActiveAndEnabled || !IsHostScene() || IsStopping ||
            _networkManager.IsListening || _networkManager.IsClient ||
            _networkManager.ShutdownInProgress || HasPreparedJoin)
        {
            return false;
        }

        if (!GameManager.Instance.DataManager.IsDataLoaded)
        {
            return false;
        }

        PlaySessionController session = GameManager.Instance.PlaySessionController;
        return session.HasActivePlayer && session.ActivePlayer.CanPrepareNetworkJoin;
    }

    private static bool IsHostScene()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameSceneId.HostScene.ToString();
    }

    [ContextMenu("Network/Start Host")]
    private void StartHostFromInspector()
    {
        if (!TryStartHost())
        {
            Debug.LogError($"Host 시작 실패: {LastStartError}", this);
        }
    }

    [ContextMenu("Network/Start Client")]
    private void StartClientFromInspector()
    {
        if (!TryStartClient(_serverAddress))
        {
            Debug.LogError($"Client 시작 실패: {LastStartError}", this);
        }
    }

    [ContextMenu("Network/Start Boss Encounter")]
    private async void StartBossEncounterFromInspector()
    {
        try
        {
            if (!await TryStartBossEncounterAsync() && this != null)
            {
                Debug.LogWarning($"보스전 출발 보류: {LastStartError}", this);
            }
        }
        catch (Exception exception)
        {
            if (this != null) { Debug.LogException(exception, this); }
        }
    }

    /// <summary>
    /// 대기실 위치와 방 정원 확인 후 참가 승인
    /// 승인 직후 초기화 대기 목록에 등록하여 자리 확보
    /// </summary>
    private void HandleConnectionApproval(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = false;
        response.CreatePlayerObject = false;
        response.Pending = false;
        if (IsStopping || !IsHostScene() || !SceneTransition.CanAcceptParticipants)
        {
            response.Reason = "출발 전 대기실에서만 참가 가능";
            return;
        }

        if (CountOccupiedSlots(request.ClientNetworkId) >= _maxPlayers)
        {
            response.Reason = "방의 정원 초과";
            return;
        }

        _joinDeadlines[request.ClientNetworkId] =
            Time.realtimeSinceStartupAsDouble + _joinTimeoutSeconds;

        response.Approved = true;
        NotifyStateChanged();
    }

    private void HandleClientConnected(ulong clientId)
    {
        NotifyStateChanged();
        if (clientId != _networkManager.LocalClientId || IsStopping ||
            _localJoinState is LocalJoinState.RequestSent or LocalJoinState.Joined)
        {
            return;
        }

        if (!HasPreparedJoin)
        {
            LastStartError = "접속 전에 로컬 캐릭터 준비 필요";
            StopSession();
            return;
        }

        BattleResults.Register();
        // 호스트의 즉시 로컬 전송 전에 서버 수신 처리 등록
        if (_networkManager.IsServer)
        {
            RegisterJoinHandler();
        }

        _localJoinState = LocalJoinState.RequestSent;
        try
        {
            CharacterJoinMessage.Send(_networkManager, _pendingJoinRequestJson);
        }
        catch (Exception exception)
        {
            LastStartError = "캐릭터 접속 요청 전송 실패";
            Debug.LogException(exception, this);
            StopSession();
        }
    }

    /// <summary>
    /// 서버 수신 경계에서 요청 검증과 캐릭터 생성
    /// 송신자 ID는 JSON이 아닌 NGO 연결 정보 사용
    /// </summary>
    private void HandleCharacterJoinRequest(ulong clientId, FastBufferReader reader)
    {
        if (IsStopping || !_joinDeadlines.ContainsKey(clientId) ||
            !_networkManager.ConnectedClients.ContainsKey(clientId) ||
            !_processedJoinClients.Add(clientId))
        {
            return;
        }

        if (!CharacterJoinMessage.TryRead(reader, _maxJoinRequestBytes, out string json, out string errorMessage) ||
            !_joinRequestSerializer.TryDeserialize(json, out CharacterJoinRequest request, out errorMessage))
        {
            RejectCharacterJoin(clientId, errorMessage);
            return;
        }

        if (!IsHostScene() ||
            !_joinPositionBounds.Contains(new Vector2(request.Position.x, request.Position.y)) ||
            request.Position.z != _serverPlayerPrefab.transform.position.z)
        {
            RejectCharacterJoin(clientId, "대기실 생성 위치 오류");
            return;
        }

        NetworkPlayerController player = null;
        try
        {
            NetworkObject playerObject = ObjectPool.Pop(_serverPlayerPrefab.gameObject, request.Position, Quaternion.identity);
            player = playerObject.GetComponent<NetworkPlayerController>();
            player.InitializeServerCharacter(GameManager.Instance.DataManager, request.CharacterState);
            player.NetworkObject.SpawnAsPlayerObject(clientId, destroyWithScene: false);
        }
        catch (Exception exception)
        {
            if (player != null && player.NetworkObject.IsSpawned)
            {
                player.NetworkObject.Despawn(true);
            }
            else if (player != null)
            {
                ObjectPool.Push(player.NetworkObject);
            }

            Debug.LogException(exception, this);
            RejectCharacterJoin(clientId, "캐릭터 초기화 실패");
        }
    }

    public void HandlePlayerInitializationFailure(NetworkPlayerController player, Exception exception)
    {
        Debug.LogException(exception, player);
        if (_networkManager.IsServer)
        {
            RejectCharacterJoin(player.OwnerClientId, "네트워크 캐릭터 구성 오류");
        }
        else
        {
            LastStartError = "네트워크 캐릭터 구성 오류";
            StopSession();
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        BattleResults?.RemoveParticipant(clientId);
        _joinDeadlines.Remove(clientId);
        _processedJoinClients.Remove(clientId);
        Roster.Remove(clientId, out _);
        if (_networkManager.IsServer && SceneTransition.IsTransitioning && !IsStopping)
        {
            FailSceneTransition("공동 씬 이동 중 참가자 연결 종료");
            return;
        }
        if (clientId == _networkManager.LocalClientId &&
            !string.IsNullOrEmpty(_networkManager.DisconnectReason))
        {
            LastStartError = _networkManager.DisconnectReason;
        }
        NotifyStateChanged();
    }

    private void HandleClientStopped(bool wasHost) => CleanupSession();

    private void HandleServerStopped(bool wasHost) => CleanupSession();

    private void OnDisable()
    {
        if (!_callbacksRegistered)
        {
            return;
        }

        // NetworkManager가 먼저 파괴된 종료 순서 허용
        if (_networkManager != null)
        {
            StopSession();
            _networkManager.ConnectionApprovalCallback = null;
            _networkManager.OnServerStarted -= RegisterJoinHandler;
            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _networkManager.OnClientStopped -= HandleClientStopped;
            _networkManager.OnServerStopped -= HandleServerStopped;
        }

        CleanupSession();
        _callbacksRegistered = false;
    }

    /// <summary>
    /// 연결 대기 취소 또는 현재 세션 종료
    /// </summary>
    public void StopSession()
    {
        if (_shutdownStage == SessionShutdownStage.CleaningUp) { return; }
        _shutdownStage = SessionShutdownStage.Stopping;
        BeginInternetCleanup();
        NotifyStateChanged();
        if (_networkManager.ShutdownInProgress)
        {
            return;
        }

        if (_networkManager.IsListening || _networkManager.IsClient || _networkManager.IsServer)
        {
            _networkManager.Shutdown();
            return;
        }

        CleanupSession();
    }

    private void UnregisterJoinHandler()
    {
        if (_joinMessagingManager == null)
        {
            return;
        }

        _joinMessagingManager.UnregisterNamedMessageHandler(CharacterJoinMessage.Name);
        _joinMessagingManager = null;
    }

    /// <summary>
    /// 네트워크 객체 파괴 전에 소유자 입력과 세션 참조를 기존 캐릭터로 복귀
    /// 온라인 성장 결과 저장은 별도 서버 결과 경로 필요
    /// </summary>
    public void ReleaseLocalCharacterJoin(NetworkPlayerController networkPlayer)
    {
        if (_ownedNetworkPlayer != networkPlayer)
        {
            return;
        }

        networkPlayer.EndOwnerInput();
        Player replacement = networkPlayer.Character;
        replacement.DisconnectNetworkOwnerPresentation();
        _ownedNetworkPlayer = null;
        // 요청 재전송 없이 소유자 연결만 해제한 뒤 세션 종료로 진행
        _localJoinState = LocalJoinState.RequestSent;

        // 출발 이후 로컬 캐릭터 복구는 결과 저장을 확인한 종료 처리에서 수행
        if (!SceneTransition.CanAcceptParticipants || _pendingLocalPlayer == null)
        {
            return;
        }

        PlaySessionController session = GameManager.Instance.PlaySessionController;
        if (session.ActivePlayer == replacement)
        {
            session.ReplacePlayer(replacement, _pendingLocalPlayer);
        }

        _pendingLocalPlayer.SetNetworkJoinLocked(false);
        _pendingLocalPlayer.gameObject.SetActive(true);
        _pendingLocalPlayer.ConnectLocalOwner();
        if (MapManager.Instance != null)
        {
            MapManager.Instance.FollowLocalPlayer(_pendingLocalPlayer);
        }

        if (!IsStopping && !_networkManager.ShutdownInProgress)
        {
            LastStartError = "소유자 네트워크 캐릭터 연결 종료";
            StopSession();
        }
    }

    private void CleanupSession()
    {
        if (_shutdownStage == SessionShutdownStage.CleaningUp) { return; }
        _shutdownStage = SessionShutdownStage.CleaningUp;
        try
        {
            BeginInternetCleanup();
            bool departedForBattle = !SceneTransition.CanAcceptParticipants;
            Player localPlayer = _pendingLocalPlayer;
            SaveData savedResult = BattleResults is { IsSaved: true, UnclaimedRewardCount: 0 }
                ? BattleResults.Current.State
                : null;
            AsyncOperation pendingSceneLoad = _bossSceneFlow?.PendingSceneLoad;
            _bossSceneFlow?.Dispose();
            _bossSceneFlow = null;
            if (_joinWatchCoroutine != null)
            {
                StopCoroutine(_joinWatchCoroutine);
                _joinWatchCoroutine = null;
            }

            if (_ownedNetworkPlayer != null)
            {
                ReleaseLocalCharacterJoin(_ownedNetworkPlayer);
            }

            UnregisterJoinHandler();
            BattleResults?.Dispose();
            BattleResults = null;
            _joinDeadlines.Clear();
            _processedJoinClients.Clear();
            _expiredJoinClients.Clear();
            Roster.Clear();
            RestorePreparedLocalCharacter();
            SceneTransition.Reset();
            if (departedForBattle && isActiveAndEnabled)
            {
                PlaySessionController playSession = GameManager.Instance.PlaySessionController;
                if (!playSession.TryReturnToBossLobby(localPlayer, savedResult, pendingSceneLoad))
                {
                    if (localPlayer != null) { Destroy(localPlayer.gameObject); }
                    playSession.EndNetworkSession(pendingSceneLoad);
                }
            }
            _shutdownStage = SessionShutdownStage.Ready;
        }
        catch
        {
            _shutdownStage = SessionShutdownStage.Failed;
            throw;
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    [ContextMenu("Network/Stop Session")]
    private void StopSessionFromInspector() => StopSession();
}
