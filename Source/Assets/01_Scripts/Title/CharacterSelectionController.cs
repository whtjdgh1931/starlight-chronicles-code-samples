using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 선택 씬에서 슬롯 조회와 캐릭터 생성을 조정
/// </summary>
public sealed class CharacterSelectionController : MonoBehaviour
{
    [SerializeField] private InventorySettings _inventorySettings;

    [Header("생성 가능 직업")]
    [SerializeField] private PlayerClass[] _supportedClasses = { PlayerClass.Warrior };
    [SerializeField] private string _unsupportedClassMessage = "이 직업은 아직 선택할 수 없습니다. 다른 직업을 선택해 주세요.";

    [Header("플레이 씬")]
    [SerializeField]
    private string _playSceneName = "HomeScene";
    [SerializeField, Min(0)] private int _startPortalId = 1;

    private CharacterSaveReader _saveReader;
    private CharacterSaveLocator _saveLocator;

    /// <summary>
    /// 데이터 준비와 플레이 씬 이동 단계
    /// </summary>
    private enum SelectionStage
    {
        Loading,
        Ready,
        Transitioning
    }

    private SelectionStage _stage;

    private DataManager _dataManager;
    private CharacterSelectionService _selectionService;
    private CharacterCreationService _creationService;
    public bool IsReady => _stage != SelectionStage.Loading;

    public IReadOnlyList<CharacterSlotEntry> Slots { get; private set; } = Array.Empty<CharacterSlotEntry>();

    public event Action OnSlotsChanged;
    public event Action<string> OnOperationFailed;

    /// <summary>
    /// 미지원 직업 선택 안내
    /// </summary>
    public event Action<string> OnUnsupportedClassSelected;

    /// <summary>
    /// 신규 캐릭터 생성 완료 알림
    /// </summary>
    public event Action OnCharacterCreated;

    private void Start()
    {
        if (_inventorySettings == null)
        {
            throw new MissingReferenceException($"{name}: InventorySettings가 필요합니다.");
        }

        if (string.IsNullOrWhiteSpace(_playSceneName))
        {
            throw new InvalidOperationException($"{name}: 플레이 씬 이름이 필요합니다.");
        }

        GameManager.Instance.CharacterSession.Clear();

        _dataManager = GameManager.Instance.DataManager;
        _dataManager.OnAllDataLoaded += HandleAllDataLoaded;

        if (_dataManager.IsDataLoaded)
        {
            HandleAllDataLoaded();
        }
    }

    /// <summary>
    /// 모든 캐릭터 슬롯을 저장 파일에서 다시 조회
    /// </summary>
    public void RefreshSlots()
    {
        if (!IsReady)
        {
            NotifyFailure("게임 데이터가 아직 준비되지 않았습니다.");

            return;
        }

        Slots = _selectionService.ReadAllSlots();
        OnSlotsChanged?.Invoke();
    }

    /// <summary>
    /// 신규 캐릭터 생성
    /// </summary>
    /// <param name="slotId"></param>
    /// <param name="playerClass"></param>
    public void CreateCharacter(int slotId, PlayerClass playerClass)
    {
        if (HasPendingResult(slotId))
        {
            NotifyFailure("이 슬롯의 보스전 결과 저장 또는 포기 후 생성 가능");
            return;
        }

        if (_stage == SelectionStage.Transitioning)
        {
            return;
        }

        if (!IsReady)
        {
            NotifyFailure("게임 데이터가 아직 준비되지 않았습니다.");

            return;
        }

        if (!ValidateCreatableClass(playerClass))
        {
            return;
        }

        if (!_creationService.TryCreate(
                slotId,
                playerClass,
                out _,
                out string errorMessage))
        {
            NotifyFailure($"캐릭터 생성에 실패했습니다. {errorMessage}");

            return;
        }

        RefreshSlots();
        OnCharacterCreated?.Invoke();
    }

    /// <summary>
    /// 지정한 슬롯의 기본, 백업, 임시 저장 데이터 삭제
    /// </summary>
    public bool TryDeleteCharacter(int slotId, out string errorMessage)
    {
        if (!IsReady)
        {
            errorMessage = "게임 데이터가 아직 준비되지 않았습니다.";
            return false;
        }

        if (HasPendingResult(slotId))
        {
            errorMessage = "이 슬롯의 보스전 결과 저장 또는 포기 후 삭제 가능";
            return false;
        }
        SaveFileStorage storage;

        try
        {
            storage = _saveLocator.CreateStorage(slotId);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            errorMessage = $"삭제할 슬롯 번호가 올바르지 않습니다. {exception.Message}";
            return false;
        }

        if (!storage.TryDelete(out errorMessage))
        {
            return false;
        }

        RefreshSlots();
        return true;
    }

    /// <summary>
    /// 지정한 슬롯을 다시 검증하고 플레이 씬으로 이동
    /// </summary>
    public void PlayCharacter(int slotId)
    {
        if (!IsReady)
        {
            NotifyFailure("게임 데이터가 아직 준비되지 않았습니다.");

            return;
        }

        if (_stage == SelectionStage.Transitioning)
        {
            return;
        }

        CharacterSaveReadResult result = _saveReader.Read(slotId, out _, out string errorMessage);

        switch (result)
        {
            case CharacterSaveReadResult.Success:
                StartPlay(slotId);
                break;

            case CharacterSaveReadResult.BackupSuccess:
                Debug.LogWarning($"백업 캐릭터 데이터를 사용합니다. {errorMessage}");

                StartPlay(slotId);
                break;

            case CharacterSaveReadResult.NotFound:
                NotifyFailure("선택한 슬롯에 캐릭터가 없습니다.");

                RefreshSlots();
                break;

            case CharacterSaveReadResult.Failed:
                NotifyFailure($"캐릭터 데이터를 불러오지 못했습니다. {errorMessage}");

                RefreshSlots();
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// 생성 가능 직업 확인과 미지원 직업 안내
    /// </summary>
    private bool ValidateCreatableClass(PlayerClass playerClass)
    {
        if (playerClass != PlayerClass.None && Array.IndexOf(_supportedClasses, playerClass) >= 0)
        {
            return true;
        }

        OnUnsupportedClassSelected?.Invoke(_unsupportedClassMessage);
        return false;
    }

    private bool HasPendingResult(int slotId)
    {
        LocalBattleResult result = GameManager.Instance.LocalBattleResult;
        return result.HasPending && result.SlotId == slotId;
    }

    public void RetryBattleResultSave()
    {
        LocalBattleResult result = GameManager.Instance.LocalBattleResult;
        if (!result.TrySave()) { NotifyFailure(result.Error); }
        else { RefreshSlots(); }
    }

    public void DiscardBattleResult() => GameManager.Instance.LocalBattleResult.Discard();

    private void StartPlay(int slotId)
    {
        if (HasPendingResult(slotId))
        {
            NotifyFailure("이 슬롯의 보스전 결과 저장 또는 포기 후 입장 가능");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(_playSceneName))
        {
            NotifyFailure($"Build Settings에 플레이 씬이 없습니다. " + $"Scene: {_playSceneName}");

            return;
        }

        _stage = SelectionStage.Transitioning;

        GameManager.Instance.CharacterSession.SelectCharacter(slotId);

        GameManager.Instance.GameSceneManager.ChangeMapScene(_playSceneName, _startPortalId);
    }

    private void NotifyFailure(string message)
    {
        Debug.LogError(message);
        OnOperationFailed?.Invoke(message);
    }

    private void HandleAllDataLoaded()
    {
        if (IsReady)
        {
            return;
        }

        _dataManager.OnAllDataLoaded -= HandleAllDataLoaded;

        SaveDataSerializer serializer = new();
        _saveLocator = GameManager.Instance.CharacterSaveLocator;

        _saveReader = new CharacterSaveReader(
          _saveLocator,
          serializer,
          new SaveDataChecker(),
          _inventorySettings,
          _dataManager);

        _selectionService = new CharacterSelectionService(_saveReader, new CharacterSlotSummaryFactory());

        _creationService = new CharacterCreationService(
            _saveLocator,
            serializer,
            new NewCharacterSaveDataFactory(),
            _dataManager.CachedPlayerStatData,
            _inventorySettings);

        _stage = SelectionStage.Ready;
        RefreshSlots();
    }

    private void OnDestroy()
    {
        if (_dataManager != null)
        {
            _dataManager.OnAllDataLoaded -= HandleAllDataLoaded;
        }
    }
}
