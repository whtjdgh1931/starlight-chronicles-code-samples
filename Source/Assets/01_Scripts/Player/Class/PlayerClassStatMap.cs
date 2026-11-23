using System;

/// <summary>
/// 플레이어 직업과 주 능력치의 고정 매핑을 제공
/// </summary>
public static class PlayerClassStatMap
{
    public static StatType GetMainStat(
       PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Warrior => StatType.Str,
            PlayerClass.Archer => StatType.Dex,
            PlayerClass.Magician => StatType.Int,
            _ => throw new ArgumentOutOfRangeException(nameof(playerClass), playerClass,"지원하지 않는 플레이어 직업입니다.")
        };
    }

    public static bool IsMainStatType(StatType statType)
    {
        return statType == StatType.Str || statType == StatType.Dex || statType == StatType.Int;
    }
}
