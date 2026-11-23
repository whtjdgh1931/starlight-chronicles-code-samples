using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 캐릭터 슬롯을 실제 저장 파일 위치로 변환
/// </summary>
public sealed class CharacterSaveLocator
{
    private const string DirectoryName = "Characters";
    private const string FilePrefix = "character_";
    private const string FileExtension = ".json";

    private readonly string _directoryPath;
    private readonly Dictionary<int, SaveFileStorage> _storages = new();

    private readonly int _slotCount;
    public int SlotCount => _slotCount;

    public CharacterSaveLocator(string persistentDataPath, int slotCount)
    {
        if (string.IsNullOrWhiteSpace(persistentDataPath))
        {
            throw new ArgumentException("저장 루트 경로가 필요합니다.", nameof(persistentDataPath));
        }

        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount));
        }

        _directoryPath = Path.Combine(persistentDataPath, DirectoryName);

        _slotCount = slotCount;
    }

    /// <summary>
    /// 슬롯 유효성 검사
    /// </summary>
    /// <param name="slotId"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void ValidateSlotId(int slotId)
    {
        if (slotId < 0 || slotId >= _slotCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotId),
                slotId,
                $"슬롯 ID는 0 이상 {_slotCount - 1} 이하여야 합니다.");
        }
    }

    /// <summary>
    /// 슬롯별 저장소 재사용으로 백업 복원 상태 공유
    /// </summary>
    public SaveFileStorage CreateStorage(int slotId)
    {
        ValidateSlotId(slotId);
        lock (_storages)
        {
            if (_storages.TryGetValue(slotId, out SaveFileStorage storage)) { return storage; }
            string fileName = $"{FilePrefix}{slotId}{FileExtension}";
            storage = new SaveFileStorage(_directoryPath, fileName);
            _storages.Add(slotId, storage);
            return storage;
        }
    }
}
