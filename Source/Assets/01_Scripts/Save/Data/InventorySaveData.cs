using System;
using System.Collections.Generic;

/// <summary>
/// 인벤토리 저장용 클래스
/// </summary>
[Serializable]
public sealed class InventorySaveData
{
    public int Gold;

    public List<InventoryTabSaveData> Tabs = new();

    /// <summary>
    /// 공간 확보 후 수령할 개인 보스 보상
    /// </summary>
    public List<ItemSaveData> PendingBossRewards = new();
}
