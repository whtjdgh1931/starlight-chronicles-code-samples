using System;
using UnityEngine;

public partial class PlayerSaveController : MonoBehaviour
{

    private SaveService _saveService;
    private PlayerStat _playerStat;
    private PlayerInventory _playerInventory;
    private DataManager _dataManager;
    private PlayerEquipment _playerEquipment;
    private GoldWallet _goldWallet;
    private PlayerQuestProgress _questProgress;
    private PlayerQuickSlots _quickSlots;

    private bool _isRuntimeReady;

    public bool IsInitialized => _saveService != null;

    /// <summary>
    /// 초기화된 저장 서비스의 실제 대상 슬롯
    /// </summary>
    public int SlotId
    {
        get
        {
            EnsureInitialized();
            return _saveService.SlotId;
        }
    }


    public void Initialize(
      int slotId,
      PlayerStat playerStat,
      PlayerInventory playerInventory,
      PlayerEquipment playerEquipment,
      GoldWallet goldWallet,
      PlayerQuestProgress questProgress,
      PlayerQuickSlots quickSlots,
      DataManager dataManager,
      CharacterSaveLocator saveLocator
       )
    {
        _playerStat = playerStat;
        _playerInventory = playerInventory;
        _playerEquipment = playerEquipment;
        _dataManager = dataManager;
        _goldWallet = goldWallet;
        _questProgress = questProgress;
        _quickSlots = quickSlots;

        SaveDataSerializer serializer = new();

        CharacterSaveReader saveReader =
            new CharacterSaveReader(
                saveLocator,
                serializer,
                new SaveDataChecker(),
                playerInventory.Settings,
                dataManager);

        _saveService = new SaveService(
            slotId,
            new SaveDataFactory(),
            serializer,
            saveLocator,
            saveReader,
            new SaveDataRestorer());
    }

    public CharacterSaveReadResult Read(out SaveData saveData, out string errorMessage)
    {
        EnsureInitialized();

        return _saveService.Read(out saveData, out errorMessage);
    }

    public void MarkRuntimeReady()
    {
        EnsureInitialized();
        _isRuntimeReady = true;
    }

    public void Restore(SaveData saveData)
    {
        EnsureRuntimeReady();

        _saveService.Restore(saveData, _playerStat, _playerInventory, _playerEquipment, _goldWallet, _questProgress, _quickSlots, _dataManager);
    }

    public CharacterSaveReadResult Load(out string errorMessage)
    {
        if (!_isRuntimeReady)
        {
            errorMessage = "플레이어 런타임 상태가 준비되지 않았습니다.";

            return CharacterSaveReadResult.Failed;
        }

        CharacterSaveReadResult result = Read(out SaveData saveData, out errorMessage);

        if (result == CharacterSaveReadResult.Success || result == CharacterSaveReadResult.BackupSuccess)
        {
            Restore(saveData);
        }

        return result;
    }

    /// <summary>
    /// 접속 준비에서 추출한 캐릭터 상태를 현재 슬롯에 저장
    /// </summary>
    public bool TrySaveState(SaveData saveData, out string errorMessage)
    {
        if (!CanSaveCurrentCharacter(out errorMessage))
        {
            return false;
        }

        return _saveService.TrySave(saveData, out errorMessage);
    }

    public bool TrySave(out string errorMessage)
    {
        if (!CanSaveCurrentCharacter(out errorMessage))
        {
            return false;
        }

        return _saveService.TrySave(_playerStat, _playerInventory, _playerEquipment, _goldWallet, _questProgress, _quickSlots, out errorMessage);
    }

    /// <summary>
    /// 현재 캐릭터의 저장 가능 상태 확인
    /// </summary>
    private bool CanSaveCurrentCharacter(out string errorMessage)
    {
        if (!_isRuntimeReady)
        {
            errorMessage = "플레이어 런타임 상태가 준비되지 않았습니다.";
            return false;
        }

        if (_playerStat.CurrentHp <= 0f)
        {
            errorMessage = "사망 상태에서는 저장할 수 없습니다. 부활 후 다시 시도해 주세요.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private void EnsureInitialized()
    {
        if (_saveService == null)
        {
            throw new InvalidOperationException("저장 시스템이 초기화되지 않았습니다.");
        }
    }

    private void EnsureRuntimeReady()
    {
        EnsureInitialized();

        if (!_isRuntimeReady)
        {
            throw new InvalidOperationException("플레이어 런타임 상태가 준비되지 않았습니다.");
        }
    }



}
