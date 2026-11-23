using System;
using System.Collections.Generic;

/// <summary>
/// Google Sheets에서 내려받은 전체 게임 데이터 스냅샷
/// </summary>
[Serializable]
public sealed class GameDataSnapshot
{
    public const int CurrentSchemaVersion = 4;

    public int SchemaVersion = CurrentSchemaVersion;
    public string DownloadedAtUtc;
    public PlayerMoveData PlayerMoveData;
    public PlayerStatData PlayerStatData;
    public List<MonsterData> Monsters = new();
    public List<QuestData> Quests = new();
    public List<MonsterAttackData> MonsterAttacks = new();
    public List<BossData> Bosses = new();
    public List<BossPhaseAttackData> BossPhaseAttacks = new();
    public List<SkillData> Skills = new();
    public List<ItemData> Items = new();
    public List<EnhanceData> EnhanceProbabilities = new();
    public List<EnhanceStatData> EnhanceStats = new();
    public List<GameDataStringEntry> Strings = new();
    public List<PortalData> Portals = new();
    public List<HuntDifficultyData> HuntDifficulties = new();
    public List<HuntSpawnData> HuntSpawns = new();
}

/// <summary>
/// 문자열 테이블의 직렬화 항목
/// </summary>
[Serializable]
public sealed class GameDataStringEntry
{
    public string Key;
    public string Value;

    public GameDataStringEntry(string key, string value)
    {
        Key = key;
        Value = value;
    }
}
