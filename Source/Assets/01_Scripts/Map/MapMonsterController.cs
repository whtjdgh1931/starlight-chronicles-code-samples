using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class MapMonsterController : MonoBehaviour
{
    /// <summary>
    /// 맵 전투의 초기화와 시작 단계
    /// </summary>
    private enum GameplayState
    {
        Uninitialized,
        Ready,
        Running
    }

    [System.Serializable]
    private sealed class HuntMonsterPrefab
    {
        public int MonsterID;
        public MonsterBase Prefab;
    }

    [SerializeField] private List<MonsterSpawner> _spawners = new List<MonsterSpawner>();
    [SerializeField] private HuntMonsterPrefab[] _huntMonsterPrefabs = System.Array.Empty<HuntMonsterPrefab>();
    [SerializeField] private ItemDropSpawner _itemDropSpawner;
    [SerializeField] private BossEncounterController _bossEncounterController;
    [SerializeField] private LocalBossBattleController _localBossBattleController;
    [SerializeField] private BossHudController _bossHudController;
    [SerializeField] private NetworkBossEncounterController _networkBossEncounterController;

    private ObjectPool _objectPool;
    private DataManager _dataManager;
    private PlayerQuestProgress _questProgress;
    private GameplayState _gameplayState;
    private bool _isNetworkGameplay;
    private readonly HashSet<MonsterBase> _trackedMonsters = new();
    private readonly Dictionary<int, MonsterBase> _huntPrefabById = new();

    /// <summary>
    /// 몬스터 생성 이벤트와 보스 HUD 초기화
    /// </summary>
    public void Initialize(ObjectPool objectPool, DataManager dataManager)
    {
        if (_gameplayState != GameplayState.Uninitialized)
        {
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        _isNetworkGameplay = networkManager != null && networkManager.IsListening;
        if (_isNetworkGameplay)
        {
            // 온라인에서는 로컬 몬스터 생성·HUD·경험치·드롭 연결 제외
            _gameplayState = GameplayState.Ready;
            return;
        }

        if (objectPool == null)
        {
            throw new System.ArgumentNullException(nameof(objectPool));
        }

        if (dataManager == null)
        {
            throw new System.ArgumentNullException(nameof(dataManager));
        }

        if (_itemDropSpawner == null)
        {
            throw new MissingReferenceException($"{name}: ItemDropSpawner 참조 필요");
        }

        if (_bossEncounterController != null && _bossHudController == null)
        {
            throw new MissingReferenceException($"{name}: BossHudController 참조 필요");
        }

        InitializeHuntPrefabLookup();
        _objectPool = objectPool;
        _dataManager = dataManager;

        foreach (MonsterSpawner spawner in _spawners)
        {
            spawner.Initialize(_objectPool, _dataManager);
            spawner.OnMonsterSpawned += HandleMonsterSpawned;
        }

        _itemDropSpawner.Initialize(_objectPool, _dataManager.ItemTable);

        if (_bossEncounterController != null)
        {
            _bossHudController.Initialize(_bossEncounterController);
            _bossEncounterController.OnBossSpawned += HandleMonsterSpawned;
        }

        _gameplayState = GameplayState.Ready;
    }

    private void InitializeHuntPrefabLookup()
    {
        _huntPrefabById.Clear();
        foreach (HuntMonsterPrefab entry in _huntMonsterPrefabs)
        {
            if (entry == null || entry.MonsterID <= 0 || entry.Prefab == null)
            {
                throw new MissingReferenceException($"{name}: 사냥터 몬스터 ID·프리팹 연결 필요");
            }
            if (!_huntPrefabById.TryAdd(entry.MonsterID, entry.Prefab))
            {
                throw new System.InvalidOperationException($"{name}: 사냥터 몬스터 프리팹 ID {entry.MonsterID} 중복");
            }
        }
    }

    /// <summary>
    /// 로컬 캐릭터의 처치 진행도와 선택 난이도별 몬스터 구성 연결
    /// </summary>
    public void BindPlayer(PlayerQuestProgress questProgress, HuntDifficulty difficulty)
    {
        _questProgress = questProgress;
        if (_isNetworkGameplay)
        {
            return;
        }
        foreach (MonsterSpawner spawner in _spawners)
        {
            spawner.SelectDifficulty(difficulty, _dataManager, _huntPrefabById);
        }
    }

    /// <summary>
    /// 로컬 플레이어 배치와 퀘스트 연결 이후 맵 전투 시작
    /// </summary>
    public void StartGameplay()
    {
        if (_gameplayState == GameplayState.Uninitialized)
        {
            throw new System.InvalidOperationException($"{name}: Initialize 호출 필요");
        }

        if (_gameplayState == GameplayState.Running)
        {
            return;
        }

        if (_isNetworkGameplay)
        {
            if (_networkBossEncounterController != null)
            {
                _networkBossEncounterController.StartEncounter(NetworkManager.Singleton);
            }

            _gameplayState = GameplayState.Running;
            return;
        }

        foreach (MonsterSpawner spawner in _spawners)
        {
            spawner.StartGameplay();
        }

        if (_bossEncounterController != null)
        {
            _bossEncounterController.Initialize(_objectPool, _dataManager);
        }

        _gameplayState = GameplayState.Running;
    }

    private void HandleMonsterSpawned(MonsterBase monster)
    {
        if (!_trackedMonsters.Add(monster))
        {
            return;
        }

        if (_localBossBattleController != null && monster is BossMonster boss)
        {
            GameManager manager = GameManager.Instance;
            _localBossBattleController.Initialize(
                manager.PlaySessionController.ActivePlayer, boss, _objectPool, manager.GameSceneManager, _dataManager);
        }
        monster.OnKilledEvent += HandleMonsterKilled;
        monster.OnReturnedToPool += HandleMonsterReturned;
    }

    /// <summary>
    /// 사망 외 풀 반환 시 보상 이벤트 연결 정리
    /// </summary>
    private void HandleMonsterReturned(MonsterBase monster)
    {
        _trackedMonsters.Remove(monster);
        monster.OnKilledEvent -= HandleMonsterKilled;
        monster.OnReturnedToPool -= HandleMonsterReturned;
    }

    private void HandleMonsterKilled(MonsterBase monster)
    {
        if (!_trackedMonsters.Remove(monster))
        {
            return;
        }

        monster.OnKilledEvent -= HandleMonsterKilled;
        monster.OnReturnedToPool -= HandleMonsterReturned;

        if (_localBossBattleController != null && monster is BossMonster boss &&
            !_localBossBattleController.TryCompleteVictory(boss))
        {
            return;
        }
        _questProgress.RecordMonsterKill(monster.Data.ID, _dataManager.QuestTable);
        EventBus.PublishExpEarned(monster.Data.Exp);
        _itemDropSpawner.Spawn(monster.Data, monster.transform.position);
    }

    private void OnDisable()
    {
        if (_gameplayState == GameplayState.Uninitialized)
        {
            return;
        }

        if (_isNetworkGameplay)
        {
            if (_networkBossEncounterController != null)
            {
                _networkBossEncounterController.Shutdown();
            }

            _isNetworkGameplay = false;
            _gameplayState = GameplayState.Uninitialized;
            return;
        }

        if (_localBossBattleController != null) { _localBossBattleController.Shutdown(); }
        foreach (MonsterSpawner spawner in _spawners)
        {
            spawner.StopGameplay();
            spawner.OnMonsterSpawned -= HandleMonsterSpawned;
        }

        if (_bossEncounterController != null)
        {
            _bossHudController.Shutdown();
            _bossEncounterController.OnBossSpawned -= HandleMonsterSpawned;
            _bossEncounterController.Shutdown();
        }

        foreach (MonsterBase monster in _trackedMonsters)
        {
            if (monster == null)
            {
                continue;
            }

            monster.OnKilledEvent -= HandleMonsterKilled;
            monster.OnReturnedToPool -= HandleMonsterReturned;
        }
        _trackedMonsters.Clear();
        _huntPrefabById.Clear();

        _objectPool = null;
        _dataManager = null;
        _gameplayState = GameplayState.Uninitialized;
    }
}
