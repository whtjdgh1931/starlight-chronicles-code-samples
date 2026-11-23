using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 몬스터 생성 클래스
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [Header("Monster Setting")]

    //생성할 몬스터 프리팹
    [SerializeField] private MonsterBase _spawnMonster;
    [SerializeField] private int _monsterID;
    [SerializeField] private HuntSpawnZone _huntZone;

    [Header("Spawn Area Setting")]
    [SerializeField] private int _maxMonsterCnt;
    [SerializeField] private int _maxSpawnCnt;
    [SerializeField] private float _spawnRange;
    [SerializeField] private Collider2D[] _spawnPlatforms = Array.Empty<Collider2D>();
    [SerializeField, Min(0f)] private float _spawnEdgeMargin = 0.75f;
    [SerializeField, Min(0f)] private float _spawnDelay;

    private readonly List<MonsterBase> _activeMonsters = new List<MonsterBase>();
    private ObjectPool _objectPool;
    private MonsterBase _activePrefab;
    private MonsterData _monsterData;
    private IReadOnlyDictionary<int, MonsterAttackData> _attackTable;
    private float _spawnTimer;
    private int _nextPlatformIndex;
    private bool _isGameplayStarted;
    private int _maxAlive;
    private int _batchCount;
    private float _spawnInterval;
    private float _initialDelay;

    /// <summary>
    /// 몬스터 스폰시 발행할 이벤트
    /// </summary>
    public event Action<MonsterBase> OnMonsterSpawned;

    /// <summary>
    /// 맵 준비 시 생성 풀과 몬스터 전투 데이터 연결
    /// </summary>
    public void Initialize(ObjectPool objectPool, DataManager dataManager)
    {
        _objectPool = objectPool;
        _attackTable = dataManager.MonsterAttackTable;
        if (_huntZone != HuntSpawnZone.None) { return; }

        if (_spawnMonster == null)
        {
            throw new MissingReferenceException($"{name}: 몬스터 프리팹 필요");
        }

        _activePrefab = _spawnMonster;
        _monsterData = dataManager.GetMonsterData(_monsterID)
            ?? throw new InvalidOperationException($"{name}: 몬스터 데이터 누락 ID {_monsterID}");
        _maxAlive = _maxMonsterCnt;
        _batchCount = _maxSpawnCnt;
        _spawnInterval = Constants.FLOAT_SpawnInterval;
        _initialDelay = _spawnDelay;
    }

    /// <summary>
    /// 사냥터 입장 시 선택 난이도의 몬스터 연결
    /// 다음 맵 입장까지 선택한 종류 유지
    /// </summary>
    public void SelectDifficulty(HuntDifficulty difficulty, DataManager dataManager, IReadOnlyDictionary<int, MonsterBase> prefabs)
    {
        if (_huntZone == HuntSpawnZone.None) { return; }

        HuntSpawnData rule = dataManager.GetHuntSpawn(difficulty, _huntZone);
        if (!prefabs.TryGetValue(rule.MonsterID, out MonsterBase prefab) || prefab == null)
        {
            throw new MissingReferenceException($"{name}: HuntSpawn {difficulty}/{_huntZone} 몬스터 {rule.MonsterID} 프리팹 미등록");
        }
        _monsterData = dataManager.GetMonsterData(rule.MonsterID)
            ?? throw new InvalidOperationException($"{name}: 몬스터 데이터 누락 ID {rule.MonsterID}");
        _activePrefab = prefab;
        _maxAlive = rule.MaxAlive;
        _batchCount = rule.BatchCount;
        _spawnInterval = rule.SpawnInterval;
        _initialDelay = rule.InitialDelay;
    }

    /// <summary>
    /// 맵 전환 완료 후 몬스터 생성 시작
    /// </summary>
    public void StartGameplay()
    {
        if (_isGameplayStarted)
        {
            return;
        }

        if (_activePrefab == null || _monsterData == null)
        {
            throw new InvalidOperationException($"{name}: 스포너 초기화와 난이도 선택 필요");
        }

        _spawnTimer = _spawnInterval - _initialDelay;
        _nextPlatformIndex = 0;
        _isGameplayStarted = true;
    }

    /// <summary>
    /// 몬스터 소환
    /// </summary>
    public void SpawnMonster()
    {
        // 외부 생성 요청에도 구역 전체 최대 수량 유지
        if (_activeMonsters.Count >= _maxAlive)
        {
            return;
        }

        Vector2 spawnPosition = GetSpawnPosition();

        GameObject monsterObject = _objectPool.Pop(_activePrefab.gameObject, transform);
        monsterObject.transform.position = spawnPosition;
        MonsterBase monster = monsterObject.GetComponent<MonsterBase>();
        // 풀에서 반환된 몸체 콜라이더 복구 후 경계 측정
        monster.Initialize(_monsterData, _attackTable);
        if (_spawnPlatforms.Length > 0)
        {
            // 외형마다 다른 피벗 높이를 보정해 몸체 하단을 발판 위에 배치
            Physics2D.SyncTransforms();
            Vector3 position = monsterObject.transform.position;
            position.y += spawnPosition.y - monster.BodyCollider.bounds.min.y;
            monsterObject.transform.position = position;
        }
        monster.DeathCompleted += HandleDeathCompleted;
        _activeMonsters.Add(monster);
        OnMonsterSpawned?.Invoke(monster);
    }

    /// <summary>
    /// 지정 발판을 순환하며 상단 안쪽에 생성 위치 배분
    /// 발판 미지정 스포너는 기존 지면 탐색 방식 유지
    /// </summary>
    private Vector2 GetSpawnPosition()
    {
        if (_spawnPlatforms.Length > 0)
        {
            Collider2D platform = _spawnPlatforms[_nextPlatformIndex];
            _nextPlatformIndex = (_nextPlatformIndex + 1) % _spawnPlatforms.Length;
            Bounds bounds = platform.bounds;
            float margin = Mathf.Min(_spawnEdgeMargin, bounds.size.x * 0.5f);
            float x = UnityEngine.Random.Range(bounds.min.x + margin, bounds.max.x - margin);
            Vector2 surface = platform.ClosestPoint(new Vector2(x, bounds.max.y + Constants.FLOAT_OffsetY));
            return surface;
        }

        float randomX = UnityEngine.Random.Range(transform.position.x - _spawnRange, transform.position.x + _spawnRange);
        Vector2 position = new Vector2(randomX, transform.position.y);
        RaycastHit2D hit = Physics2D.Raycast(position, Vector2.down, Constants.FLOAT_CheckDistance, 1 << Constants.LAYER_Ground);
        if (hit.collider != null)
        {
            position.y = hit.point.y + Constants.FLOAT_OffsetY;
        }
        return position;
    }

    private void Update()
    {
        if (!_isGameplayStarted)
        {
            return;
        }

        _spawnTimer += Time.deltaTime;

        if (_spawnTimer < _spawnInterval)
        {
            return;
        }
        _spawnTimer = 0;

        if (_activeMonsters.Count < _maxAlive)
        {
            //부족한 수만큼 생성
            int spawnCnt = Mathf.Min(_batchCount, _maxAlive - _activeMonsters.Count);
            for (int i = 0; i < spawnCnt; i++)
            {
                SpawnMonster();
            }
        }
    }

    /// <summary>
    /// 완료된 로컬 몬스터의 추적 해제와 풀 반환
    /// </summary>
    private void HandleDeathCompleted(MonsterBase monster)
    {
        monster.DeathCompleted -= HandleDeathCompleted;
        _activeMonsters.Remove(monster);
        _objectPool.Push(monster.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 start = new Vector3(transform.position.x - _spawnRange, transform.position.y, transform.position.z);
        Vector3 end = new Vector3(transform.position.x + _spawnRange, transform.position.y, transform.position.z);
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(start, 0.2f);
        Gizmos.DrawWireSphere(end, 0.2f);
    }

    /// <summary>
    /// 맵 비활성화 시 몬스터 생성 중지
    /// </summary>
    public void StopGameplay()
    {
        _isGameplayStarted = false;
        _spawnTimer = 0f;
    }

    private void OnDestroy()
    {
        foreach (MonsterBase monster in _activeMonsters)
        {
            if (monster != null)
            {
                monster.DeathCompleted -= HandleDeathCompleted;
            }
        }
        _activeMonsters.Clear();
    }

}
