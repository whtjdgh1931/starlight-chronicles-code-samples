
/// <summary>
/// 런타임 상태 저장, 저장 파일 복원, 백업 복원 순서대로 조정
/// </summary>
public sealed partial class SaveService
{

    /// <summary>
    /// 실제 파일 저장 대상 슬롯
    /// </summary>
    public int SlotId => _slotId;

    private readonly int _slotId;
    private readonly SaveDataFactory _saveDataFactory;
    private readonly SaveDataSerializer _serializer;
    private readonly CharacterSaveReader _saveReader;
    private readonly SaveDataRestorer _restorer;
    private ISaveWriter _writer;



    public SaveService(
          int slotId,
          SaveDataFactory saveDataFactory,
          SaveDataSerializer serializer,
          CharacterSaveLocator saveLocator,
          CharacterSaveReader saveReader,
          SaveDataRestorer restorer)
    {
        saveLocator.ValidateSlotId(slotId);

        _slotId = slotId;
        _saveDataFactory = saveDataFactory;
        _serializer = serializer;
        _writer = saveLocator.CreateStorage(slotId);
        _saveReader = saveReader;
        _restorer = restorer;
    }

    public CharacterSaveReadResult Read(out SaveData saveData, out string errorMessage)
    {
        return _saveReader.Read(_slotId, out saveData, out errorMessage);
    }

    public void Restore(
     SaveData saveData,
     PlayerStat playerStat,
     PlayerInventory playerInventory,
     PlayerEquipment playerEquipment,
     GoldWallet goldWallet,
     PlayerQuestProgress questProgress,
     PlayerQuickSlots quickSlots,
     DataManager dataManager)
    {
        _restorer.Restore(saveData, playerStat, playerInventory, playerEquipment, goldWallet, questProgress, quickSlots, dataManager);
    }

    public bool TrySave(
    PlayerStat playerStat,
    PlayerInventory playerInventory,
    PlayerEquipment playerEquipment,
    GoldWallet goldWallet,
    PlayerQuestProgress questProgress,
    PlayerQuickSlots quickSlots,
    out string errorMessage)
    {
        SaveData saveData = _saveDataFactory.Create(playerStat, playerInventory, playerEquipment, goldWallet, questProgress, quickSlots);

        return TrySave(saveData, out errorMessage);
    }

    /// <summary>
    /// 이미 추출한 캐릭터 상태를 동일한 슬롯에 저장
    /// 접속 요청과 파일 저장 사이의 중복 상태 추출 방지
    /// </summary>
    public bool TrySave(SaveData saveData, out string errorMessage)
    {
        string json = _serializer.Serialize(saveData, false);

        return _writer.TryWrite(json, out errorMessage);
    }
}
