using System;

/// <summary>
/// 판정 당시 아이템별 보유·필요·부족 수량의 불변 진단
/// </summary>
public sealed class ItemRequirementProgress
{
    public int ItemId { get; }
    public string Name { get; }
    public long Current { get; }
    public long Required { get; }
    public long Missing => Math.Max(0L, Required - Current);
    public bool Satisfied => Missing == 0;

    /// <summary>
    /// 실행 당시 수량 복사
    /// </summary>
    public ItemRequirementProgress(int itemId, string name, long current, long required)
    {
        ItemId = itemId;
        Name = name;
        Current = current;
        Required = required;
    }
}
