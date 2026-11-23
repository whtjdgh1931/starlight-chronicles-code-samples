
/// <summary>
/// 캐릭터 저장 데이터를 읽는 클래스
/// </summary>
public sealed class CharacterSaveReader
{
    private readonly CharacterSaveLocator _saveLocator;
    private readonly SaveDataSerializer _serializer;
    private readonly SaveDataChecker _checker;
    private readonly InventorySettings _inventorySettings;
    private readonly DataManager _dataManager;

    public int SlotCount => _saveLocator.SlotCount;

    public CharacterSaveReader(
        CharacterSaveLocator saveLocator,
        SaveDataSerializer serializer,
        SaveDataChecker checker,
        InventorySettings inventorySettings,
        DataManager dataManager)
    {
        _saveLocator = saveLocator;
        _serializer = serializer;
        _checker = checker;
        _inventorySettings = inventorySettings;
        _dataManager = dataManager;
    }

    public CharacterSaveReadResult Read(int slotId, out SaveData saveData, out string errorMessage)
    {
        SaveFileStorage storage = _saveLocator.CreateStorage(slotId);

        SaveFileStorage.ReadResult primaryResult = storage.Read(out string primaryJson, out string primaryError);

        if (primaryResult == SaveFileStorage.ReadResult.Success &&
            TryCreateValidData(
                primaryJson,
                out saveData,
                out primaryError))
        {
            errorMessage = string.Empty;
            return CharacterSaveReadResult.Success;
        }

        SaveFileStorage.ReadResult backupResult = storage.ReadBackup(out string backupJson, out string backupError);

        if (backupResult == SaveFileStorage.ReadResult.Success &&
            TryCreateValidData(
                backupJson,
                out saveData,
                out backupError))
        {
            storage.PreserveBackupOnNextWrite();
            errorMessage = primaryError;

            return CharacterSaveReadResult.BackupSuccess;
        }

        saveData = null;

        if (primaryResult == SaveFileStorage.ReadResult.NotFound && backupResult == SaveFileStorage.ReadResult.NotFound)
        {
            errorMessage = string.Empty;
            return CharacterSaveReadResult.NotFound;
        }

        errorMessage = CombineErrors(primaryError, backupError);

        return CharacterSaveReadResult.Failed;
    }

    private bool TryCreateValidData(
      string json,
      out SaveData saveData,
      out string errorMessage)
    {
        if (!_serializer.TryDeserialize(json, out saveData, out errorMessage))
        {
            return false;
        }

        if (!_checker.TryCheck(saveData, _inventorySettings, _dataManager, out errorMessage))
        {
            saveData = null;
            return false;
        }

        return true;
    }

    private static string CombineErrors(string primaryError, string backupError)
    {
        if (string.IsNullOrEmpty(primaryError))
        {
            return backupError;
        }

        if (string.IsNullOrEmpty(backupError))
        {
            return primaryError;
        }

        return $"본 저장 파일 오류: {primaryError} " + $"백업 저장 파일 오류: {backupError}";
    }
}
