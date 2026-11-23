using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 데이터 준비와 단일 보스 생성
/// </summary>
public sealed class BossSpawner : MonoBehaviour
{
    [SerializeField] private BossMonster _bossPrefab;
    [SerializeField, Min(1)] private int _bossId;
    [SerializeField] private Transform _spawnPoint;

    private ObjectPool _objectPool;
    private MonsterData _monsterData;
    private BossData _bossData;
    private IReadOnlyDictionary<int, MonsterAttackData> _attackTable;
    private IReadOnlyList<BossPhaseAttackData> _initialPhaseAttacks;
    private BossMonster _activeBoss;
    private bool _isInitialized;

    public event Action<BossMonster> OnBossSpawned;

    /// <summary>
    /// 보스 생성에 필요한 오브젝트 풀 연결
    /// </summary>
    public void Initialize(ObjectPool objectPool)
    {
        ValidateReferences(objectPool);
        _objectPool = objectPool;
    }

    /// <summary>
    /// 로딩 완료된 보스 데이터 준비
    /// </summary>
    public void Configure(DataManager dataManager)
    {
        if (dataManager == null)
        {
            throw new ArgumentNullException(nameof(dataManager));
        }

        _monsterData = dataManager.GetMonsterData(_bossId);
        _bossData = dataManager.GetBossData(_bossId);
        _attackTable = dataManager.MonsterAttackTable;
        _initialPhaseAttacks = dataManager.GetBossPhaseAttacks(_bossId, (int)BossMonster.Phase.First);

        ValidateData();
        _isInitialized = true;
    }

    private void ValidateReferences(ObjectPool objectPool)
    {
        if (_bossPrefab == null)
        {
            throw new MissingReferenceException($"{name}: BossMonster Prefab 참조 필요");
        }

        if (_spawnPoint == null)
        {
            throw new MissingReferenceException($"{name}: 보스 생성 위치 참조 필요");
        }

        if (objectPool == null)
        {
            throw new ArgumentNullException(nameof(objectPool));
        }
    }

    private void ValidateData()
    {
        if (_monsterData == null)
        {
            throw new InvalidOperationException($"{name}: MonsterData 누락 BossID: {_bossId}");
        }

        if (_bossData == null)
        {
            throw new InvalidOperationException($"{name}: BossData 누락 BossID: {_bossId}");
        }

        if (_initialPhaseAttacks.Count == 0)
        {
            throw new InvalidOperationException($"{name}: 1페이즈 공격 데이터 누락 BossID: {_bossId}");
        }
    }

    /// <summary>
    /// 준비된 데이터로 보스 단일 생성
    /// </summary>
    public BossMonster SpawnBoss()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException($"{name}: BossSpawner 초기화 필요");
        }

        if (_activeBoss != null)
        {
            throw new InvalidOperationException($"{name}: 활성 보스 중복 생성 불가");
        }

        GameObject bossObject = _objectPool.Pop(_bossPrefab.gameObject, transform);
        bossObject.transform.SetPositionAndRotation(_spawnPoint.position, _spawnPoint.rotation);

        _activeBoss = bossObject.GetComponent<BossMonster>();
        _activeBoss.Initialize(_monsterData, _bossData, _attackTable, _initialPhaseAttacks);
        _activeBoss.DeathCompleted += HandleDeathCompleted;

        OnBossSpawned?.Invoke(_activeBoss);
        return _activeBoss;
    }

    /// <summary>
    /// 완료된 보스의 추적 해제와 풀 반환
    /// </summary>
    private void HandleDeathCompleted(MonsterBase monster)
    {
        monster.DeathCompleted -= HandleDeathCompleted;
        _activeBoss = null;
        _objectPool.Push(monster.gameObject);
    }

    /// <summary>
    /// 보상 없이 활성 보스 반환과 생성 준비 상태 해제
    /// </summary>
    public void Shutdown()
    {
        if (_activeBoss != null)
        {
            _activeBoss.CompleteDeath();
        }

        _activeBoss = null;
        _isInitialized = false;
        _objectPool = null;
    }
}
