using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 단일 보스맵의 NGO 로딩·서버 배치·소유자 준비 확인
/// 맵 참조는 도착 시 조회하고 진행 상태는 세션에서 공유
/// </summary>
public sealed class NetworkBossSceneFlow : IDisposable
{
    private readonly NetworkManager _networkManager;
    private readonly NetworkSessionController _session;
    private readonly NetworkSceneManager _sceneManager;
    private readonly HashSet<ulong> _pendingOwners = new();
    private double _deadline;
    public AsyncOperation PendingSceneLoad { get; private set; }

    public NetworkBossSceneFlow(NetworkManager networkManager, NetworkSessionController session)
    {
        _networkManager = networkManager;
        _session = session;
        _sceneManager = networkManager.SceneManager;
        _sceneManager.OnSceneEvent += HandleSceneEvent;
    }

    /// <summary>
    /// 세션에서 출발 조건 확인 후 참가자 전체의 공동 이동 시작
    /// </summary>
    public bool TryStart()
    {
        if (!_session.SceneTransition.TryBeginLoading())
        {
            return false;
        }

        try
        {
            _pendingOwners.Clear();
            _deadline = Time.realtimeSinceStartupAsDouble + _session.SceneTransitionTimeoutSeconds;
            foreach (NetworkClient client in _networkManager.ConnectedClientsList)
            {
                _pendingOwners.Add(client.ClientId);
                client.PlayerObject.GetComponent<NetworkPlayerController>().SetServerSceneTransitionLocked(true);
            }

            SceneEventProgressStatus status = _sceneManager.LoadScene(_session.BossScene.ToString(), LoadSceneMode.Single);
            if (status == SceneEventProgressStatus.Started)
            {
                return true;
            }

            _session.FailSceneTransition($"공동 씬 로딩 시작 실패: {status}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, _session);
            _session.FailSceneTransition("보스맵 출발 구성 오류");
        }
        return false;
    }

    /// <summary>
    /// 해당 플레이어 소유자의 준비 응답 일회 처리
    /// 중복·지연 응답은 현재 단계와 대기 목록에서 제외
    /// </summary>
    public void ConfirmOwnerReady(ulong clientId)
    {
        if (!_networkManager.IsServer || !_session.SceneTransition.IsPreparing || !_pendingOwners.Remove(clientId))
        {
            return;
        }
        if (_pendingOwners.Count > 0)
        {
            return;
        }

        try
        {
            if (!_session.SceneTransition.TryBeginPlaying())
            {
                return;
            }
            foreach (NetworkClient client in _networkManager.ConnectedClientsList)
            {
                client.PlayerObject.GetComponent<NetworkPlayerController>().SetServerSceneTransitionLocked(false);
            }
            MapManager.Instance.StartGameplay();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, _session);
            _session.FailSceneTransition("보스 전투 시작 실패");
        }
    }

    /// <summary>
    /// 접속 감시 루프에서 공동 이동과 준비 제한 시간 확인
    /// </summary>
    public void Tick(double now)
    {
        if (_session.SceneTransition.IsTransitioning && now >= _deadline)
        {
            _session.FailSceneTransition("공동 씬 이동 또는 화면 준비 시간 초과");
        }
    }

    private void PrepareServerMap(SceneEvent sceneEvent)
    {
        if (sceneEvent.ClientsThatTimedOut.Count > 0 || !_pendingOwners.SetEquals(sceneEvent.ClientsThatCompleted))
        {
            _session.FailSceneTransition("참가자 전체의 보스맵 로딩 미완료");
            return;
        }

        try
        {
            if (!_session.SceneTransition.TryBeginPreparing())
            {
                return;
            }
            _deadline = Time.realtimeSinceStartupAsDouble + _session.SceneTransitionTimeoutSeconds;
            MapManager map = MapManager.Instance;
            if (map == null || map.gameObject.scene.name != _session.BossScene.ToString())
            {
                throw new InvalidOperationException("도착한 보스맵의 MapManager 필요");
            }

            map.Initialize();
            Vector3 position = map.GetPortalPosition(_session.BossArrivalPortalId);
            foreach (NetworkClient client in _networkManager.ConnectedClientsList)
            {
                client.PlayerObject.GetComponent<NetworkPlayerController>().PlaceAtServerMapSpawn(position);
            }

            // 전원 배치 이후 준비 요청, 호스트의 즉시 RPC 응답 순서 보장
            foreach (NetworkClient client in _networkManager.ConnectedClientsList)
            {
                client.PlayerObject.GetComponent<NetworkPlayerController>().PrepareBossMapOwner();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, _session);
            _session.FailSceneTransition("보스맵 배치 또는 초기화 실패");
        }
    }

    private void HandleSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneName != _session.BossScene.ToString())
        {
            return;
        }

        if (sceneEvent.SceneEventType == SceneEventType.Load)
        {
            PendingSceneLoad = sceneEvent.AsyncOperation;
            if (!_networkManager.IsServer && _session.SceneTransition.TryBeginLoading())
            {
                _deadline = Time.realtimeSinceStartupAsDouble + _session.SceneTransitionTimeoutSeconds;
            }
            return;
        }

        if (_networkManager.IsServer && sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted
            && _session.SceneTransition.IsLoading)
        {
            PrepareServerMap(sceneEvent);
        }
    }

    public void Dispose()
    {
        _sceneManager.OnSceneEvent -= HandleSceneEvent;
        _pendingOwners.Clear();
    }
}
