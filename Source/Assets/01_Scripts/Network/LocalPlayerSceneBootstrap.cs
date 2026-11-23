using UnityEngine;

/// <summary>
/// 씬 로컬 플레이어의 세션 연결과 씬 전환 유지
/// 원격 플레이어에는 부착하지 않음
/// </summary>
[RequireComponent(typeof(Player))]
public class LocalPlayerSceneBootstrap : MonoBehaviour
{

    private Player _player;
    private DataManager _dataManager;

    private void Start()
    {
        _player = GetComponent<Player>();

        PlaySessionController session = GameManager.Instance.PlaySessionController;
        if (session.HasActivePlayer && session.ActivePlayer != _player)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        _dataManager = GameManager.Instance.DataManager;
        if (_dataManager.IsDataLoaded)
        {
            _player.InitializeLocal();
            return;
        }

        _dataManager.OnAllDataLoaded += HandleDataLoaded;
    }

    private void HandleDataLoaded()
    {
        _dataManager.OnAllDataLoaded -= HandleDataLoaded;
        _player.InitializeLocal();
    }

    private void OnDestroy()
    {
        if (_dataManager != null)
        {
            _dataManager.OnAllDataLoaded -= HandleDataLoaded;
        }
    }

}
