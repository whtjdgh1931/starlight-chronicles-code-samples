using UnityEngine;

using System;

/// <summary>
/// 보스 생성 흐름 시작
/// </summary>
public sealed class BossEncounterController : MonoBehaviour
{
    [SerializeField] private BossSpawner _bossSpawner;

    private DataManager _dataManager;
    private bool _hasStarted;

    private BossMonster _activeBoss;

    public event Action<BossMonster> OnBossSpawned;

    /// <summary>
    /// 준비된 게임 데이터로 보스전 시작
    /// </summary>
    public void Initialize(ObjectPool objectPool, DataManager dataManager)
    {
        if (_bossSpawner == null)
        {
            throw new MissingReferenceException($"{name}: BossSpawner 참조 필요");
        }

        if (objectPool == null)
        {
            throw new ArgumentNullException(nameof(objectPool));
        }

        if (dataManager == null)
        {
            throw new ArgumentNullException(nameof(dataManager));
        }

        Shutdown();

        _dataManager = dataManager;
        _bossSpawner.Initialize(objectPool);
        _bossSpawner.OnBossSpawned += HandleBossSpawned;

        if (_dataManager.IsDataLoaded)
        {
            StartEncounter();
            return;
        }

        _dataManager.OnAllDataLoaded += HandleDataLoaded;
    }

    private void StartEncounter()
    {
        if (_hasStarted)
        {
            return;
        }

        _bossSpawner.Configure(_dataManager);
        _bossSpawner.SpawnBoss();
        _hasStarted = true;
    }

    private void HandleDataLoaded()
    {
        _dataManager.OnAllDataLoaded -= HandleDataLoaded;
        StartEncounter();
    }

    private void HandleBossSpawned(BossMonster boss)
    {
        _activeBoss = boss;
        _activeBoss.OnReturnedToPool += HandleBossReturned;
        OnBossSpawned?.Invoke(boss);
    }

    private void HandleBossReturned(MonsterBase monster)
    {
        if (monster != _activeBoss)
        {
            return;
        }

        _activeBoss.OnReturnedToPool -= HandleBossReturned;
        _activeBoss = null;
    }

    /// <summary>
    /// 데이터와 보스 생성 이벤트 연결 해제
    /// </summary>
    public void Shutdown()
    {
        if (_dataManager != null)
        {
            _dataManager.OnAllDataLoaded -= HandleDataLoaded;
        }

        if (_bossSpawner != null)
        {
            _bossSpawner.OnBossSpawned -= HandleBossSpawned;
            _bossSpawner.Shutdown();
        }

        if (_activeBoss != null)
        {
            _activeBoss.OnReturnedToPool -= HandleBossReturned;
            _activeBoss = null;
        }
        _hasStarted = false;
        _dataManager = null;
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
