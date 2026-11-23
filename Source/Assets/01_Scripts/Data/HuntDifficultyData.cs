using System;

/// <summary>사냥터 난이도의 표시 이름과 입장 조건</summary>
[Serializable]
public sealed class HuntDifficultyData
{
    [SheetColumn("ID")]
    public HuntDifficulty Difficulty;

    public string Name;
    public int RequiredLevel;

    /// <summary>현재 레벨의 난이도 입장 가능 여부</summary>
    public bool AllowsEntry(int playerLevel) => playerLevel >= RequiredLevel;
}
