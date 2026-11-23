using System.IO;

/// <summary>
/// 마지막 정상 게임 데이터 스냅샷의 로컬 저장과 복구
/// </summary>
public sealed class GameDataCache
{
    private const string DirectoryName = "GameData";
    private const string FileName = "game-data-cache.json";

    private readonly SaveFileStorage _storage;
    private readonly GameDataSnapshotSerializer _serializer;
    private readonly GameDataSnapshotValidator _validator;

    public GameDataCache(string persistentDataPath)
    {
        string directoryPath = Path.Combine(persistentDataPath, DirectoryName);
        _storage = new SaveFileStorage(directoryPath, FileName);
        _serializer = new GameDataSnapshotSerializer();
        _validator = new GameDataSnapshotValidator();
    }

    /// <summary>
    /// 검증된 스냅샷의 원자적 저장
    /// </summary>
    public bool TrySave(GameDataSnapshot snapshot, out string errorMessage)
    {
        if (!_validator.TryValidate(snapshot, out errorMessage))
        {
            return false;
        }

        string json = _serializer.Serialize(snapshot);
        return _storage.TryWrite(json, out errorMessage);
    }

    /// <summary>
    /// 기본 캐시 우선, 손상 시 백업 캐시 복구
    /// </summary>
    public bool TryLoad(out GameDataSnapshot snapshot, out string errorMessage)
    {
        SaveFileStorage.ReadResult readResult = _storage.Read(out string json, out string primaryReadError);
        string primaryDataError = string.Empty;

        if (readResult == SaveFileStorage.ReadResult.Success && TryParseAndValidate(json, out snapshot, out primaryDataError))
        {
            errorMessage = string.Empty;
            return true;
        }

        SaveFileStorage.ReadResult backupResult = _storage.ReadBackup(out string backupJson, out string backupReadError);
        string backupDataError = string.Empty;

        if (backupResult == SaveFileStorage.ReadResult.Success && TryParseAndValidate(backupJson, out snapshot, out backupDataError))
        {
            _storage.PreserveBackupOnNextWrite();
            errorMessage = string.Empty;
            return true;
        }

        snapshot = null;
        errorMessage = CreateLoadError(
            readResult,
            primaryReadError,
            primaryDataError,
            backupResult,
            backupReadError,
            backupDataError);
        return false;
    }

    private bool TryParseAndValidate(string json, out GameDataSnapshot snapshot, out string errorMessage)
    {
        return _serializer.TryDeserialize(json, out snapshot, out errorMessage) &&
            _validator.TryValidate(snapshot, out errorMessage);
    }

    private static string CreateLoadError(
        SaveFileStorage.ReadResult readResult,
        string primaryReadError,
        string primaryDataError,
        SaveFileStorage.ReadResult backupResult,
        string backupReadError,
        string backupDataError)
    {
        if (readResult == SaveFileStorage.ReadResult.NotFound && backupResult == SaveFileStorage.ReadResult.NotFound)
        {
            return "저장된 게임 데이터 캐시가 없습니다.";
        }

        string primaryError = ResolveError(primaryReadError, primaryDataError, "기본 캐시 데이터 검증 실패");
        string backupError = ResolveError(backupReadError, backupDataError, "백업 캐시 데이터 검증 실패");
        return $"게임 데이터 캐시 복구 실패. 기본: {primaryError}, 백업: {backupError}";
    }

    private static string ResolveError(string readError, string dataError, string fallbackError)
    {
        if (!string.IsNullOrWhiteSpace(readError))
        {
            return readError;
        }

        return string.IsNullOrWhiteSpace(dataError) ? fallbackError : dataError;
    }
}
