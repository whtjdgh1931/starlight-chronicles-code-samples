using System;

/// <summary>
/// 퀵슬롯 한 칸의 종류와 원본 데이터 ID
/// 배열 순서로 1·2·3·A·S·D 위치 구분
/// </summary>
[Serializable]
public struct QuickSlotSaveData
{
    public QuickSlotKind Kind;
    public int ContentId;
}
