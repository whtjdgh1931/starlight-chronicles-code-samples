using UnityEngine;

/// <summary>
/// 플레이 씬 직접 실행을 위한 테스트 캐릭터 세션 준비
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class DirectPlaySceneBootstrap : MonoBehaviour
{
    [SerializeField] private int _testSlotId;
    [SerializeField] private PlayerClass _testClass = PlayerClass.Warrior;
    [SerializeField] private InventorySettings _inventorySettings;

    private DataManager _dataManager;

    private void Start()
    {
#if UNITY_EDITOR
        _dataManager = GameManager.Instance.DataManager;

        if (_dataManager.IsDataLoaded)
        {
            PrepareTestSession();
            return;
        }

        _dataManager.OnAllDataLoaded += HandleDataLoaded;
#else
        enabled = false;
#endif
    }

    /// <summary>
    /// 새 테스트 캐릭터 저장을 준비하고 선택
    /// </summary>
    private void PrepareTestSession()
    {
        CharacterSession session = GameManager.Instance.CharacterSession;

        if (session.HasSelectedCharacter)
        {
            return;
        }

        if (_inventorySettings == null)
        {
            Debug.LogError("DirectPlaySceneBootstrap에 InventorySettings가 필요합니다.");
            return;
        }

        SaveDataSerializer serializer = new();
        SaveData saveData = new NewCharacterSaveDataFactory().Create(
            _testClass,
            _dataManager.CachedPlayerStatData,
            _inventorySettings);
        SaveFileStorage storage = GameManager.Instance.CharacterSaveLocator.CreateStorage(_testSlotId);

        if (!storage.TryWrite(serializer.Serialize(saveData, false), out string errorMessage))
        {
            Debug.LogError($"테스트 캐릭터 저장을 갱신하지 못했습니다. {errorMessage}");
            return;
        }

        session.SelectCharacter(_testSlotId);
    }

    private void HandleDataLoaded()
    {
        _dataManager.OnAllDataLoaded -= HandleDataLoaded;
        PrepareTestSession();
    }

    private void OnDestroy()
    {
        if (_dataManager != null)
        {
            _dataManager.OnAllDataLoaded -= HandleDataLoaded;
        }
    }
}
