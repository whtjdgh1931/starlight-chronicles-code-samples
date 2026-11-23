using System;

/// <summary>
/// 인벤토리와 장비창 저장을 위한 데이터
/// </summary>
[Serializable]
public sealed class ItemSaveData
{
    /// <summary>
    /// 아이템 ID
    /// </summary>
    public int ItemId;

    /// <summary>
    /// 저장 수량
    /// </summary>
    public int Count;

    /// <summary>
    /// 장비아이템일 시 강화 레벨
    /// </summary>
    public int EnhanceLevel;
}
