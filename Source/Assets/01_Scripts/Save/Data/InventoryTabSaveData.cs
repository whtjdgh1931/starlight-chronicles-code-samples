using System;
using System.Collections.Generic;

/// <summary>
/// 인벤토리 탭 저장용 클래스
/// </summary>
[Serializable]
public sealed class InventoryTabSaveData
{
    public InventoryTab Tab;

    /// <summary>
    /// 열린 슬롯 갯수
    /// </summary>
    public int UnlockedSlotCnt;


    public List<InventorySlotSaveData> Slots = new();

}