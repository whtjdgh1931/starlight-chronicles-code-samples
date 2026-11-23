using System;

/// <summary>
/// 캐릭터 생성 및 저장하는 클래스
/// </summary>
public sealed class CharacterCreationService
{
    private readonly CharacterSaveLocator _saveLocator;
    private readonly SaveDataSerializer _serializer;
    private readonly NewCharacterSaveDataFactory _dataFactory;
    private readonly PlayerStatData _statData;
    private readonly InventorySettings _inventorySettings;

    public CharacterCreationService(
      CharacterSaveLocator saveLocator,
      SaveDataSerializer serializer,
      NewCharacterSaveDataFactory dataFactory,
      PlayerStatData statData,
      InventorySettings inventorySettings)
    {
        _saveLocator = saveLocator;
        _serializer = serializer;
        _dataFactory = dataFactory;
        _statData = statData;
        _inventorySettings = inventorySettings;
    }

    public bool TryCreate(
       int slotId,
       PlayerClass playerClass,
       out CharacterSlotSummary summary,
       out string errorMessage)
    {
        summary = null;

        try
        {
            _saveLocator.ValidateSlotId(slotId);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            errorMessage = exception.Message;
            return false;
        }

        SaveFileStorage storage = _saveLocator.CreateStorage(slotId);

        if (!TryCheckEmptySlot(storage, out errorMessage))
        {
            return false;
        }

        SaveData saveData = _dataFactory.Create(playerClass, _statData, _inventorySettings);

        string json = _serializer.Serialize(saveData, false);

        if (!storage.TryWrite(json, out errorMessage))
        {
            return false;
        }

        summary = new CharacterSlotSummary(slotId, playerClass, saveData.Player.Level);

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryCheckEmptySlot(SaveFileStorage storage, out string errorMessage)
    {
        SaveFileStorage.ReadResult primaryResult = storage.Read(out _, out string primaryError);

        SaveFileStorage.ReadResult backupResult = storage.ReadBackup(out _, out string backupError);

        if (primaryResult == SaveFileStorage.ReadResult.Failed)
        {
            errorMessage = primaryError;
            return false;
        }

        if (backupResult == SaveFileStorage.ReadResult.Failed)
        {
            errorMessage = backupError;
            return false;
        }

        if (primaryResult == SaveFileStorage.ReadResult.Success ||
            backupResult == SaveFileStorage.ReadResult.Success)
        {
            errorMessage = "이미 저장 데이터가 존재하는 슬롯입니다.";

            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

}
