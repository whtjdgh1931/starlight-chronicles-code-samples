using UnityEngine;
using UnityEngine.Tilemaps;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [SerializeField] private Tilemap _currentTilemap;
    [SerializeField] private ObjectPool _objectPool;
    [SerializeField] private MapCameraController _cameraController;
    [SerializeField] private DamageNumberSpawner _damageNumberSpawner;
    [SerializeField] private DamageImpactPresentation _damageImpactPresentation;

    // HostScene 전용 선택 UI
    [SerializeField] private HostSessionView _hostSessionView;

    [SerializeField] private MerchantInteractionController _merchantController;
    [SerializeField] private HomeTrainingGround _trainingGround;
    [SerializeField] private SkyAmbientSpawner _skyAmbientSpawner;

    private MapMonsterController _monsterController;

    public Tilemap CurrentTileMap => _currentTilemap;
    public ObjectPool ObjectPool => _objectPool;
    public DamageImpactPresentation DamageImpactPresentation => _damageImpactPresentation;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 맵의 오브젝트 풀과 몬스터를 준비합니다.
    /// </summary>
    public void Initialize()
    {
        CombatSoundPlayer soundPlayer = GameManager.Instance.CombatSoundPlayer;
        soundPlayer.StopAll();
        _objectPool.SetCombatSoundPlayer(soundPlayer);
        _objectPool.Initialize();

        // 씬 전환에도 유지되는 로컬·네트워크 플레이어의 풀 갱신
        foreach (Player player in FindObjectsByType<Player>(FindObjectsInactive.Include))
        {
            _objectPool.Bind(player.gameObject);
        }
        _damageNumberSpawner.Initialize(_objectPool);
        _damageImpactPresentation.SetObjectPool(_objectPool);

        _monsterController = FindAnyObjectByType<MapMonsterController>(FindObjectsInactive.Exclude);
        if (_monsterController != null)
        {
            _monsterController.Initialize(_objectPool, GameManager.Instance.DataManager);
        }
    }

    /// <summary>
    /// 플레이어를 진입 포털에 배치하고 카메라 연결
    /// </summary>
    public void PlaceLocalPlayer(Player player)
    {
        Vector3 position = GetPortalPosition(GameManager.Instance.NextSpawnPortalID);
        player.PlaceAtMapSpawn(position);
        if (_merchantController != null)
        {
            GameManager manager = GameManager.Instance;
            _merchantController.Bind(player, manager.DataManager, manager.UIManager.MerchantView);
        }
        // 몬스터가 있는 로컬 맵에서 해당 캐릭터의 진행도 연결
        if (_monsterController != null) { _monsterController.BindPlayer(player.QuestProgress, GameManager.Instance.PlaySessionController.SelectedHuntDifficulty); }
        FollowLocalPlayer(player);
    }

    /// <summary>
    /// 현재 맵의 진입 포털 위치 조회
    /// 카메라 연결과 플레이어 이동은 호출자에서 처리
    /// </summary>
    public Vector3 GetPortalPosition(int portalId)
    {
        Portal[] portals = FindObjectsByType<Portal>(FindObjectsInactive.Exclude);

        foreach (Portal portal in portals)
        {
            if (portal.PortalID == portalId && portal.gameObject.scene == gameObject.scene)
            {
                return portal.transform.position;
            }
        }

        throw new System.InvalidOperationException(
            $"{gameObject.scene.name}: 진입 포털 {portalId} 누락");
    }

    /// <summary>
    /// 플레이어 위치 변경 없이 카메라 추적 대상 연결
    /// </summary>
    public void FollowLocalPlayer(Player player)
    {
        _cameraController.Initialize(player);
    }

    /// <summary>
    /// 맵 전환 완료 후 게임 플레이 시작
    /// </summary>
    public void StartGameplay()
    {
        if (_skyAmbientSpawner != null)
        {
            _skyAmbientSpawner.Initialize(_objectPool);
        }
        if (_trainingGround != null)
        {
            _trainingGround.Initialize(GameManager.Instance.DataManager);
        }
        if (_hostSessionView != null)
        {
            _hostSessionView.Initialize(FindAnyObjectByType<NetworkSessionController>());
        }
        if (_monsterController != null)
        {
            _monsterController.StartGameplay();
        }
    }
}
