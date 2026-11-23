using System;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 현재 네트워크 보스와 보스 HUD 연결 관리
/// 활성 보스 한 마리 기준
/// </summary>
public sealed class NetworkBossEncounterController : MonoBehaviour
{
    [SerializeField] private BossHudController _bossHud;
    [SerializeField] private NetworkBossBattleController _battle;
    [SerializeField] private NetworkBossController _bossPrefab;
    [SerializeField] private Transform _spawnPoint;

    private NetworkBossController _activeBoss;

    /// <summary>
    /// 데이터 초기화가 완료된 네트워크 보스 연결
    /// 보스 생성 이후 각 컴퓨터에서 호출
    /// </summary>
    public void BindBoss(NetworkBossController boss)
    {
        if (_activeBoss == boss)
        {
            return;
        }

        if (_activeBoss != null)
        {
            throw new InvalidOperationException("기존 보스 제거 후 새 보스 연결 필요");
        }

        if (_bossHud == null)
        {
            throw new MissingReferenceException($"{name}: BossHudController 참조 필요");
        }

        _bossHud.Initialize();

        _activeBoss = boss;
        _activeBoss.Removed += HandleBossRemoved;

        _bossHud.ShowBoss(boss.Boss);
    }

    /// <summary>
    /// 온라인 맵 준비 완료 후 서버에서 단일 보스 생성
    /// 클라이언트는 NGO 생성 통지를 통해 화면 연결
    /// </summary>
    public void StartEncounter(NetworkManager networkManager)
    {
        if (!networkManager.IsServer || _activeBoss != null)
        {
            return;
        }

        if (_bossPrefab == null || _spawnPoint == null || _bossHud == null || _battle == null)
        {
            throw new MissingReferenceException($"{name}: 네트워크 보스 프리팹·생성 위치·HUD 참조 필요");
        }

        _bossPrefab.ValidateConfiguration(GameManager.Instance.DataManager);

        NetworkSessionController session = networkManager.GetComponent<NetworkSessionController>();
        NetworkObject bossObject = session.ObjectPool.Pop(_bossPrefab.gameObject, _spawnPoint.position, _spawnPoint.rotation);
        NetworkBossController boss = bossObject.GetComponent<NetworkBossController>();
        try
        {
            boss.NetworkObject.Spawn(destroyWithScene: true);
            if (_activeBoss != boss)
            {
                throw new InvalidOperationException($"{name}: 네트워크 보스 초기화 또는 HUD 연결 실패");
            }
            _battle.BeginBattle(boss.Boss);
        }
        catch
        {
            if (boss.NetworkObject.IsSpawned)
            {
                boss.NetworkObject.Despawn(true);
            }
            else
            {
                session.ObjectPool.Push(boss.NetworkObject);
            }

            throw;
        }
    }

    private void HandleBossRemoved(NetworkBossController boss)
    {
        if (_activeBoss == boss)
        {
            Shutdown();
        }
    }

    /// <summary>
    /// 보스 제거 또는 화면 종료 시 구독과 HUD 정리
    /// 네트워크 객체 제거는 NetworkBossController에서 담당
    /// </summary>
    public void Shutdown()
    {
        if (_activeBoss == null)
        {
            return;
        }

        _activeBoss.Removed -= HandleBossRemoved;
        _activeBoss = null;

        _bossHud.Shutdown();
    }

    private void OnDisable()
    {
        Shutdown();
    }
}
