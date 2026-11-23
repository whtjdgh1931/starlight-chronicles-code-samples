using System;
using System.Collections.Generic;

/// <summary>
/// 하나의 저장 파일을 구성하는 최상위 데이터
/// </summary>
[Serializable]
public sealed class SaveData
{
    /// <summary>
    /// 저장 데이터 버전
    /// </summary>
    public const int CurrentVersion = 4;

    public int Version = CurrentVersion;

    public PlayerSaveData Player = new();

    public InventorySaveData Inventory = new();

    public List<EquippedItemSaveData> EquipItems = new();

    public List<QuestSaveData> Quests = new();

    public QuickSlotSaveData[] QuickSlots = new QuickSlotSaveData[PlayerQuickSlots.SlotCount];

    /// <summary>
    /// 퀘스트·퀵슬롯 기록이 없던 이전 버전의 캐릭터 정보 유지
    /// 파일을 덮어쓰지 않고 메모리의 저장 구조만 갱신
    /// </summary>
    public void MigrateLegacyVersion()
    {
        if (Version == 2)
        {
            Quests ??= new List<QuestSaveData>();
            Version = 3;
        }
        if (Version == 3)
        {
            QuickSlots = new QuickSlotSaveData[PlayerQuickSlots.SlotCount];
            Version = CurrentVersion;
        }
    }


}
