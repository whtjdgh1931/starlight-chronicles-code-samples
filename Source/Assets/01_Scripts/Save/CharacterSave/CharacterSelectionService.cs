using System.Collections.Generic;

/// <summary>
/// 캐릭터 슬롯을 읽어 선택 화면용 정보로 변환
/// </summary>
public sealed class CharacterSelectionService
{
    private readonly CharacterSaveReader _saveReader;
    private readonly CharacterSlotSummaryFactory _summaryFactory;

    public CharacterSelectionService(CharacterSaveReader saveReader, CharacterSlotSummaryFactory summaryFactory)
    {
        _saveReader = saveReader;
        _summaryFactory = summaryFactory;
    }

    public IReadOnlyList<CharacterSlotEntry> ReadAllSlots()
    {
        List<CharacterSlotEntry> slots = new(_saveReader.SlotCount);

        for (int slotId = 0; slotId < _saveReader.SlotCount; slotId++)
        {
            slots.Add(ReadSlot(slotId));
        }

        return slots;
    }

    public CharacterSlotEntry ReadSlot(int slotId)
    {
        CharacterSaveReadResult result = _saveReader.Read(slotId, out SaveData saveData, out string errorMessage);

        switch (result)
        {
            case CharacterSaveReadResult.Success:
                return CreateAvailableEntry(slotId, saveData, CharacterSlotState.Available);

            case CharacterSaveReadResult.BackupSuccess:
                return CreateAvailableEntry(slotId, saveData, CharacterSlotState.BackupAvailable);

            case CharacterSaveReadResult.NotFound:
                return new CharacterSlotEntry(slotId, CharacterSlotState.Empty, null, string.Empty);

            case CharacterSaveReadResult.Failed:
                return new CharacterSlotEntry(slotId, CharacterSlotState.Corrupted, null, errorMessage);

            default:
                throw new System.ArgumentOutOfRangeException();
        }
    }

    private CharacterSlotEntry CreateAvailableEntry(int slotId, SaveData saveData, CharacterSlotState state)
    {
        if (!_summaryFactory.TryCreate(slotId, saveData, out CharacterSlotSummary summary, out string errorMessage))
        {
            return new CharacterSlotEntry(slotId, CharacterSlotState.Corrupted, null, errorMessage);
        }

        return new CharacterSlotEntry(slotId, state, summary, string.Empty);
    }

}
