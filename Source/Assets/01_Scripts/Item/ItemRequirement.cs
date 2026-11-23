using System;

/// <summary>
/// 아이템 종류별 필요 수량 정의
/// </summary>
[Serializable]
public sealed class ItemRequirement
{
    public int ItemId;
    public int Count;
}
