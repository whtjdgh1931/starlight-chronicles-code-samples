using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프로젝트에서 사용하는 씬 식별자
/// </summary>
public enum GameSceneId
{
    None = 0,
    TitleScene = 1,
    SelectScene = 2,
    HomeScene = 3,
    HuntScene = 4,
    HostScene = 5,
    BossScene = 6
}

public class GameSceneManager : MonoBehaviour
{
    [SerializeField] private Image _fadeImage;
    [SerializeField] private float _fadeDuration = 0.5f;
    [SerializeField, Min(1f)] private float _playerRegistrationTimeoutSeconds = 30f;

    [Header("싱글 사망 후 마을 복귀")]
    [SerializeField, Min(0)] private int _revivePortalId = 1;
    [SerializeField, Min(1)] private int _soloBossLobbyPortalId = 5;
    [SerializeField, Min(0f)] private float _reviveInvincibleDuration = 2f;

    private Coroutine _transition;
    private AsyncOperation _activeLoad;

    public void Initialize()
    {
        if (_fadeImage == null)
        {
            CreateFadeCanvas();
        }
    }

    /// <summary>
    /// 맵 초기화가 필요 없는 UI 중심 씬으로 전환
    /// </summary>
    public void ChangeScene(string sceneName)
    {
        BeginTransition(sceneName, false, 0);
    }

    public void ChangeScene(GameSceneId sceneId)
    {
        ChangeScene(sceneId.ToString());
    }

    /// <summary>
    /// 취소할 수 없는 기존 씬 로딩 완료 후 UI 씬으로 복귀
    /// 네트워크 종료 중 씬 로딩의 동시 실행 방지
    /// </summary>
    public void ChangeSceneAfterLoad(GameSceneId sceneId, AsyncOperation pendingSceneLoad)
    {
        ChangeSceneAfterLoad(sceneId, pendingSceneLoad, false, 0, false);
    }

    /// <summary>네트워크 종료 후 원정대 로비 맵과 귀환 포털 초기화</summary>
    internal void ReturnToBossLobbyAfterLoad(AsyncOperation pendingSceneLoad, bool revivePlayer)
    {
        ChangeSceneAfterLoad(GameSceneId.HostScene, pendingSceneLoad, true, _soloBossLobbyPortalId, revivePlayer);
    }

    private void ChangeSceneAfterLoad(GameSceneId sceneId, AsyncOperation pendingSceneLoad, bool initializeMap, int portalId, bool revivePlayer)
    {
        // 취소 불가능한 엔진 로딩은 유지하고 이전 화면 전환 코루틴만 종료
        AsyncOperation pending = _activeLoad != null && !_activeLoad.isDone ? _activeLoad : pendingSceneLoad;
        if (_transition != null) { StopCoroutine(_transition); }
        ReleaseTransition();
        _transition = StartCoroutine(ReturnAfterLoad(sceneId, pending, initializeMap, portalId, revivePlayer));
    }

    public void ChangeMapScene(GameSceneId sceneId, int targetPortalId)
    {
        ChangeMapScene(sceneId.ToString(), targetPortalId);
    }

    /// <summary>
    /// 포털 위치와 MapManager 초기화가 필요한 플레이 맵으로 전환
    /// </summary>
    public void ChangeMapScene(string sceneName, int targetPortalId)
    {
        BeginTransition(sceneName, true, targetPortalId);
    }

    /// <summary>로비에서 혼자 도전 씬 전환 요청</summary>
    public bool TryEnterSoloBoss(int arrivalPortalId)
    {
        string destination = nameof(GameSceneId.BossScene);
        if (_transition != null || arrivalPortalId <= 0 ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != nameof(GameSceneId.HostScene) ||
            !Application.CanStreamedLevelBeLoaded(destination))
        {
            return false;
        }

        BeginTransition(destination, true, arrivalPortalId);
        return true;
    }

    /// <summary>로컬 보스전 패배 이후 로비 도착과 부활 요청</summary>
    public bool TryReturnToLobbyAfterSoloBossDefeat(Player player, out string errorMessage)
    {
        if (_transition != null)
        {
            errorMessage = "이미 이동 중이에요. 잠시 기다려 주세요.";
            return false;
        }

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != nameof(GameSceneId.BossScene) ||
            player == null || player != GameManager.Instance.PlaySessionController.ActivePlayer ||
            !player.IsDead || player.IsNetworkControlled || player.IsNetworkJoinLocked)
        {
            errorMessage = "혼자 도전 중 쓰러졌을 때 사용할 수 있어요.";
            return false;
        }

        string lobby = nameof(GameSceneId.HostScene);
        if (!Application.CanStreamedLevelBeLoaded(lobby))
        {
            errorMessage = "로비를 불러올 수 없어요.";
            return false;
        }

        BeginTransition(lobby, true, _soloBossLobbyPortalId, revivePlayer: true);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 로컬 사망 후 마을 복귀를 지원하는 씬 확인
    /// </summary>
    internal static bool SupportsTownReturnAfterDeath(string sceneName)
    {
        return sceneName == nameof(GameSceneId.HomeScene) || sceneName == nameof(GameSceneId.HuntScene);
    }

    /// <summary>
    /// 마을·사냥터에서 사망한 로컬 플레이어의 마을 복귀 요청, 자동 저장 제외
    /// </summary>
    public bool TryReturnToTownAfterDeath(out string errorMessage)
    {
        if (_transition != null)
        {
            errorMessage = "이미 이동 중이에요. 잠시 기다려 주세요.";
            return false;
        }
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Player player = GameManager.Instance.PlaySessionController.ActivePlayer;
        if (!SupportsTownReturnAfterDeath(sceneName) ||
            player == null || !player.IsDead || player.IsNetworkControlled || player.IsNetworkJoinLocked)
        {
            errorMessage = "마을이나 사냥터에서 쓰러졌을 때 사용할 수 있어요.";
            return false;
        }
        if (!Application.CanStreamedLevelBeLoaded(nameof(GameSceneId.HomeScene)))
        {
            errorMessage = "마을을 불러올 수 없어요. 씬 설정을 확인해 주세요.";
            return false;
        }

        BeginTransition(nameof(GameSceneId.HomeScene), true, _revivePortalId, revivePlayer: true);
        errorMessage = string.Empty;
        return true;
    }

    private void CreateFadeCanvas()
    {
        GameObject canvasObj = new GameObject(Constants.STR_FadeCanvas);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Constants.SceneFadeSortingOrder;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);
        GameObject imageObj = new GameObject(Constants.STR_FadeImage);
        imageObj.transform.SetParent(canvasObj.transform, false);
        _fadeImage = imageObj.AddComponent<Image>();
        _fadeImage.color = new Color(0, 0, 0, 0);

        RectTransform rt = _fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        _fadeImage.raycastTarget = false;

    }

    private void BeginTransition(string sceneName, bool initializeMap, int targetPortalId, bool revivePlayer = false)
    {
        if (_transition != null) { return; }
        _transition = StartCoroutine(TransitionRoutine(sceneName, initializeMap, targetPortalId, revivePlayer));
    }

    private void ReleaseTransition()
    {
        _fadeImage.color = Color.clear;
        _fadeImage.raycastTarget = false;
        GameManager.Instance.InputManager.enabled = true;
        _transition = null;
    }

    private IEnumerator ReturnAfterLoad(GameSceneId sceneId, AsyncOperation pendingSceneLoad, bool initializeMap, int portalId, bool revivePlayer)
    {
        yield return null;
        if (pendingSceneLoad != null && !pendingSceneLoad.isDone)
        {
            yield return pendingSceneLoad;
        }
        // 엔진 로딩은 취소 API가 없어 완료 이후에만 다음 씬 요청
        _activeLoad = null;
        yield return TransitionRoutine(sceneId.ToString(), initializeMap, portalId, revivePlayer);
    }

    /// <summary>
    /// 화면 전환 예외와 코루틴 중단 시 페이드·입력 잠금 해제
    /// 중첩 코루틴 대신 직접 순회하여 로딩·맵 초기화 예외 관찰
    /// </summary>
    private IEnumerator TransitionRoutine(string sceneName, bool initializeMap, int targetPortalId, bool revivePlayer = false)
    {
        yield return null;
        Exception failure = null;
        IEnumerator steps = TransitionSteps(sceneName, initializeMap, targetPortalId, revivePlayer);
        try
        {
            while (true)
            {
                bool next;
                try { next = steps.MoveNext(); }
                catch (Exception exception) { failure = exception; break; }
                if (!next) { break; }
                yield return steps.Current;
            }
        }
        finally
        {
            (steps as IDisposable)?.Dispose();
            ReleaseTransition();
        }
        if (failure != null)
        {
            Debug.LogException(failure, this);
            if (initializeMap) { GameManager.Instance.PlaySessionController.AbortMapEntry(_activeLoad); }
        }
    }

    private IEnumerator TransitionSteps(string sceneName, bool initializeMap, int targetPortalId, bool revivePlayer)
    {
        GameManager.Instance.InputManager.enabled = false;
        _fadeImage.raycastTarget = true;

        float timer = 0f;

        while (timer < _fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            _fadeImage.color = new Color(0f, 0f, 0f, timer / _fadeDuration);
            yield return null;
        }

        _fadeImage.color = Color.black;

        if (initializeMap)
        {
            GameManager.Instance.NextSpawnPortalID = targetPortalId;
        }

        _activeLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        AsyncOperation asyncLoad = _activeLoad;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        _activeLoad = null;
        yield return null;

        if (initializeMap)
        {
            DataManager dataManager = GameManager.Instance.DataManager;
            PlaySessionController playSession = GameManager.Instance.PlaySessionController;
            float registrationTimer = 0f;

            // 데이터 로딩과 로컬 플레이어의 저장 복원·등록 대기
            while ((!dataManager.IsDataLoaded || !playSession.HasActivePlayer) &&
                   registrationTimer < _playerRegistrationTimeoutSeconds)
            {
                registrationTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!dataManager.IsDataLoaded || !playSession.HasActivePlayer)
            {
                throw new TimeoutException($"맵 진입 준비 시간 초과: {sceneName}");
            }

            MapManager.Instance.Initialize();
            MapManager.Instance.PlaceLocalPlayer(playSession.ActivePlayer);
            if (revivePlayer)
            {
                playSession.ActivePlayer.Revive(_reviveInvincibleDuration);
            }
        }

        timer = 0f;

        while (timer < _fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            _fadeImage.color = new Color(0f, 0f, 0f, 1f - timer / _fadeDuration);

            yield return null;
        }

        _fadeImage.color = Color.clear;
        _fadeImage.raycastTarget = false;

        if (initializeMap)
        {
            MapManager.Instance.StartGameplay();
            if (revivePlayer)
            {
                Player player = GameManager.Instance.PlaySessionController.ActivePlayer;
                player.SetLocalInputOwner(!player.IsDead);
            }
        }

        GameManager.Instance.InputManager.enabled = true;
    }
}
