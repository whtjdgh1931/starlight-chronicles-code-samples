using System;
using UnityEngine;

/// <summary>
/// 플레이 중인 Player의 수명과 캐릭터 선택 화면 복귀를 관리
/// </summary>
public sealed class PlaySessionController
{
    private readonly CharacterSession _characterSession;
    private readonly GameSceneManager _sceneManager;


    private Player _activePlayer;
    private bool _isEndingSession;

    public bool HasActivePlayer => _activePlayer != null;
    public Player ActivePlayer => _activePlayer;

    public HuntDifficulty SelectedHuntDifficulty { get; private set; } = HuntDifficulty.Beginner;

    /// <summary>
    /// 입장 조건 확인을 마친 사냥터 구성 보관
    /// 캐릭터 저장 데이터에는 포함하지 않는 세션 상태
    /// </summary>
    public void SelectHuntDifficulty(HuntDifficulty difficulty) => SelectedHuntDifficulty = difficulty;


    public PlaySessionController(CharacterSession characterSession, GameSceneManager sceneManager)
    {
        _characterSession = characterSession ?? throw new ArgumentNullException( nameof(characterSession));

        _sceneManager = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));
    }

    /// <summary>
    /// 현재 플레이어 등록과 새 플레이 세션의 종료 상태 초기화
    /// 입력 활성화는 호출 측에서 처리
    /// </summary>
    public void RegisterPlayer(Player player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        if (_activePlayer != null && _activePlayer != player)
        {
            throw new InvalidOperationException("이미 다른 플레이어가 플레이 세션에 등록되어 있습니다.");
        }

        if (_activePlayer == null) { SelectedHuntDifficulty = HuntDifficulty.Beginner; }
        _activePlayer = player;
        _isEndingSession = false;
    }

    /// <summary>
    /// 예상한 현재 플레이어와 일치할 때 준비된 플레이어로 참조 교체
    /// 입력·UI 연결과 객체 정리는 호출 측에서 처리
    /// </summary>
    public void ReplacePlayer(Player expectedPlayer, Player replacementPlayer)
    {
        if (expectedPlayer == null)
        {
            throw new ArgumentNullException(nameof(expectedPlayer));
        }

        if (replacementPlayer == null)
        {
            throw new ArgumentNullException(nameof(replacementPlayer));
        }

        if (_isEndingSession)
        {
            throw new InvalidOperationException("플레이 세션 종료 중에는 플레이어 교체 불가");
        }

        if (_activePlayer != expectedPlayer)
        {
            throw new InvalidOperationException("교체 대상과 현재 플레이어 불일치");
        }

        if (!replacementPlayer.CanCaptureCharacterState)
        {
            throw new InvalidOperationException("새 플레이어의 캐릭터 상태 초기화 필요");
        }

        _activePlayer = replacementPlayer;
    }

    /// <summary>
    /// 현재 캐릭터 저장과 제거 후 선택 씬 복귀
    /// 저장 실패 시 현재 플레이 세션 유지
    /// </summary>
    public bool TryReturnToCharacterSelection(string selectionSceneName, out string errorMessage)
    {
        if (_isEndingSession)
        {
            errorMessage = "이미 플레이 세션을 종료하고 있습니다.";

            return false;
        }

        if (_activePlayer == null)
        {
            errorMessage = "종료할 플레이어가 없습니다.";

            return false;
        }

        if (_activePlayer.IsNetworkControlled || _activePlayer.IsNetworkJoinLocked)
        {
            errorMessage = "네트워크 접속 준비 또는 세션 종료 후 캐릭터 선택 가능";
            return false;
        }

        if (string.IsNullOrWhiteSpace(selectionSceneName))
        {
            errorMessage = "캐릭터 선택 씬 이름이 필요합니다.";

            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(
                selectionSceneName))
        {
            errorMessage =$"Build Settings에 씬이 없습니다. " + $"Scene: {selectionSceneName}";

            return false;
        }

        if (!_activePlayer.TrySave(out errorMessage))
        {
            return false;
        }

        _isEndingSession = true;

        Player playerToDestroy = _activePlayer;
        playerToDestroy.DisconnectLocalOwner();
        _activePlayer = null;
        SelectedHuntDifficulty = HuntDifficulty.Beginner;

        _characterSession.Clear();

        UnityEngine.Object.Destroy(playerToDestroy.gameObject);
        _sceneManager.ChangeScene(selectionSceneName);

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 맵 준비 실패 시 불완전한 로컬 캐릭터 제거와 선택 화면 복귀
    /// 실패한 상태의 자동 저장 제외
    /// </summary>
    public void AbortMapEntry(AsyncOperation pendingSceneLoad)
    {
        if (_activePlayer != null && !_activePlayer.IsNetworkControlled)
        {
            _activePlayer.DisconnectLocalOwner();
            UnityEngine.Object.Destroy(_activePlayer.gameObject);
        }
        _isEndingSession = false;
        EndNetworkSession(pendingSceneLoad);
    }

    /// <summary>저장 완료된 전투 결과를 로컬 캐릭터에 적용하고 원정대 로비로 복귀</summary>
    internal bool TryReturnToBossLobby(Player player, SaveData savedResult, AsyncOperation pendingSceneLoad)
    {
        if (_isEndingSession || player == null || savedResult == null || !_characterSession.HasSelectedCharacter ||
            !Application.CanStreamedLevelBeLoaded(nameof(GameSceneId.HostScene)))
        {
            return false;
        }

        try
        {
            player.RestoreSavedBattleResult(savedResult);
            player.gameObject.SetActive(true);
            player.ConnectLocalOwner();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, player);
            return false;
        }

        _activePlayer = null;
        RegisterPlayer(player);
        _sceneManager.ReturnToBossLobbyAfterLoad(pendingSceneLoad, player.IsDead);
        return true;
    }

    /// <summary>
    /// 출발 이후 네트워크 종료 시 캐릭터 참조 정리와 선택 화면 복귀
    /// 접속 전 저장 상태는 유지하고 미확정 온라인 결과 저장 제외
    /// </summary>
    public void EndNetworkSession(AsyncOperation pendingSceneLoad)
    {
        if (_isEndingSession)
        {
            return;
        }
        _isEndingSession = true;
        _activePlayer = null;
        SelectedHuntDifficulty = HuntDifficulty.Beginner;
        _characterSession.Clear();
        _sceneManager.ChangeSceneAfterLoad(GameSceneId.SelectScene, pendingSceneLoad);
    }
}
